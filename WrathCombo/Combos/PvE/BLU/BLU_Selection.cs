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
