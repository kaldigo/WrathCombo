namespace WrathCombo.Engine.Selection;

internal sealed record PreparationInput(uint Followup, ulong Recipient, uint Preparation, bool FollowupPermitted,
    bool RecipientValid, bool Needed, bool PreparationPermitted, bool PreparationReady, bool Prepared,
    bool MayPrepareNow, float CompletionSeconds, float? DeadlineSeconds, long ObservedAt, long Generation);

internal static class PreparationPolicy
{
    internal static SelectionDecision? Select(PreparationInput input)
    {
        if (!input.FollowupPermitted || !input.RecipientValid || !input.Needed) return null;
        if (input.DeadlineSeconds is { } deadline && deadline < input.CompletionSeconds) return null;
        if (input.Prepared || input.Preparation == 0)
            return new(input.Followup, input.Followup, input.Recipient, 0, "Eligible planned follow-up", input.ObservedAt, input.Generation);
        if (!input.PreparationPermitted || !input.PreparationReady || !input.MayPrepareNow) return null;
        return new(input.Preparation, input.Followup, input.Recipient, input.Preparation, "Prepare eligible planned follow-up", input.ObservedAt, input.Generation);
    }
}
