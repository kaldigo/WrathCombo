using System;

namespace WrathCombo.Engine.Selection;

internal readonly record struct HealLandingInput(float Impact, float Cast, float RecastRemaining,
    float NextGcd, float EffectDelay, bool Shield, float SafetyMargin = .15f);
internal readonly record struct HealLandingPlan(bool Fits, float StartIn, float LandingIn, bool ReserveGcd);
internal static class HealLanding
{
    internal static HealLandingPlan Plan(HealLandingInput s)
    {
        if (!float.IsFinite(s.Impact) || !float.IsFinite(s.Cast) || s.Cast < 0 ||
            !float.IsFinite(s.RecastRemaining) || !float.IsFinite(s.NextGcd) || s.NextGcd <= 0 ||
            !float.IsFinite(s.EffectDelay) || s.EffectDelay < 0 || s.Impact < -2) return default;
        float available = Math.Max(0, s.RecastRemaining);
        float duration = s.Cast + s.EffectDelay;
        float desired = s.Shield ? s.Impact - s.SafetyMargin : s.Impact + s.SafetyMargin;
        float start = Math.Max(available, desired - duration);
        float landing = start + duration;
        bool fits = s.Shield ? landing <= desired : landing <= s.Impact + Math.Max(1.5f, s.SafetyMargin);
        // Only reserve the immediately preceding GCD. Long-range forecasts must
        // not make a healthy healer idle for the whole preparation horizon.
        bool reserve = fits && start > available && start < available + s.NextGcd;
        return new(fits, start, landing, reserve);
    }
}
