namespace Ustas.RimAI.Communication.Data;

/// <summary>
/// How a slave talks, decided by the state the slave is actually in.
///
/// The base instruction used to carry one fixed line for the role - "fearful,
/// obedient; reference forced labor and exhaustion" - and the model obeyed it
/// to the letter: every slave, in every line, complained about forced labour,
/// including the ones with nothing to do who had just slept twelve hours and
/// whose suppression stood at a hundred percent. The owner's report on
/// 2026-10-13 was a slave saying "I have only just woken up and forced labour
/// has already worn me out".
///
/// So the instruction now says only what is true of every slave, and the
/// pawn's own context carries a demeanor line worked out from suppression and
/// tiredness. It does not depend on the "prisoner and slave status" context
/// option: that one adds the number, this one is how the pawn speaks.
/// </summary>
public static class CaptiveDemeanorPolicy
{
    /// <summary>The role line of the base instruction as shipped until 2026-10-13, in Ukrainian.</summary>
    public const string SupersededSlaveLineUkrainian =
        "Раб: наляканий і слухняний; згадуй примусову працю та виснаження; називай колоністів «господарями»";

    /// <summary>The same line in the English default that preceded the Ukrainian one.</summary>
    public const string SupersededSlaveLineEnglish =
        "Slave: fearful, obedient; reference forced labor and exhaustion; call colonists \"master\"";

    public const string SlaveLineUkrainian =
        "Раб: слухняний; називай колоністів «господарями»; настрій і теми бери з рядка Demeanor у його профілі, "
        + "а примусову працю чи виснаження згадуй лише тоді, коли про них каже сам профіль";

    public const string SlaveLineEnglish =
        "Slave: obedient; call colonists \"master\"; take mood and topics from the Demeanor line of the profile, "
        + "and mention forced labor or exhaustion only when the profile itself does";

    /// <summary>
    /// The stored instruction with the superseded role line replaced and everything else -
    /// the player's own edits included - left as it was.
    /// </summary>
    public static string ReplaceSupersededRoleLines(string instruction)
    {
        if (string.IsNullOrEmpty(instruction))
            return instruction;
        return instruction
            .Replace(SupersededSlaveLineUkrainian, SlaveLineUkrainian)
            .Replace(SupersededSlaveLineEnglish, SlaveLineEnglish);
    }

    /// <summary>
    /// The demeanor line for a slave. Model-facing, so English like the rest of the context.
    /// </summary>
    /// <param name="suppressionPercent">The suppression need, 0 to 100.</param>
    /// <param name="exhausted">True when the pawn is short of rest right now.</param>
    public static string SlaveDemeanor(float suppressionPercent, bool exhausted)
    {
        string manner = suppressionPercent switch
        {
            < 20f => "defiant and resentful; may speak of freedom or of getting away",
            < 50f => "sullen; may grumble about servitude, briefly and under the breath",
            < 80f => "obedient and guarded; keeps complaints to self; small talk about everyday things",
            _ => "will broken - docile, quiet, eager to please; never complains about work or servitude; "
                 + "talks about everyday things: food, weather, the task at hand, people nearby"
        };
        return exhausted
            ? $"Demeanor: {manner}; short of sleep right now"
            : $"Demeanor: {manner}";
    }
}
