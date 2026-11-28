namespace WrathCombo.Engine.Selection;

// Only accepted actions latch a sequence's mode. Icon queries are read-only.
internal sealed class AdaptiveDamage
{
    private bool? acceptedArea;
    internal bool Select(bool available, int hits, int threshold, bool inSequence) =>
        inSequence && acceptedArea is { } mode ? mode : available && hits >= System.Math.Max(1, threshold);
    internal void Confirm(bool area) => acceptedArea = area;
    internal void Reset() => acceptedArea = null;
}
