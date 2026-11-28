using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class BLM
{
    internal static uint BindInstant(uint input, uint result, Preset preset)
    {
        if (!ExperimentalSelection.ActiveBlackMage || SelectionRuntime.ScriptedOpener || result != Triplecast && result != Role.Swiftcast ||
            preset is not (Preset.BLM_ST_SimpleMode or Preset.BLM_ST_AdvancedMode or Preset.BLM_AoE_SimpleMode or Preset.BLM_AoE_AdvancedMode)) return result;
        if (SelectionRuntime.Preview(input) is { } existing && PreparedCast.Owns(existing)) return result;
        if (CurrentTarget is not { } target) return All.Cease;
        bool area = preset is Preset.BLM_AoE_SimpleMode or Preset.BLM_AoE_AdvancedMode;
        uint spell = SelectionRuntime.PlannedGcd;
        if (spell == 0)
        {
            spell = OriginalHook(area ? Blizzard2 : ActionLearned(Blizzard3) ? Blizzard3 : Blizzard);
            if (IsInFirePhase && !IsEndOfFirePhase)
            {
                if (area) UseAoEFirePhaseGcd(ref spell, useTriplecast: false, useTranspose: false);
                else UseFirePhaseGcd(ref spell, useTranspose: false, usePolyglot: false);
            }
            else if (IsInIcePhase)
            {
                if (area) UseAoEIcePhaseGcd(ref spell, useTranspose: false);
                else UseIcePhaseGcd(ref spell, useTranspose: false);
            }
        }
        if (NativeExecution.CastTime(spell) <= 0) return ActionReady(spell) ? spell : All.Cease;
        return PreparedCast.Prepare(input, spell, target.GameObjectId, result);
    }

    internal static unsafe uint ReviewTestingCast(uint input, uint action, Preset preset)
    {
        if (!ExperimentalSelection.ActiveBlackMage || !SelectionRuntime.Automatic && input < 1000000 ||
            preset is not (Preset.BLM_ST_SimpleMode or Preset.BLM_ST_AdvancedMode or Preset.BLM_AoE_SimpleMode or Preset.BLM_AoE_AdvancedMode) ||
            !Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(action, out var row) || !row.CanTargetHostile) return action;
        float? limit = EncounterForecast.Earliest(BossForecastProvider.MaxCastTime, BossForecastProvider.Downtime);
        var forecast = BossForecastProvider.Fresh;
        bool moving = IsMoving() || forecast?.Moving == true;
        if (limit is null && !moving && forecast?.ForceCancel != true && forecast?.ForceCancelAI != true) return action;
        float cast = ActionManager.GetAdjustedCastTime(ActionType.Action, action) / 1000f;
        var snapshot = new CastBudgetInput(cast, RemainingGCD, moving, forecast?.ForceCancel == true || forecast?.ForceCancelAI == true ? 0 : limit, null);
        bool fits = CastBudget.Fits(snapshot);
        TestingApi.RecordDecision("Cast.Budget", snapshot, fits);
        if (fits) return action;
        if (SelectionRuntime.ScriptedOpener) return All.Cease;
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
        if (!area && !moving && forecast?.ForceCancel != true && forecast?.ForceCancelAI != true)
        {
            uint fallback = OriginalHook(IsInFirePhase ? Fire : Blizzard);
            if (ActionRoutes.Allowed(fallback) && ActionReady(fallback) && InActionRange(fallback) &&
                CastBudget.Fits(new(NativeExecution.CastTime(fallback), RemainingGCD, false, limit, null)))
                return fallback;
        }
        return All.Cease;
    }
}
