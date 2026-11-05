using System;

namespace WrathCombo.Engine.Selection;

internal sealed record CastBudgetInput(float Cast, float GcdRemaining, bool Moving, float? SafeCastWindow, float? TargetReturns);
internal static class CastBudget
{
    internal static bool Fits(CastBudgetInput s)
    {
        if (!float.IsFinite(s.Cast) || s.Cast < 0 || !float.IsFinite(s.GcdRemaining)) return false;
        if (s.Cast == 0) return true;
        if (s.Moving) return false;
        float finish = Math.Max(0, s.GcdRemaining) + s.Cast + .1f;
        return (EncounterForecast.Normalize(s.SafeCastWindow) is not { } safe || finish < safe) &&
            (EncounterForecast.Normalize(s.TargetReturns) is not { } returns || finish < returns);
    }
}
