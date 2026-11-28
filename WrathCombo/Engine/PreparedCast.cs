using System;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using WrathCombo.Combos.PvE;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class PreparedCast
{
    internal static bool Owns(SelectionDecision decision) => decision.Preparation is RoleActions.Magic.Swiftcast or BLM.Triplecast &&
        !IncomingRaise.IsRaise(decision.PlannedGcd);

    internal static bool StillValid(SelectionDecision decision)
    {
        var target = Svc.Objects.SearchById(decision.Recipient);
        return target is { IsDead: false, IsTargetable: true } && ActionRoutes.Allowed(decision.PlannedGcd) &&
            ActionLearned(decision.PlannedGcd) && InActionRange(decision.PlannedGcd, target) &&
            (HasInstant(decision) || Environment.TickCount64 - decision.ObservedAt < 1200) &&
            ((uint)Player.Job != 27 || SMN.PreparedSpellStillValid(decision));
    }

    internal static bool Continue(uint input, Preset preset, out uint action)
    {
        action = input;
        if (!ExperimentalSelection.ActivePreparedCaster && !ExperimentalSelection.ActivePictomancer && !ExperimentalSelection.ActiveBlackMage ||
            SelectionRuntime.ScriptedOpener || preset.Attributes().AutoAction is not { IsHeal: false } || SelectionRuntime.Commitment is not { } pending || !Owns(pending)) return false;
        if (!StillValid(pending)) { SelectionRuntime.CancelCommitment("Instant-cast preparation no longer useful"); return false; }
        if (!HasInstant(pending) || !ActionReadyForTarget(pending.PlannedGcd, pending.Recipient))
        { action = All.Cease; return true; }
        action = SelectionRuntime.Publish(input, pending with { Action = pending.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 });
        return true;
    }

    private static bool HasInstant(SelectionDecision decision) => decision.Preparation == BLM.Triplecast
        ? LocalPlayer.HasStatus(BLM.Buffs.Triplecast) : LocalPlayer.HasStatus(RoleActions.Magic.Buffs.Swiftcast);

    internal static uint Prepare(uint input, uint spell, ulong recipient, uint preparation = RoleActions.Magic.Swiftcast)
    {
        if (SelectionRuntime.Commitment != null || !ActionRoutes.Allowed(spell) || !ActionLearned(spell) ||
            NativeExecution.CastTime(spell) <= 0 || !ActionRoutes.Allowed(preparation) ||
            !(preparation == RoleActions.Magic.Swiftcast ? RoleActions.Magic.CanSwiftcast(weave: false) :
                preparation == BLM.Triplecast && ExperimentalSelection.ActiveBlackMage && ActionReadyForTarget(preparation, Player.Object!.GameObjectId)) || !NativeExecution.BaseAvailable(spell, recipient)) return All.Cease;
        return SelectionRuntime.Publish(input, new(preparation, spell, recipient, preparation,
            "Instant-cast preparation reserved for the selected enabled spell", Environment.TickCount64, SelectionRuntime.Generation));
    }
}
