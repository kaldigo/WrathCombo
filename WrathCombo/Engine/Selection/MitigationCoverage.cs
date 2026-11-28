using System;
using System.Collections.Immutable;
using System.Linq;

namespace WrathCombo.Engine.Selection;

internal sealed record MitigationCoverageInput(ImmutableArray<float> Remaining, float? Impact, float Fraction = .75f);
internal static class MitigationCoverage
{
    internal static bool CoversHit(MitigationCoverageInput s)
    {
        if (s.Remaining.IsDefaultOrEmpty || EncounterForecast.Normalize(s.Impact) is not { } impact ||
            !float.IsFinite(s.Fraction) || s.Fraction <= 0 || s.Fraction > 1) return false;
        return s.Remaining.Count(duration => float.IsFinite(duration) && duration > impact + .5f) >= Math.Ceiling(s.Remaining.Length * s.Fraction);
    }
}
