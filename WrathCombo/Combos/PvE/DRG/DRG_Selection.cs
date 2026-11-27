using System;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class DRG
{
    // This is the weaponskill portion used by the main providers, without
    // executing an opener, a content request or a second full rotation preview.
    private static uint PlannedWeaponskill(bool area) => DoBasicCombo(onAoE: area,
        includeDisembowel: area && (IsEnabled(Preset.DRG_AoE_SimpleMode) || IsEnabled(Preset.DRG_AoE_Disembowel)));

    internal static bool PreparedWeaponskillStillValid(SelectionDecision decision)
    {
        var target = Svc.Objects.SearchById(decision.Recipient);
        return target is { IsDead: false, IsTargetable: true } && ActionRoutes.Allowed(decision.PlannedGcd) &&
            ActionLearned(decision.PlannedGcd) && InActionRange(decision.PlannedGcd, target) &&
            (LocalPlayer.HasStatus(Buffs.LifeSurge) || Environment.TickCount64 - decision.ObservedAt < 1200);
    }

    internal static uint ReviewPreparation(uint input, uint result, Preset preset)
    {
        if (!ExperimentalSelection.ActiveDrg || SelectionRuntime.ScriptedOpener || preset is not (Preset.DRG_ST_SimpleMode or Preset.DRG_AoE_SimpleMode or
            Preset.DRG_ST_AdvancedMode or Preset.DRG_AoE_AdvancedMode)) return result;
        if (InCombat() && PlayerHealthPercentageHp() > 35 && NativeExecution.Capture().CanSubmit(false, 0))
        {
            bool areaMode = preset is Preset.DRG_AoE_SimpleMode or Preset.DRG_AoE_AdvancedMode;
            bool simple = preset is Preset.DRG_ST_SimpleMode or Preset.DRG_AoE_SimpleMode;
            bool Enabled(Preset single, Preset aoe) => simple || IsEnabled(areaMode ? aoe : single);
            uint earned = EarnedActionDeadline.Choose([
                new(MirageDive, LocalPlayer.Status(Buffs.DiveReady).RemainingTimeOrZero(),
                    Enabled(Preset.DRG_ST_Mirage, Preset.DRG_AoE_Mirage) && ActionRoutes.Allowed(MirageDive) &&
                    UseMirageDive(areaMode, ignoreDoubleMirageHold: true)),
                new(Starcross, LocalPlayer.Status(Buffs.StarcrossReady).RemainingTimeOrZero(),
                    Enabled(Preset.DRG_ST_Starcross, Preset.DRG_AoE_Starcross) && ActionRoutes.Allowed(Starcross) && UseStarcross())
            ], RemainingGCD, GCDTotal > 0 ? GCDTotal : 2.5f);
            if (earned != 0) return earned;
        }
        if (SelectionRuntime.Commitment is { Preparation: LifeSurge } prepared &&
            Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(result) is { ActionCategory.RowId: 3, CanTargetHostile: true })
        {
            if (!PreparedWeaponskillStillValid(prepared)) { SelectionRuntime.CancelCommitment("Life Surge target or weaponskill invalid"); return result; }
            if (!LocalPlayer.HasStatus(Buffs.LifeSurge) || !ActionReadyForTarget(prepared.PlannedGcd, prepared.Recipient)) return All.Cease;
            return SelectionRuntime.Publish(input, prepared with { Action = prepared.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 });
        }
        if (result != LifeSurge || CurrentTarget is not { IsDead: false } target) return result;
        bool area = preset is Preset.DRG_AoE_SimpleMode or Preset.DRG_AoE_AdvancedMode;
        uint next = SelectionRuntime.PlannedGcd != 0 ? SelectionRuntime.PlannedGcd : PlannedWeaponskill(area);
        if (!ActionRoutes.Allowed(next) || !ActionLearned(next) || !InActionRange(next, target)) return result;
        return SelectionRuntime.Publish(input, new(LifeSurge, next, target.GameObjectId, LifeSurge,
            "Life Surge retains the selected weaponskill and recipient", Environment.TickCount64, SelectionRuntime.Generation));
    }
}
