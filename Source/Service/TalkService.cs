using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Communication;
using RimWorld;
using Verse;
using Cache = Ustas.RimAI.Communication.Data.Cache;
using Logger = Ustas.RimAI.Communication.Util.Logger;
using RimAI.Core.Runtime;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// Core service for generating and managing AI-driven conversations between pawns.
/// </summary>
public static class TalkService
{
    /// <summary>
    /// Initiates the process of generating a conversation. It performs initial checks and then
    /// starts a background task to handle the actual AI communication.
    /// </summary>
    public static bool GenerateTalk(TalkRequest talkRequest)
    {
        // Guard clauses to prevent generation when the feature is disabled or the AI service is busy.
        var settings = Settings.Get();
        if (!settings.IsEnabled || !CommonUtil.ShouldAiBeActiveOnSpeed()) return false;
        if (settings.GetActiveConfig() == null) return false;
        if (AIService.IsBusy()) return false;

        // A bedtime or waking line whose moment has passed is dropped; a live one is re-aimed at whoever is near now.
        if (!SleepDialogueTracker.TryRefreshRequest(talkRequest)) return false;

        PawnState pawn1 = Cache.Get(talkRequest.Initiator);
        if (!talkRequest.TalkType.IsFromUser() && (pawn1 == null || !pawn1.CanGenerateTalk())) return false;
        
        if (!settings.AllowSimultaneousConversations && AnyPawnHasPendingResponses()) return false;

        // Ensure the recipient is valid and capable of talking.
        PawnState pawn2 = talkRequest.Recipient != null ? Cache.Get(talkRequest.Recipient) : null;
        if (pawn2 == null || talkRequest.Recipient?.Name == null || !pawn2.CanDisplayTalk())
        {
            talkRequest.Recipient = null;
        }

        // An announcement the player makes through a pawn is that pawn's to speak.
        bool isPlayerAnnouncement = talkRequest.IsAnnouncement && talkRequest.Recipient != null && talkRequest.Recipient.IsPlayer();
        Pawn mainPawn = isPlayerAnnouncement ? talkRequest.Recipient : talkRequest.Initiator;

        List<Pawn> nearbyPawns = PawnSelector.GetAllNearByPawns(talkRequest.Initiator, isAnnouncement: talkRequest.IsAnnouncement);
        // The recipient may have just been nulled above; a null must not reach nearbyPawns.
        if (isPlayerAnnouncement)
            nearbyPawns.Insert(0, talkRequest.Initiator);
        else if (talkRequest.Recipient != null && talkRequest.Recipient.IsPlayer())
            nearbyPawns.Insert(0, talkRequest.Recipient);
        var (status, isInDanger) = mainPawn.GetPawnStatusFull(nearbyPawns, talkRequest.IsAnnouncement);
        
        // Avoid spamming generations if the pawn's status hasn't changed recently.
        if (!talkRequest.TalkType.IsFromUser() && status == pawn1.LastStatus && pawn1.RejectCount < 2)
        {
            pawn1.RejectCount++;
            return false;
        }
        
        if (!talkRequest.TalkType.IsFromUser() && isInDanger) talkRequest.TalkType = TalkType.Urgent;
        
        pawn1.RejectCount = 0;
        pawn1.LastStatus = status;

        // Select the most relevant pawns for the conversation context.
        List<Pawn> pawns = new List<Pawn> { mainPawn, isPlayerAnnouncement ? null : talkRequest.Recipient }
            .Where(p => p != null)
            .Concat(nearbyPawns.Where(p =>
            {
                var pawnState = Cache.Get(p);
                pawnState?.DrainIncomingTalkResponses();
                // Everyone in earshot hears an announcement, whatever they were about to say.
                return pawnState != null && pawnState.CanDisplayTalk() &&
                       (talkRequest.IsAnnouncement || pawnState.TalkResponses.Empty());
            }))
            .Distinct()
            .Take(talkRequest.IsAnnouncement ? Math.Max(settings.Context.MaxPawnContextCount, 8) : settings.Context.MaxPawnContextCount)
            .ToList();

        if (talkRequest.IsAnnouncement)
            foreach (var p in pawns.Where(p => p != null && !p.IsPlayer()))
                Cache.Get(p)?.IgnoreAllTalkResponses([TalkType.Urgent, TalkType.User, TalkType.Announcement]);
        
        // A sleep line was aimed at whoever is nearby when it was refreshed; that decides it, not the headcount alone.
        if (talkRequest.TalkType == TalkType.Sleep)
            talkRequest.IsMonologue = pawns.Count == 1;
        else if (pawns.Count == 1)
            talkRequest.IsMonologue = true;

        if (!settings.AllowMonologue && talkRequest.IsMonologue && !talkRequest.TalkType.IsFromUser())
            return false;

        // Delegate prompt assembly to PromptManager (Handles Simple/Advanced modes and fallbacks)
        talkRequest.PromptMessages = PromptManager.Instance.BuildMessages(talkRequest, pawns, status);
        
        // Update prompt with the actual rendered content (important for Advanced Mode history)
        var extracted = PromptManager.ExtractUserPrompt(talkRequest.PromptMessages);
        if (!string.IsNullOrEmpty(extracted))
        {
            talkRequest.Prompt = extracted;
        }
        
        // Offload the AI request and processing to a background thread to avoid blocking the game's main thread.
        RimAiBackground.Run(() => GenerateAndProcessTalkAsync(talkRequest));

        pawn1.MarkRequestSpoken(talkRequest);
        
        return true;
    }

    /// <summary>
    /// Handles the asynchronous AI streaming and processes the responses.
    /// </summary>
    private static async Task GenerateAndProcessTalkAsync(TalkRequest talkRequest)
    {
        var initiator = talkRequest.Initiator;
        try
        {
            Cache.Get(initiator).IsGeneratingTalk = true;
            TalkLifecycle.PublishTalkRequestEnrichment(talkRequest);

            var receivedResponses = new List<TalkResponse>();

            // Call the streaming chat service. The callback is executed as each piece of dialogue is parsed.
            await AIService.ChatStreaming(talkRequest, talkResponse =>
                {
                    Logger.Debug($"Streamed: {talkResponse}");

                    PawnState pawnState = Cache.GetByName(talkResponse.Name);
                    talkResponse.Name = pawnState.Pawn.LabelShort;

                    // Link replies to the previous message in the conversation.
                    if (receivedResponses.Any())
                    {
                        talkResponse.ParentTalkId = receivedResponses.Last().Id;
                    }

                    receivedResponses.Add(talkResponse);

                    // Hand off to the main thread for display later; PawnState.TalkResponses itself must only ever be touched from the main thread.
                    pawnState.QueueIncomingResponse(talkResponse);
                }
            );

            // Once the stream is complete, save the full conversation to history.
            AddResponsesToHistory(receivedResponses, talkRequest.Prompt);
            StoryThreadService.Capture(talkRequest, receivedResponses);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Dialogue generation cancelled for a more urgent talk.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex.StackTrace);
        }
        finally
        {
            var initiatorState = Cache.Get(initiator);
            if (initiatorState != null) initiatorState.IsGeneratingTalk = false;
        }
    }

    /// <summary>
    /// Serializes the generated responses and adds them to the message history for all involved pawns.
    /// </summary>
    private static void AddResponsesToHistory(List<TalkResponse> responses, string prompt)
    {
        if (!responses.Any()) return;
        string serializedResponses = JsonUtil.SerializeToJson(responses);
        var uniquePawns = responses
            .Select(r => Cache.GetByName(r.Name)?.Pawn)
            .Where(p => p != null)
            .Distinct();

        foreach (var pawn in uniquePawns)
        {
            TalkHistory.AddMessageHistory(pawn, prompt, serializedResponses);
        }
    }

    /// <summary>
    /// Iterates through all pawns on each game tick to display any queued talks.
    /// </summary>
    public static void DisplayTalk(bool ignoreReplyInterval = false)
    {
        // Story threads finished on the background thread are recorded here, on the main thread.
        StoryThreadService.DrainCaptures();

        // Drain all pawns upfront so every pawn has a consistent view of TalkResponses for this tick cycle.
        foreach (Pawn pawn in Cache.Keys)
        {
            Cache.Get(pawn)?.DrainIncomingTalkResponses();
        }

        foreach (Pawn pawn in Cache.Keys)
        {
            PawnState pawnState = Cache.Get(pawn);
            if (pawnState == null) continue;

            if (pawnState.TalkResponses.Empty()) continue;

            // Danger first: a calm line queued before the raid must not be the next thing said.
            bool inDanger = pawn.IsInDanger();
            if (inDanger)
                pawnState.IgnoreAllTalkResponses([TalkType.Urgent, TalkType.User, TalkType.Announcement]);

            var talk = pawnState.TalkResponses.FirstOrDefault();
            if (talk == null)
            {
                if (!pawnState.TalkResponses.Empty())
                    pawnState.TalkResponses.RemoveAt(0);
                continue;
            }

            // Skip this talk if its parent was ignored or the pawn is currently unable to speak.
            if (TalkHistory.IsTalkIgnored(talk.ParentTalkId) || !pawnState.CanDisplayTalk())
            {
                pawnState.IgnoreTalkResponse();
                continue;
            }

            // Reactions to an announcement come quickly, as a crowd's do.
            int replyInterval = inDanger || talk.TalkType == TalkType.Announcement ? 2 : CommunicationSettings.ReplyInterval;

            // Enforce a delay for replies to make conversations feel more natural.
            int parentTalkTick = TalkHistory.GetSpokenTick(talk.ParentTalkId);
            if (!ignoreReplyInterval && (parentTalkTick == -1 || !CommonUtil.HasPassed(parentTalkTick, replyInterval))) continue;

            CreateInteraction(pawn, talk);
            
            break; // Display only one talk per tick to prevent overwhelming the screen.
        }
    }

    /// <summary>
    /// Retrieves the text for a pawn's current talk. Called by the game's UI system.
    /// </summary>
    public static string GetTalk(Pawn pawn)
    {
        PawnState pawnState = Cache.Get(pawn);
        if (pawnState == null) return null;

        pawnState.DrainIncomingTalkResponses();
        TalkResponse talkResponse = ConsumeTalk(pawnState);
        pawnState.LastTalkTick = GenTicks.TicksGame;

        return talkResponse.Text;
    }
    
    /// <summary>
    /// Calls AI service directly for debug purpose.
    /// </summary>
    public static void GenerateTalkDebug(TalkRequest talkRequest)
    {
        RimAiBackground.Run(() => GenerateAndProcessTalkAsync(talkRequest));
    }

    /// <summary>
    /// Dequeues a talk and updates its history as either spoken or ignored.
    /// </summary>
    private static TalkResponse ConsumeTalk(PawnState pawnState)
    {
        // Failsafe check
        if (pawnState.TalkResponses.Empty()) 
            return new TalkResponse(TalkType.Other, null!, "");
        
        var talkResponse = pawnState.TalkResponses.First();
        pawnState.TalkResponses.Remove(talkResponse);
        TalkHistory.AddSpoken(talkResponse.Id);
        var apiLog = ApiHistory.GetApiLog(talkResponse.Id);
        if (apiLog != null)
            apiLog.SpokenTick = GenTicks.TicksGame;

        Overlay.NotifyLogUpdated();
        return talkResponse;
    }

    private static void CreateInteraction(Pawn pawn, TalkResponse talk)
    {
        if (!TalkLifecycle.CanDisplay(pawn, talk))
            return;

        // Create the interaction log entry, which triggers the display of the talk bubble in-game.
        InteractionDef intDef = DefDatabase<InteractionDef>.GetNamed("RimTalkInteraction");
        var recipient = talk.GetTarget() ?? pawn;
        var playLogEntryInteraction = new PlayLogEntry_RimTalkInteraction(intDef, pawn, recipient, null);

        if (playLogEntryInteraction.CachedString.NullOrEmpty())
            return;
        
        Find.PlayLog.Add(playLogEntryInteraction);

        if (Settings.Get().ApplyMoodAndSocialEffects && pawn != recipient)
        {
            var interactionType = talk.GetInteractionType();
            var memory = interactionType.GetThoughtDef();
            if (memory != null)
            {
                recipient.needs?.mood?.thoughts?.memories?.TryGainMemory(memory, pawn);
                if (interactionType is InteractionType.Chat)
                {
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(memory, recipient);
                }
            }
        }

        var apiLog = ApiHistory.GetApiLog(talk.Id);
        TalkLifecycle.PublishInteractionCreated(new TalkInteractionCreatedArgs
        {
            Speaker = pawn,
            TalkResponse = talk,
            TalkRequest = apiLog?.TalkRequest,
            SpeakerName = talk.Name ?? string.Empty,
            Text = talk.Text ?? string.Empty,
            TalkId = talk.Id.ToString(),
            Participants = apiLog?.TalkRequest?.Participants?.ConvertAll(static pawn => (object)pawn),
            IsPlayerInitiated = talk.TalkType.IsFromUser(),
            Channel = apiLog?.Channel.ToString() ?? string.Empty
        });
    }

    private static bool AnyPawnHasPendingResponses()
    {
        return Cache.GetAll().Any(pawnState =>
        {
            pawnState.DrainIncomingTalkResponses();
            return pawnState.TalkResponses.Count > 0;
        });
    }
}
