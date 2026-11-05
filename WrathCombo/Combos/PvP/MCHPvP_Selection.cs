using System;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvP;

internal static partial class MCHPvP
{
    private static uint PrepareAnalysis(uint input, uint tool) => CurrentTarget is { } target && tool != 0
        ? SelectionRuntime.Publish(input, new(Analysis, tool, target.GameObjectId, Analysis,
            "Analysis prepares the selected eligible tool", Environment.TickCount64, SelectionRuntime.Generation))
        : PvE.All.Cease;

    internal static unsafe bool PreparedToolStillValid(SelectionDecision decision)
    {
        var target = Svc.Objects.SearchById(decision.Recipient) as IBattleChara;
        ushort primed = decision.PlannedGcd switch
        { Drill => Buffs.DrillPrimed, BioBlaster => Buffs.BioblasterPrimed, AirAnchor => Buffs.AirAnchorPrimed, ChainSaw => Buffs.ChainSawPrimed, _ => 0 };
        return primed != 0 && LocalPlayer.HasStatus(primed) && target is not null && !target.IsDead() &&
            target.IsTargetable && target.IsHostile() && ActionRoutes.Allowed(decision.PlannedGcd) &&
            PvpTargetEligibility.CanDamage(decision.PlannedGcd, target) && InActionRange(decision.PlannedGcd, target) &&
            ActionManager.CanUseActionOnTarget(decision.PlannedGcd, target.GameObject()) &&
            (LocalPlayer.HasStatus(Buffs.Analysis) || Environment.TickCount64 - decision.ObservedAt < 2000);
    }

    private static bool TryPreparedTool(uint input, out uint result)
    {
        result = 0;
        if (!ExperimentalSelection.ActivePvpMachinist || input is PvPIntentRouting.Offense or PvPIntentRouting.Defense or PvPIntentRouting.Support ||
            SelectionRuntime.Commitment is not { Preparation: Analysis } prepared || !PreparedToolStillValid(prepared)) return false;
        result = LocalPlayer.HasStatus(Buffs.Analysis) && ActionReadyForTarget(prepared.PlannedGcd, prepared.Recipient)
            ? SelectionRuntime.Publish(input, prepared with { Action = prepared.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 })
            : PvE.All.Cease;
        return true;
    }
}
