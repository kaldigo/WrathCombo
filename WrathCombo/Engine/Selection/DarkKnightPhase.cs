using System;

namespace WrathCombo.Engine.Selection;

internal static class DarkKnightPhase
{
    internal static bool UseScorn(float scornRemaining, int deliriumStacks, float deliriumRemaining,
        bool darkside, float recastRemaining, float gcd)
    {
        if (!float.IsFinite(scornRemaining) || scornRemaining <= 0 || !float.IsFinite(gcd) || gcd <= 0) return false;
        float next = Math.Max(0, recastRemaining) + gcd + .1f;
        if (scornRemaining <= next) return true;
        if (deliriumStacks > 0)
            return scornRemaining < deliriumRemaining && scornRemaining <= Math.Max(0, recastRemaining) + gcd * (deliriumStacks + 1) + .1f;
        return darkside;
    }
}
