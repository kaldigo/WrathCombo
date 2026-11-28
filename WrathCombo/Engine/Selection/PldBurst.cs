namespace WrathCombo.Engine.Selection;

internal sealed record PldBurstInput(uint PlannedGcd, bool Eligible, bool BurstActive, bool ProcAvailable,
    bool Aoe, bool RiotLearned, bool FinisherLearned, bool ProminenceLearned, bool AtonementLearned);

internal static class PldBurst
{
    internal static bool Start(PldBurstInput s)
    {
        if (!s.Eligible || s.BurstActive) return false;
        if (s.AtonementLearned && !s.Aoe) return s.ProcAvailable;
        if (s.Aoe) return s.PlannedGcd == (s.ProminenceLearned ? 16457u : 7381u);
        if (!s.RiotLearned) return s.PlannedGcd == 9;
        if (!s.FinisherLearned) return s.PlannedGcd == 15;
        return s.PlannedGcd is 21 or 3539;
    }
}
