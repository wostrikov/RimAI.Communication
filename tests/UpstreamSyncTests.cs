using System;
using System.IO;

/// <summary>
/// Duplicate names, mapless events, empty answers, hidden-faction visitors, the ambient cooldown,
/// letters and the stream's last line: that each is wired where the talk path reads it.
/// </summary>
internal static class UpstreamSyncTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        string ai = Read("AIService.cs.src");
        string talk = Read("TalkService.cs.src");
        string prompt = Read("PromptService.cs.src");
        string builder = Read("ContextBuilder.cs.src");
        string pawnUtil = Read("PawnUtil.cs.src");
        string request = Read("TalkRequest.cs.src");
        string pool = Read("TalkRequestPool.cs.src");
        string archive = Read("ArchivePatch.cs.src");
        string tick = Read("TickManagerPatch.cs.src");
        string stream = Read("OpenAIStreamHandler.cs.src");
        string openAi = Read("OpenAIClient.cs.src");

        // Duplicate names.
        T(prompt.Contains("public static string GetUniqueName(Pawn pawn, List<Pawn> pawns = null)"), "unique-name-exists");
        T(prompt.Contains("WithUniqueName(CreatePawnContext(pawn, infoLevel), pawn, pawns)"), "context-header-uses-unique-name");
        T(prompt.Contains("var shortName = GetUniqueName(mainPawn, pawns);"), "prompt-speaker-uses-unique-name");
        T(builder.Contains("PromptService.GetUniqueName(speaker, pawns)"), "announcement-and-player-lines-use-unique-name");
        T(request.Contains("public void RememberPromptNames()") && request.Contains("public PawnState ResolvePawnState(string name)"), "request-resolves-names");
        T(talk.Contains("talkRequest.RememberPromptNames();"), "names-remembered-on-main-thread");
        T(talk.Contains("talkRequest.ResolvePawnState(talkResponse.Name)") && talk.Contains("if (pawnState?.Pawn == null) return;"), "stream-resolves-speaker-and-survives-unknown-name");
        T(talk.Contains("talkResponse.TargetPawn = talkRequest.ResolvePawnState(talkResponse.TargetName)?.Pawn;"), "stream-resolves-target");
        T(ai.Contains("if (request.ResolvePawnState(response.Name) == null) return;"), "ai-service-filters-by-resolution");
        T(!talk.Contains("Cache.GetByName(talkResponse.Name)"), "no-lookup-by-bare-name-in-stream");

        // World events and a pawn without a map.
        T(request.Contains("public int MapId { get; set; } = -1;"), "request-map-defaults-to-any");
        T(pool.Contains("if (pawn?.Map == null) return null;") && pool.Contains("request.MapId != -1 && request.MapId != pawn.Map.uniqueID"), "pool-serves-any-map-event");

        // Monologue, empty responses, beggars and pilgrims, the cooldown.
        T(builder.Contains("short monologue (only {shortName} speaks)"), "monologue-is-one-speaker");
        T(ai.Contains("Empty Response (AI returned no content)"), "empty-response-told-apart");
        T(pawnUtil.Contains("private static bool IsHostileToPlayer(Pawn pawn)") && pawnUtil.Contains("RelationWith(Faction.OfPlayer, allowNull: true)"), "hidden-faction-hostility-without-dummy-relation");
        T(!pawnUtil.Contains("pawn.Faction.IsPlayer || pawn.Faction.def.hidden)"), "hidden-factions-not-excluded");
        T(tick.Contains("!AIService.CurrentRequest.TalkType.IsFastTrack()") && tick.Contains("_lastTalkEndTick = GenTicks.TicksGame;"), "cooldown-starts-after-generation");

        // Letters and messages.
        T(archive.Contains("TalkRequestPool.Add(prompt, mapId: eventMap?.uniqueID ?? -1, talkType: talkType);"), "letter-is-one-pool-request");
        T(!archive.Contains("AddTalkRequest"), "letter-no-longer-per-colonist");
        T(archive.Contains("\"Verse.Message\", out var messagesEnabled"), "messages-off-unless-enabled");
        T(archive.Contains("(Target: {pawn.LabelShort})"), "letter-names-its-target");

        // The stream's last line.
        T(stream.Contains("public void Flush()"), "openai-stream-flushes");
        T(openAi.Contains("streamHandler.Flush();"), "openai-client-flushes-before-reading");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
