namespace Ustas.RimAI.Communication.Prompt;

/// <summary>
/// Simple vs Advanced base-instruction ownership. Advanced keeps the
/// preset entry; Simple temporarily replaces it with the settings text.
/// </summary>
public static class PromptPresetModePolicy
{
    public static string ResolveBaseInstruction(
        bool advancedMode,
        string simpleInstruction,
        string currentBaseContent,
        string defaultInstruction)
    {
        if (advancedMode)
            return currentBaseContent;
        return string.IsNullOrWhiteSpace(simpleInstruction)
            ? defaultInstruction
            : simpleInstruction;
    }
}
