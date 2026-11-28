namespace WrathCombo.Engine.Selection;

// A transformed icon alone cannot identify which enemy owns a stored release.
internal sealed class AcceptedRelease
{
    internal uint Source { get; private set; }
    internal ulong Recipient { get; private set; }
    private long acceptedAt, generation;
    internal void Confirm(uint source, ulong recipient, long now, long context)
    {
        if (source == 0 || recipient == 0) return;
        Source = source; Recipient = recipient; acceptedAt = now; generation = context;
    }
    internal bool Valid(uint source, long now, long context, bool recipientEligible, bool observedActive)
    {
        if (Source != source) return false;
        if (context != generation || now < acceptedAt || now - acceptedAt >= 20000 ||
            !recipientEligible || !observedActive && now - acceptedAt >= 1000)
        { Reset(); return false; }
        return observedActive;
    }
    internal void Reset() { Source = 0; Recipient = 0; }
}
