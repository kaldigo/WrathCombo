using System;

namespace WrathCombo.Engine.Selection;

internal readonly record struct CastTransition(uint Previous, uint Current, bool Changed, bool RecastReleasedEarly);
internal sealed class CastLifecycle
{
    private uint current, completed;
    private long startedAt, expectedEnd, completedAt;
    internal void Complete(uint action, long now) { completed = action; completedAt = now; }
    internal CastTransition Observe(uint action, float remaining, bool gcdActive, long now)
    {
        long end = now + (long)(float.IsFinite(remaining) ? Math.Max(0, remaining) * 1000 : 0);
        if (action == current && !(action != 0 && end - expectedEnd > 200))
        {
            if (action != 0) expectedEnd = end;
            return new(current, action, false, false);
        }
        uint previous = current;
        bool effect = completed == previous && completedAt >= startedAt && now - completedAt is >= 0 and < 1500;
        bool interrupted = previous != 0 && action == 0 && !effect && !gcdActive && expectedEnd - now > 100;
        current = action; startedAt = now; expectedEnd = action != 0 ? end : 0;
        return new(previous, action, true, interrupted);
    }
}
