using System;
using System.Linq;
using ECommons.GameHelpers;
using WrathCombo.Combos.PvE;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static partial class HealerSelection
{
    private static long reserveUntil, reservedEvent, reservedGeneration;
    internal static bool ReservesDamageGcd => ExperimentalSelection.ActiveHealer && SelectionRuntime.Automatic &&
        RotationBehaviour.Support != false && !IsMoving() && BossForecastProvider.Fresh?.Moving != true &&
        reservedGeneration == SelectionRuntime.Generation && Environment.TickCount64 < reserveUntil &&
        SelectionRuntime.Prevention.Contains(reservedEvent);

    private static SelectionDecision? ScheduledRecovery(uint input, PreventionWindow window, bool gcds)
    {
        if (RotationBehaviour.Support == false || window.DueAt is not { } impact ||
            SelectionRuntime.Prevention.Fulfilled(window.Id, PreventionPurpose.Recovery)) return null;
        float seconds = (impact - Environment.TickCount64) / 1000f;
        if (seconds < -1.5f || seconds > 5) return null;
        uint spell = 0;
        bool enabled = false;
        switch (Job)
        {
            case 24:
                WHM.GetMatchingConfigAoE(0, Player.Object, out spell, out enabled);
                if (!enabled) WHM.GetMatchingConfigAoE(6, Player.Object, out spell, out enabled);
                if (!enabled && SimplePartyHealing)
                {
                    spell = ActionLearned(WHM.Medica2) && !LocalPlayer.HasStatus(WHM.Buffs.Medica2) && !LocalPlayer.HasStatus(WHM.Buffs.Medica3)
                        ? OriginalHook(WHM.Medica2) : WHM.CanLily && !WHM.BloodLilyReady ? WHM.AfflatusRapture : WHM.Medica1;
                    enabled = true;
                }
                if (!enabled && !ActionLearned(WHM.Medica2)) { spell = WHM.Medica1; enabled = true; }
                break;
            case 28:
                SCH.GetMatchingConfigAoE(5, out spell, out enabled);
                enabled |= SimplePartyHealing && SCH.HasAetherflow;
                if (!enabled || seconds > .5f) return null;
                break;
            case 33:
                if (LocalPlayer.HasStatus(AST.Buffs.Macrocosmos) &&
                    OriginalHook(AST.Macrocosmos) == AST.MicroCosmos)
                    return ScheduleLanding(input, AST.MicroCosmos, Self, window);
                // Release a mature Star only once useful recovery can land.
                AST.GetMatchingConfigAoE(5, out spell, out enabled);
                if (!enabled || spell != AST.StellarDetonation)
                {
                    AST.GetMatchingConfigAoE(6, out spell, out enabled);
                    if (!enabled) AST.GetMatchingConfigAoE(7, out spell, out enabled);
                }
                if (!enabled && SimplePartyHealing)
                {
                    spell = LocalPlayer.HasStatus(AST.Buffs.GiantDominance) ? AST.StellarDetonation : OriginalHook(AST.AspectedHelios);
                    enabled = true;
                }
                break;
        }
        if (IsGcd(spell) && !gcds) return null;
        return enabled ? ScheduleLanding(input, spell, Self, window) : null;
    }

    internal static SelectionDecision? ScheduleLanding(uint input, uint spell, ulong recipient, PreventionWindow window)
    {
        if (RotationBehaviour.Support == false || window.DueAt is not { } impact || spell == 0 ||
            SelectionRuntime.Prevention.Fulfilled(window.Id, PreventionPurpose.Recovery) ||
            IsMoving() && NativeExecution.CastTime(spell) > 0 || !ActionRoutes.Allowed(spell)) return null;
        float seconds = (impact - Environment.TickCount64) / 1000f;
        // Coverage is based on living recipients; full HP before impact is not a
        // reason to exclude them. A spread party should not trigger a blind AoE.
        var party = GetPartyMembers().Where(p => p.BattleChara is { IsDead: false }).ToArray();
        // Pneuma's damage line is not its healing geometry. Its secondary heal
        // is a 20-yalm circle around the caster (official Sage job guide).
        int covered = spell == SGE.Pneuma ? party.Count(member => GetTargetDistance(member.BattleChara) <= 20)
            : NumberOfAlliesInRange(spell, Player.Object);
        if (party.Length > 1 && covered < Math.Ceiling(party.Length * .6)) return null;
        var plan = HealLanding.Plan(new(seconds, NativeExecution.CastTime(spell), IsGcd(spell) ? RemainingGCD : 0,
            GCDTotal > 0 ? GCDTotal : 2.5f, .1f, false));
        TestingApi.RecordDecision("Healing.Landing", new { window.Id, Spell = spell, Impact = seconds }, plan);
        if (!plan.Fits || !ExecutionSafety.HealingCastFits(NativeExecution.CastTime(spell), plan.StartIn)) return null;
        if (SelectionRuntime.Automatic && plan.ReserveGcd)
        {
            reservedEvent = window.Id; reservedGeneration = SelectionRuntime.Generation;
            reserveUntil = Environment.TickCount64 + (long)(plan.StartIn * 1000);
        }
        uint preparation = Job == 24 && IsGcd(spell) && (SimplePartyHealing || IsEnabled(Preset.WHM_Raidwide_PlenaryIndulgence))
            ? WHM.PlenaryIndulgence : Job == 40 && spell == SGE.Pneuma && (IsEnabled(Preset.SGE_AoE_Simple_Heal) || IsEnabled(Preset.SGE_AoE_Adv_Heal_Zoe)) ? SGE.Zoe : 0;
        if (preparation != 0 && !SelectionRuntime.Prevention.Fulfilled(window.Id, PreventionPurpose.Enhancement) &&
            SelectionRuntime.Commitment is null && (CanWeave() || RemainingGCD <= .1f) &&
            ActionRoutes.Allowed(preparation) && ActionReadyForTarget(preparation, Self))
        {
            var preparedPlan = HealLanding.Plan(new(seconds, NativeExecution.CastTime(spell), Math.Max(RemainingGCD, .7f),
                GCDTotal > 0 ? GCDTotal : 2.5f, .1f, false));
            if (preparedPlan.Fits && (plan.ReserveGcd || preparedPlan.StartIn <= Math.Max(0, RemainingGCD) + .5f))
            {
                var prepared = new SelectionDecision(preparation, spell, recipient, preparation,
                    "Prepare the selected post-impact healing action", Environment.TickCount64, SelectionRuntime.Generation, window.Id,
                    Purpose: PreventionPurpose.Enhancement,
                    FollowupAfter: SelectionRuntime.Automatic ? Environment.TickCount64 + (long)(preparedPlan.StartIn * 1000) : 0,
                    FollowupPurpose: PreventionPurpose.Recovery);
                SelectionRuntime.Publish(input, prepared);
                return prepared;
            }
        }
        if (plan.StartIn > .5f) return null;
        if (Ready(input, spell, recipient, "Recovery scheduled to land after predicted damage", window.Id) is not { } ready) return null;
        var decision = ready with { Purpose = PreventionPurpose.Recovery, ExecuteAfter = Environment.TickCount64 + (long)(plan.StartIn * 1000) };
        SelectionRuntime.Publish(input, decision);
        return decision;
    }
}
