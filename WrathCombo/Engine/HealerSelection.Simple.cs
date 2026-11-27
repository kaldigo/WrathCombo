using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using WrathCombo.Combos.PvE;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.Native;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static partial class HealerSelection
{
    private sealed record SimpleCandidate(uint Action, ulong Recipient, uint Followup = 0, long EventId = 0);

    private static bool TrySimple(uint input, Preset preset, out uint action)
    {
        action = input;
        bool st = preset is Preset.WHM_SimpleSTHeals or Preset.SCH_Simple_ST_Heal or Preset.AST_Simple_ST_Heals &&
            CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.SingleTargetHeals, Job switch { 24 => WHM.Cure, 28 => SCH.Physick, _ => AST.Benefic });
        bool area = preset is Preset.WHM_Simple_AoEHeals or Preset.SCH_Simple_AoE_Heal or Preset.AST_Simple_AoE_Heals &&
            CustomActionHelper.OneButtonRotationChecker(input, CustomActionType.AoEHeals, Job switch { 24 => WHM.Medica1, 28 => SCH.Succor, _ => AST.Helios }, PartyHeal);
        if (!st && !area) return false;
        if (Continuation(input) is { } committed) { action = committed.Action; return true; }
        if (Job == 24 && st && ContentSpecificActions.TryGet(ref action, out var content, healing: true))
        { action = content; return true; }
        if (Prevention(input, true, true) is { } prevention) { action = prevention.Action; return true; }
        var selected = SelectionRuntime.SelectAlly(BasicHeal);
        if (selected.Recipient is not { } recipient || Svc.Objects.SearchById(recipient) is not IBattleChara target)
        { action = All.Cease; return true; }
        foreach (var candidate in SimpleCandidates(area, target))
        {
            SelectionDecision? decision = candidate.Followup == 0
                ? Ready(input, candidate.Action, candidate.Recipient, "Simple healing priority on validated recipient", candidate.EventId)
                : Prepare(input, candidate.Followup, candidate.Recipient, candidate.Action, 0, null, candidate.EventId);
            if (decision is null) continue;
            action = decision.Action;
            return true;
        }
        action = All.Cease;
        return true;
    }

    private static IEnumerable<SimpleCandidate> SimpleCandidates(bool area, IBattleChara target)
    {
        ulong ally = target.GameObjectId;
        float hp = GetTargetHPPercent(target, forceUsePending: true), partyHp = GetPartyAvgHPPercent();
        bool tank = target.Role is CombatRole.Tank && target.IsInParty() || !IsInParty();
        var window = ExperimentalSelection.PreventiveHealing ? SelectionRuntime.Prevention.Available("raidwide") : null;
        bool incoming = window is not null && GroupDamageIncoming();
        SimpleCandidate SelfAction(uint action) => new(action, Self, EventId: incoming && action is
            WHM.Temperance or WHM.LiturgyOfTheBell or SCH.Expedient or SCH.SacredSoil or AST.CollectiveUnconscious ? window!.Id : 0);
        SimpleCandidate AllyAction(uint action) => new(action, ally);
        if (Job == 24 && !area)
        {
            if (hp <= 20) yield return AllyAction(WHM.Benediction);
            if (hp <= 50) yield return AllyAction(WHM.Tetragrammaton);
        }
        if (Job == 28 && SCH.EndAetherpact) yield return SelfAction(SCH.DissolveUnion);
        if (Job == 33 && AST.HasNoDPSCard) yield return SelfAction(OriginalHook(AST.AstralDraw));
        if (!area && SelectionRuntime.SelectAlly(Role.Esuna, eligible: actor => actor.HasCleansableDebuff && GetTargetHPPercent(actor) >= 40).Recipient is { } cleanse)
            yield return new(Role.Esuna, cleanse);
        if (Job == 28 && InCombat() && !SCH.HasAetherflow)
        {
            yield return SelfAction(SCH.Aetherflow);
            if (!area && !SCH.FairyBusy) yield return SelfAction(SCH.Dissipation);
        }
        if (Role.CanLucidDream(Job == 24 && area ? WHM.Config.WHM_AoEHeals_Lucid : 6500) && (Job == 33 || CanWeave()))
            yield return SelfAction(Role.LucidDreaming);

        if (Job == 24)
        {
            uint plannedHeal = BasicHeal;
            ulong plannedRecipient = ally;
            if (area)
            {
                plannedRecipient = Self;
                if (!WHM.BloodLilyReady && ActionReadyForTarget(WHM.AfflatusRapture, Self)) plannedHeal = WHM.AfflatusRapture;
                else if (NumberOfAlliesInRange(WHM.Cure3) >= GetPartyMembers().Count * .75 && ActionReadyForTarget(WHM.Cure3, ally))
                { plannedHeal = WHM.Cure3; plannedRecipient = ally; }
                else if (!LocalPlayer.HasStatus(WHM.Buffs.Medica2) && !LocalPlayer.HasStatus(WHM.Buffs.Medica3) && ActionReadyForTarget(OriginalHook(WHM.Medica2), Self))
                    plannedHeal = OriginalHook(WHM.Medica2);
                else plannedHeal = OriginalHook(WHM.Medica1);
            }
            if (area) yield return SelfAction(WHM.Assize);
            if ((area || !InBossEncounter()) && TimeStoodStill >= TimeSpan.FromSeconds(5)) yield return SelfAction(WHM.Asylum);
            if (!area)
            {
                if (!target.HasStatus(WHM.Buffs.Regen) && hp >= 40) yield return AllyAction(WHM.Regen);
                if (!target.HasStatus(WHM.Buffs.DivineBenison)) yield return AllyAction(WHM.DivineBenison);
                if (tank) yield return AllyAction(WHM.Aquaveil);
            }
            if (area ? partyHp <= 70 || incoming && !PartyMitigationCoverage.Covers(window!) : !InBossEncounter()) yield return SelfAction(WHM.Temperance);
            if (area)
            {
                yield return SelfAction(WHM.DivineCaress);
                if (partyHp <= 50 || incoming) yield return SelfAction(WHM.LiturgyOfTheBell);
                if (partyHp <= 70 || incoming) yield return new(WHM.PlenaryIndulgence, plannedRecipient, plannedHeal, incoming ? window!.Id : 0);
            }
            if (!WHM.BloodLilyReady) yield return area ? SelfAction(WHM.AfflatusRapture) : AllyAction(WHM.AfflatusSolace);
            if (GetRemainingCharges(WHM.ThinAir) == 2 && plannedHeal != WHM.AfflatusRapture) yield return new(WHM.ThinAir, plannedRecipient, plannedHeal);
            if (area)
            {
                if (NumberOfAlliesInRange(WHM.Cure3) >= GetPartyMembers().Count * .75) yield return AllyAction(WHM.Cure3);
                if (!LocalPlayer.HasStatus(WHM.Buffs.Medica2) && !LocalPlayer.HasStatus(WHM.Buffs.Medica3)) yield return SelfAction(OriginalHook(WHM.Medica2));
                yield return SelfAction(OriginalHook(WHM.Medica1));
            }
            else yield return AllyAction(BasicHeal);
        }
        else if (Job == 28)
        {
            if (area && incoming && !PartyMitigationCoverage.Covers(window!))
            {
                yield return SelfAction(SCH.Expedient);
                yield return SelfAction(SCH.SacredSoil);
            }
            if (SCH.Gauge.SeraphTimer > 0 && !SCH.FairyBusy) yield return SelfAction(SCH.Consolation);
            if (!area)
            {
                if (hp <= 50) { yield return AllyAction(SCH.Excogitation); yield return AllyAction(SCH.Lustrate); }
                if (!InBossEncounter() && TimeStoodStill >= TimeSpan.FromSeconds(5)) yield return SelfAction(SCH.SacredSoil);
                if (tank) yield return AllyAction(SCH.Protraction);
                if (SCH.Gauge.FairyGauge >= 50 && IsOriginal(SCH.Aetherpact) && !SCH.FairyBusy) yield return AllyAction(SCH.Aetherpact);
            }
            else if (InCombat() && ActionLearned(SCH.Indomitability) && IsOffCooldown(SCH.Indomitability))
            {
                if (!SCH.HasAetherflow && !LocalPlayer.HasStatus(SCH.Buffs.Recitation)) yield return new(SCH.Recitation, Self, SCH.Indomitability);
                yield return SelfAction(SCH.Indomitability);
            }
            if ((area || !InBossEncounter()) && HasPetPresent() && !SCH.FairyBusy)
            {
                yield return SelfAction(SCH.WhisperingDawn);
                yield return SelfAction(SCH.FeyIllumination);
                yield return SelfAction(SCH.FeyBlessing);
                yield return SelfAction(OriginalHook(SCH.SummonSeraph));
                yield return SelfAction(SCH.Seraphism);
            }
            if (!area && !InBossEncounter()) yield return SelfAction(SCH.Expedient);
            uint shield = OriginalHook(area ? SCH.Succor : SCH.Adloquium);
            bool convert = area ? GetPartyBuffPercent(SCH.Buffs.Galvanize) >= 50 || GetPartyBuffPercent(SGE.Buffs.EukrasianPrognosis) >= 50
                : target.HasStatus(SCH.Buffs.Galvanize, true) || !LocalPlayer.HasStatus(SCH.Buffs.EmergencyTactics);
            if (convert && !LocalPlayer.HasStatus(SCH.Buffs.EmergencyTactics)) yield return new(OriginalHook(SCH.EmergencyTactics), area ? Self : ally, shield);
            yield return new(shield, area ? Self : ally);
            if (!area) yield return AllyAction(SCH.Physick);
        }
        else
        {
            if (!area)
            {
                if (hp <= 30) yield return AllyAction(AST.EssentialDignity);
                if (tank) yield return AllyAction(AST.Exaltation);
            }
            else
            {
                if (OriginalHook(AST.Macrocosmos) == AST.MicroCosmos && partyHp < 50) yield return SelfAction(AST.MicroCosmos);
                if (LocalPlayer.HasStatus(AST.Buffs.GiantDominance)) yield return SelfAction(AST.StellarDetonation);
                if (LocalPlayer.HasStatus(AST.Buffs.HoroscopeHelios)) yield return SelfAction(AST.HoroscopeHeal);
            }
            if (area || !InBossEncounter())
            {
                yield return SelfAction(OriginalHook(AST.CelestialOpposition));
                if (!area && !LocalPlayer.HasStatus(AST.Buffs.NeutralSect)) yield return new(AST.NeutralSect, ally, AST.AspectedBenefic);
                if (AST.HasLady && ActionLearned(AST.MinorArcana)) yield return SelfAction(OriginalHook(AST.LadyOfCrown));
                yield return SelfAction(OriginalHook(AST.CollectiveUnconscious));
                if (area && !LocalPlayer.HasStatus(AST.Buffs.NeutralSect)) yield return new(AST.NeutralSect, Self, PartyHeal);
            }
            if (!area)
            {
                if (!target.HasStatus(AST.Buffs.AspectedBenefic) || !target.HasStatus(AST.Buffs.NeutralSectShield) && LocalPlayer.HasStatus(AST.Buffs.NeutralSect)) yield return AllyAction(AST.AspectedBenefic);
                if ((AST.HasArrow || AST.HasBole) && tank) yield return AllyAction(OriginalHook(AST.Play2));
                if (AST.HasEwer || AST.HasSpire) yield return AllyAction(OriginalHook(AST.Play3));
                if (!target.HasStatus(AST.Buffs.Intersection) && GetRemainingCharges(AST.EssentialDignity) <= GetRemainingCharges(AST.CelestialIntersection)) yield return AllyAction(AST.CelestialIntersection);
                yield return AllyAction(AST.EssentialDignity);
                yield return AllyAction(BasicHeal);
            }
            else
            {
                yield return SelfAction(AST.Macrocosmos);
                uint spell = GetPartyBuffPercent(AST.Buffs.AspectedHelios) <= 50 && GetPartyBuffPercent(AST.Buffs.HeliosConjunction) <= 50 ? PartyHeal : AST.Helios;
                if (!LocalPlayer.HasStatus(AST.Buffs.HoroscopeHelios)) yield return new(AST.Horoscope, Self, spell);
                yield return SelfAction(spell);
            }
        }
    }
}
