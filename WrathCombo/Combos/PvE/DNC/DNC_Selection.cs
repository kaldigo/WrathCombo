using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.Native;
using static WrathCombo.Combos.PvE.DNC.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class DNC
{
    internal static bool DanceInProgress => Gauge.IsDancing;

    internal static bool TryTestingDance(uint input, Preset preset, out uint action)
    {
        action = 0;
        if (!ExperimentalSelection.ActiveDancer || !Gauge.IsDancing || preset is not
            (Preset.DNC_ST_SimpleMode or Preset.DNC_ST_AdvancedMode or Preset.DNC_AoE_SimpleMode or Preset.DNC_AoE_AdvancedMode)) return false;
        bool area = preset is Preset.DNC_AoE_SimpleMode or Preset.DNC_AoE_AdvancedMode;
        if (!CustomActionHelper.OneButtonRotationChecker(input, area ? CustomActionType.AoEDPS : CustomActionType.SingleTargetDPS,
                area ? Windmill : Cascade)) return false;
        // An observed dance belongs to the player, including a manually initiated dance.
        // Initiation toggles cannot strand its remaining steps.
        if (LocalPlayer.HasStatus(Buffs.StandardStep))
            action = Gauge.CompletedSteps < 2 ? Gauge.NextStep : FinishOrHold(StandardFinish2);
        else if (LocalPlayer.HasStatus(Buffs.TechnicalStep))
            action = Gauge.CompletedSteps < 4 ? Gauge.NextStep : FinishOrHold(TechnicalFinish4);
        return action != 0;
    }

    private static bool CanBeginTestingDance(uint start, int steps)
    {
        if (!ExperimentalSelection.ActiveDancer || !InCombat()) return true;
        var snapshot = new DanceBudgetInput(ActionManager.GetAdjustedRecastTime(ActionType.Action, start) / 1000f,
            ActionManager.GetAdjustedRecastTime(ActionType.Action, Emboite) / 1000f, steps, RemainingGCD,
            ExperimentalSelection.Forecasts ? BossForecastProvider.Downtime : null);
        bool result = DanceBudget.CanStart(snapshot);
        TestingApi.RecordDecision("DNC.EntryBudget", snapshot, result);
        return result;
    }

    private static bool TryTestingDeadline(bool area, bool simple, out uint action)
    {
        action = 0;
        if (!ExperimentalSelection.ActiveDancer || Gauge.IsDancing) return false;
        // Only ready, permitted continuations compete here. Initiators retain their existing owners.
        IEnumerable<(uint Action, float Remaining)> Candidates()
        {
            if (simple || IsEnabled(area ? Preset.DNC_AoE_Adv_LD : Preset.DNC_ST_Adv_LD))
                yield return (LastDance, LocalPlayer.Status(Buffs.LastDanceReady).RemainingTimeOrZero());
            yield return (StarfallDance, LocalPlayer.Status(Buffs.FlourishingStarfall).RemainingTimeOrZero());
            if (simple || IsEnabled(area ? Preset.DNC_AoE_Adv_Tillana : Preset.DNC_ST_Adv_Tillana))
                yield return (Tillana, LocalPlayer.Status(Buffs.FlourishingFinish).RemainingTimeOrZero());
            if (Gauge.Esprit >= 50 && (simple || IsEnabled(area ? Preset.DNC_AoE_Adv_DawnDance : Preset.DNC_ST_Adv_DawnDance)))
                yield return (OriginalHook(DanceOfTheDawn), LocalPlayer.Status(Buffs.DanceOfTheDawnReady).RemainingTimeOrZero());
        }
        action = Candidates().Where(c => DanceBudget.ExpiresBeforeNext(c.Remaining, RemainingGCD, (float)GCD) &&
                ActionRoutes.Allowed(c.Action) && ActionReady(c.Action) &&
                (c.Action is LastDance or Tillana ? EnemyIn15Yalms : InActionRange(c.Action)))
            .OrderBy(c => c.Remaining).Select(c => c.Action).FirstOrDefault();
        return action != 0;
    }
}
