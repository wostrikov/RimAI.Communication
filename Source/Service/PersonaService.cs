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
        Hediff_Persona.GetOrAddNew(pawn).Personality = personality;
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
            return await overrideTask;

        string pawnBackstory = PromptService.CreatePawnBackstory(pawn, PromptService.InfoLevel.Full);

        try
        {
            var request = new TalkRequest(Constant.PersonaGenInstruction, pawn)
            {
                Context = $"[Character]\n{pawnBackstory}"
            };
            PersonalityData personalityData = await AIService.Query<PersonalityData>(request);

            if (personalityData?.Persona != null)
            {
                personalityData.Persona = personalityData.Persona.Replace("**", "").Trim();
            }

            return personalityData;
        }
        catch (Exception e)
        {
            Logger.Error(e.Message);
            return null;
        }
    }
}