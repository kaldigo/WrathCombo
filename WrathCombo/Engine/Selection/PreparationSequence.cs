namespace WrathCombo.Engine.Selection;

internal sealed record SelectionDecision(uint Action, uint PlannedGcd, ulong Recipient, uint Preparation, string Reason, long ObservedAt, long Generation, long PreventionEvent = 0);

// Press records a proposal; only a confirmed action accepts it. Preview is a read.
internal sealed class PreparationSequence
{
    private SelectionDecision? pressed;
    private SelectionDecision? accepted;
    internal SelectionDecision? Current(long now, long generation) =>
        accepted is { } d && d.Generation == generation && now - d.ObservedAt is >= 0 and < 10000 ? d : null;
    internal void Press(SelectionDecision decision) => pressed = decision;
    internal void Confirm(uint action, long now, long generation, bool consumesPreparation)
    {
        if (pressed is { } p && p.Generation == generation && now - p.ObservedAt is >= 0 and < 3000 && action == p.Preparation)
        {
            accepted = p with { ObservedAt = now };
            pressed = null;
        }
        else if (accepted is { } current && (action == current.PlannedGcd || consumesPreparation)) Reset();
    }
    internal void CancelPress() => pressed = null;
    internal void CancelAccepted() => accepted = null;
    internal void Reset() { pressed = null; accepted = null; }
}
