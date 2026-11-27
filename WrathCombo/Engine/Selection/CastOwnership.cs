namespace WrathCombo.Engine.Selection;

internal sealed class CastOwnership
{
    private uint current, pending;
    private long currentGeneration, pendingAt, pendingGeneration;
    private bool awaitingObservation;
    internal void Submit(uint action, uint nativeCast, bool queued, long generation, long now)
    {
        if (nativeCast == action && !queued) { current = action; currentGeneration = generation; awaitingObservation = true; }
        else { pending = action; pendingAt = now; pendingGeneration = generation; }
    }
    internal bool Observe(uint nativeCast, long generation, long now)
    {
        if (pending != 0 && now - pendingAt is < 0 or > 1500) pending = 0;
        if (pending != 0 && nativeCast == pending && generation > pendingGeneration)
        { current = pending; currentGeneration = generation; pending = 0; awaitingObservation = false; }
        if (current == nativeCast && awaitingObservation) { currentGeneration = generation; awaitingObservation = false; }
        if (current != nativeCast || currentGeneration != generation) current = 0;
        return nativeCast != 0 && current == nativeCast;
    }
    internal void ManualInput(bool queued = false)
    {
        pending = 0;
        if (!queued) { current = 0; awaitingObservation = false; }
    }
}
