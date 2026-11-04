using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Core;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.Services;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class SelectionRuntime
{
    [ThreadStatic] private static bool automatic;
    [ThreadStatic] private static bool refreshOnPress;
    internal static bool Automatic => automatic;
    private sealed class Scope : IDisposable
    {
        private readonly bool previous = automatic;
        internal Scope() => automatic = true;
        public void Dispose() => automatic = previous;
    }
    internal static IDisposable AutomaticScope() => new Scope();
    private sealed class PressScope(bool refresh) : IDisposable
    {
        private readonly bool previous = Set(refresh);
        private static bool Set(bool value) { bool previous = refreshOnPress; refreshOnPress = value; return previous; }
        public void Dispose() => refreshOnPress = previous;
    }
    internal static IDisposable BeginPress(bool queued, uint input = 0) => new PressScope(!queued && (ExperimentalSelection.ActiveHealer || ExperimentalSelection.ActiveDrg || ExperimentalSelection.ActiveTargets || input == PveIntentRouting.FullRotation));
    internal static bool ConsumePressRefresh() { bool refresh = refreshOnPress; refreshOnPress = false; return refresh; }
    private static readonly Dictionary<uint, SelectionDecision> previews = new();
    private static readonly PreparationSequence sequence = new();
    private static SelectionDecision? automaticDecision;
    private static SelectionDecision? pendingConfirmation;
    internal static readonly PreventionBudget Prevention = new();
    internal static readonly PendingHealth PendingHealth = new();
    internal static uint ExpectedHealth(IBattleChara target) => PendingHealth.Project(target.CurrentHp, target.MaxHp, target.GameObjectId, Environment.TickCount64);
    private static long generation;
    private static string context = "";
    private static bool wasInCombat;
    private static long encounterGeneration;
    internal static long Generation { get { UpdateContext(); return generation; } }
    private static void UpdateContext()
    {
        string value = $"{Player.Object?.GameObjectId}:{Player.Job}:{Player.Level}:{Svc.ClientState.TerritoryType}:{Player.IsDead}:{Svc.ClientState.IsPvP}";
        bool inCombat = InCombat();
        bool leftCombat = wasInCombat && !inCombat;
        wasInCombat = inCombat;
        if (value == context && !leftCombat) return;
        context = value; generation++; previews.Clear(); sequence.Reset(); Prevention.Reset(); PendingHealth.Reset(); pendingConfirmation = null; automaticDecision = null;
    }
    internal static void Reset() { generation++; previews.Clear(); sequence.Reset(); automaticDecision = null; Prevention.Reset(); PendingHealth.Reset(); pendingConfirmation = null; }
    internal static SelectionDecision? Commitment => sequence.Current(Environment.TickCount64, Generation);
    internal static SelectionDecision? Preview(uint input) => previews.GetValueOrDefault(input);
    internal static void Tick()
    {
        if (!ExperimentalSelection.ActiveHealer && !ExperimentalSelection.ActiveDrg && !ExperimentalSelection.ActiveTargets) return;
        UpdateContext();
        PendingHealth.Tick(Environment.TickCount64);
        if (encounterGeneration != BossForecastProvider.Generation)
        {
            encounterGeneration = BossForecastProvider.Generation;
            if (Commitment?.PreventionEvent > 0 || pendingConfirmation?.PreventionEvent > 0) sequence.Reset();
            Prevention.Reset();
        }
        using var openerRoute = new WrathFork.Contract.ActionRouteContext(ActionRoutes.For(Combos.PvE.All.SingleTargetDPS));
        if (ExperimentalSelection.ActiveDrg && IsEnabled(Preset.DRG_ST_Opener)) Combos.PvE.DRG.Opener().ObserveForTesting();
        if (ExperimentalSelection.ActiveSage && IsEnabled(Preset.SGE_ST_Adv_DPS_Opener)) Combos.PvE.SGE.Opener().ObserveForTesting();
        if (ExperimentalSelection.ActiveHealer)
        {
            var forecast = ExperimentalSelection.Forecasts ? BossForecastProvider.Current : null;
            float? raidwide = forecast?.Remaining(forecast.Raidwide, Environment.TickCount64, BossForecastProvider.Generation);
            var cast = ObservedRaidwide();
            raidwide = EncounterForecast.Earliest(raidwide, cast.Seconds);
            bool marked = CheckForSharedDamageEffect(out _, out _, 6f);
            Prevention.Observe("raidwide", marked || raidwide is >= 0 and <= 6,
                Environment.TickCount64, generation, 0, raidwide, cast.Seconds == raidwide ? cast.SourceKey : "");
            bool buster = TryGetTankBusterTarget(out var victim, out var busterKey);
            Prevention.Observe("tankbuster", buster, Environment.TickCount64, generation, buster ? victim.SafeGameObjectId ?? 0 : 0, null, busterKey);
        }
        if (Commitment is { } accepted && (!HealerSelection.CommitmentStillNeeded(accepted) ||
            accepted.PreventionEvent != 0 && !Prevention.Contains(accepted.PreventionEvent)))
        {
            TestingApi.Observe("PreparationCanceled:recipient-or-coverage", accepted.Preparation, accepted.PlannedGcd, accepted.Recipient);
            sequence.CancelAccepted();
        }
    }
    internal static void BeginPreview(uint input) { UpdateContext(); previews.Remove(input); }
    internal static uint Publish(uint input, SelectionDecision decision)
    {
        previews[input] = decision;
        if (automatic) automaticDecision = decision;
        TestingApi.Observe("PlannedGcd:" + decision.Reason, decision.Action, decision.PlannedGcd, decision.Recipient);
        var recipient = Svc.Objects.SearchById(decision.Action == decision.Preparation ? Player.Object!.GameObjectId : decision.Recipient);
        return decision.Action.Retarget(input, recipient);
    }
    internal static unsafe bool Press(uint input, uint result, out ulong? boundRecipient)
    {
        boundRecipient = null;
        if (!ExperimentalSelection.ActiveHealer && !ExperimentalSelection.ActiveDrg && !ExperimentalSelection.ActiveTargets) return true;
        UpdateContext();
        var decision = previews.GetValueOrDefault(input) ?? (automatic && automaticDecision?.Action == result ? automaticDecision : null);
        if (decision is null || decision.Action != result) { sequence.CancelPress(); return true; }
        var recipient = Svc.Objects.SearchById(decision.Recipient);
        uint validateAction = decision.Preparation != 0 ? decision.PlannedGcd : decision.Action;
        if (decision.Generation != Generation || Environment.TickCount64 - decision.ObservedAt >= 500 || recipient is null ||
            !recipient.IsTargetable || recipient.IsDead() || !ActionRoutes.Allowed(validateAction) || !ActionRoutes.Allowed(decision.Action) ||
            !ActionManager.CanUseActionOnTarget(validateAction, recipient.Struct()) || !InActionRange(validateAction, recipient))
        { sequence.CancelPress(); return false; }
        if (decision.Preparation != 0 || decision.CompletionAction != 0) sequence.Press(decision);
        else sequence.CancelPress();
        pendingConfirmation = decision;
        boundRecipient = decision.Preparation != 0 ? Player.Object!.GameObjectId : decision.Recipient;
        return true;
    }
    internal static void Confirm(uint action, bool consumesPreparation, IReadOnlyCollection<ulong>? recipients = null)
    {
        if (!ExperimentalSelection.ActiveHealer) return;
        var expected = pendingConfirmation?.Action == action ? pendingConfirmation : Commitment?.PlannedGcd == action ? Commitment : null;
        bool confirmedRecipient = expected is null || expected.Preparation == action || expected.Recipient == Player.Object?.GameObjectId ||
            recipients is null || recipients.Contains(expected.Recipient);
        sequence.Confirm(action, Environment.TickCount64, Generation, consumesPreparation, confirmedRecipient);
        if (!confirmedRecipient) { pendingConfirmation = null; return; }
        if (pendingConfirmation is { } pending && pending.Action == action && pending.Generation == generation && Environment.TickCount64 - pending.ObservedAt < 3000)
        {
            if (pending.Preparation == 0 && pending.CompletionAction == 0) Prevention.Confirm(pending.PreventionEvent);
            pendingConfirmation = null;
        }
    }
    internal static unsafe uint RetargetEnemy(uint input, uint action)
    {
        if (action >= 1000000 || !Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(action, out var row) || !row.CanTargetHostile)
            return action;
        var selected = SimpleTarget.HardTarget as IBattleChara;
        if (selected is not null && !selected.IsHostile()) selected = null;
        var targets = Svc.Objects.GetBattleCharas().Where(a => a.IsHostile() && !a.IsDead && a.IsTargetable).ToArray();
        var actors = targets.Select(a => new TargetActor(a.GameObjectId, a.Position, a.HitboxRadius,
            !a.IsInvincible && !Service.Configuration.IgnoredNPCs.ContainsKey(a.BaseId) &&
            ActionManager.CanUseActionOnTarget(action, a.GameObject()) && InActionRange(action, a), false, GetTargetHPPercent(a) / 100f,
            AutoRotation.AutoRotationController.DPSTargeting.FightingParty(a) || !automatic && a.GameObjectId == selected?.GameObjectId)).ToImmutableArray();
        var shape = row.CastType switch { 2 => EffectShape.Circle, 3 => EffectShape.Cone, 4 => EffectShape.Line, _ => EffectShape.Single };
        // Unsupported geometry is not guessed: retain the native/legacy result until its adapter exists.
        if (row.CastType is not (1 or 2 or 3 or 4)) return action;
        var overrides = ImmutableArray.CreateBuilder<TargetOverride>();
        if (!automatic || ExperimentalSelection.OverridesInAutomation)
        {
            if (ExperimentalSelection.EnemyMouseover && SimpleTarget.ModelMouseOverTarget is IBattleChara hover && hover.IsHostile())
                overrides.Add(new(TargetSource.WorldMouseover, hover.GameObjectId));
            if (ExperimentalSelection.EnemyFocus && SimpleTarget.FocusTarget is IBattleChara focus && focus.IsHostile())
                overrides.Add(new(TargetSource.Focus, focus.GameObjectId));
        }
        if (selected is not null && !ExperimentalSelection.AutomaticEnemies)
            overrides.Insert(ExperimentalSelection.EnemyMouseover && overrides.Count > 0 && overrides[0].Source == TargetSource.WorldMouseover ? 1 : 0,
                new(TargetSource.Selected, selected.GameObjectId));
        bool acquire = ExperimentalSelection.AutomaticEnemies || ExperimentalSelection.AcquireEnemy && selected is null;
        var snapshot = new TargetSelectionInput(Player.Position, Player.Object!.HitboxRadius,
            new(ActionManager.GetActionRange(action), row.EffectRange, shape, row.CanTargetSelf, Width: row.XAxisModifier > 0 ? row.XAxisModifier : 4),
            new(acquire, ExperimentalSelection.Fallback, shape != EffectShape.Single, false, true), actors, actors, overrides.ToImmutable());
        var result = TargetSelection.Select(snapshot);
        TestingApi.RecordDecision("Target", snapshot, result);
        if (result.Recipient is not { } recipient) return Combos.PvE.All.Cease;
        return Publish(input, new(action, action, recipient, 0, result.Reason, Environment.TickCount64, Generation));
    }
    internal static unsafe TargetDecision SelectAlly(uint action, ulong? explicitRecipient = null)
    {
        if (!Player.Available) return TargetDecision.None("No player");
        var party = GetPartyMembers().Select(m => m.BattleChara).Where(a => a is not null).Cast<IBattleChara>().ToArray();
        var overrides = new List<(TargetSource Source, IBattleChara? Actor)>();
        if (explicitRecipient is { } id)
            overrides.Add((TargetSource.Explicit, Svc.Objects.SearchById(id) as IBattleChara));
        else
        {
            if ((!automatic || ExperimentalSelection.OverridesInAutomation) && ExperimentalSelection.PartyMouseover) overrides.Add((TargetSource.PartyMouseover, SimpleTarget.UIMouseOverTarget));
            if ((!automatic || ExperimentalSelection.OverridesInAutomation) && ExperimentalSelection.WorldMouseover) overrides.Add((TargetSource.WorldMouseover, SimpleTarget.ModelMouseOverTarget as IBattleChara));
            var selectedAlly = SimpleTarget.SoftTarget as IBattleChara;
            if (selectedAlly is null || selectedAlly.IsHostile()) selectedAlly = SimpleTarget.HardTarget as IBattleChara;
            overrides.Add((TargetSource.Selected, selectedAlly));
            if (ExperimentalSelection.Focus) overrides.Add((TargetSource.Focus, SimpleTarget.FocusTarget as IBattleChara));
        }
        var actors = party.Concat(overrides.Where(o => o.Actor is not null).Select(o => o.Actor!)).Append(Player.Object!)
            .DistinctBy(a => a.GameObjectId).Where(a => !a.IsHostile()).ToArray();
        var snapshot = actors.Select(a => new TargetActor(a.GameObjectId, a.Position, a.HitboxRadius,
            !a.IsDead && a.IsTargetable && ActionManager.CanUseActionOnTarget(action, a.GameObject()) && InActionRange(action, a),
            party.Any(p => p.GameObjectId == a.GameObjectId), GetTargetHPPercent(a) / 100f)).ToImmutableArray();
        var requested = explicitRecipient is { } requestedId ? ImmutableArray.Create(new TargetOverride(TargetSource.Explicit, requestedId))
            : overrides.Where(o => o.Actor is not null && !o.Actor.IsHostile()).Select(o => new TargetOverride(o.Source, o.Actor!.GameObjectId)).ToImmutableArray();
        var input = new TargetSelectionInput(Player.Position, Player.Object!.HitboxRadius, new(ActionManager.GetActionRange(action), 0),
            new(ExperimentalSelection.AutomaticAllies, ExperimentalSelection.Fallback, false, true, false), snapshot, snapshot, requested);
        var result = TargetSelection.Select(input);
        TestingApi.RecordDecision("Target", input, result);
        return result;
    }
}
