using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Combos.PvE;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.CustomComboNS;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class HealingGroundSelection
{
    internal static bool Supported(uint action) => action is WHM.Asylum or WHM.LiturgyOfTheBell or SCH.SacredSoil or AST.EarthlyStar;

    internal static uint Select(uint input, uint action)
    {
        if (!ExperimentalSelection.ActiveHealer || !ExperimentalSelection.GroundTargets || !Supported(action) ||
            !Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(action, out var row)) return action;
        if (SelectionRuntime.Preview(input) is { GroundPosition: not null } existing && existing.Action == action) return action;
        var deliberate = new List<(TargetSource Source, IBattleChara? Actor)>();
        if (!SelectionRuntime.Automatic || ExperimentalSelection.OverridesInAutomation)
        {
            if (ExperimentalSelection.PartyMouseover) deliberate.Add((TargetSource.PartyMouseover, SimpleTarget.UIMouseOverTarget));
            if (ExperimentalSelection.WorldMouseover) deliberate.Add((TargetSource.WorldMouseover, SimpleTarget.ModelMouseOverTarget as IBattleChara));
        }
        deliberate.Add((TargetSource.Selected, (SimpleTarget.SoftTarget ?? SimpleTarget.HardTarget) as IBattleChara));
        if (ExperimentalSelection.Focus && (!SelectionRuntime.Automatic || ExperimentalSelection.OverridesInAutomation))
            deliberate.Add((TargetSource.Focus, SimpleTarget.FocusTarget as IBattleChara));
        var party = GetPartyMembers().Select(member => member.BattleChara).OfType<IBattleChara>().Append(Player.Object!).DistinctBy(actor => actor.GameObjectId).ToArray();
        var all = party.Concat(deliberate.Select(item => item.Actor).OfType<IBattleChara>()).DistinctBy(actor => actor.GameObjectId)
            .Where(actor => !actor.IsHostile()).ToArray();
        var points = new Dictionary<ulong, Vector3>();
        float range = ActionManager.GetActionRange(action);
        foreach (var actor in all)
            if (!actor.IsDead && actor.IsTargetable && IsOverGround(actor.Position + new Vector3(0, 2, 0), out var ground) &&
                Vector3.Distance(Player.Position, ground) <= range)
                points[actor.GameObjectId] = ground;
        var candidates = all.Select(actor => new TargetActor(actor.GameObjectId, points.GetValueOrDefault(actor.GameObjectId, actor.Position),
            0, points.ContainsKey(actor.GameObjectId), party.Any(member => member.GameObjectId == actor.GameObjectId), GetTargetHPPercent(actor) / 100f)).ToImmutableArray();
        var affected = party.Where(actor => !actor.IsDead && actor.IsTargetable).Select(actor => new TargetActor(actor.GameObjectId,
            actor.Position, actor.HitboxRadius, true, true, GetTargetHPPercent(actor) / 100f)).ToImmutableArray();
        var overrides = deliberate.Where(item => item.Actor is not null && !item.Actor.IsHostile())
            .Select(item => new TargetOverride(item.Source, item.Actor!.GameObjectId)).ToImmutableArray();
        float radius = action == WHM.LiturgyOfTheBell ? 20 : row.EffectRange;
        var snapshot = new TargetSelectionInput(Player.Position, 0, new(range, radius, EffectShape.Circle),
            new(ExperimentalSelection.AutomaticAllies, ExperimentalSelection.Fallback, true, true, false), candidates, affected,
            TargetOrder.Apply(overrides, ExperimentalSelection.AllyOverrideOrder).ToImmutableArray());
        var choice = TargetSelection.Select(snapshot);
        TestingApi.RecordDecision("Target", snapshot, choice);
        if (choice.Recipient is not { } recipient || !points.TryGetValue(recipient, out var position)) return All.Cease;
        var proposal = SelectionRuntime.Preview(input);
        if (proposal?.Action != action)
            proposal = new(action, action, recipient, 0, choice.Reason, Environment.TickCount64, SelectionRuntime.Generation);
        return SelectionRuntime.Publish(input, proposal.PlaceGround(recipient, position, Environment.TickCount64,
            "Retained healing ground placement: " + choice.Reason));
    }

    internal static bool StillValid(uint action, Vector3 point) => Supported(action) &&
        Vector3.Distance(Player.Position, point) <= ActionManager.GetActionRange(action) &&
        IsOverGround(point + new Vector3(0, 2, 0), out var ground) && Vector3.Distance(point, ground) < .5f;
}
