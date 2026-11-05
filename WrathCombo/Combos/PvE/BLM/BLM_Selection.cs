using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class BLM
{
    internal static unsafe uint ReviewTestingCast(uint input, uint action, Preset preset)
    {
        if (!ExperimentalSelection.ActiveBlackMage || !SelectionRuntime.Automatic && input < 1000000 ||
            preset is not (Preset.BLM_ST_SimpleMode or Preset.BLM_ST_AdvancedMode or Preset.BLM_AoE_SimpleMode or Preset.BLM_AoE_AdvancedMode) ||
            !Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(action, out var row) || !row.CanTargetHostile) return action;
        float? limit = EncounterForecast.Earliest(BossForecastProvider.MaxCastTime, BossForecastProvider.Downtime);
        if (limit is null) return action;
        float cast = ActionManager.GetAdjustedCastTime(ActionType.Action, action) / 1000f;
        var snapshot = new CastBudgetInput(cast, RemainingGCD, IsMoving(), limit, null);
        bool fits = CastBudget.Fits(snapshot);
        TestingApi.RecordDecision("Cast.Budget", snapshot, fits);
        if (fits) return action;
        bool area = preset is Preset.BLM_AoE_SimpleMode or Preset.BLM_AoE_AdvancedMode;
        bool simple = preset is Preset.BLM_ST_SimpleMode or Preset.BLM_AoE_SimpleMode;
        uint alternative = action;
        if (!area && (simple || IsEnabled(Preset.BLM_ST_Movement)) &&
            UseMovementGcd(ref alternative, !simple, forecast: true))
        {
            // A configured movement candidate can itself be a cast at a synced level.
            // Validate it instead of assuming every entry is always instant.
            if (ActionManager.GetAdjustedCastTime(ActionType.Action, alternative) == 0) return alternative;
        }
        if (area && (simple || IsEnabled(Preset.BLM_AoE_Movement)) &&
            ActionReady(Triplecast) && !LocalPlayer.HasStatus(Buffs.Triplecast) &&
            !LocalPlayer.HasStatus(Role.Buffs.Swiftcast) && !JustUsed(Triplecast)) return Triplecast;
        return All.Cease;
    }
}
