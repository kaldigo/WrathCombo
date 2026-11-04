using System;

namespace WrathCombo.Engine.Selection;

internal sealed record BardRefreshInput(float FirstDot, float SecondDot, float BuffRemaining,
    float Gcd, float GcdRemaining, float ConfiguredWindow, float? Downtime);

internal static class BardRefresh
{
    // Refresh only two existing DoTs, while the buff will still affect the actual GCD.
    // Requiring a useful extension avoids repeatedly refreshing a newly applied pair.
    internal static bool UnderExpiringBuff(BardRefreshInput s)
    {
        if (!float.IsFinite(s.Gcd) || s.Gcd <= 0 || !float.IsFinite(s.FirstDot) ||
            !float.IsFinite(s.SecondDot) || s.FirstDot <= 0 || s.SecondDot <= 0) return false;
        float wait = Math.Max(0, s.GcdRemaining);
        if (!float.IsFinite(s.BuffRemaining) || s.BuffRemaining <= wait + .1f ||
            s.BuffRemaining > Math.Max(s.ConfiguredWindow, wait + s.Gcd)) return false;
        if (Math.Max(s.FirstDot, s.SecondDot) - wait >= 30) return false;
        return EncounterForecast.Normalize(s.Downtime) is not { } downtime || downtime > wait + 3;
    }
}
