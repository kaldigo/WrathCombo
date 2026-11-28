using System;

namespace WrathCombo.Engine.Selection;

internal static class PvpEligibility
{
    internal static bool CanDamage(bool invulnerable, bool hardenedScales, float guardRemaining,
        bool bypassesGuard, float castTime)
    {
        if (invulnerable || hardenedScales) return false;
        if (bypassesGuard || guardRemaining <= 0) return true;
        return float.IsFinite(guardRemaining) && float.IsFinite(castTime) && castTime > guardRemaining + .1f;
    }
}
