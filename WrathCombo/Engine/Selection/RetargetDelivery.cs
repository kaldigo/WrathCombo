namespace WrathCombo.Engine.Selection;

internal static class RetargetDelivery
{
    internal static ulong Select(ulong validatedRecipient, ulong queuedRecipient, bool processingQueue, bool overwriteQueue, bool changed) =>
        queuedRecipient != 0 && (processingQueue || !overwriteQueue || !changed) ? queuedRecipient : validatedRecipient;
}
