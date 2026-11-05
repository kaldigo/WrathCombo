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
        if (pending is not null)
        {
            uint continuation = input;
            if (ExperimentalContinuation(ref continuation)) return SelectionRuntime.Preview(input) ??
                new SelectionDecision(All.Cease, pending.PlannedGcd, pending.Recipient, 0, "Committed follow-up temporarily unavailable", Environment.TickCount64, SelectionRuntime.Generation);
            return null;
        }
        if (RotationBehaviour.Protection == false) return null;
        if (tankbuster && SelectionRuntime.Prevention.Available("tankbuster") is { } buster && buster.Recipient != 0)
        {
            if (control == 0) input = All.SingleTargetHeals;
            using var singleRoute = control == 0 ? new ActionRouteContext(ActionRoutes.For(input)) : null;
            if (CanWeave() && HasAddersgallAboveHold && IsEnabled(Preset.SGE_ST_Adv_Heal_Taurochole) && EventAbility(input, Taurochole, buster.Recipient, buster.Id) is { } tauro) return tauro;
            var target = SelectionRuntime.SelectAlly(EukrasianDiagnosis, buster.Recipient);
            if (target.Recipient is { } recipient)
            {
                var actor = Svc.Objects.SearchById(recipient);
                uint suggestion = input;
                if (gcds && PublishPreparation(ref suggestion, EukrasianDiagnosis, recipient, Eukrasia,
                    IsEnabled(Preset.SGE_ST_Adv_Heal_EDiagnosis), !actor.HasStatus(Buffs.EukrasianDiagnosis, true) && !actor.HasStatus(SCH.Buffs.Galvanize, true),
                    LocalPlayer.HasStatus(Buffs.Eukrasia), null, buster.Id)) return SelectionRuntime.Preview(input);
            }
        }
        if (raidwide && SelectionRuntime.Prevention.Available("raidwide") is { } window)
        {
            if (control == 0) input = All.AoeHeals;
            if (CanWeave() && !PartyMitigationCoverage.Covers(window))
            {
                if (IsEnabled(Preset.SGE_Raidwide_Kerachole) && HasAddersgallAboveHold && !LocalPlayer.HasStatus(Buffs.Kerachole, true) &&
                    EventAbility(input, Kerachole, Player.Object!.GameObjectId, window.Id) is { } kera) return kera;
                if (IsEnabled(Preset.SGE_Raidwide_Holos) && GetPartyAvgHPPercent() <= Config.SGE_Raidwide_HolosOption && !LocalPlayer.HasStatus(Buffs.Holosakos, true) &&
                    EventAbility(input, Holos, Player.Object!.GameObjectId, window.Id) is { } holos) return holos;
            }
            uint suggestion = input;
            if (gcds && SelectPrognosis(ref suggestion, ForecastRaidwide(), window.Id)) return SelectionRuntime.Preview(input);
        }
        return null;
    }

    private static SelectionDecision? EventAbility(uint input, uint action, ulong recipient, long window)
    {
        if (!ActionReadyForTarget(action, recipient) || !TryHealingRecipient(action, recipient, out _)) return null;
        var decision = new SelectionDecision(action, 0, recipient, 0, "One prevention action for observed damage window", Environment.TickCount64, SelectionRuntime.Generation, window);
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
        if (ExperimentalContinuation(ref action)) return true;
        bool damage = stDamage || aoeDamage;
        if (damage && (!ExperimentalSelection.IncludeHealing || RotationBehaviour.Support == false)) return false;
        bool gcds = !damage || ExperimentalSelection.HealingGcds;
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
            if (damage) return false;
            action = ActionRoutes.Allowed(Prognosis) ? OriginalHook(Prognosis) : All.Cease;
            return true;
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
        var snapshot = new PreparationInput(followup, recipient, preparation,
            permitted && ActionLearned(followup) && ActionRoutes.Allowed(followup) && LocalPlayer.CurrentMp >= ActionManager.GetActionCost(ActionType.Action, followup, 0, 0, 0, 0), true, needed,
            ActionRoutes.Allowed(preparation), ActionReadyForTarget(preparation, Player.Object!.GameObjectId), prepared, preparation == Eukrasia || CanWeave(),
            prepared ? 1.5f : 2.5f, deadline, Environment.TickCount64, SelectionRuntime.Generation);
        var result = PreparationPolicy.Select(snapshot);
        TestingApi.RecordDecision("Preparation", snapshot, result);
        if (result is null) return false;
        input = SelectionRuntime.Publish(input, result with { PreventionEvent = preventionEvent });
        return true;
    }

    private static bool SelectPrognosis(ref uint input, float? deadline = null, long preventionEvent = 0)
    {
        uint followup = ActionLearned(EukrasianPrognosis2) ? EukrasianPrognosis2 : EukrasianPrognosis;
        bool needed = GetPartyBuffPercent(Buffs.EukrasianPrognosis) <= Config.SGE_AoE_Adv_Heal_EPrognosisOption &&
            GetPartyBuffPercent(SCH.Buffs.Galvanize) <= Config.SGE_AoE_Adv_Heal_EPrognosisOption;
        return PublishPreparation(ref input, followup, Player.Object!.GameObjectId, Eukrasia,
            IsEnabled(Preset.SGE_Raidwide_EPrognosis) || IsEnabled(Preset.SGE_AoE_Adv_Heal_EPrognosis), needed, LocalPlayer.HasStatus(Buffs.Eukrasia), deadline, preventionEvent);
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
        if (!prepared) { input = All.Cease; return true; }
        input = SelectionRuntime.Publish(input, commitment with { Action = commitment.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 });
        return true;
    }
}
