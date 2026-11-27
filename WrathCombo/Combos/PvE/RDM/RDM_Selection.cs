using System;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.Native;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class RDM
{
    internal static bool PreparedRaiseStillValid(SelectionDecision decision)
    {
        var target = Svc.Objects.SearchById(decision.Recipient);
        return decision.PlannedGcd == Verraise && target is not null && target.IsTargetable &&
            SelectionRuntime.TargetLifeAllowed(Verraise, target) && InActionRange(Verraise, target);
    }

    internal static bool TryTestingSelection(uint input, Preset preset, out uint action)
    {
        action = input;
        if (!ExperimentalSelection.ActiveRedMage || SelectionRuntime.Commitment is not { } prepared) return false;
        bool damage = preset is Preset.RDM_ST_SimpleMode or Preset.RDM_ST_DPS &&
            CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.SingleTargetDPS, Jolt, Jolt2, Jolt3) ||
            preset is Preset.RDM_AoE_SimpleMode or Preset.RDM_AoE_DPS &&
            CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.AoEDPS, Scatter, Impact);
        if (!damage) return false;
        action = ContinueRaise(input, prepared);
        return true;
    }

    private static uint ContinueRaise(uint input, SelectionDecision prepared)
    {
        if (!PreparedRaiseStillValid(prepared) || !HasDualcast && !HasSwiftcast ||
            !ActionReadyForTarget(Verraise, prepared.Recipient)) return All.Cease;
        return SelectionRuntime.Publish(input, prepared with { Action = Verraise, Preparation = 0, ObservedAt = Environment.TickCount64 });
    }

    private static unsafe uint TestingRaise(uint input)
    {
        if (SelectionRuntime.Commitment is { } prepared) return ContinueRaise(input, prepared);
        var recipient = SelectionRuntime.SelectAlly(Verraise).Recipient;
        if (recipient is null || !ActionLearned(Verraise) || !ActionRoutes.Allowed(Verraise)) return All.Cease;
        int raiseCost = ActionManager.GetActionCost(ActionType.Action, Verraise, 0, 0, 0, 0);
        if (LocalPlayer.CurrentMp < raiseCost) return All.Cease;
        uint preparation = 0;
        if (!HasDualcast && !HasSwiftcast)
        {
            if (Role.CanSwiftcast()) preparation = Role.Swiftcast;
            else if (IsEnabled(Preset.RDM_Raise_Vercure) && !IsMoving() && ActionReadyForTarget(Vercure, Player.Object!.GameObjectId) &&
                LocalPlayer.CurrentMp >= raiseCost + ActionManager.GetActionCost(ActionType.Action, Vercure, 0, 0, 0, 0)) preparation = Vercure;
            else if (!ExperimentalSelection.HardCastRaise || IsMoving()) return All.Cease;
        }
        if (preparation == 0 && !ActionReadyForTarget(Verraise, recipient.Value)) return All.Cease;
        return SelectionRuntime.Publish(input, new(preparation == 0 ? Verraise : preparation, Verraise, recipient.Value, preparation,
            "Reserve instant-cast resources for the requested resurrection", Environment.TickCount64, SelectionRuntime.Generation));
    }
}
