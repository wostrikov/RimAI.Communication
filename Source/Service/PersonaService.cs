using System;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Communication.Util;
using Verse;

namespace Ustas.RimAI.Communication.Data;

public static class PersonaService
{
    public static IPersonaGenerator OverrideGenerator { get; set; }

    public static string GetPersonality(Pawn pawn)
    {
        return Hediff_Persona.GetOrAddNew(pawn).Personality;
    }

    public static void SetPersonality(Pawn pawn, string personality)
    {
        var hediff = Hediff_Persona.GetOrAddNew(pawn);
        if (hediff == null)
            return;
        hediff.Personality = PersonaGeneratePersistPolicy.Sanitize(personality);
    }

    public static float GetTalkInitiationWeight(Pawn pawn)
    {
        return Hediff_Persona.GetOrAddNew(pawn).TalkInitiationWeight;
    }

    public static void SetTalkInitiationWeight(Pawn pawn, float frequency)
    {
        Hediff_Persona.GetOrAddNew(pawn).TalkInitiationWeight = frequency;
    }

    public static async Task<PersonalityData> GeneratePersona(Pawn pawn)
    {
        if (OverrideGenerator != null && OverrideGenerator.TryGenerate(pawn, out var overrideTask) && overrideTask != null)
            return AcceptGenerated(pawn, await overrideTask);

        string pawnBackstory = PromptService.CreatePawnBackstory(pawn, PromptService.InfoLevel.Full);

        try
        {
            var request = new TalkRequest(Constant.PersonaGenInstruction, pawn)
            {
                Context = $"[Character]\n{pawnBackstory}"
            };
            return AcceptGenerated(pawn, await AIService.Query<PersonalityData>(request));
        }
        catch (Exception e)
        {
            Logger.Error(e.Message);
            return null;
        }
    }

    static PersonalityData AcceptGenerated(Pawn pawn, PersonalityData personalityData)
    {
        if (personalityData == null)
            return null;
        if (PersonaGeneratePersistPolicy.TryAcceptGenerated(personalityData.Persona, out string sanitized))
        {
            personalityData.Persona = sanitized;
            SetPersonality(pawn, sanitized);
        }
        else
        {
            personalityData.Persona = sanitized;
        }
        return personalityData;
    }
}