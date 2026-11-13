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
    [ThreadStatic] private static EnemyEvaluation? enemyEvaluation;
    private sealed class EnemyEvaluation(EnemyEvaluation? previous) : IDisposable
    {
        internal bool Initialized;
        internal ulong? Recipient;
        public void Dispose() => enemyEvaluation = previous;
    }
    internal static IDisposable? EnterEnemyEvaluation(uint input)
    {
        if (!ExperimentalSelection.ActiveTargets || !automatic && input < 1000000) return null;
        var scope = new EnemyEvaluation(enemyEvaluation);
        enemyEvaluation = scope;
        return scope;
    }
    internal static bool HasEvaluationTarget => enemyEvaluation?.Initialized == true;
    internal static int? DamageCoverage(uint root)
    {
        if (!ExperimentalSelection.ActiveTargets || !Player.Available) return null;
        root = OriginalHook(root);
        if (!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(root, out var row) ||
            row.CastType is not (1 or 2 or 3 or 4 or 10)) return null;
        return SelectEnemy(root, validateDamage: false).Affected.Length;
    }
    internal static IBattleChara? EvaluationTarget => enemyEvaluation?.Recipient is { } id ? Svc.Objects.SearchById(id) as IBattleChara : null;
    internal static IBattleChara? ResolveAutomaticEnemy(uint root, IBattleChara? legacy)
    {
        if (!ExperimentalSelection.ActiveTargets || !Player.Available) return legacy;
        root = OriginalHook(root);
        if (!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(root, out var row) ||
            row.CastType is not (1 or 2 or 3 or 4 or 10) || !row.CanTargetHostile && !(row.CanTargetSelf && row.CastType == 2)) return legacy;
        var decision = SelectEnemy(root, validateDamage: false);
        return decision.Recipient is { } id ? Svc.Objects.SearchById(id) as IBattleChara : null;
    }
    internal static void ObserveDamageControl(uint root)
    {
        if (enemyEvaluation is not { Initialized: false } scope || root == 0) return;
        root = OriginalHook(root);
        if (!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(root, out var row) ||
            row.CastType is not (1 or 2 or 3 or 4 or 10) || !row.CanTargetHostile && !(row.CanTargetSelf && row.CastType == 2)) return;
        // Root geometry chooses the rotation's context once. A later action may fail its
        // own validation, but must not move a DoT/proc decision to an unchecked enemy.
        scope.Recipient = SelectEnemy(root, validateDamage: false).Recipient;
        scope.Initialized = true;
    }
    private sealed class Scope : IDisposable
    {
        private readonly bool previous = automatic;
        internal Scope() => automatic = true;
        public void Dispose() => automatic = previous;
    }
    internal static IDisposable AutomaticScope() => new Scope();
    [ThreadStatic] private static int pressDepth;
    internal static bool IdleDamageDisplayAllowed =>
        !Automatic && !HandlingPress && !InPvP() && !InCombat() && !CountdownActive &&
        Player.Object is { IsDead: false, IsCasting: false } && SimpleTarget.HardTarget?.IsHostile() != true &&
        !(Service.Configuration.PenaltyPause > 0 && PlayerHasActionPenalty(false)) && Commitment == null &&
        !(Player.Job == ECommons.ExcelServices.Job.DNC && Combos.PvE.DNC.DanceInProgress) &&
        !(Player.Job == ECommons.ExcelServices.Job.NIN && Combos.PvE.NIN.InMudra);

    internal static bool HandlingPress => pressDepth != 0;
    private sealed class PressScope : IDisposable
    {
        private readonly bool previous;
        internal PressScope(bool refresh)
        {
            previous = refreshOnPress;
            refreshOnPress = refresh;
            pressDepth++;
        }
        public void Dispose()
        {
            refreshOnPress = previous;
            pressDepth--;
        }
    }
    internal static IDisposable BeginPress(bool queued, uint input = 0) => new PressScope(!queued && (PvPIntentRouting.Enabled || ExperimentalSelection.ActiveHealer || ExperimentalSelection.ActivePreparedAction || ExperimentalSelection.ActiveNinja || ExperimentalSelection.ActiveDrg || ExperimentalSelection.ActiveTargets || input == PveIntentRouting.FullRotation));
    internal static bool ConsumePressRefresh() { bool refresh = refreshOnPress; refreshOnPress = false; return refresh; }
    private static readonly Dictionary<uint, SelectionDecision> previews = new();
    private static readonly PreparationSequence sequence = new();
    private static SelectionDecision? automaticDecision;
    private static uint automaticInput;
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
        if (!ExperimentalSelection.ActiveHealer && !ExperimentalSelection.ActivePreparedAction && !ExperimentalSelection.ActiveDrg && !ExperimentalSelection.ActiveTargets) return;
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
        if (Commitment is { } accepted && (!(ExperimentalSelection.ActivePvpMachinist ? Combos.PvP.MCHPvP.PreparedToolStillValid(accepted) : ExperimentalSelection.ActiveBlueMage ? Combos.PvE.BLU.PreparedSpellStillValid(accepted) : ExperimentalSelection.ActiveMachinist ? Combos.PvE.MCH.PreparedToolStillValid(accepted) : ExperimentalSelection.ActiveSummoner ? Combos.PvE.SMN.PreparedSpellStillValid(accepted) : ExperimentalSelection.ActiveRedMage ? Combos.PvE.RDM.PreparedRaiseStillValid(accepted) : HealerSelection.CommitmentStillNeeded(accepted)) ||
            accepted.PreventionEvent != 0 && !Prevention.Contains(accepted.PreventionEvent)))
        {
            TestingApi.Observe("PreparationCanceled:recipient-or-coverage", accepted.Preparation, accepted.PlannedGcd, accepted.Recipient);
            sequence.CancelAccepted();
        }
    }
    internal static void BeginPreview(uint input) { UpdateContext(); previews.Remove(input); if (automatic) automaticDecision = null; }
    internal static void RejectPress() { sequence.CancelPress(); pendingConfirmation = null; }
    internal static IBattleChara? AutomaticRecipient(uint action) => automaticDecision is { } decision &&
        decision.Action == action && decision.Generation == Generation && Environment.TickCount64 - decision.ObservedAt is >= 0 and < 500
        ? decision.Preparation != 0 ? Player.Object : Svc.Objects.SearchById(decision.Recipient) as IBattleChara : null;
    internal static bool MaySwitchVisibleTarget(IGameObject target, bool legacy)
    {
        if (target.IsHostile()) return !ExperimentalSelection.ActiveTargets && legacy; // Managed enemies follow accepted actions.
        if (ExperimentalSelection.ActiveTargets && target.GameObjectId == Player.Object?.GameObjectId)
            return ExperimentalSelection.SwitchAllyTarget;
        return ExperimentalSelection.ActiveHealer || PvpAllySelection.Active ? ExperimentalSelection.SwitchAllyTarget : legacy;
    }
    internal static void FollowAcceptedEnemy(uint action, ulong recipient)
    {
        if (!automatic || !ExperimentalSelection.ActiveTargets || Player.Object is not { } player) return;
        var selected = Svc.Targets.Target;
        bool vacant = selected == null || selected.GameObjectId == player.GameObjectId;
        if (!(vacant ? ExperimentalSelection.AcquireVisibleEnemy : ExperimentalSelection.SwitchEnemyTarget)) return;

        // A point-blank attack is delivered to self but has a separately evaluated enemy.
        if (recipient == player.GameObjectId && automaticDecision is { EvaluatedEnemy: { } enemy } decision &&
            decision.Action == action && decision.Recipient == recipient && decision.Generation == Generation &&
            Environment.TickCount64 - decision.ObservedAt is >= 0 and < 500)
            recipient = enemy;
        else if (!Data.ActionWatching.ActionSheet.TryGetValue(action, out var row) || !row.CanTargetHostile) return;
        if (selected?.GameObjectId == recipient ||
            Svc.Objects.SearchById(recipient) is not IBattleChara { IsDead: false, IsTargetable: true } target ||
            !target.IsHostile()) return;
        Svc.Targets.Target = target;
        AccordDiagnostics.DiagnosticLog.Event("Runner.VisibleEnemy", () => new { Action = action, Previous = selected?.GameObjectId, Enemy = recipient });
    }

    internal static uint Publish(uint input, SelectionDecision decision)
    {
        AccordDiagnostics.DiagnosticLog.Event("Decision.Publish", () => new { Input = input, Decision = decision });
        previews[input] = decision;
        if (automatic) { automaticDecision = decision; automaticInput = input; }
        TestingApi.Observe("PlannedGcd:" + decision.Reason, decision.Action, decision.PlannedGcd, decision.Recipient);
        var recipient = Svc.Objects.SearchById(decision.Action == decision.Preparation ? Player.Object!.GameObjectId : decision.Recipient);
        return decision.Action.Retarget(input, recipient);
    }
    // Keep the chosen proposal through the native adjusted-action hook. Re-running
    // an arbitrary native-action combo here can discard its recipient or preparation.
    internal static unsafe bool ExecuteAutomatic(SelectionDecision decision)
    {
        if (!automatic || automaticDecision != decision || decision.Generation != Generation) return false;
        uint input = automaticInput;
        using var route = ActionRoutes.For(input) is { } policy ? new WrathFork.Contract.ActionRouteContext(policy) : null;
        uint action = HealingGroundSelection.Select(input, decision.Action);
        if (action != decision.Action || action >= Combos.PvE.All.SingleTargetDPS) return false;
        var current = automaticDecision!;
        ulong recipient = current.Preparation != 0 ? Player.Object!.GameObjectId : current.Recipient;
        if (!ActionReadyForTarget(action, recipient)) return false;
        using var checkedAction = new RotationBehaviour.ExecuteChecked(action);
        return ActionManager.Instance()->UseAction(ActionType.Action, action, recipient);
    }

    internal static unsafe bool Press(uint input, uint result, out ulong? boundRecipient, out System.Numerics.Vector3? boundGround)
    {
        AccordDiagnostics.DiagnosticLog.Event("Decision.Press", () => new { Input = input, Result = result, Automatic = automatic, Preview = previews.GetValueOrDefault(input), AutomaticDecision = automaticDecision });
        boundRecipient = null;
        boundGround = null;
        if (!PvPIntentRouting.Enabled && !ExperimentalSelection.ActiveHealer && !ExperimentalSelection.ActivePreparedAction && !ExperimentalSelection.ActiveDrg && !ExperimentalSelection.ActiveTargets) return true;
        UpdateContext();
        // Checked automatic dispatch owns this proposal; a cached native-button
        // preview must not override its freshly selected recipient.
        var decision = automatic && RotationBehaviour.CheckedAction == result && automaticDecision?.Action == result
            ? automaticDecision
            : previews.GetValueOrDefault(input) ?? (automatic && automaticDecision?.Action == result ? automaticDecision : null);
        if (decision is null || decision.Action != result) { sequence.CancelPress(); return true; }
        if (!decision.ManualDamage && decision.EvaluatedEnemy is { } evaluated && SelectEnemy(result, fixedRecipient: evaluated).Recipient != evaluated)
        { sequence.CancelPress(); return AccordDiagnostics.DiagnosticLog.Reject("Decision press validation failed"); }
        if (decision.GroundPosition is { } position)
        {
            if (decision.Generation != Generation || Environment.TickCount64 - decision.ObservedAt >= 500 ||
                !ActionRoutes.Allowed(result) || !HealingGroundSelection.StillValid(result, position)) return AccordDiagnostics.DiagnosticLog.Reject("Decision press validation failed");
            sequence.CancelPress();
            pendingConfirmation = decision;
            boundRecipient = decision.Recipient;
            boundGround = position;
            return true;
        }
        var recipient = Svc.Objects.SearchById(decision.Recipient);
        uint validateAction = decision.Preparation != 0 ? decision.PlannedGcd : decision.Action;
        if (decision.Generation != Generation || Environment.TickCount64 - decision.ObservedAt >= 500 || recipient is null ||
            !recipient.IsTargetable || !TargetLifeAllowed(validateAction, recipient) || !ActionRoutes.Allowed(validateAction) || !ActionRoutes.Allowed(decision.Action) ||
            Svc.ClientState.IsPvP && recipient.IsHostile() && !PvpTargetEligibility.CanDamage(validateAction, recipient) ||
            !ActionManager.CanUseActionOnTarget(validateAction, recipient.Struct()) || !decision.ManualDamage && !InActionRange(validateAction, recipient))
        { sequence.CancelPress(); return AccordDiagnostics.DiagnosticLog.Reject("Decision press validation failed"); }
        if (decision.Preparation != 0 || decision.CompletionAction != 0) sequence.Press(decision);
        else sequence.CancelPress();
        pendingConfirmation = decision;
        boundRecipient = decision.Preparation != 0 ? Player.Object!.GameObjectId : decision.Recipient;
        return true;
    }
    internal static void Confirm(uint action, bool consumesPreparation, IReadOnlyCollection<ulong>? recipients = null)
    {
        AccordDiagnostics.DiagnosticLog.Event("Decision.Confirm", () => new { Action = action, consumesPreparation, Recipients = recipients, Pending = pendingConfirmation, Commitment });
        if (!ExperimentalSelection.ActiveHealer && !ExperimentalSelection.ActivePreparedAction) return;
        var expected = pendingConfirmation?.Action == action ? pendingConfirmation : Commitment?.PlannedGcd == action ? Commitment : null;
        bool confirmedRecipient = expected is null || expected.GroundPosition is not null || expected.Preparation == action || expected.Recipient == Player.Object?.GameObjectId ||
            recipients is null || recipients.Contains(expected.Recipient);
        sequence.Confirm(action, Environment.TickCount64, Generation, consumesPreparation, confirmedRecipient);
        if (!confirmedRecipient) { pendingConfirmation = null; return; }
        if (pendingConfirmation is { } pending && pending.Action == action && pending.Generation == generation && Environment.TickCount64 - pending.ObservedAt < 3000)
        {
            if (pending.Preparation == 0 && pending.CompletionAction == 0) Prevention.Confirm(pending.PreventionEvent);
            pendingConfirmation = null;
        }
    }
    internal static unsafe uint RetargetEnemy(uint input, uint action, bool damageRotation)
    {
        // A provider which published an action/recipient decision owns that pair.
        // Press-time validation may reject it, but the fallback pass cannot move it.
        if (Preview(input) is { } retained && retained.Action == action) return action;
        if (Svc.ClientState.IsPvP && !automatic && input < 1000000) return action;
        if (action >= 1000000 || !Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(action, out var row))
            return action;
        // Healing spells share self/circle/attack-type metadata with damage spells.
        // Only reviewed damage spells and weaponskills enter the hostile coverage pass.
        bool selfDamage = HasEvaluationTarget && row.CanTargetSelf && row.CastType == 2 &&
            (row.ActionCategory.RowId == 3 || action is Combos.PvE.WHM.Holy or Combos.PvE.WHM.Holy3 or
                Combos.PvE.SCH.ArtOfWar or Combos.PvE.SCH.ArtOfWarII or Combos.PvE.SGE.Dyskrasia or
                Combos.PvE.SGE.Dyskrasia2 or Combos.PvE.SGE.EukrasianDyskrasia or Combos.PvE.DRK.Unleash or
                Combos.PvE.DRK.StalwartSoul or Combos.PvE.PLD.HolyCircle);
        if (!row.CanTargetHostile && !selfDamage) return action;
        if (row.CastType is not (1 or 2 or 3 or 4 or 10)) return action;
        var result = HasEvaluationTarget && EvaluationTarget is null ? TargetDecision.None("No evaluation target") :
            SelectEnemy(action, fixedRecipient: HasEvaluationTarget ? enemyEvaluation!.Recipient : null);
        if (result.Recipient is not { } recipient)
        {
            // The provider has already selected a real action. A failed coverage or
            // target estimate need not veto a manual attempt on its native recipient.
            bool centered = row.CanTargetSelf && ActionManager.GetActionRange(action) <= 0;
            if (!automatic && PveIntentRouting.IsDamageButton(input) && !InPvP() && ActionLearned(action))
            {
                var target = centered ? Player.Object : ManualEnemyForAction(action);
                if (target is { IsDead: false, IsTargetable: true } && (centered || row.CanTargetHostile && target.IsHostile()) &&
                    ActionManager.CanUseActionOnTarget(action, target.GameObject()))
                    return Publish(input, new(action, action, target.GameObjectId, 0, "Manual damage on native recipient",
                        Environment.TickCount64, Generation, ManualDamage: true));
            }
            // Split layouts can use native roots (e.g. Heavy Shot and Quick Nock).
            // Keep the provider's learned action as the idle icon while the executable result stays Cease.
            if (!automatic && !InPvP() && !InCombat() && !CountdownActive &&
                (damageRotation || PveIntentRouting.IsDamageButton(input)) && ActionLearned(action) &&
                SimpleTarget.HardTarget?.IsHostile() != true)
                Service.ActionReplacer.RememberIdleIcon(input, action);
            return Combos.PvE.All.Cease;
        }
        ulong? evaluatedEnemy = null;
        if (row.CanTargetSelf && ActionManager.GetActionRange(action) <= 0)
        { evaluatedEnemy = recipient; recipient = Player.Object!.GameObjectId; }
        return Publish(input, new(action, action, recipient, 0, result.Reason, Environment.TickCount64, Generation,
            EvaluatedEnemy: evaluatedEnemy, ManualDamage: !automatic && !InPvP() && PveIntentRouting.IsDamageButton(input)));
    }
    private static unsafe TargetDecision SelectEnemy(uint action, bool validateDamage = true, ulong? fixedRecipient = null)
    {
        var row = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRow(action);
        bool centered = row.CanTargetSelf && ActionManager.GetActionRange(action) <= 0;
        var selected = SimpleTarget.HardTarget as IBattleChara;
        if (selected is not null && !selected.IsHostile()) selected = null;
        var overrides = EnemyOverrides(selected);
        var targets = Svc.Objects.GetBattleCharas().Where(a => a.IsHostile() && !a.IsDead && a.IsTargetable).ToArray();
        var actors = targets.Select(a => new TargetActor(a.GameObjectId, a.Position, a.HitboxRadius,
            !a.IsInvincible && !Service.Configuration.IgnoredNPCs.ContainsKey(a.BaseId) &&
            (!validateDamage || !Svc.ClientState.IsPvP || PvpTargetEligibility.CanDamage(action, a)) &&
            (centered ? ActionManager.CanUseActionOnTarget(action, Player.GameObject) :
                ActionManager.CanUseActionOnTarget(action, a.GameObject()) && (!validateDamage || InActionRange(action, a))), false, GetTargetHPPercent(a) / 100f,
            Svc.ClientState.IsPvP || AutoRotation.AutoRotationController.DPSTargeting.FightingParty(a) || !automatic && overrides.Any(o => o.Id == a.GameObjectId))).ToImmutableArray();
        var shape = row.CastType switch { 2 => EffectShape.Circle, 3 => EffectShape.Cone, 4 => EffectShape.Line, 10 => EffectShape.Donut, _ => EffectShape.Single };
        bool acquire = ExperimentalSelection.AutomaticEnemies || ExperimentalSelection.AcquireEnemy && selected is null;
        ImmutableArray<TargetOverride> requested = fixedRecipient is { } fixedId
            ? [new(TargetSource.Explicit, fixedId)] : overrides;
        var snapshot = new TargetSelectionInput(Player.Position, Player.Object!.HitboxRadius,
            new(validateDamage ? centered ? row.EffectRange : ActionManager.GetActionRange(action) : Math.Max(30, (int)row.EffectRange),
                row.EffectRange, shape, centered, HalfAngle: MathF.PI / 3, Width: row.XAxisModifier > 0 ? row.XAxisModifier : 4, InnerRadius: shape == EffectShape.Donut ? row.XAxisModifier > 0 ? row.XAxisModifier : 8 : 0),
            new(acquire, ExperimentalSelection.Fallback, shape != EffectShape.Single, false, true, AllowManualSplash: !automatic && !Svc.ClientState.IsPvP), actors, actors,
            requested);
        var result = TargetSelection.Select(snapshot);
        TestingApi.RecordDecision("Target", snapshot, result);
        return result;
    }
    private static ImmutableArray<TargetOverride> EnemyOverrides(IBattleChara? selected)
    {
        var overrides = ImmutableArray.CreateBuilder<TargetOverride>();
        if (!automatic || ExperimentalSelection.OverridesInAutomation)
        {
            if (ExperimentalSelection.EnemyMouseover && SimpleTarget.ModelMouseOverTarget is IBattleChara hover && hover.IsHostile())
                overrides.Add(new(TargetSource.WorldMouseover, hover.GameObjectId));
            if (ExperimentalSelection.EnemyFocus && SimpleTarget.FocusTarget is IBattleChara focus && focus.IsHostile())
                overrides.Add(new(TargetSource.Focus, focus.GameObjectId));
        }
        if (selected != null && selected.IsHostile() && (!automatic || !ExperimentalSelection.AutomaticEnemies))
            overrides.Add(new(TargetSource.Selected, selected.GameObjectId));
        return TargetOrder.Apply(overrides, ExperimentalSelection.EnemyOverrideOrder).ToImmutableArray();
    }

    private static unsafe IBattleChara? ManualEnemyForAction(uint action)
    {
        // Keep the recipient used to evaluate procs and DoTs, even if its range estimate failed.
        if (enemyEvaluation?.Recipient is { } fixedId) return Svc.Objects.SearchById(fixedId) as IBattleChara;
        foreach (var candidate in EnemyOverrides(SimpleTarget.HardTarget as IBattleChara))
        {
            if (Svc.Objects.SearchById(candidate.Id) is IBattleChara { IsDead: false, IsTargetable: true } actor &&
                ActionManager.CanUseActionOnTarget(action, actor.GameObject())) return actor;
            if (!ExperimentalSelection.Fallback) break;
        }
        return null;
    }

    internal static bool TargetLifeAllowed(uint action, IGameObject target) =>
        action is Combos.PvE.WHM.Raise or Combos.PvE.SCH.Resurrection or Combos.PvE.AST.Ascend or Combos.PvE.SGE.Egeiro or Combos.PvE.RDM.Verraise or Combos.PvE.BLU.AngelWhisper
            ? target.IsDead() && !target.HasStatus(Combos.PvE.All.Buffs.Raised, true) && !RaiseBeingCast(target.GameObjectId) : !target.IsDead();

    private static bool RaiseBeingCast(ulong recipient) => GetPartyMembers().Any(member =>
        member.BattleChara is { IsCasting: true } caster && caster.GameObjectId != Player.Object?.GameObjectId &&
        caster.CastTargetObjectId == recipient && caster.CastActionId is Combos.PvE.WHM.Raise or Combos.PvE.SCH.Resurrection or
            Combos.PvE.AST.Ascend or Combos.PvE.SGE.Egeiro or Combos.PvE.RDM.Verraise or Combos.PvE.BLU.AngelWhisper);

    internal static unsafe TargetDecision SelectAlly(uint action, ulong? explicitRecipient = null, Func<IBattleChara, bool>? eligible = null)
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
            TargetLifeAllowed(action, a) && a.IsTargetable && (eligible?.Invoke(a) ?? true) && ActionManager.CanUseActionOnTarget(action, a.GameObject()) && InActionRange(action, a),
            party.Any(p => p.GameObjectId == a.GameObjectId), GetTargetHPPercent(a) / 100f)).ToImmutableArray();
        var requested = explicitRecipient is { } requestedId ? ImmutableArray.Create(new TargetOverride(TargetSource.Explicit, requestedId))
            : overrides.Where(o => o.Actor is not null && !o.Actor.IsHostile()).Select(o => new TargetOverride(o.Source, o.Actor!.GameObjectId)).ToImmutableArray();
        var input = new TargetSelectionInput(Player.Position, Player.Object!.HitboxRadius, new(ActionManager.GetActionRange(action), 0),
            new(ExperimentalSelection.AutomaticAllies, ExperimentalSelection.Fallback, false, true, false), snapshot, snapshot,
            TargetOrder.Apply(requested, ExperimentalSelection.AllyOverrideOrder).ToImmutableArray());
        var result = TargetSelection.Select(input);
        TestingApi.RecordDecision("Target", input, result);
        return result;
    }
}
