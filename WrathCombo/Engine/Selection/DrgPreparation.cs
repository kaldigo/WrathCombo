namespace WrathCombo.Engine.Selection;

internal sealed record DrgPreparationInput(uint PlannedGcd, uint FullThrust, uint VorpalThrust, uint Disembowel,
    uint ChaosThrust, uint AoeStarter, bool DrakesbaneLearned, bool FullThrustLearned, bool SonicThrustLearned,
    bool CoerthanTormentLearned, bool Aoe, bool InRange, bool Buffed, bool Ready, bool AlreadyPrepared);

internal static class DrgPreparation
{
    internal static bool LifeSurge(DrgPreparationInput s)
    {
        if (!s.Ready || s.AlreadyPrepared || !s.InRange) return false;
        if (s.Aoe)
            return s.CoerthanTormentLearned ? s.PlannedGcd == 16477 && s.Buffed
                : s.SonicThrustLearned ? s.PlannedGcd == 7397 : s.PlannedGcd == s.AoeStarter;
        if (s.PlannedGcd == s.FullThrust || s.PlannedGcd == 36952) return true;
        if (!s.FullThrustLearned && (s.PlannedGcd == s.VorpalThrust || s.PlannedGcd == s.Disembowel)) return true;
        return s.DrakesbaneLearned && s.Buffed && (s.PlannedGcd == s.ChaosThrust || s.PlannedGcd is 3554 or 3556);
    }
}
