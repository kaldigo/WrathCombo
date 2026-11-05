using System;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class MCH
{
    private static uint testingPlannedGcd;
    internal static void BeginTestingSelection() => testingPlannedGcd = 0;
    internal static unsafe bool PreparedToolStillValid(SelectionDecision decision)
    {
        var target = Svc.Objects.SearchById(decision.Recipient) as IBattleChara;
        return target is not null && target.IsTargetable && !target.IsDead() && target.IsHostile() &&
            ActionRoutes.Allowed(decision.PlannedGcd) && ActionLearned(decision.PlannedGcd) &&
            InActionRange(decision.PlannedGcd, target) &&
            ActionManager.CanUseActionOnTarget(decision.PlannedGcd, target.GameObject()) &&
            (LocalPlayer.HasStatus(Buffs.Reassembled) || Environment.TickCount64 - decision.ObservedAt < 2000);
    }

    internal static uint ReviewTestingPreparation(uint input, uint result)
    {
        if (!ExperimentalSelection.ActiveMachinist) return result;
        if (SelectionRuntime.Commitment is { Preparation: Reassemble } prepared &&
            Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(result, out var row) &&
            row.ActionCategory.Value.RowId is 2 or 3)
        {
            if (!PreparedToolStillValid(prepared)) return result;
            if (!LocalPlayer.HasStatus(Buffs.Reassembled) || !ActionReadyForTarget(prepared.PlannedGcd, prepared.Recipient)) return All.Cease;
            return SelectionRuntime.Publish(input, prepared with { Action = prepared.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 });
        }
        if (result != Reassemble || testingPlannedGcd == 0 || CurrentTarget is not { } target ||
            !target.IsHostile() || !ActionRoutes.Allowed(testingPlannedGcd)) return result;
        return SelectionRuntime.Publish(input, new(Reassemble, testingPlannedGcd, target.GameObjectId, Reassemble,
            "Reassemble retains the selected tool and recipient", Environment.TickCount64, SelectionRuntime.Generation));
    }
}
