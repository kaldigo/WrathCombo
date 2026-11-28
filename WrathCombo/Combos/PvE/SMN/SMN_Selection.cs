using System;
using System.Linq;
using static WrathCombo.Combos.PvE.SMN.Config;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class SMN
{
    private static uint SelectEgi(bool area, bool simple)
    {
        if (ActionReady(OriginalHook(Aethercharge)) || Gauge.SummonTimerRemaining != 0 || Gauge.AttunementTimerRemaining != 0) return 0;
        Span<SummonCandidate> candidates = stackalloc SummonCandidate[3];
        var priorities = area ? SMN_AoE_Egi_Priority : SMN_ST_Egi_Priority;
        int count = 0;
        foreach (int index in simple ? new[] { 0, 1, 2 } : priorities.OrderBy(p => p).Select(p => priorities.IndexOf(p)))
        {
            uint spell; bool enabled;
            _ = area ? GetMatchingConfigAoE(index, out spell, out enabled) : GetMatchingConfigST(index, out spell, out enabled);
            if (simple)
            {
                spell = OriginalHook(index == 0 ? SummonTopaz : index == 1 ? SummonEmerald : SummonRuby);
                enabled = index == 0 ? Gauge.IsTitanReady : index == 1 ? Gauge.IsGarudaReady : Gauge.IsIfritReady;
            }
            if (count == candidates.Length) break;
            candidates[count++] = new(spell, enabled && ActionRoutes.Allowed(spell) && ActionReady(spell), index == 2);
        }
        uint ruby = ActionLearned(RubyRite) ? RubyRite : ActionLearned(RubyRuin3) ? RubyRuin3 : ActionLearned(RubyRuin2) ? RubyRuin2 : RubyRuin1;
        bool fits = !ExperimentalSelection.ActiveSummoner || !SelectionRuntime.Automatic ||
            CastBudget.Fits(new(NativeExecution.CastTime(ruby), RemainingGCD, IsMoving() || BossForecastProvider.Fresh?.Moving == true,
                BossForecastProvider.MaxCastTime, BossForecastProvider.Downtime));
        uint selected = SummonPhase.Choose(candidates[..count], fits);
        TestingApi.RecordDecision("SMN.ElementalPhase", new { Area = area, Simple = simple, HardCastFits = fits }, selected);
        return selected;
    }

    internal static uint ForecastAlternative(uint input, uint blocked, Preset preset)
    {
        bool area = preset is Preset.SMN_AoE_Simple_Combo or Preset.SMN_AoE_Advanced_Combo;
        bool simple = preset is Preset.SMN_ST_Simple_Combo or Preset.SMN_AoE_Simple_Combo;
        if (CurrentTarget is { } enemy && (simple || IsEnabled(area ?
                Preset.SMN_AoE_Advanced_Combo_Ruin4 : Preset.SMN_ST_Advanced_Combo_Ruin4)) &&
            LocalPlayer.HasStatus(Buffs.FurtherRuin) && !LocalPlayer.HasStatus(Role.Buffs.Swiftcast) &&
            ActionRoutes.Allowed(Ruin4) && ActionReadyForTarget(Ruin4, enemy.GameObjectId) && InActionRange(Ruin4, enemy)) return Ruin4;
        if (CurrentTarget is not { } target || !simple && !IsEnabled(area ?
            Preset.SMN_AoE_Advanced_Combo_DemiEgiMenu_SwiftcastEgi : Preset.SMN_ST_Advanced_Combo_DemiEgiMenu_SwiftcastEgi)) return All.Cease;
        return PreparedCast.Prepare(input, blocked, target.GameObjectId);
    }
    private static unsafe bool PrepareDamageSpell(ref uint input, uint spell, bool enabled)
    {
        if (!enabled || GetActionCastTime(spell) <= 0 || !ActionRoutes.Allowed(spell) ||
            CurrentTarget is not { } target || !target.IsHostile() || target.IsDead() ||
            !ActionReadyForTarget(spell, target.GameObjectId) || !InActionRange(spell, target) ||
            !ActionManager.CanUseActionOnTarget(spell, target.GameObject())) return false;
        // Preparing an attack must not steal Swiftcast from a confirmed resurrection.
        if (SelectionRuntime.Commitment is not null) return false;
        input = SelectionRuntime.Publish(input, new(Role.Swiftcast, spell, target.GameObjectId, Role.Swiftcast,
            "Swiftcast for the selected enabled summon spell", Environment.TickCount64, SelectionRuntime.Generation));
        return true;
    }

    internal static bool PreparedSpellStillValid(SelectionDecision decision)
    {
        var target = Svc.Objects.SearchById(decision.Recipient);
        if (target is null || !target.IsTargetable || !SelectionRuntime.TargetLifeAllowed(decision.PlannedGcd, target) ||
            !InActionRange(decision.PlannedGcd, target)) return false;
        if (decision.PlannedGcd == Resurrection) return true;
        return decision.PlannedGcd == OriginalHook(Gemshine) || decision.PlannedGcd == OriginalHook(PreciousBrilliance) ||
            decision.PlannedGcd == Slipstream && LocalPlayer.HasStatus(Buffs.GarudasFavor);
    }

    internal static bool TryContinuePreparation(uint input, Preset preset, out uint action)
    {
        action = input;
        if (preset is not (Preset.SMN_ST_Simple_Combo or Preset.SMN_ST_Advanced_Combo or
            Preset.SMN_AoE_Simple_Combo or Preset.SMN_AoE_Advanced_Combo)) return false;
        return ContinuePreparedSpell(ref action);
    }

    private static bool ContinuePreparedSpell(ref uint input)
    {
        if (!ExperimentalSelection.ActiveSummoner || SelectionRuntime.Commitment is not { } prepared) return false;
        if (!PreparedSpellStillValid(prepared) || !LocalPlayer.HasStatus(Role.Buffs.Swiftcast) ||
            !ActionReadyForTarget(prepared.PlannedGcd, prepared.Recipient))
        { input = All.Cease; return true; }
        input = SelectionRuntime.Publish(input, prepared with { Action = prepared.PlannedGcd, Preparation = 0, ObservedAt = Environment.TickCount64 });
        return true;
    }

    private static uint TestingRaise(uint input)
    {
        if (SelectionRuntime.Commitment is { } offensive && offensive.PlannedGcd != Resurrection)
            SelectionRuntime.CancelCommitment("Explicit resurrection supersedes offensive preparation");
        if (ContinuePreparedSpell(ref input)) return input;
        var recipient = SelectionRuntime.SelectAlly(Resurrection).Recipient;
        if (recipient is null || !ActionRoutes.Allowed(Resurrection) || !ActionReadyForTarget(Resurrection, recipient.Value)) return All.Cease;
        bool instant = LocalPlayer.HasStatus(Role.Buffs.Swiftcast);
        bool prepare = !instant && Role.CanSwiftcast();
        if (!instant && !prepare && (!ExperimentalSelection.HardCastRaise || IsMoving())) return All.Cease;
        return SelectionRuntime.Publish(input, new(prepare ? Role.Swiftcast : Resurrection, Resurrection, recipient.Value,
            prepare ? Role.Swiftcast : 0, "Manual resurrection retains its recipient and Swiftcast", Environment.TickCount64, SelectionRuntime.Generation));
    }
}
