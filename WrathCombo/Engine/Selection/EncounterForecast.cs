using System;

namespace WrathCombo.Engine.Selection;

internal sealed record EncounterForecast(long ObservedAt, long Generation, string Module, float? Raidwide, float? Tankbuster,
    float? Downtime, float? Knockback)
{
    internal float? Remaining(float? value, long now, long generation)
    {
        if (generation != Generation || now - ObservedAt is < 0 or > 1500 || value is null) return null;
        float remaining = value.Value - (now - ObservedAt) / 1000f;
        return remaining >= 0 ? remaining : null;
    }
    internal static float? Normalize(float? seconds) => seconds is { } n && float.IsFinite(n) && n >= 0 && n < 3600 ? n : null;
    internal static float? Earliest(float? a, float? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);
}
