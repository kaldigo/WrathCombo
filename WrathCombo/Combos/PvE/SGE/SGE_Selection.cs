using System;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using WrathCombo.Combos.PvE.ALL;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.Native;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.CustomComboNS;
using WrathFork.Contract;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class SGE
{
    internal static SelectionDecision? UrgentRecovery(uint input, bool gcds)
    {
        if (!ExperimentalSelection.ActiveSage || RotationBehaviour.Support == false) return null;
        var buster = EncounterDamage.Tankbuster();
        ulong? priorityRecipient = buster.Recipient != 0 && buster.Seconds is >= 0 and <= 4 ? buster.Recipient : null;
        var selected = SelectionRuntime.SelectAlly(Diagnosis);
        if (priorityRecipient != null && (selected.Recipient is not { } urgentRecipient ||
            Svc.Objects.SearchById(urgentRecipient) is not { } urgentActor || GetTargetHPPercent(urgentActor, forceUsePending: true) > 35))
            selected = SelectionRuntime.SelectAlly(Diagnosis, priorityRecipient);
        if (selected.Recipient is not { } recipient || Svc.Objects.SearchById(recipient) is not { } target ||
            !HealerSelection.UrgentHealth(GetTargetHPPercent(target, forceUsePending: true), recipient)) return null;
        if (IsEnabled(Preset.SGE_ST_Simple_Heal))
            foreach (uint spell in new[] { Taurochole, Druochole, Pepsis })
            {
                if (!ActionRoutes.Allowed(spell) || !TryHealingRecipient(spell, recipient, out var actual) ||
                    !ActionReadyForTarget(spell, actual) || spell == Pepsis && !target.HasStatus(Buffs.EukrasianDiagnosis)) continue;
                var decision = new SelectionDecision(spell, 0, actual, 0, "Urgent simple-mode recovery", Environment.TickCount64, SelectionRuntime.Generation);
                SelectionRuntime.Publish(input, decision);
                return decision;
            }
        foreach (int priority in System.Linq.Enumerable.Range(1, Config.SGE_ST_Heals_Priority.Count))
        {
            int index = Config.SGE_ST_Heals_Priority.IndexOf(priority);
            if (!TrySTHealOption(index, target, out uint spell, out int threshold) ||
                spell is not (Taurochole or Druochole or Pepsis) || GetTargetHPPercent(target) > threshold ||
                !ActionRoutes.Allowed(spell) || !TryHealingRecipient(spell, recipient, out var actual) || !ActionReadyForTarget(spell, actual)) continue;
            var decision = new SelectionDecision(spell, 0, actual, 0, "Urgent recovery preempts nonurgent preparation", Environment.TickCount64, SelectionRuntime.Generation);
            SelectionRuntime.Publish(input, decision);
            return decision;
        }
        uint heal = LocalPlayer.HasStatus(Buffs.Eukrasia) ? EukrasianDiagnosis : Diagnosis;
        if (!gcds || !ActionRoutes.Allowed(heal) || !ActionReadyForTarget(heal, recipient)) return null;
        var fallback = new SelectionDecision(heal, heal, recipient, 0, "Urgent direct healing without new preparation", Environment.TickCount64, SelectionRuntime.Generation);
        SelectionRuntime.Publish(input, fallback);
        return fallback;
    }
    internal static SelectionDecision? PrePullShield(ulong focus)
    {
        if (RotationBehaviour.Protection == false) return null;
        uint input = All.SingleTargetHeals;
        using var route = new ActionRouteContext(ActionRoutes.For(input));
        SelectionRuntime.BeginPreview(input);
        uint suggestion = input;
        if (ExperimentalContinuation(ref suggestion)) return SelectionRuntime.Preview(input);
        var selected = SelectionRuntime.SelectAlly(EukrasianDiagnosis, focus);
        if (selected.Recipient is not { } recipient) return null;
        var target = Svc.Objects.SearchById(recipient);
        if (PublishPreparation(ref suggestion, EukrasianDiagnosis, recipient, Eukrasia, true,
            !target.HasStatus(Buffs.EukrasianDiagnosis, true) && !target.HasStatus(SCH.Buffs.Galvanize, true),
            LocalPlayer.HasStatus(Buffs.Eukrasia), null)) return SelectionRuntime.Preview(input);
        return null;
    }

    internal static SelectionDecision? AutomaticPrevention(bool raidwide, bool tankbuster, uint control = 0, bool gcds = true)
    {
        if (!ExperimentalSelection.ActiveSage || !ExperimentalSelection.PreventiveHealing) return null;
        uint input = control == 0 ? All.AoeHeals : control;
        var pending = SelectionRuntime.Commitment;
        if (control == 0 && pending?.PlannedGcd == EukrasianDiagnosis) input = All.SingleTargetHeals;
        SelectionRuntime.BeginPreview(input);
        using var route = control == 0 ? new ActionRouteContext(ActionRoutes.For(input)) : null;
        if (UrgentRecovery(input, gcds) is { } urgent) return urgent;
        if (pending is not null)
        {
            uint continuation = input;
            if (ExperimentalContinuation(ref continuation)) return SelectionRuntime.Preview(input) ??
                new SelectionDecision(All.Cease, pending.PlannedGcd, pending.Recipient, 0, "Committed follow-up temporarily unavailable", Environment.TickCount64, SelectionRuntime.Generation);
            return null;
        }
        if (RotationBehaviour.Protection == false && RotationBehaviour.Support == false) return null;
        bool simpleST = IsEnabled(Preset.SGE_ST_Simple_Heal), simpleArea = IsEnabled(Preset.SGE_AoE_Simple_Heal);
        if (RotationBehaviour.Protection != false && tankbuster)
        foreach (var buster in SelectionRuntime.Prevention.Active("tankbuster"))
        {
            if (buster.Recipient == 0 || buster.DueAt is { } busterImpact && busterImpact - Environment.TickCount64 is < 0 or > 6000) continue;
            if (control == 0) input = All.SingleTargetHeals;
            using var singleRoute = control == 0 ? new ActionRouteContext(ActionRoutes.For(input)) : null;
            if (!SelectionRuntime.Prevention.Fulfilled(buster.Id, PreventionPurpose.Reduction) &&
                CanWeave() && (simpleST ? HasAddersgall : HasAddersgallAboveHold) && (simpleST || IsEnabled(Preset.SGE_ST_Adv_Heal_Taurochole)) && EventAbility(input, Taurochole, buster.Recipient, buster.Id) is { } tauro) return tauro;
            var busterPatient = Svc.Objects.SearchById(buster.Recipient);
            if ((CanWeave() || RemainingGCD <= .1f) && !SelectionRuntime.Prevention.Fulfilled(buster.Id, PreventionPurpose.Shield) &&
                (TrySTHealOption(4, busterPatient, out _, out _) || simpleST && !busterPatient.HasStatus(Buffs.Panhaima, true)) &&
                EventAbility(input, Haima, buster.Recipient, buster.Id) is { } haima) return haima;
            var target = SelectionRuntime.SelectAlly(EukrasianDiagnosis, buster.Recipient);
            if (target.Recipient is { } recipient)
            {
                var actor = Svc.Objects.SearchById(recipient);
                uint suggestion = input;
                if (gcds && !SelectionRuntime.Prevention.Fulfilled(buster.Id, PreventionPurpose.Shield) && PublishPreparation(ref suggestion, EukrasianDiagnosis, recipient, Eukrasia,
                    simpleST || IsEnabled(Preset.SGE_ST_Adv_Heal_EDiagnosis), !actor.HasStatus(Buffs.EukrasianDiagnosis, true) && !actor.HasStatus(SCH.Buffs.Galvanize, true),
                    LocalPlayer.HasStatus(Buffs.Eukrasia), buster.DueAt is { } at ? (at - Environment.TickCount64) / 1000f : null, buster.Id)) return SelectionRuntime.Preview(input);
            }
        }
        if (raidwide && SelectionRuntime.Prevention.Available("raidwide") is { } window)
        {
            if (control == 0) input = All.AoeHeals;
            float? seconds = window.DueAt is { } at ? (at - Environment.TickCount64) / 1000f : null;
            bool before = seconds is null or > 0;
            if (RotationBehaviour.Protection != false && before && (seconds is null or <= 6) && (CanWeave() || RemainingGCD <= .1f) &&
                !SelectionRuntime.Prevention.Fulfilled(window.Id, PreventionPurpose.Reduction) && !PartyMitigationCoverage.Covers(window))
            {
                if ((simpleArea || IsEnabled(Preset.SGE_Raidwide_Kerachole)) && (simpleArea ? HasAddersgall : HasAddersgallAboveHold) && !LocalPlayer.HasStatus(Buffs.Kerachole, true) &&
                    EventAbility(input, Kerachole, Player.Object!.GameObjectId, window.Id) is { } kera) return kera;
                if (IsEnabled(Preset.SGE_Raidwide_Holos) && GetPartyAvgHPPercent() <= Config.SGE_Raidwide_HolosOption && !LocalPlayer.HasStatus(Buffs.Holosakos, true) &&
                    EventAbility(input, Holos, Player.Object!.GameObjectId, window.Id) is { } holos) return holos;
            }
            if (before && seconds is >= 0 and <= 5 && (CanWeave() || RemainingGCD <= .1f))
            {
                bool multi = GroupDamageIncoming(out bool multihit) && multihit;
                foreach (int index in multi ? new[] { 4, 2, 6 } : new[] { 2, 6 })
                {
                    if (!TimedRecoveryOption(index, simpleArea, out uint reactive) || JustUsed(reactive)) continue;
                    bool protection = reactive == Panhaima;
                    if ((protection ? RotationBehaviour.Protection : RotationBehaviour.Support) == false) continue;
                    if (EventAbility(input, reactive, Player.Object!.GameObjectId, window.Id) is { } prepared) return prepared;
                }
            }
            uint suggestion = input;
            if (RotationBehaviour.Protection != false && gcds && before && (seconds is null or <= 4) && !SelectionRuntime.Prevention.Fulfilled(window.Id, PreventionPurpose.Shield) &&
                SelectPrognosis(ref suggestion, seconds, window.Id)) return SelectionRuntime.Preview(input);
            if (RotationBehaviour.Support != false && seconds is >= -1.5f and <= 5)
            {
                if (gcds && (simpleArea || IsEnabled(Preset.SGE_AoE_Adv_Heal_Zoe) || LocalPlayer.HasStatus(Buffs.Zoe)) && HasBattleTarget() && InActionRange(Pneuma) && ActionReady(Pneuma) &&
                    HealerSelection.ScheduleLanding(input, Pneuma, CurrentTarget!.GameObjectId, window) is { } pneuma) return pneuma;
                if (seconds <= .5f && TimedRecoveryOption(1, simpleArea, out uint heal) &&
                    HealerSelection.ScheduleLanding(input, heal, Player.Object!.GameObjectId, window) is { } recovery) return recovery;
            }
        }
        return null;
    }

    private static bool TimedRecoveryOption(int index, bool simple, out uint action)
    {
        if (TryAoEHealOption(index, out action, out _)) return true;
        if (!simple) return false;
        action = index switch { 1 => Ixochole, 2 => OriginalHook(Physis), 4 => Panhaima, 6 => Philosophia, _ => 0 };
        return index switch { 1 => UseIxochole(), 2 => UsePhysis(), 4 => UsePanhaima(), 6 => UsePhilosophia(), _ => false };
    }

    private static SelectionDecision? EventAbility(uint input, uint action, ulong recipient, long window)
    {
        if (!ActionRoutes.Allowed(action) || !ActionReadyForTarget(action, recipient) || !TryHealingRecipient(action, recipient, out _)) return null;
        var purpose = action is Taurochole or Kerachole or Holos ? PreventionPurpose.Reduction :
            action is Haima or Panhaima ? PreventionPurpose.Shield : PreventionPurpose.Reactive;
        if (SelectionRuntime.Prevention.Fulfilled(window, purpose)) return null;
        var decision = new SelectionDecision(action, 0, recipient, 0, "Timed prevention for observed damage window", Environment.TickCount64, SelectionRuntime.Generation, window, Purpose: purpose);
        SelectionRuntime.Publish(input, decision);
        return decision;
    }

    internal static bool TryTestingSelection(uint input, Preset preset, out uint action)
    {
        action = input;
        if (!ExperimentalSelection.ActiveSage) return false;
        bool stHeal = preset == Preset.SGE_ST_Advanced_Heal && CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.SingleTargetHeals, Diagnosis);
        bool aoeHeal = preset == Preset.SGE_AoE_Advanced_Heal && CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.AoEHeals, Prognosis);
        bool stDamage = preset == Preset.SGE_ST_Advanced_DPS && CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.SingleTargetDPS, Dosis, Dosis2, Dosis3);
        bool aoeDamage = preset == Preset.SGE_AoE_Advanced_DPS && CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.AoEDPS, Dyskrasia, Dyskrasia2);
        if (!stHeal && !aoeHeal && !stDamage && !aoeDamage) return false;
        if ((!stDamage && !aoeDamage || ExperimentalSelection.EffectiveHealing) &&
            UrgentRecovery(input, !stDamage && !aoeDamage || ExperimentalSelection.EffectiveHealingGcds) is { } urgent)
        { action = urgent.Action; return true; }
        if (ExperimentalContinuation(ref action)) return true;
        bool damage = stDamage || aoeDamage;
        if (damage && (!ExperimentalSelection.EffectiveHealing || RotationBehaviour.Support == false)) return false;
        bool gcds = !damage || ExperimentalSelection.EffectiveHealingGcds;
        if (AutomaticPrevention(aoeHeal || damage, stHeal || damage, input, gcds) is { } prevention)
        { action = prevention.Action; return true; }
        if (!damage && TryTestingUtility(ref action, aoeHeal)) return true;
        if (aoeHeal || aoeDamage)
        {
            float hp = GetPartyAvgHPPercent();
            if (CanWeave())
                for (int priority = 0; priority < Config.SGE_AoE_Heals_Priority.Count; priority++)
                {
                    int index = Config.SGE_AoE_Heals_Priority.IndexOf(priority + 1);
                    if (index is 7 or 8 || !TryAoEHealOption(index, out uint spell, out int threshold) || hp > threshold || !ActionReadyForTarget(spell, Player.Object!.GameObjectId)) continue;
                    action = SelectionRuntime.Publish(input, new(spell, 0, Player.Object!.GameObjectId, 0, "Configured party healing ability", Environment.TickCount64, SelectionRuntime.Generation));
                    return true;
                }
            if (gcds && hp < 80 && IsEnabled(Preset.SGE_AoE_Adv_Heal_Zoe) && ActionReady(Pneuma) && HasBattleTarget() && InActionRange(Pneuma))
            {
                if (SelectPneuma(ref action)) return true;
            }
            if (gcds && RotationBehaviour.Protection != false &&
                (hp < 80 || !damage || ForecastRaidwide() is >= 2.5f and <= 6f) && SelectPrognosis(ref action, ForecastRaidwide())) return true;
            if (!damage)
            {
                action = ActionRoutes.Allowed(Prognosis) ? OriginalHook(Prognosis) : All.Cease;
                return true;
            }
            // An AoE damage button can still need a single-target heal.
        }

        var recipient = SelectionRuntime.SelectAlly(Diagnosis);
        if (recipient.Recipient is not { } targetId)
        {
            if (!damage || recipient.Source != TargetSource.Automatic) { action = All.Cease; return true; }
            return false;
        }
        var target = Svc.Objects.SearchById(targetId);
        float health = GetTargetHPPercent(target, Config.SGE_ST_Adv_Heal_IncludeShields);
        if (gcds && IsEnabled(Preset.SGE_ST_Adv_Heal_Esuna) && target.HasCleansableDebuff &&
            health >= Config.SGE_ST_Adv_Heal_Esuna && ActionReadyForTarget(Role.Esuna, targetId))
        {
            action = SelectionRuntime.Publish(input, new(Role.Esuna, Role.Esuna, targetId, 0, "Cleanse selected eligible ally", Environment.TickCount64, SelectionRuntime.Generation));
            return true;
        }
        if (CanWeave())
            for (int priority = 0; priority < Config.SGE_ST_Heals_Priority.Count; priority++)
            {
                int index = Config.SGE_ST_Heals_Priority.IndexOf(priority + 1);
                if (index is 1 or 7 || !TrySTHealOption(index, target, out uint spell, out int threshold) || health > threshold) continue;
                if (!TryHealingRecipient(spell, targetId, out var actualRecipient)) continue;
                if (!ActionReadyForTarget(spell, actualRecipient)) continue;
                action = SelectionRuntime.Publish(input, new(spell, 0, actualRecipient, 0, "Configured single-target healing ability", Environment.TickCount64, SelectionRuntime.Generation));
                return true;
            }
        if (gcds && RotationBehaviour.Protection != false && SelectDiagnosis(ref action, false)) return true;
        if (damage && (!gcds || health >= 80)) return false;
        action = ActionRoutes.Allowed(Diagnosis) ? SelectionRuntime.Publish(input,
            new(Diagnosis, Diagnosis, targetId, 0, "Selected healing GCD", Environment.TickCount64, SelectionRuntime.Generation)) : All.Cease;
        return true;
    }

    private static unsafe bool TryHealingRecipient(uint spell, ulong intended, out ulong recipient)
    {
        recipient = intended;
        var target = Svc.Objects.SearchById(intended);
        if (target is not null && ActionManager.CanUseActionOnTarget(spell, target.Struct()) && InActionRange(spell, target)) return true;
        if (Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(spell, out var row) && row.CanTargetSelf && target is not null &&
            GetTargetDistance(target) <= row.EffectRange)
        { recipient = Player.Object!.GameObjectId; return true; }
        return false;
    }

    private static bool TryTestingUtility(ref uint input, bool aoe)
    {
        if (!aoe && IsEnabled(Preset.SGE_ST_Adv_Heal_Kardia) && !LocalPlayer.HasStatus(Buffs.Kardia) &&
            SimpleTarget.AnyLivingTank is { } tank && ActionReadyForTarget(Kardia, tank.GameObjectId))
        {
            input = SelectionRuntime.Publish(input, new(Kardia, 0, tank.GameObjectId, 0, "Configured Kardia recipient", Environment.TickCount64, SelectionRuntime.Generation));
            return true;
        }
        if (!CanWeave()) return false;
        uint ability = 0;
        if (IsEnabled(aoe ? Preset.SGE_AoE_Adv_Heal_Lucid : Preset.SGE_ST_Adv_Heal_Lucid) &&
            UseLucid(aoe ? Config.SGE_AoE_Adv_Heal_LucidOption : Config.SGE_ST_Adv_Heal_LucidOption)) ability = Role.LucidDreaming;
        else if (IsEnabled(aoe ? Preset.SGE_AoE_Adv_Heal_Rhizomata : Preset.SGE_ST_Adv_Heal_Rhizomata) &&
            UseRhizo(1)) ability = Rhizomata;
        if (ability == 0 || !ActionRoutes.Allowed(ability)) return false;
        input = SelectionRuntime.Publish(input, new(ability, 0, Player.Object!.GameObjectId, 0, "Configured healer resource recovery", Environment.TickCount64, SelectionRuntime.Generation));
        return true;
    }

    private static float? ForecastRaidwide()
    {
        var forecast = BossForecastProvider.Current;
        return ExperimentalSelection.Forecasts && forecast is not null
            ? forecast.Remaining(forecast.Raidwide, Environment.TickCount64, BossForecastProvider.Generation) : null;
    }
    internal static bool CommitmentStillNeeded(SelectionDecision decision)
    {
        if (decision.PlannedGcd == Egeiro) return HealerSelection.CommitmentStillNeeded(decision);
        var target = Svc.Objects.SearchById(decision.Recipient);
        if (target is null || !target.IsTargetable || target.IsDead() || !InActionRange(decision.PlannedGcd, target)) return false;
        if (decision.PlannedGcd == EukrasianDiagnosis &&
            (target.HasStatus(Buffs.EukrasianDiagnosis, true) || target.HasStatus(SCH.Buffs.Galvanize, true))) return false;
        return true;
    }
    private static bool SelectPneuma(ref uint input)
    {
        if (!HasBattleTarget() || !InActionRange(Pneuma) || !ActionReady(Pneuma)) { input = All.Cease; return true; }
        return PublishPreparation(ref input, Pneuma, CurrentTarget!.GameObjectId, Zoe, true, true,
            LocalPlayer.HasStatus(Buffs.Zoe) || !ActionReady(Zoe) || !ActionRoutes.Allowed(Zoe) || !CanWeave(), null);
    }
    private static bool SelectDiagnosis(ref uint input, bool simpleMode)
    {
        var committed = SelectionRuntime.Commitment;
        var selection = SelectionRuntime.SelectAlly(EukrasianDiagnosis,
            committed?.PlannedGcd == EukrasianDiagnosis ? committed.Recipient : null);
        if (selection.Recipient is not { } recipient)
        {
            if (selection.Source != TargetSource.Automatic || committed is not null) { input = All.Cease; return true; }
            return false;
        }
        var target = Svc.Objects.SearchById(recipient);
        bool permitted = simpleMode || IsEnabled(Preset.SGE_ST_Adv_Heal_EDiagnosis);
        bool needed = !target.HasStatus(Buffs.EukrasianDiagnosis, true) && !target.HasStatus(SCH.Buffs.Galvanize, true) &&
            (simpleMode || GetTargetHPPercent(target, Config.SGE_ST_Adv_Heal_IncludeShields) <= Config.SGE_ST_Adv_Heal_EDiagnosisHP);
        return PublishPreparation(ref input, EukrasianDiagnosis, recipient, Eukrasia, permitted, needed,
            LocalPlayer.HasStatus(Buffs.Eukrasia), null);
    }

    private static unsafe bool PublishPreparation(ref uint input, uint followup, ulong recipient, uint preparation,
        bool permitted, bool needed, bool prepared, float? deadline, long preventionEvent = 0)
    {
        if (!ExecutionSafety.HealingCastFits(NativeExecution.CastTime(followup), prepared ? 0 : .7f)) return false;
        var snapshot = new PreparationInput(followup, recipient, preparation,
            permitted && ActionLearned(followup) && ActionRoutes.Allowed(followup) && LocalPlayer.CurrentMp >= ActionManager.GetActionCost(ActionType.Action, followup, 0, 0, 0, 0), true, needed,
            ActionRoutes.Allowed(preparation), ActionReadyForTarget(preparation, Player.Object!.GameObjectId), prepared, preparation == Eukrasia || CanWeave(),
            Math.Max(RemainingGCD, prepared ? 0 : preparation == Eukrasia ?
                ActionManager.GetAdjustedRecastTime(ActionType.Action, Eukrasia) / 1000f : .7f) + NativeExecution.CastTime(followup) + .15f,
            deadline, Environment.TickCount64, SelectionRuntime.Generation);
        var result = PreparationPolicy.Select(snapshot);
        TestingApi.RecordDecision("Preparation", snapshot, result);
        if (result is null) return false;
        input = SelectionRuntime.Publish(input, result with { PreventionEvent = preventionEvent,
            Purpose = preventionEvent == 0 ? PreventionPurpose.All : PreventionPurpose.Shield });
        return true;
    }

    private static bool SelectPrognosis(ref uint input, float? deadline = null, long preventionEvent = 0)
    {
        uint followup = ActionLearned(EukrasianPrognosis2) ? EukrasianPrognosis2 : EukrasianPrognosis;
        bool simple = IsEnabled(Preset.SGE_AoE_Simple_Heal);
        int coverageLimit = simple ? 50 : Config.SGE_AoE_Adv_Heal_EPrognosisOption;
        bool needed = GetPartyBuffPercent(Buffs.EukrasianPrognosis) <= coverageLimit &&
            GetPartyBuffPercent(SCH.Buffs.Galvanize) <= coverageLimit;
        return PublishPreparation(ref input, followup, Player.Object!.GameObjectId, Eukrasia,
            simple || IsEnabled(Preset.SGE_Raidwide_EPrognosis) || IsEnabled(Preset.SGE_AoE_Adv_Heal_EPrognosis), needed, LocalPlayer.HasStatus(Buffs.Eukrasia), deadline, preventionEvent);
    }

    internal static bool ExperimentalContinuation(ref uint input)
    {
        if (!ExperimentalSelection.ActiveSage || SelectionRuntime.Commitment is not { } commitment) return false;
        if (commitment.PlannedGcd == Egeiro)
        {
            input = HealerSelection.Continuation(input)?.Action ?? All.Cease;
            return true;
        }
        if (commitment.PlannedGcd == EukrasianDiagnosis)
        {
            if (SelectionRuntime.SelectAlly(EukrasianDiagnosis, commitment.Recipient).Recipient is null)
            { input = All.Cease; return true; }
            var target = Svc.Objects.SearchById(commitment.Recipient);
            if (target.HasStatus(Buffs.EukrasianDiagnosis, true) || target.HasStatus(SCH.Buffs.Galvanize, true))
            { input = All.Cease; return true; }
        }
        if (!ActionRoutes.Allowed(commitment.PlannedGcd)) { input = All.Cease; return true; }
        if (commitment.PlannedGcd == Pneuma &&
            (Svc.Objects.SearchById(commitment.Recipient) is not { } enemy || !InActionRange(Pneuma, enemy) || enemy.IsDead()))
        { input = All.Cease; return true; }
        bool prepared = commitment.Preparation == Eukrasia ? LocalPlayer.HasStatus(Buffs.Eukrasia) : LocalPlayer.HasStatus(Buffs.Zoe);
        if (!prepared)
        {
            if (Environment.TickCount64 - commitment.ObservedAt >= 1200)
            { SelectionRuntime.CancelCommitment("Sage preparation status missing or consumed"); return false; }
            input = All.Cease; return true;
        }
        input = SelectionRuntime.Publish(input, commitment with { Action = commitment.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 });
        return true;
    }
}
