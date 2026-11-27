using System;

namespace WrathCombo.Engine.Selection;

// Native recast activity is authoritative. Inactive groups may retain their last
// total/elapsed values after a cancelled cast; those values are diagnostic only.
internal readonly record struct ActionReadiness(bool Available, bool GcdActive, float GcdTotal,
    float GcdRemaining, uint Cast, float CastRemaining, float AnimationLock, bool Queued, uint QueuedAction)
{
    internal static float Remaining(bool active, float total, float elapsed) =>
        active && float.IsFinite(total) && float.IsFinite(elapsed)
            ? Math.Clamp(total - elapsed, 0, Math.Max(0, total)) : 0;

    internal bool CanSubmit(bool gcd, float queueWindow) => Available &&
        AnimationLock <= 0 && CastRemaining <= Math.Max(0, queueWindow) &&
        (!gcd || GcdRemaining <= Math.Max(0, queueWindow));
}
