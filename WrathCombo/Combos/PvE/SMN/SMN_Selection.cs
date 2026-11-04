using System;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class SMN
{
    private static unsafe bool PrepareDamageSpell(ref uint input, uint spell, bool enabled)
    {
        if (!enabled || GetActionCastTime(spell) <= 0 || !ActionRoutes.Allowed(spell) ||
            CurrentTarget is not { } target || !target.IsHostile() || target.IsDead() ||
            !ActionReadyForTarget(spell, target.GameObjectId) || !InActionRange(spell, target) ||
            !ActionManager.CanUseActionOnTarget(spell, target.GameObject())) return false;
        // Preparing an attack must not steal Swiftcast from a confirmed resurrection.
        if (SelectionRuntime.Commitment is not null) return false;
        input = SelectionRuntime.Publish(input, new(Role.Swiftcast, spell, target.GameObjectId, Role.Swiftcast,
            "Swiftcast for the selected enabled summon spell", Environment.TickCount64, SelectionRuntime.Generation));
        return true;
    }

    internal static bool PreparedSpellStillValid(SelectionDecision decision)
    {
        var target = Svc.Objects.SearchById(decision.Recipient);
        if (target is null || !target.IsTargetable || !SelectionRuntime.TargetLifeAllowed(decision.PlannedGcd, target) ||
            !InActionRange(decision.PlannedGcd, target)) return false;
        if (decision.PlannedGcd == Resurrection) return true;
        return decision.PlannedGcd == OriginalHook(Gemshine) || decision.PlannedGcd == OriginalHook(PreciousBrilliance) ||
            decision.PlannedGcd == Slipstream && LocalPlayer.HasStatus(Buffs.GarudasFavor);
    }

    private static bool ContinuePreparedSpell(ref uint input)
    {
        if (!ExperimentalSelection.ActiveSummoner || SelectionRuntime.Commitment is not { } prepared) return false;
        if (!PreparedSpellStillValid(prepared) || !LocalPlayer.HasStatus(Role.Buffs.Swiftcast) ||
            !ActionReadyForTarget(prepared.PlannedGcd, prepared.Recipient))
        { input = All.Cease; return true; }
        input = SelectionRuntime.Publish(input, prepared with { Action = prepared.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 });
        return true;
    }

    private static uint TestingRaise(uint input)
    {
        if (ContinuePreparedSpell(ref input)) return input;
        var recipient = SelectionRuntime.SelectAlly(Resurrection).Recipient;
        if (recipient is null || !ActionRoutes.Allowed(Resurrection) || !ActionReadyForTarget(Resurrection, recipient.Value)) return All.Cease;
        bool instant = LocalPlayer.HasStatus(Role.Buffs.Swiftcast);
        bool prepare = !instant && Role.CanSwiftcast();
        if (!instant && !prepare && (!ExperimentalSelection.HardCastRaise || IsMoving())) return All.Cease;
        return SelectionRuntime.Publish(input, new(prepare ? Role.Swiftcast : Resurrection, Resurrection, recipient.Value,
            prepare ? Role.Swiftcast : 0, "Manual resurrection retains its recipient and Swiftcast", Environment.TickCount64, SelectionRuntime.Generation));
    }
}
