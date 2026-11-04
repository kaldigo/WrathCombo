namespace WrathCombo.Engine.Selection;

internal sealed record PvpMchPreparationInput(uint PlannedTool, bool Ready, bool Prepared, bool Overheated, bool PreferAnchor);

internal static class PvpMchPreparation
{
    internal static bool Analysis(PvpMchPreparationInput s) => s.Ready && !s.Prepared && !s.Overheated &&
        (s.PlannedTool == 29405 || s.PlannedTool == (s.PreferAnchor ? 29407u : 29408u));
}
