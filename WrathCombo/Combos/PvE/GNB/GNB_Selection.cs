using WrathCombo.CustomComboNS;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
using static WrathCombo.Combos.PvE.GNB.Config;

namespace WrathCombo.Combos.PvE;

internal partial class GNB
{
    private static uint PlanTestingGcd(bool area, bool advanced)
    {
        if (!ExperimentalSelection.ActiveGunbreaker) return 0;
        Preset Option(Preset single, Preset aoe) => advanced ? area ? aoe : single : area ? Preset.GNB_AoE_Simple : Preset.GNB_ST_Simple;
        uint Allowed(uint action) => action != 0 && ActionLearned(action) && ActionRoutes.Allowed(action) && InActionRange(action) ? action : 0;
        // These steps reconstruct from the native gauge, including manual starts and ST/AoE changes.
        uint chain = GunStep switch { 1 or 2 => OriginalHook(GnashingFang), 3 or 4 => OriginalHook(ReignOfBeasts), _ => 0 };
        var reign = Option(Preset.GNB_ST_Reign, Preset.GNB_AoE_Reign);
        var sonic = Option(Preset.GNB_ST_SonicBreak, Preset.GNB_AoE_SonicBreak);
        uint urgent = IsEnabled(sonic) && CanSB && StatusTime(Buffs.ReadyToBreak) <= RemainingGCD + GCDLength + .1f
            ? SonicBreak : 0;
        bool wantFang = !area && (GetCooldownRemainingTime(GnashingFang) < .5f || !ActionLearned(ReignOfBeasts));
        bool roomForChain = ResourceWindow.CanFinish(new(0, false, GCDLength, RemainingGCD, 3, false, BossForecastProvider.Downtime));
        uint burst = wantFang
            ? ShouldUseGnashingFangBurst(Option(Preset.GNB_ST_GnashingFang, Preset.GNB_AoE_Simple)) && roomForChain ? GnashingFang : 0
            : ShouldUseReignOfBeasts(reign) && roomForChain ? ReignOfBeasts : 0;
        bool useSonic = area && advanced
            ? IsEnabled(sonic) && CanSB && (GNB_AoE_SonicBreak_EarlyOrLate == 0 || StatusTime(Buffs.ReadyToBreak) <= GCDLength + 10) && !CanFB
            : ShouldUseSonicBreak(sonic) && (!area || !CanFB);
        uint filler = !area && roomForChain && ShouldUseGnashingFangFiller(Option(Preset.GNB_ST_GnashingFang, Preset.GNB_AoE_Simple),
            advanced ? GNB_ST_HoldGFCharge : 0) ? GnashingFang : 0;
        uint basic = area ? AOECombo(advanced ? GNB_AoE_Overcap_Choice : 0, advanced ? GNB_AoE_FatedCircle_BurstStrike : 0)
            : STCombo(advanced ? GNB_ST_Overcap_Choice : 0);
        uint spend = area
            ? ShouldUseFatedCircle(Option(Preset.GNB_ST_BurstStrike, Preset.GNB_AoE_FatedCircle), advanced ? GNB_AoE_FatedCircle_Setup : 0)
                ? ActionLearned(FatedCircle) ? FatedCircle : !advanced || GNB_AoE_FatedCircle_BurstStrike == 0 ? BurstStrike : basic : 0
            : ShouldUseBurstStrike(Option(Preset.GNB_ST_BurstStrike, Preset.GNB_AoE_FatedCircle), advanced ? GNB_ST_BurstStrike_Setup : 0) ? BurstStrike : 0;
        var snapshot = new GunbreakerPlanInput(Allowed(chain), Allowed(urgent), Allowed(burst),
            ShouldUseDoubleDown(Option(Preset.GNB_ST_DoubleDown, Preset.GNB_AoE_DoubleDown)) ? Allowed(DoubleDown) : 0,
            useSonic ? Allowed(SonicBreak) : 0, Allowed(filler), Allowed(spend), Allowed(basic));
        uint result = GunbreakerPlan.Choose(snapshot);
        TestingApi.RecordDecision("GNB.PlannedGcd", snapshot, result);
        return result == 0 ? All.Cease : result;
    }
}
