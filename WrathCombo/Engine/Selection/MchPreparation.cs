namespace WrathCombo.Engine.Selection;

internal sealed record MchPreparationInput(uint PlannedGcd, bool Ready, bool Allowed, bool InRange,
    bool AlreadyPrepared, bool Overheated, bool DrillLearned, bool CleanShotLearned);

internal static class MchPreparation
{
    internal static bool Reassemble(MchPreparationInput s)
    {
        if (!s.Ready || !s.Allowed || !s.InRange || s.AlreadyPrepared || s.Overheated) return false;
        // Direct weaponskill damage only. Full Metal Field already guarantees
        // critical/direct hits; Bioblaster and Flamethrower are not tool substitutes.
        return s.PlannedGcd is 25788 or 36981 or 16500 or 16498 or 2870 or 25786 ||
            !s.DrillLearned && s.PlannedGcd is 2873 or 7413 ||
            !s.CleanShotLearned && s.PlannedGcd == 2872;
    }
}
