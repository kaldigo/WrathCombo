using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class WAR
{
    private static uint SelectDamageGcd(Combo flags)
    {
        if (ExperimentalSelection.ActiveWarrior && HasNascentChaos)
        {
            bool area = flags.HasFlag(Combo.AoE), simple = flags.HasFlag(Combo.Simple);
            uint chaos = OriginalHook(area ? Decimate : InnerBeast);
            bool allowed = (simple || IsEnabled(area ? Preset.WAR_AoE_Decimate : Preset.WAR_ST_FellCleave)) &&
                ActionRoutes.Allowed(chaos) && ActionReady(chaos) && InActionRange(chaos);
            uint deadline = EarnedActionDeadline.Choose([new(chaos,
                LocalPlayer.Status(Buffs.NascentChaos).RemainingTimeOrZero(), allowed)], RemainingGCD,
                GCDTotal > 0 ? GCDTotal : 2.5f);
            if (deadline != 0) return deadline;
        }
        uint action = flags.HasFlag(Combo.AoE) ? AoECombo : STCombo;
        TryGCDAttacks(flags, ref action);
        return action;
    }
}
