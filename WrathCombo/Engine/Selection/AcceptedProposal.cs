namespace WrathCombo.Engine.Selection;

internal sealed class AcceptedProposal<T> where T : class
{
    private (uint Action, T Value, long At, long Generation)? pressed;
    internal void Press(uint action, T value, long now, long generation) => pressed = (action, value, now, generation);
    internal T? Confirm(uint action, long now, long generation)
    {
        var proposal = pressed;
        pressed = null;
        return proposal is { } p && p.Action == action && p.Generation == generation &&
               now - p.At is >= 0 and < 3000 ? p.Value : null;
    }
    internal void Reset() => pressed = null;
}
