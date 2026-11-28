using System;

namespace WrathCombo.Engine.Selection;

internal readonly record struct SummonCandidate(uint Action, bool Available, bool RequiresCast);
internal static class SummonPhase
{
    internal static uint Choose(ReadOnlySpan<SummonCandidate> ordered, bool hardCastFits)
    {
        uint fallback = 0;
        foreach (var candidate in ordered)
        {
            if (!candidate.Available || candidate.Action == 0) continue;
            if (fallback == 0) fallback = candidate.Action;
            if (!candidate.RequiresCast || hardCastFits) return candidate.Action;
        }
        // Summoning itself is instant. With no alternative phase, preserve a
        // legal summon rather than pretending movement permanently removes it.
        return fallback;
    }
}
