using System;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class BLU
{
    internal static uint ReviewCapabilities(uint input, uint result, Preset preset)
    {
        if (!ExperimentalSelection.ActiveBlueMage || preset is not (Preset.BLU_ST_DPS or Preset.BLU_AoE_DPS or Preset.BLU_ST_Tank or Preset.BLU_AoE_Tank)) return result;
        if (LocalPlayer.HasStatus(Buffs.PhantomFlurry) || LocalPlayer.HasStatus(Buffs.ChelonianGate) || LocalPlayer.HasStatus(Buffs.WaningNocturne)) return result;
        if (result < All.SingleTargetDPS && ActionReady(result)) return result;
        bool area = preset is Preset.BLU_AoE_DPS or Preset.BLU_AoE_Tank;
        // Basic equipped fillers only: never infer permission for Moon Flute,
        // Final Sting, Self-destruct, a new channel or another role's utility.
        foreach (uint action in area ? new[] { Electrogenesis, Plaincracker, SonicBoom, WaterCannon } : new[] { SonicBoom, WaterCannon })
            if (IsSpellActive(action) && ActionRoutes.Allowed(action) && ActionReady(action) &&
                (CurrentTarget is null || InActionRange(action)))
            {
                SelectionRuntime.Plan(input, action);
                return action;
            }
        return All.Cease;
    }

    private static bool PrepareTestingDot(ref uint input)
    {
        if (!IsSpellActive(SongOfTorment) || !ActionRoutes.Allowed(SongOfTorment) ||
            Target is not { } recipient || !recipient.IsHostile() || !recipient.IsTargetable || recipient.IsDead() ||
            !InActionRange(SongOfTorment, recipient) || !ActionReadyForTarget(SongOfTorment, recipient.GameObjectId)) return false;
        input = SelectionRuntime.Publish(input, new(Bristle, SongOfTorment, recipient.GameObjectId, Bristle,
            "Bristle for equipped, permitted Song of Torment", Environment.TickCount64, SelectionRuntime.Generation));
        return true;
    }

    internal static bool PreparedSpellStillValid(SelectionDecision decision)
    {
        var target = Svc.Objects.SearchById(decision.Recipient);
        return IsSpellActive(decision.PlannedGcd) && ActionRoutes.Allowed(decision.PlannedGcd) &&
            target is not null && target.IsTargetable && !target.IsDead() && InActionRange(decision.PlannedGcd, target) &&
            (LocalPlayer.HasStatus(Buffs.Bristle) || Environment.TickCount64 - decision.ObservedAt < 2000);
    }

    internal static uint ReviewTestingPreparation(uint input, uint result)
    {
        if (!ExperimentalSelection.ActiveBlueMage || SelectionRuntime.Commitment is not { Preparation: Bristle } prepared ||
            !Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(result, out var row) || row.ActionCategory.Value.RowId is not (2 or 3)) return result;
        if (!PreparedSpellStillValid(prepared)) return result;
        if (!LocalPlayer.HasStatus(Buffs.Bristle) || !ActionReadyForTarget(prepared.PlannedGcd, prepared.Recipient)) return All.Cease;
        return SelectionRuntime.Publish(input, prepared with { Action = prepared.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 });
    }
}
