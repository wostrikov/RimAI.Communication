namespace Ustas.RimAI.Communication.Policy;

/// <summary>
/// Whether a pawn with nobody to talk to may say something to itself this turn. Pawns are
/// meant to talk to each other; a pawn alone mostly stays quiet, and only now and then
/// mutters a line, so the colony does not fill up with speeches addressed to no one.
/// </summary>
public static class MonologuePolicy
{
    public const float DefaultChance = 0.15f;

    /// <param name="allowMonologue">The player's switch: off means never.</param>
    /// <param name="chance">The share of lonely turns that are spoken, 0 to 1.</param>
    /// <param name="roll">A uniform draw in [0, 1).</param>
    public static bool Admit(bool allowMonologue, float chance, double roll)
        => allowMonologue && roll < Clamp01(chance);

    public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
