using System;
using System.Collections.Generic;
using ECommons.GameHelpers;
using WrathCombo.Combos.PvE;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static partial class HealerSelection
{
    private static PreventionPurpose EventPurpose(uint action, long eventId) => eventId == 0 ? PreventionPurpose.All : action switch
    {
        AST.Bole or WHM.Aquaveil or AST.Exaltation or WHM.Temperance or SCH.Expedient or SCH.SacredSoil or SCH.FeyIllumination or AST.CollectiveUnconscious or AST.SunSign => PreventionPurpose.Reduction,
        AST.Spire or WHM.DivineBenison or AST.CelestialIntersection or WHM.DivineCaress or SCH.Consolation => PreventionPurpose.Shield,
        SCH.Protraction or SCH.Excogitation or WHM.LiturgyOfTheBell or WHM.Asylum or SCH.SummonSeraph or SCH.Seraphism or AST.EarthlyStar or AST.Macrocosmos => PreventionPurpose.Reactive,
        _ => PreventionPurpose.Recovery,
    };

    private static IEnumerable<(uint Action, bool Enabled, float Earliest, float Latest)> Toolkit()
    {
        uint action; bool enabled;
        if (SimplePartyHealing)
            foreach (var candidate in SimpleCandidates(true, Player.Object!))
                if (candidate.Followup == 0 && !IsGcd(candidate.Action) &&
                    EventPurpose(candidate.Action, 1) is PreventionPurpose.Reduction or PreventionPurpose.Shield or PreventionPurpose.Reactive)
                    yield return (candidate.Action, true, 0, 5);
        switch (Job)
        {
            case 24:
                WHM.GetMatchingConfigAoE(8, Player.Object, out action, out enabled);
                yield return (action, enabled, 0, 5);
                WHM.GetMatchingConfigAoE(4, Player.Object, out action, out enabled);
                yield return (action, enabled, 0, 3);
                break;
            case 28:
                SCH.GetMatchingConfigAoE(3, out action, out enabled);
                yield return (action, enabled, 0, 5);
                // Use the existing job checks for pet presence/busy state and
                // configured content; never spend fairy resources from a preview.
                SCH.GetMatchingConfigAoE(6, out action, out enabled);
                yield return (action, enabled, 2, 8);
                SCH.GetMatchingConfigAoE(4, out action, out enabled);
                yield return (action, enabled && !ActionReady(SCH.SummonSeraph), 2, 6);
                SCH.GetMatchingConfigAoE(1, out action, out enabled);
                yield return (action, enabled, 0, 5);
                yield return (SCH.SacredSoil, IsEnabled(Preset.SCH_Raidwide_SacredSoil) && SCH.HasAetherflow, 0, 5);
                break;
            case 33:
                bool star = SimplePartyHealing || IsEnabled(Preset.AST_ST_DPS_EarthlyStar) || IsEnabled(Preset.AST_AOE_DPS_EarthlyStar);
                yield return (AST.EarthlyStar, star && !LocalPlayer.HasStatus(AST.Buffs.EarthlyDominance) &&
                    !LocalPlayer.HasStatus(AST.Buffs.GiantDominance), 10, 15);
                yield return (AST.SunSign, IsEnabled(Preset.AST_Raidwide_NeutralSect), 0, 5);
                if (GroupDamageIncoming(out bool multihit) && multihit)
                    yield return (AST.Macrocosmos, IsEnabled(Preset.AST_AOE_DPS_MacroCosmos) &&
                        !LocalPlayer.HasStatus(AST.Buffs.Macrocosmos) &&
                        (!InBossEncounter() || AST.Config.AST_AOE_DPS_MacroCosmos_SubOption == 1), 0, 5);
                break;
        }
    }

    private static bool SimplePartyHealing => Job switch
    {
        24 => IsEnabled(Preset.WHM_Simple_AoEHeals),
        28 => IsEnabled(Preset.SCH_Simple_AoE_Heal),
        33 => IsEnabled(Preset.AST_Simple_AoE_Heals),
        _ => false,
    };

    private static SelectionDecision? BusterToolkit(uint input, PreventionWindow window, bool gcds)
    {
        var victim = ECommons.DalamudServices.Svc.Objects.SearchById(window.Recipient);
        if (victim is null) return null;
        bool simple = Job switch { 24 => IsEnabled(Preset.WHM_SimpleSTHeals), 28 => IsEnabled(Preset.SCH_Simple_ST_Heal), _ => IsEnabled(Preset.AST_Simple_ST_Heals) };
        if (simple && victim is Dalamud.Game.ClientState.Objects.Types.IBattleChara patient && (CanWeave() || RemainingGCD <= .1f))
            foreach (var candidate in SimpleCandidates(false, patient))
                if (candidate.Followup == 0 && !IsGcd(candidate.Action) &&
                    EventPurpose(candidate.Action, window.Id) is not PreventionPurpose.Recovery &&
                    Ready(input, candidate.Action, candidate.Recipient, "Simple-mode attributed tankbuster toolkit", window.Id) is { } ready) return ready;
        int[] indices = Job switch { 24 => [3, 2], 28 => [2, 1], _ => [2, 0, 3, 6] };
        if (CanWeave() || RemainingGCD <= .1f)
            foreach (int index in indices)
            {
                uint action; bool enabled;
                _ = Job switch { 24 => WHM.GetMatchingConfigST(index, victim, out action, out enabled),
                    28 => SCH.GetMatchingConfigST(index, victim, out action, out enabled),
                    _ => AST.GetMatchingConfigST(index, victim, out action, out enabled) };
                if (enabled && Ready(input, action, window.Recipient, "Attributed tankbuster toolkit", window.Id) is { } decision) return decision;
            }
        if (gcds && Job == 28 && (simple || IsEnabled(Preset.SCH_ST_Heal_Adloquium)) &&
            !victim.HasStatus(SCH.Buffs.Galvanize, true) && !victim.HasStatus(SGE.Buffs.EukrasianDiagnosis, true))
            return Prepare(input, OriginalHook(SCH.Adloquium), window.Recipient, 0, 0,
                window.DueAt is { } at ? (at - Environment.TickCount64) / 1000f : null, window.Id);
        return null;
    }

    private static SelectionDecision? TimedToolkit(uint input, PreventionWindow window)
    {
        if (!CanWeave() && RemainingGCD > .1f) return null;
        if (window.DueAt is not { } due) return null;
        float seconds = (due - Environment.TickCount64) / 1000f;
        foreach (var candidate in Toolkit())
        {
            if (!candidate.Enabled || seconds < candidate.Earliest || seconds > candidate.Latest) continue;
            var purpose = EventPurpose(candidate.Action, window.Id);
            if ((purpose is PreventionPurpose.Reduction or PreventionPurpose.Shield ? RotationBehaviour.Protection : RotationBehaviour.Support) == false) continue;
            if (SelectionRuntime.Prevention.Fulfilled(window.Id, purpose)) continue;
            if (purpose == PreventionPurpose.Reduction && PartyMitigationCoverage.Covers(window)) continue;
            if (Ready(input, candidate.Action, Self, "Event-timed toolkit on validated recipients", window.Id) is { } decision) return decision;
        }
        return null;
    }
}
