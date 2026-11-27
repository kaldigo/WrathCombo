using System;

namespace WrathCombo.Engine.Selection;

internal readonly record struct EarnedActionCandidate(uint Action, float Remaining, bool Eligible, int SlotsNeeded = 1);
internal static class EarnedActionDeadline
{
    internal static uint Choose(ReadOnlySpan<EarnedActionCandidate> candidates, float recastRemaining, float gcd)
    {
        if (!float.IsFinite(recastRemaining) || !float.IsFinite(gcd) || gcd <= 0) return 0;
        uint selected = 0;
        float earliest = float.PositiveInfinity;
        foreach (var candidate in candidates)
        {
            if (!candidate.Eligible || !float.IsFinite(candidate.Remaining) || candidate.Remaining <= 0 || candidate.SlotsNeeded <= 0) continue;
            float nextOpportunity = Math.Max(0, recastRemaining) + gcd * candidate.SlotsNeeded + .1f;
            if (candidate.Remaining > nextOpportunity || candidate.Remaining >= earliest) continue;
            selected = candidate.Action; earliest = candidate.Remaining;
        }
        return selected;
    }
}
