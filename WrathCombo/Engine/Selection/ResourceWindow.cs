using System;

namespace WrathCombo.Engine.Selection;

internal sealed record BurstDurationInput(float BuffRemaining, bool BuffRequired, float Gcd, float GcdRemaining,
    int Actions, bool ExistingPhase, float? Downtime, float FinalCast = 0);

internal static class ResourceWindow
{
    internal static bool CanFinish(BurstDurationInput s)
    {
        if (s.ExistingPhase || !float.IsFinite(s.Gcd) || s.Gcd <= 0 || s.Actions <= 0 ||
            !float.IsFinite(s.GcdRemaining) || !float.IsFinite(s.FinalCast) || s.FinalCast < 0) return false;
        float duration = Math.Max(0, s.GcdRemaining) + s.Gcd * (s.Actions - 1) + s.FinalCast + .7f;
        if (s.BuffRequired && (!float.IsFinite(s.BuffRemaining) || s.BuffRemaining <= duration)) return false;
        return EncounterForecast.Normalize(s.Downtime) is not { } downtime || downtime > duration;
    }

    internal static int ShieldReserve(int configured, bool learned, bool availableSoon, bool alreadyShielded, bool incoming)
        => !learned ? 0 : Math.Max(configured, availableSoon && !alreadyShielded && incoming ? 3000 : 0);
}
