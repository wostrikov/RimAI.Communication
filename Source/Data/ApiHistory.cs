using System;
using System.Collections.Generic;
using System.Threading;
using Ustas.RimAI.Communication.Client;
using Ustas.RimAI.Communication.Data;
using Verse;

namespace Ustas.RimAI.Communication.Data;

/// <summary>
/// Requests and responses for the debug window and the overlay. The request thread writes it
/// while those windows read it on the main thread, and a long session would otherwise keep
/// every log it ever made.
/// </summary>
public static class ApiHistory
{
    public const int MaxEntries = 500;

    private static readonly BoundedHistory<Guid, ApiLog> History = new(MaxEntries);
    private static int _conversationIdIndex = -1;
    
    public static ApiLog GetApiLog(Guid id) => History.TryGetValue(id, out var apiLog) ? apiLog : null;

    public static ApiLog AddRequest(TalkRequest request, Channel channel)
    {
        var log = new ApiLog(request.PromptNameOf(request.Initiator) ?? request.Initiator.LabelShort, request, null, null, DateTime.Now, channel)
            {
                IsFirstDialogue = true,
                ConversationId = request.IsMonologue ? -1 : Interlocked.Increment(ref _conversationIdIndex)
            };
        History.Set(log.Id, log);
        return log;
    }

    public static void UpdatePayload(Guid id, Payload payload)
    {
        if (History.TryGetValue(id, out var log))
        {
            log.Payload = payload;
        }
    }

    public static ApiLog AddResponse(Guid id, string response, string name, string interactionType, Payload payload = null, int elapsedMs = 0)
    {
        if (!History.TryGetValue(id, out var originalLog)) return null;

        // first message
        if (originalLog.Response == null)
        {
            originalLog.Name = name ?? originalLog.Name;
            originalLog.Response = response;
            originalLog.InteractionType = interactionType;
            originalLog.Payload = payload;
            originalLog.ElapsedMs = (int)(DateTime.Now - originalLog.Timestamp).TotalMilliseconds;
            return originalLog;
        }
        
        // multi-turn messages
        var newLog = new ApiLog(name, originalLog.TalkRequest, response, payload, DateTime.Now, originalLog.Channel);
        newLog.InteractionType = interactionType;
        newLog.ElapsedMs = elapsedMs;
        newLog.ConversationId = originalLog.ConversationId;
        History.Set(newLog.Id, newLog);
        return newLog;
    }
    
    public static ApiLog AddUserHistory(Pawn initiator, Pawn recipient, string text, TalkType talkType = TalkType.User)
    {
        var prompt = talkType == TalkType.Announcement
            ? $"{initiator.LabelShort} announced"
            : $"{initiator.LabelShort} talked to {recipient?.LabelShort}";
        TalkRequest talkRequest = new(prompt, initiator, recipient, talkType);
        var log = new ApiLog(initiator.LabelShort, talkRequest, text, null, DateTime.Now, Channel.User);
        History.Set(log.Id, log);
        return log;
    }

    /// <summary>The logs, oldest first, as a copy the caller may enumerate at leisure.</summary>
    public static IEnumerable<ApiLog> GetAll() => History.Snapshot();

    public static void Clear()
    {
        History.Clear();
    }
}
