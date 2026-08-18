namespace Ustas.RimAI.Communication;

internal abstract class CommunicationSettingsCollaborator
{
    internal readonly Settings Owner;

    protected CommunicationSettingsCollaborator(Settings owner)
    {
        Owner = owner;
    }

    protected CommunicationSettingsPages Pages => Owner.Pages;
}

internal sealed class CommunicationSettingsPages
{
    internal readonly Settings Owner;
    internal readonly CommunicationBasicSettingsPage Basic;
    internal readonly CommunicationApiSettingsPage Api;
    internal readonly CommunicationCloudApiSettingsPage CloudApi;
    internal readonly CommunicationAiInstructionSettingsPage AiInstruction;
    internal readonly CommunicationContextFilterSettingsPage ContextFilter;
    internal readonly CommunicationEventFilterSettingsPage EventFilter;
    internal readonly CommunicationPromptPresetSettingsPage PromptPreset;
    internal readonly CommunicationPromptPresetListPanel PromptList;
    internal readonly CommunicationPromptEntryEditor PromptEditor;
    internal readonly CommunicationPromptPresetSidePanel PromptSide;

    internal CommunicationSettingsPages(Settings owner)
    {
        Owner = owner;
        Basic = new CommunicationBasicSettingsPage(owner);
        Api = new CommunicationApiSettingsPage(owner);
        CloudApi = new CommunicationCloudApiSettingsPage(owner);
        AiInstruction = new CommunicationAiInstructionSettingsPage(owner);
        ContextFilter = new CommunicationContextFilterSettingsPage(owner);
        EventFilter = new CommunicationEventFilterSettingsPage(owner);
        PromptPreset = new CommunicationPromptPresetSettingsPage(owner);
        PromptList = new CommunicationPromptPresetListPanel(owner);
        PromptEditor = new CommunicationPromptEntryEditor(owner);
        PromptSide = new CommunicationPromptPresetSidePanel(owner);
    }
}
