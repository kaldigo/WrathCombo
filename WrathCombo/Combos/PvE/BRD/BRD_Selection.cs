using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;

namespace WrathCombo.Combos.PvE;

internal partial class BRD
{
    private static uint SelectDamageGcd(Combo flags)
    {
        if (ExperimentalSelection.ActiveBard)
        {
            bool area = flags.HasFlag(Combo.AoE), simple = flags.HasFlag(Combo.Simple);
            bool Ready(uint action, Preset single, Preset aoe) => (simple || IsEnabled(area ? aoe : single)) &&
                ActionRoutes.Allowed(action) && ActionReady(action) && InActionRange(action);
            uint deadline = EarnedActionDeadline.Choose([
                new(BlastArrow, LocalPlayer.Status(Buffs.BlastArrowReady).RemainingTimeOrZero(), Ready(BlastArrow, Preset.BRD_ST_ApexArrow, Preset.BRD_AoE_ApexArrow)),
                new(RadiantEncore, LocalPlayer.Status(Buffs.RadiantEncoreReady).RemainingTimeOrZero(), Ready(RadiantEncore, Preset.BRD_Adv_BuffsEncore, Preset.BRD_AoE_BuffsEncore)),
                new(ResonantArrow, LocalPlayer.Status(Buffs.ResonantArrowReady).RemainingTimeOrZero(), Ready(ResonantArrow, Preset.BRD_Adv_BuffsResonant, Preset.BRD_AoE_BuffsResonant)),
            ], RemainingGCD, GCDTotal > 0 ? GCDTotal : 2.5f);
            if (deadline != 0) return deadline;
        }
        uint action = OriginalHook(flags.HasFlag(Combo.AoE) ? QuickNock : HeavyShot);
        return TryGCDAttacks(flags, ref action) && ActionReady(action)
            ? action : OriginalHook(flags.HasFlag(Combo.AoE) ? QuickNock : HeavyShot);
    }
}
