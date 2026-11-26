using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using WrathCombo.Combos.PvE;
using WrathCombo.Extensions;
using WrathCombo.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class TankPull
{
    internal const uint Button = 1_000_019;
    private static long manualUntil, gatheringUntil;
    private static ulong approaching;
    internal static bool IsTank => (uint)Player.Job is 19 or 21 or 32 or 37;
    private static (uint Area, uint Ranged, uint Basic) Actions => (uint)Player.Job switch
    {
        19 => (PLD.TotalEclipse, PLD.ShieldLob, PLD.FastBlade),
        21 => (WAR.Overpower, WAR.Tomahawk, WAR.HeavySwing),
        32 => (DRK.Unleash, DRK.Unmend, DRK.HardSlash),
        37 => (GNB.DemonSlice, GNB.LightningShot, GNB.KeenEdge),
        _ => default,
    };
    private static bool InArea(IBattleChara enemy) =>
        AreaRange() > 0 && GetTargetDistance(enemy) <= AreaRange();
    private static float AreaRange() =>
        Data.ActionWatching.ActionSheet.TryGetValue(Actions.Area, out var row) ? row.EffectRange : 0;
    private static bool Usable(uint action, IBattleChara target) =>
        action != 0 && ActionLearned(action) && InActionRange(action, target) &&
        ActionReadyForTarget(action, target.GameObjectId, true, true);
    internal static uint Preview()
    {
        if (!Player.Available || !IsTank || InPvP()) return All.Cease;
        if (Svc.Targets.Target is not IBattleChara { IsDead: false, IsTargetable: true } enemy || !enemy.IsHostile())
            return ActionLearned(Actions.Ranged) ? Actions.Ranged : Actions.Basic;
        return Choose(enemy);
    }
    private static uint Choose(IBattleChara enemy)
    {
        if (InArea(enemy) && Usable(Actions.Area, Player.Object!)) return Actions.Area;
        if (Usable(Actions.Ranged, enemy)) return Actions.Ranged;
        if (Usable(RoleActions.Tank.Provoke, enemy)) return RoleActions.Tank.Provoke;
        if (!ActionLearned(Actions.Ranged) && Usable(Actions.Basic, enemy)) return Actions.Basic;
        return ActionLearned(Actions.Ranged) ? Actions.Ranged : Actions.Basic;
    }
    internal static unsafe bool Press()
    {
        if (!Player.Available || Player.IsDead || !IsTank || InPvP() ||
            Svc.Targets.Target is not IBattleChara { IsDead: false, IsTargetable: true } enemy || !enemy.IsHostile())
            return false;
        uint action = Choose(enemy);
        ulong target = action == Actions.Area ? Player.Object!.GameObjectId : enemy.GameObjectId;
        // Hold the exact native action and recipient through game queue replay.
        manualUntil = Environment.TickCount64 + 600;
        using var scope = AutomationOwnership.QueuedExternalScope();
        bool accepted = ActionManager.Instance()->UseAction(ActionType.Action, action, target);
        AccordDiagnostics.DiagnosticLog.Event("Tank.Pull.Manual", () => new { action, Target = enemy.GameObjectId, accepted });
        return accepted;
    }

    private static unsafe bool Protected(IBattleChara enemy) => enemy.IsBoss() ||
        enemy.TargetObject is IBattleChara { IsDead: false } owner && owner.GameObjectId != Player.Object!.GameObjectId &&
        (uint)owner.ClassJob.RowId is 19 or 21 or 32 or 37;
    private static unsafe bool ThreatensGroup(IBattleChara enemy)
    {
        if (!enemy.Struct()->InCombat || enemy.IsCasting || Protected(enemy)) return false;
        var victim = enemy.TargetObject as IBattleChara;
        return victim != null && !victim.IsHostile() &&
            (victim.GameObjectId == Player.Object!.GameObjectId || victim.IsInParty() || victim.Struct()->IsAllianceMember);
    }

    // Returns true while an action was submitted or a short approach window owns the GCD.
    internal static unsafe bool Tick()
    {
        long now = Environment.TickCount64;
        if (now < manualUntil) return true;
        if (!ExperimentalSelection.AutoRoleMechanics || !IsTank || InPvP() || !Player.Available || Player.IsDead)
        { approaching = 0; return false; }
        var selected = Svc.Targets.Target as IBattleChara;
        var enemies = Svc.Objects.GetBattleCharas().Where(e => e.IsHostile() && !e.IsDead && e.IsTargetable &&
            !e.IsInvincible && !Service.Configuration.IgnoredNPCs.ContainsKey(e.BaseId)).ToArray();
        // Selected idle enemies authorize a pull. Active bosses and other tanks' enemies are excluded.
        bool Eligible(IBattleChara e) => !Protected(e) && (ThreatensGroup(e) ||
            !e.Struct()->InCombat && selected is { IsDead: false, IsTargetable: true } &&
            selected.IsHostile() && !selected.Struct()->InCombat &&
            System.Numerics.Vector3.Distance(e.Position, selected.Position) <= AreaRange());
        var candidates = enemies.Where(Eligible).ToArray();
        var nearby = candidates.Where(InArea).ToArray();
        if (nearby.Length >= 2 && (IsMoving() || nearby.Any(e => e.TargetObjectId != Player.Object!.GameObjectId)) && !enemies.Any(e => InArea(e) && !Eligible(e)))
        {
            approaching = 0;
            return Submit(Actions.Area, Player.Object!.GameObjectId, "Two-enemy pickup");
        }
        var loose = candidates.Where(e => e.TargetObjectId != Player.Object!.GameObjectId)
            .OrderBy(e => e.GameObjectId == selected?.GameObjectId ? 0 : 1).ThenBy(e => GetTargetDistance(e)).FirstOrDefault();
        if (loose == null) { approaching = 0; return false; }
        // Wait at most 400 ms when running into a grouped pair, then resume tagging.
        bool grouping = IsMoving() && candidates.Count(e => GetTargetDistance(e) <= AreaRange() + 3 &&
            System.Numerics.Vector3.Distance(e.Position, loose.Position) <= AreaRange()) >= 2;
        if (grouping && approaching != loose.GameObjectId)
        { approaching = loose.GameObjectId; gatheringUntil = now + 400; }
        if (grouping && now < gatheringUntil) return true;
        uint action = Usable(Actions.Ranged, loose) ? Actions.Ranged : 0;
        // Provoke only rescues a non-tank victim, after excluding bosses.
        if (action == 0 && ThreatensGroup(loose) && Usable(RoleActions.Tank.Provoke, loose))
            action = RoleActions.Tank.Provoke;
        return action != 0 && Submit(action, loose.GameObjectId, "Loose add pickup");
    }
    private static unsafe bool Submit(uint action, ulong target, string reason)
    {
        if (!ActionLearned(action) || AutomationOwnership.Busy || !ActionReadyForTarget(action, target, true, true)) return false;
        using var native = AutomationOwnership.QueuedExternalScope();
        bool accepted = ActionManager.Instance()->UseAction(ActionType.Action, action, target);
        AccordDiagnostics.DiagnosticLog.Event("Tank.Pull.Automatic", () => new { action, target, reason, accepted });
        return accepted;
    }
}
