using System;
using System.Collections.Generic;
using System.IO;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Personas;

internal static class PersonaGeneratePersistTests
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

        T(PersonaGeneratePersistPolicy.Sanitize("**blunt** and loyal") == "blunt and loyal", "sanitize-stars");
        T(PersonaGeneratePersistPolicy.Sanitize("  \n  ") == string.Empty, "sanitize-empty");
        T(!PersonaGeneratePersistPolicy.TryAcceptGenerated("**  **", out _), "reject-empty-after-sanitize");
        T(PersonaGeneratePersistPolicy.TryAcceptGenerated("**witty**", out string accepted) && accepted == "witty", "accept-generated");

        var store = new Dictionary<string, string>();
        T(PersonaGeneratePersistPolicy.Persist(store, "pawn:alice", "**dry humor**"), "persist-alice");
        T(!PersonaGeneratePersistPolicy.Persist(store, "pawn:bob", "   "), "reject-bob-empty");
        T(PersonaGeneratePersistPolicy.Persist(store, "pawn:bob", "stoic"), "persist-bob");
        T(PersonaGeneratePersistPolicy.Read(store, "pawn:alice") == "dry humor", "read-alice");
        T(PersonaGeneratePersistPolicy.Read(store, "pawn:bob") == "stoic", "read-bob-isolated");
        T(PersonaGeneratePersistPolicy.Read(store, "pawn:carol") == string.Empty, "missing-pawn");

        string talk = PersonaGeneratePersistPolicy.FormatTalkLine(PersonaGeneratePersistPolicy.Read(store, "pawn:alice"));
        T(PersonaGeneratePersistPolicy.ShouldInjectTalk(store["pawn:alice"]), "inject-yes");
        T(talk.StartsWith(PersonaProjectionDefaults.TalkPersonalityLinePrefix), "talk-prefix");
        T(talk.Contains("dry humor"), "talk-contains-persisted");
        T(!talk.Contains("stoic"), "talk-does-not-leak-other-pawn");
        T(PersonaGeneratePersistPolicy.FormatTalkLine("") == string.Empty, "talk-empty");

        string service = Read("PersonaService.cs.src");
        string prompt = Read("PromptService.cs.src");
        T(service.Contains("PersonaGeneratePersistPolicy.TryAcceptGenerated"), "host-generate-accept");
        T(service.Contains("SetPersonality(pawn, sanitized)"), "host-generate-persists");
        T(service.Contains("OverrideGenerator"), "host-override-before-paid");
        T(service.Contains("PersonaGeneratePersistPolicy.Sanitize"), "host-set-sanitizes");
        T(prompt.Contains("PersonaGeneratePersistPolicy.ShouldInjectTalk"), "host-talk-gate");
        T(prompt.Contains("PersonaGeneratePersistPolicy.FormatTalkLine"), "host-talk-line");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
