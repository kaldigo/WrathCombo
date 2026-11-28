using System;

namespace WrathCombo.Engine.Selection;

internal readonly record struct SelfHealPlan(bool Needed, float Landing, float DeferredLanding, string Reason)
{
    internal static SelfHealPlan Evaluate(float health, float threshold, bool predictive, float available,
        float cast, float nextGcd, float? hit, float projectedHealth)
    {
        float landing = Math.Max(0, available) + Math.Max(0, cast) + .7f;
        float deferred = landing + Math.Max(.1f, nextGcd);
        if (!float.IsFinite(health) || health <= 0 || threshold <= 0) return new(false, landing, deferred, "Disabled or unavailable");
        if (health <= threshold) return new(true, landing, deferred, "Configured recovery threshold");
        if (!predictive || health > Math.Max(threshold, 70)) return new(false, landing, deferred, "No preventive recovery needed");
        if (projectedHealth <= threshold) return new(true, landing, deferred, "Observed ongoing loss before the following heal opportunity");
        return hit is >= 0 && hit <= deferred
            ? new(true, landing, deferred, "Incoming damage before a heal deferred by another GCD")
            : new(false, landing, deferred, "A later recovery opportunity remains");
    }
}
