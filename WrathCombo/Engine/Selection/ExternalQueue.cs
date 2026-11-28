namespace RotationInterop;

/// <summary>Retains the native meaning of a queued external action, without claiming
/// an existing manual queue after a failed external attempt.</summary>
internal sealed class ExternalQueue
{
    private (uint Type, uint Action, ulong Target, long At)? pending;
    internal bool Matches(uint type, uint action, ulong target, long now) => pending is { } item &&
        item.Type == type && item.Action == action && item.Target == target && now - item.At is >= 0 and < 3000;
    internal void Observe(bool accepted, bool queued, uint type, uint action, ulong target,
        uint queuedAction, ulong queuedTarget, long now)
    {
        if (!queued) pending = null;
        else if (accepted && queuedAction == action && queuedTarget == target)
            pending = (type, action, target, now);
        else Validate(queued, queuedAction, queuedTarget);
    }
    internal void Validate(bool queued, uint action, ulong target)
    {
        if (pending is { } item && (!queued || item.Action != action || item.Target != target)) pending = null;
    }
}
