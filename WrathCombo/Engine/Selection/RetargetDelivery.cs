namespace WrathCombo.Engine.Selection;

internal static class RetargetDelivery
{
    internal static ulong? Validate(ulong original, ulong proposed, bool eligible, bool strict) =>
        eligible ? proposed : strict ? null : original;

    internal static bool ConflictsWithBoundRecipient(ulong bound, ulong queued, bool overwriteQueue) =>
        queued != 0 && queued != bound && !overwriteQueue;
    internal static ulong Select(ulong validatedRecipient, ulong queuedRecipient, bool processingQueue, bool overwriteQueue, bool changed) =>
        queuedRecipient != 0 && (processingQueue || !overwriteQueue || !changed) ? queuedRecipient : validatedRecipient;
}
