using System;
using System.IO;
using Ustas.RimAI.Communication.Prompt;

internal static class PromptContextAndPresetTests
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

        string context = PromptContextFacetPolicy.AssembleContext(
            "Traits: Industrious",
            "Mood: Content (70%)",
            "Memory: Ate fine meal",
            "Social: lover of Bob");
        string status = PromptContextFacetPolicy.AssembleStatus(
            "Job: hauling steel",
            "Nearby: Bob cooking");
        T(PromptContextFacetPolicy.HasRequiredFacets(context, status), "all-six-facets");
        T(!PromptContextFacetPolicy.HasRequiredFacets(context.Replace("Memory:", "X:"), status), "missing-thoughts");
        T(!PromptContextFacetPolicy.HasRequiredFacets(context, status.Replace("Job:", "X:")), "missing-job");
        T(!PromptContextFacetPolicy.HasRequiredFacets(context.Replace("Mood:", "X:"), status), "missing-mood");
        T(!PromptContextFacetPolicy.HasRequiredFacets(context.Replace("Traits:", "X:"), status), "missing-traits");
        T(!PromptContextFacetPolicy.HasRequiredFacets(context.Replace("Social:", "X:"), status), "missing-relations");
        T(!PromptContextFacetPolicy.HasRequiredFacets(context, status.Replace("Nearby:", "X:")), "missing-situation");

        T(PromptPresetModePolicy.ResolveBaseInstruction(true, "simple", "preset-base", "default") == "preset-base", "advanced-keeps-preset");
        T(PromptPresetModePolicy.ResolveBaseInstruction(false, "simple-uk", "preset-base", "default") == "simple-uk", "simple-uses-settings");
        T(PromptPresetModePolicy.ResolveBaseInstruction(false, "  ", "preset-base", "default") == "default", "simple-empty-falls-back");

        var dto = new PresetDto
        {
            Version = 1,
            Name = "Probe",
            Description = "roundtrip",
            Entries =
            {
                new EntryDto
                {
                    Name = "Base Instruction",
                    Content = "Speak Ukrainian.",
                    Role = "System",
                    Position = "Before",
                    Enabled = true
                },
                new EntryDto
                {
                    Name = "Job template",
                    Content = "{{ pawn.job }}",
                    Role = "User",
                    Enabled = true
                }
            }
        };
        string json = PresetDtoJsonCodec.Export(dto);
        T(!string.IsNullOrWhiteSpace(json) && json.Contains("Probe"), "export-json");
        T(json.Contains("Speak Ukrainian."), "export-base");
        T(json.Contains("{{ pawn.job }}"), "export-template");
        var imported = PresetDtoJsonCodec.Import(json);
        T(imported != null && imported.Name == "Probe", "import-name");
        T(imported.Entries.Count == 2, "import-entries");
        T(imported.Entries[0].Content == "Speak Ukrainian.", "import-base");
        T(imported.Entries[1].Content == "{{ pawn.job }}", "import-template");

        string promptService = Read("PromptService.cs.src");
        string contextBuilder = Read("ContextBuilder.cs.src");
        string statusUtil = Read("PawnUtil.cs.src");
        string manager = Read("PromptManager.cs.src");
        string serializer = Read("PresetSerializer.cs.src");
        T(promptService.Contains("GetMoodContext") && promptService.Contains("GetThoughtsContext"), "service-mood-thoughts");
        T(promptService.Contains("GetRelationsContext"), "service-relations");
        T(promptService.Contains("CreatePawnBackstory"), "service-backstory-traits-owner");
        T(contextBuilder.Contains("GetTraitsContext") && contextBuilder.Contains("Traits:"), "builder-traits");
        T(contextBuilder.Contains("GetThoughtsContext") && contextBuilder.Contains("Memory:"), "builder-thoughts");
        T(contextBuilder.Contains("GetMoodContext") && contextBuilder.Contains("Mood:"), "builder-mood");
        T(contextBuilder.Contains("GetRelationsContext"), "builder-relations");
        T(statusUtil.Contains("GetPawnActivity") && statusUtil.Contains("Nearby:"), "status-job-situation");
        T(promptService.Contains("{status}"), "decorate-appends-status");
        T(manager.Contains("PromptPresetModePolicy.ResolveBaseInstruction"), "manager-uses-mode-policy");
        T(serializer.Contains("PresetDtoMapping.FromPreset") && serializer.Contains("PresetDtoJsonCodec.Export"), "serializer-uses-codec");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
