using System.Collections.Generic;
using System.Linq;

namespace WrathCombo.Engine.Selection;

internal sealed class LimitedSequence
{
    private readonly Queue<uint> steps = new();
    private long started;
    private ulong capability;
    internal uint Current => steps.TryPeek(out var action) ? action : 0;
    internal bool Begin(IEnumerable<uint> actions, ulong currentCapability, long now)
    {
        var plan = actions.ToArray();
        if (Current != 0 || currentCapability == 0 || plan.Length == 0 || plan.Any(action => action == 0)) return false;
        foreach (uint action in plan) steps.Enqueue(action);
        capability = currentCapability; started = now;
        return true;
    }
    internal bool Validate(ulong currentCapability, bool remainingAllowed, long now)
    {
        if (currentCapability != capability || !remainingAllowed || now - started is < 0 or >= 10000) Reset();
        return Current != 0;
    }
    internal void Confirm(uint action, uint adjustedExpected)
    {
        if (Current != 0 && (action == Current || action == adjustedExpected)) steps.Dequeue();
    }
    internal void Reset() { steps.Clear(); capability = 0; started = 0; }
}
