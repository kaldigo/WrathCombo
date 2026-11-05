namespace WrathCombo.Engine.Selection;

internal sealed record SelectionDecision(uint Action, uint PlannedGcd, ulong Recipient, uint Preparation, string Reason, long ObservedAt, long Generation, long PreventionEvent = 0, uint CompletionAction = 0,
    System.Numerics.Vector3? GroundPosition = null, ulong? EvaluatedEnemy = null)
{
    internal SelectionDecision PlaceGround(ulong recipient, System.Numerics.Vector3 position, long now, string reason) =>
        this with { Recipient = recipient, GroundPosition = position, ObservedAt = now, Reason = reason };
}

// Press records a proposal; only a confirmed action accepts it. Preview is a read.
internal sealed class PreparationSequence
{
    private SelectionDecision? pressed;
    private SelectionDecision? accepted;
    internal SelectionDecision? Current(long now, long generation) =>
        accepted is { } d && d.Generation == generation && now - d.ObservedAt is >= 0 and < 10000 ? d : null;
    internal void Press(SelectionDecision decision) => pressed = decision;
    internal void Confirm(uint action, long now, long generation, bool consumesPreparation, bool recipientConfirmed = true)
    {
        if (!recipientConfirmed) { Reset(); return; }
        if (pressed is { } p && p.Generation == generation && now - p.ObservedAt is >= 0 and < 3000 &&
            (action == p.Preparation || p.Preparation == 0 && p.CompletionAction != 0 && action == p.PlannedGcd))
        {
            accepted = action == p.Preparation ? p with { ObservedAt = now } : Completion(p, now);
            pressed = null;
        }
        else if (accepted is { } current && action == current.PlannedGcd)
        {
            accepted = current.CompletionAction == 0 ? null : Completion(current, now);
            pressed = null;
        }
        else if (accepted is not null && consumesPreparation) Reset();
    }
    private static SelectionDecision Completion(SelectionDecision decision, long now) => decision with
    { Action = decision.CompletionAction, PlannedGcd = decision.CompletionAction, Preparation = 0, CompletionAction = 0,
        ObservedAt = now, Reason = "Complete confirmed preparation on the retained recipient" };
    internal void CancelPress() => pressed = null;
    internal void CancelAccepted() => accepted = null;
    internal void Reset() { pressed = null; accepted = null; }
}
