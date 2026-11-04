using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Combos.PvE;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.Native;
using WrathFork.Contract;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

// The same selection path serves healing controls, optional damage inclusion and the runner.
// Job priority/threshold configuration is retained; recipient and preparation ownership is shared.
internal static class HealerSelection
{
    [ThreadStatic] private static bool ownsPrevention;
    internal static bool ReplacesLegacyPrevention => ExperimentalSelection.ActiveHealer && ownsPrevention;
    internal static IDisposable Enter(Preset preset) => new ProviderScope(preset);
    private sealed class ProviderScope : IDisposable
    {
        private readonly bool previous = ownsPrevention;
        internal ProviderScope(Preset preset) => ownsPrevention = preset is Preset.WHM_STHeals or Preset.WHM_AoEHeals or
            Preset.SCH_ST_Heal or Preset.SCH_AoE_Heal or Preset.AST_ST_Heals or Preset.AST_AoE_Heals || IsDamageProvider(preset);
        public void Dispose() => ownsPrevention = previous;
    }
    private static bool IsDamageProvider(Preset preset) => preset.Attributes().AutoAction is { IsHeal: false };
    private static RoleActions.IHealer Role => Healer.Role;
    private static uint Job => (uint)Player.Job;
    private static ulong Self => Player.Object!.GameObjectId;
    private static uint RaiseSpell => Job switch { 24 => WHM.Raise, 28 => SCH.Resurrection, 33 => AST.Ascend, _ => SGE.Egeiro };
    private static uint BasicHeal => Job switch { 24 => ActionLearned(WHM.Cure2) ? WHM.Cure2 : WHM.Cure, 28 => SCH.Physick, _ => ActionLearned(AST.Benefic2) ? AST.Benefic2 : AST.Benefic };
    private static uint PartyHeal => Job switch { 24 => OriginalHook(WHM.Medica1), 28 => OriginalHook(SCH.Succor), _ => ActionLearned(AST.AspectedHelios) ? OriginalHook(AST.AspectedHelios) : AST.Helios };

    internal static SelectionDecision? PrePull(ulong focus, bool hot, bool shield)
    {
        if (ExperimentalSelection.ActiveSage) return shield ? SGE.PrePullShield(focus) : null;
        uint input = All.SingleTargetHeals;
        using var route = new ActionRouteContext(ActionRoutes.For(input));
        SelectionRuntime.BeginPreview(input);
        if (Continuation(input) is { } continuation) return continuation;
        var target = Svc.Objects.SearchById(focus);
        if (target is null || target.IsDead() || !target.IsTargetable) return null;
        if (Job == 28 && shield && RotationBehaviour.Protection != false &&
            !target.HasStatus(SCH.Buffs.Galvanize, true) && !target.HasStatus(SGE.Buffs.EukrasianDiagnosis, true) &&
            !JustUsed(OriginalHook(SCH.Adloquium), 4))
            return Prepare(input, OriginalHook(SCH.Adloquium), focus, 0, 0, null);
        if (!hot || RotationBehaviour.Support == false || Job is not (24 or 33)) return null;
        uint spell = Job == 24 ? WHM.Regen : AST.AspectedBenefic;
        ushort buff = Job == 24 ? WHM.Buffs.Regen : AST.Buffs.AspectedBenefic;
        if (JustUsed(spell, 4) || target.HasStatus(buff, true)) return null;
        return Ready(input, spell, focus, "Prepull regeneration on the explicitly focused ally");
    }

    internal static bool TrySelect(uint input, Preset preset, out uint action)
    {
        action = input;
        if (!ExperimentalSelection.ActiveHealer) return false;
        if (preset == Preset.ALL_Healer_Raise && input == RaiseSpell)
        { action = Raise(input)?.Action ?? All.Cease; return true; }
        bool damageInput = input is All.SingleTargetDPS or All.AoEDPS or PveIntentRouting.FullRotation && IsDamageProvider(preset) &&
            CustomActionHelper.OneButtonRotationChecker(input, CustomActionHelper.GetTypeByAttribute(preset.Attributes().AutoAction));
        if (damageInput && SelectionRuntime.Commitment?.PlannedGcd == RaiseSpell && Continuation(input) is { } raising)
        { action = raising.Action; return true; }
        if (damageInput && ExperimentalSelection.IncludeRaise && RotationBehaviour.Support != false &&
            !GetPartyMembers().Any(member => member.BattleChara is { IsDead: false } ally && GetTargetHPPercent(ally) < 30) &&
            Raise(input) is { } resurrection)
        { action = resurrection.Action; return true; }
        if (ExperimentalSelection.ActiveSage) return false;
        if (Job == 28 && preset == Preset.SCH_DeploymentTactics && input == SCH.DeploymentTactics)
        { action = Deployment(input)?.Action ?? All.Cease; return true; }
        bool st = preset is Preset.WHM_STHeals or Preset.SCH_ST_Heal or Preset.AST_ST_Heals &&
            CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.SingleTargetHeals, Job switch { 24 => WHM.Cure, 28 => SCH.Physick, _ => AST.Benefic });
        bool area = preset is Preset.WHM_AoEHeals or Preset.SCH_AoE_Heal or Preset.AST_AoE_Heals &&
            CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.AoEHeals,
                Job switch { 24 => WHM.Medica1, 28 => SCH.Succor, _ => AST.Config.AST_AoE_SimpleHeals_AltMode > 0 ? AST.Helios : AST.AspectedHelios }, PartyHeal);
        // Custom damage controls have a single shared identity across jobs. Native damage
        // replacements continue through their original provider unless explicitly mapped here.
        bool damage = damageInput;
        if (!st && !area && !damage) return false;
        if (Continuation(input) is { } continuation) { action = continuation.Action; return true; }
        if (damage && (!ExperimentalSelection.IncludeHealing || RotationBehaviour.Support == false)) return false;
        bool gcds = !damage || ExperimentalSelection.HealingGcds;
        if (Prevention(input, true, true, gcds) is { } prevention) { action = prevention.Action; return true; }
        if (!damage && Utility(input, area, gcds) is { } utility) { action = utility.Action; return true; }
        if (Recovery(input, area || input == All.AoEDPS || input == PveIntentRouting.FullRotation && PveIntentRouting.Area, gcds, !damage) is { } recovery)
        { action = recovery.Action; return true; }
        if (damage) return false;
        action = All.Cease;
        return true;
    }

    internal static SelectionDecision? Continuation(uint input)
    {
        if (SelectionRuntime.Commitment is not { } decision) return null;
        if (!CommitmentStillNeeded(decision)) return NoAction(decision, "Prepared recipient is no longer eligible");
        if (decision.PlannedGcd == RaiseSpell)
            return RaiseOnRecipient(input, decision.Recipient) ?? NoAction(decision, "Wait for resurrection preparation or spell availability");
        uint next = decision.PlannedGcd;
        if (next == SCH.DeploymentTactics && !Svc.Objects.SearchById(decision.Recipient).HasStatus(SCH.Buffs.Galvanize))
            return NoAction(decision, "Wait for the confirmed shield to become visible");
        if (!ActionRoutes.Allowed(next) || !ActionReadyForTarget(next, decision.Recipient))
            return NoAction(decision, "Prepared follow-up is temporarily unavailable");
        var followup = decision with { Action = next, Preparation = 0, ObservedAt = Environment.TickCount64 };
        SelectionRuntime.Publish(input, followup);
        return followup;
    }
    private static SelectionDecision NoAction(SelectionDecision decision, string reason) => decision with { Action = All.Cease, Reason = reason };
    internal static unsafe bool CommitmentStillNeeded(SelectionDecision decision)
    {
        if (decision.PlannedGcd == RaiseSpell)
        {
            var dead = Svc.Objects.SearchById(decision.Recipient);
            return dead is not null && dead.IsTargetable && SelectionRuntime.TargetLifeAllowed(RaiseSpell, dead) &&
                InActionRange(RaiseSpell, dead) && ActionManager.CanUseActionOnTarget(RaiseSpell, dead.Struct());
        }
        if (ExperimentalSelection.ActiveSage) return SGE.CommitmentStillNeeded(decision);
        var target = Svc.Objects.SearchById(decision.Recipient);
        return target is not null && target.IsTargetable && !target.IsDead() && InActionRange(decision.PlannedGcd, target);
    }

    internal static SelectionDecision? Recovery(uint input, bool area, bool gcds, bool explicitHeal)
    {
        var selected = SelectionRuntime.SelectAlly(BasicHeal);
        if (selected.Recipient is not { } targetId) return null;
        var target = Svc.Objects.SearchById(targetId);
        float hp = area ? GetPartyAvgHPPercent() : GetTargetHPPercent(target, forceUsePending: true);
        var priorities = Job switch
        {
            24 => area ? WHM.Config.WHM_AoE_Heals_Priority : WHM.Config.WHM_ST_Heals_Priority,
            28 => area ? SCH.Config.SCH_AoE_Heals_Priority : SCH.Config.SCH_ST_Heals_Priority,
            _ => area ? AST.Config.AST_AoE_SimpleHeals_Priority : AST.Config.AST_ST_SimpleHeals_Priority,
        };
        for (int priority = 1; priority <= priorities.Count; priority++)
        {
            int index = priorities.IndexOf(priority);
            uint spell; bool enabled;
            int threshold = Job switch
            {
                24 => area ? WHM.GetMatchingConfigAoE(index, target, out spell, out enabled) : WHM.GetMatchingConfigST(index, target, out spell, out enabled),
                28 => area ? SCH.GetMatchingConfigAoE(index, out spell, out enabled) : SCH.GetMatchingConfigST(index, target, out spell, out enabled),
                _ => area ? AST.GetMatchingConfigAoE(index, out spell, out enabled) : AST.GetMatchingConfigST(index, target, out spell, out enabled),
            };
            // Recitation supplies the otherwise missing Aetherflow cost. Eligibility must
            // consider that planned preparation, without spending it during a preview.
            if (Job == 28 && area && spell == SCH.Indomitability &&
                IsEnabled(Preset.SCH_AoE_Heal_Indomitability) && SCH.Config.SCH_AoE_Heal_Indomitability_Recitation &&
                ActionRoutes.Allowed(SCH.Recitation) && ActionReadyForTarget(SCH.Recitation, Self)) enabled = true;
            if (!enabled || hp > threshold || !ActionRoutes.Allowed(spell)) continue;
            if (Job == 28 && spell == OriginalHook(SCH.Adloquium) && SCH.Config.SCH_ST_Heal_AldoquimOpts[1] &&
                !LocalPlayer.HasStatus(SCH.Buffs.EmergencyTactics) &&
                (target.HasStatus(SGE.Buffs.EukrasianDiagnosis, true) || target.HasStatus(SGE.Buffs.EukrasianPrognosis, true))) continue;
            if (Job == 33 && spell is AST.NeutralSect or AST.Horoscope)
            {
                if (!gcds) continue;
                if (Prepare(input, PartyHeal, Self, spell, 0, null) is { } preparation) return preparation;
                continue;
            }
            if (Job == 24 && spell == WHM.PlenaryIndulgence)
            {
                if (gcds && Prepare(input, PartyHeal, Self, spell, 0, null) is { } plenary) return plenary;
                continue;
            }
            if (IsGcd(spell) && !gcds || !IsGcd(spell) && !CanWeave() && !explicitHeal && InCombat()) continue;
            if (!RecipientFor(spell, targetId, out var recipient)) continue;
            if (Job == 28 && area && !LocalPlayer.HasStatus(SCH.Buffs.Recitation) &&
                (spell == SCH.Indomitability && SCH.Config.SCH_AoE_Heal_Indomitability_Recitation ||
                 spell == OriginalHook(SCH.Succor) && SCH.Config.SCH_AoE_Heal_Succor_Options[1]))
            {
                if (Prepare(input, spell, recipient, SCH.Recitation, 0, null) is { } preparation) return preparation;
            }
            if (Job == 24 && IsGcd(spell) && !LocalPlayer.HasStatus(WHM.Buffs.ThinAir) &&
                IsEnabled(area ? Preset.WHM_AoEHeals_ThinAir : Preset.WHM_STHeals_ThinAir) &&
                GetRemainingCharges(WHM.ThinAir) > (area ? WHM.Config.WHM_AoEHeals_ThinAir : WHM.Config.WHM_STHeals_ThinAir))
            {
                if (Prepare(input, spell, recipient, WHM.ThinAir, 0, null) is { } preparation) return preparation;
            }
            if (Ready(input, spell, recipient, "Configured healing priority on validated recipient") is { } candidate) return candidate;
        }
        if (!gcds || !explicitHeal && hp >= 80) return null;
        uint fallback = area ? PartyHeal : BasicHeal;
        return RecipientFor(fallback, targetId, out var fallbackTarget) ? Ready(input, fallback, fallbackTarget, "Healing spell fallback") : null;
    }

    private static SelectionDecision? Raise(uint input)
    {
        if (SelectionRuntime.Commitment is { } accepted)
            return accepted.PlannedGcd == RaiseSpell ? Continuation(input) : null;
        var selected = SelectionRuntime.SelectAlly(RaiseSpell);
        if (selected.Recipient is not { } recipient) return null;
        // Do not race an already observed resurrection cast from another ally.
        if (GetPartyMembers().Any(member => member.BattleChara is { IsCasting: true } caster &&
            caster.GameObjectId != Self && caster.CastTargetObjectId == recipient &&
            caster.CastActionId is WHM.Raise or SCH.Resurrection or AST.Ascend or SGE.Egeiro or RDM.Verraise)) return null;
        return RaiseOnRecipient(input, recipient);
    }

    private static unsafe SelectionDecision? RaiseOnRecipient(uint input, ulong recipient)
    {
        bool thinAir = Job == 24 && IsEnabled(Preset.WHM_ThinAirRaise) &&
            !LocalPlayer.HasStatus(WHM.Buffs.ThinAir) && ActionRoutes.Allowed(WHM.ThinAir) &&
            ActionReadyForTarget(WHM.ThinAir, Self);
        // A preparation accepted by the server can precede its status update. Keep the
        // retained recipient, but do not send the same preparation again in that gap.
        if (Job == 24 && JustUsed(WHM.ThinAir, 2) && !LocalPlayer.HasStatus(WHM.Buffs.ThinAir) ||
            JustUsed(Role.Swiftcast, 2) && !LocalPlayer.HasStatus(Role.Buffs.Swiftcast)) return null;
        bool affordable = LocalPlayer.HasStatus(WHM.Buffs.ThinAir) ||
            LocalPlayer.CurrentMp >= ActionManager.GetActionCost(ActionType.Action, RaiseSpell, 0, 0, 0, 0);
        if (!affordable && thinAir)
            return Prepare(input, RaiseSpell, recipient, WHM.ThinAir, 0, null);
        if (!affordable) return null;
        if (!LocalPlayer.HasStatus(Role.Buffs.Swiftcast) && ActionReadyForTarget(Role.Swiftcast, Self))
            return Prepare(input, RaiseSpell, recipient, Role.Swiftcast, 0, null);
        if (thinAir) return Prepare(input, RaiseSpell, recipient, WHM.ThinAir, 0, null);
        if (LocalPlayer.HasStatus(Role.Buffs.Swiftcast)) return Ready(input, RaiseSpell, recipient, "Resurrection on retained eligible recipient");
        return ExperimentalSelection.HardCastRaise && !IsMoving()
            ? Ready(input, RaiseSpell, recipient, "Permitted hard-cast resurrection") : null;
    }

    private static SelectionDecision? Utility(uint input, bool area, bool gcds)
    {
        if (Job == 28)
        {
            if (SCH.EndAetherpact && Ready(input, SCH.DissolveUnion, Self, "Release completed fairy healing") is { } release) return release;
            if (InCombat() && CanWeave() && !SCH.HasAetherflow && IsEnabled(area ? Preset.SCH_AoE_Heal_Aetherflow : Preset.SCH_ST_Heal_Aetherflow) &&
                (!area || !SCH.Config.SCH_AoE_Heal_Aetherflow_Indomitability || GetCooldownRemainingTime(SCH.Indomitability) <= 1) &&
                Ready(input, SCH.Aetherflow, Self, "Configured healing resource recovery") is { } resource) return resource;
            if (InCombat() && !SCH.HasAetherflow && !SCH.FairyBusy && IsEnabled(area ? Preset.SCH_AoE_Heal_Dissipation : Preset.SCH_ST_Heal_Dissipation) &&
                (!area || !SCH.Config.SCH_AoE_Heal_Dissipation_Indomitability || GetCooldownRemainingTime(SCH.Indomitability) <= 1) &&
                Ready(input, SCH.Dissipation, Self, "Explicitly permitted fairy sacrifice") is { } sacrifice) return sacrifice;
        }
        bool cleanse = !area && IsEnabled(Job switch { 24 => Preset.WHM_STHeals_Esuna, 28 => Preset.SCH_ST_Heal_Esuna, _ => Preset.AST_ST_Heals_Esuna });
        int minimum = Job switch { 24 => WHM.Config.WHM_STHeals_Esuna, 28 => SCH.Config.SCH_ST_Heal_EsunaOption, _ => AST.Config.AST_ST_SimpleHeals_Esuna };
        if (cleanse && gcds && SelectionRuntime.SelectAlly(Role.Esuna, eligible: ally =>
                ally.HasCleansableDebuff && GetTargetHPPercent(ally) >= minimum).Recipient is { } recipient &&
            Svc.Objects.SearchById(recipient) is { HasCleansableDebuff: true } target)
        {
            if (GetTargetHPPercent(target) >= minimum && Ready(input, Role.Esuna, recipient, "Cleanse the selected eligible ally") is { } esuna) return esuna;
        }
        bool lucid = Job switch
        {
            24 => IsEnabled(area ? Preset.WHM_AoEHeals_Lucid : Preset.WHM_STHeals_Lucid) && Role.CanLucidDream(area ? WHM.Config.WHM_AoEHeals_Lucid : WHM.Config.WHM_STHeals_Lucid),
            28 => IsEnabled(area ? Preset.SCH_AoE_Heal_Lucid : Preset.SCH_ST_Heal_Lucid) && Role.CanLucidDream(area ? SCH.Config.SCH_AoE_Heal_LucidOption : SCH.Config.SCH_ST_Heal_LucidOption),
            _ => IsEnabled(area ? Preset.AST_AoE_Heals_Lucid : Preset.AST_ST_Heals_Lucid) && Role.CanLucidDream(area ? AST.Config.AST_AoE_Heals_LucidDreaming : AST.Config.AST_ST_Heals_LucidDreaming),
        };
        return lucid && CanWeave() ? Ready(input, Role.LucidDreaming, Self, "Configured mana recovery") : null;
    }

    internal static SelectionDecision? Deployment(uint input)
    {
        if (Continuation(input) is { } continuation) return continuation;
        var selection = SelectionRuntime.SelectAlly(SCH.Adloquium);
        if (selection.Recipient is not { } recipient || !ActionRoutes.Allowed(SCH.DeploymentTactics) || !ActionLearned(SCH.DeploymentTactics)) return null;
        var target = Svc.Objects.SearchById(recipient);
        if (target.HasStatus(SCH.Buffs.Galvanize)) return Ready(input, SCH.DeploymentTactics, recipient, "Deploy existing shield from selected ally");
        if (!ActionReadyForTarget(SCH.DeploymentTactics, recipient)) return null;
        uint preparation = IsEnabled(Preset.SCH_DeploymentTactics_Recitation) && !LocalPlayer.HasStatus(SCH.Buffs.Recitation) ? SCH.Recitation : 0;
        return Prepare(input, OriginalHook(SCH.Adloquium), recipient, preparation, SCH.DeploymentTactics, null);
    }

    internal static SelectionDecision? Prevention(uint input, bool raidwide, bool tankbuster, bool gcds = true)
    {
        if (ExperimentalSelection.ActiveSage) return SGE.AutomaticPrevention(raidwide, tankbuster, input, gcds);
        if (!ExperimentalSelection.ActiveHealer) return null;
        if (Continuation(input) is { } continuation) return continuation;
        if (RotationBehaviour.Protection == false) return null;
        if (tankbuster && SelectionRuntime.Prevention.Available("tankbuster") is { Recipient: not 0 } buster)
        {
            uint action; bool permitted;
            var victim = Svc.Objects.SearchById(buster.Recipient);
            _ = Job switch { 24 => WHM.GetMatchingConfigST(2, victim, out action, out permitted),
                28 => SCH.GetMatchingConfigST(1, victim, out action, out permitted),
                _ => AST.GetMatchingConfigST(2, victim, out action, out permitted) };
            if (permitted && CanWeave() && Ready(input, action, buster.Recipient, "Known tankbuster recipient", buster.Id) is { } defense) return defense;
        }
        if (!raidwide || SelectionRuntime.Prevention.Available("raidwide") is not { } window) return null;
        float? deadline = window.DueAt is { } due ? Math.Max(0, (due - Environment.TickCount64) / 1000f) : null;
        if (CanWeave())
        {
            if (Job == 24 && RotationBehaviour.Support != false && GroupDamageIncoming(out bool multihit) && multihit &&
                IsEnabled(Preset.WHM_Raidwide_LiturgyOfTheBell) && !LocalPlayer.HasStatus(WHM.Buffs.LiturgyOfTheBell) && !IsMoving() &&
                Ready(input, WHM.LiturgyOfTheBell, Self, "Bell for an observed multihit raidwide", window.Id) is { } bell) return bell;
            if (Job == 24 && IsEnabled(Preset.WHM_Raidwide_Temperance) && Ready(input, WHM.Temperance, Self, "One mitigation for incoming raidwide", window.Id) is { } temperance) return temperance;
            if (Job == 28 && IsEnabled(Preset.SCH_Raidwide_Expedient) && Ready(input, SCH.Expedient, Self, "One mitigation for incoming raidwide", window.Id) is { } expedient) return expedient;
            if (Job == 33 && IsEnabled(Preset.AST_Raidwide_CollectiveUnconscious) && Ready(input, AST.CollectiveUnconscious, Self, "One mitigation for incoming raidwide", window.Id) is { } collective) return collective;
        }
        if (!gcds) return null;
        if (Job == 28 && IsEnabled(Preset.SCH_Raidwide_Succor) && SCH.ShieldCheck)
            return Prepare(input, PartyHeal, Self, SCH.RaidwideRecitation() ? SCH.Recitation : 0, 0, deadline, window.Id);
        if (Job == 33 && IsEnabled(Preset.AST_Raidwide_NeutralSect) && !LocalPlayer.HasStatus(AST.Buffs.NeutralSectShield, true))
            return Prepare(input, PartyHeal, Self, LocalPlayer.HasStatus(AST.Buffs.NeutralSect) ? 0 : AST.NeutralSect, 0, deadline, window.Id);
        return null;
    }

    private static unsafe SelectionDecision? Prepare(uint input, uint followup, ulong recipient, uint preparation, uint completion, float? deadline, long eventId = 0)
    {
        if (!ActionLearned(followup) || !ActionRoutes.Allowed(followup) || !RecipientFor(followup, recipient, out var actual) || actual != recipient ||
            completion != 0 && !ActionRoutes.Allowed(completion)) return null;
        bool free = preparation is SCH.Recitation or WHM.ThinAir || LocalPlayer.HasStatus(SCH.Buffs.Recitation) || LocalPlayer.HasStatus(WHM.Buffs.ThinAir);
        if (!free && LocalPlayer.CurrentMp < ActionManager.GetActionCost(ActionType.Action, followup, 0, 0, 0, 0)) return null;
        float cast = preparation == Role.Swiftcast || LocalPlayer.HasStatus(Role.Buffs.Swiftcast) ? 0 : GetActionCastTime(followup);
        if (SelectionRuntime.Automatic && IsMoving() && cast > 0 && !LocalPlayer.HasStatus(Role.Buffs.Swiftcast)) return null;
        float duration = Math.Max(IsGcd(followup) ? RemainingGCD : 0, preparation != 0 ? 0.7f : 0) + cast + 0.7f;
        if (completion != 0) duration += 0.7f;
        if (deadline is { } seconds && duration > seconds) return null;
        if (preparation != 0 && (!ActionRoutes.Allowed(preparation) || !ActionReadyForTarget(preparation, Self) || InCombat() && !CanWeave())) return null;
        var decision = new SelectionDecision(preparation != 0 ? preparation : followup, followup, recipient, preparation,
            "Prepare the selected healing action and retain its recipient", Environment.TickCount64, SelectionRuntime.Generation, eventId, completion);
        SelectionRuntime.Publish(input, decision);
        return decision;
    }
    private static SelectionDecision? Ready(uint input, uint action, ulong recipient, string reason, long eventId = 0)
    {
        if (!ActionRoutes.Allowed(action) || !ActionReadyForTarget(action, recipient) || !RecipientFor(action, recipient, out var actual)) return null;
        var decision = new SelectionDecision(action, IsGcd(action) ? action : 0, actual, 0, reason, Environment.TickCount64, SelectionRuntime.Generation, eventId);
        SelectionRuntime.Publish(input, decision);
        return decision;
    }
    private static bool IsGcd(uint action) => Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(action)?.ActionCategory.RowId is 2 or 3;
    private static unsafe bool RecipientFor(uint action, ulong intended, out ulong recipient)
    {
        recipient = intended;
        var target = Svc.Objects.SearchById(intended);
        if (target is null || !SelectionRuntime.TargetLifeAllowed(action, target) || !target.IsTargetable) return false;
        if (ActionManager.CanUseActionOnTarget(action, target.Struct()) && InActionRange(action, target)) return true;
        if (Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(action) is { CanTargetSelf: true } row && GetTargetDistance(target) <= row.EffectRange)
        { recipient = Self; return true; }
        return false;
    }
}
