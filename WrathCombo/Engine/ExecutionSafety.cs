using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using WrathCombo.Combos.PvE;
using WrathCombo.Combos.PvE.Content;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine.Selection;
using WrathCombo.Services;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static unsafe class ExecutionSafety
{
    private static readonly CastOwnership ownership = new();
    internal static void ManualInput(bool queued = false) => ownership.ManualInput(queued);
    internal static void Submitted(uint action)
    {
        var state = NativeExecution.Capture(fresh: true);
        if (NativeExecution.CastTime(action) <= 0 && state.Cast != action) return;
        ownership.Submit(action, state.Cast, state.Queued && state.QueuedAction == action,
            NativeExecution.CastGeneration, Environment.TickCount64);
    }

    internal static bool ChannelBlocksAll => Player.Available && !Svc.ClientState.IsPvP &&
        ((uint)Player.Job == 31 && LocalPlayer.HasStatus(MCH.Buffs.Flamethrower) ||
         (uint)Player.Job == 36 && (LocalPlayer.Status(BLU.Buffs.PhantomFlurry).RemainingTimeOrZero() > 1 ||
             LocalPlayer.HasStatus(BLU.Buffs.ChelonianGate) && !LocalPlayer.HasStatus(BLU.Buffs.AuspiciousTrance)) ||
         (uint)Player.Job == 19 && IsEnabled(Preset.PLD_BlockForWings) && LocalPlayer.HasStatus(PLD.Buffs.PassageOfArms));

    private static bool ChannelAllows(uint action)
    {
        if (ChannelBlocksAll) return false;
        if ((uint)Player.Job != 36) return true;
        if (LocalPlayer.HasStatus(BLU.Buffs.PhantomFlurry)) return action == OriginalHook(BLU.PhantomFlurry);
        if (LocalPlayer.HasStatus(BLU.Buffs.ChelonianGate)) return action == BLU.DivineCataract;
        return true;
    }

    internal static bool MustStop => BossForecastProvider.Fresh is { SpecialMode: 1 } forecast &&
        (forecast.SpecialModeIn is null || forecast.Remaining(forecast.SpecialModeIn, Environment.TickCount64, BossForecastProvider.Generation) is null or <= .2f);

    internal static void Tick()
    {
        if (!Player.Available || Svc.ClientState.IsPvP || !AutomationOwnership.Allowed || !Service.Configuration.RotationConfig.Enabled) return;
        var state = NativeExecution.Capture();
        if (MustStop && UIState.Instance() is var ui && ui != null && ui->WeaponState.AutoAttackState.Get())
            ui->WeaponState.AutoAttackState.Set(false);
        if (!ownership.Observe(state.Cast, NativeExecution.CastGeneration, Environment.TickCount64) ||
            RotationInterop.ManualLimitBreak.Waiting()) return;
        var forecast = BossForecastProvider.Fresh;
        bool cancel = forecast?.ForceCancel == true || forecast?.ForceCancelAI == true || MustStop;
        var actor = Svc.Objects.SearchById(Player.Object!.CastTargetObjectId);
        cancel |= IncomingRaise.IsRaise(state.Cast)
            ? actor is { IsDead: false } || actor is IBattleChara { HasRaiseStatus: true }
            : actor is { IsDead: true };
        if (!cancel) return;
        var nativeUi = UIState.Instance();
        if (nativeUi == null) return;
        nativeUi->Hotbar.CancelCast();
        SelectionRuntime.Interrupt(state.Cast);
        AccordDiagnostics.DiagnosticLog.Event("Execution.CancelCast", () => new { Action = state.Cast, forecast?.ForceCancel, forecast?.ForceCancelAI, Pyretic = MustStop });
        ownership.ManualInput();
    }

    internal static bool HealingCastFits(float cast, float earliestStart = 0)
    {
        if (!SelectionRuntime.Automatic || !ExperimentalSelection.Forecasts) return true;
        if (MustStop) return false;
        if (cast <= 0) return true;
        var forecast = BossForecastProvider.Fresh;
        if (forecast?.ForceCancel == true || forecast?.ForceCancelAI == true) return false;
        return CastBudget.Fits(new(cast, Math.Max(earliestStart, RemainingGCD),
            IsMoving() || forecast?.Moving == true, BossForecastProvider.MaxCastTime, null));
    }

    internal static uint Review(uint input, uint action, Preset preset)
    {
        if (!SelectionRuntime.Automatic || !ExperimentalSelection.Forecasts || Svc.ClientState.IsPvP || action >= All.SingleTargetDPS) return action;
        if (MustStop) return All.Cease;
        float cast = NativeExecution.CastTime(action);
        if (cast <= 0) return action;
        var forecast = BossForecastProvider.Fresh;
        bool hostile = Data.ActionWatching.ActionSheet.TryGetValue(action, out var row) && row.CanTargetHostile;
        var budget = new CastBudgetInput(cast, RemainingGCD, IsMoving() || forecast?.Moving == true,
            BossForecastProvider.MaxCastTime, hostile ? BossForecastProvider.Downtime : null);
        bool fits = forecast?.ForceCancel != true && forecast?.ForceCancelAI != true && CastBudget.Fits(budget);
        TestingApi.RecordDecision("Cast.SharedBudget", budget, fits);
        if (fits) return action;
        if (SelectionRuntime.ScriptedOpener) return All.Cease;
        if (!hostile || preset.Attributes().AutoAction is not { IsHeal: false } ||
            SelectionRuntime.Preview(input) is { Purpose: PreventionPurpose.Recovery }) return All.Cease;
        uint alternative = (uint)Player.Job switch
        {
            35 => RDM.ForecastAlternative(input, action, preset),
            27 => SMN.ForecastAlternative(input, action, preset),
            42 => PCT.ForecastAlternative(input, action, preset),
            28 when preset == Preset.SCH_ST_Simple_DPS || IsEnabled(Preset.SCH_ST_ADV_DPS_Ruin2Movement) => SCH.Ruin2,
            34 when preset == Preset.SAM_ST_SimpleMode || IsEnabled(Preset.SAM_ST_Adv_RangedUptime) => SAM.Enpi,
            _ => All.Cease,
        };
        if (alternative >= All.SingleTargetDPS || !ActionRoutes.Allowed(alternative) || !ActionReady(alternative) ||
            NativeExecution.CastTime(alternative) > 0) return All.Cease;
        return alternative;
    }

    internal static bool CanExecute(uint action, ulong recipient)
    {
        if (!SelectionRuntime.Automatic || Svc.ClientState.IsPvP) return true;
        if (MustStop || !ChannelAllows(action)) return false;
        bool targeted = action is PLD.Intervene or WAR.Onslaught or WAR.PrimalRend or DRK.Shadowstride or GNB.Trajectory or
            DRG.DragonfireDive or DRG.Stardiver or MNK.Thunderclap or SAM.Gyoten or VPR.Slither or RDM.Corpsacorps or
            SMN.CrimsonCyclone || action == OccultCrescent.PhantomKick;
        float fixedDistance = action switch
        {
            DNC.EnAvant => 10, PCT.Smudge or RPR.HellsIngress => 15,
            RPR.HellsEgress or DRG.ElusiveJump or RDM.Displacement => -15,
            SAM.Yaten => -10, _ => 0,
        };
        if (!targeted && fixedDistance == 0 || Player.Object is not { } player) return true;
        Vector3 from = player.Position, destination;
        if (targeted)
        {
            if (Svc.Objects.SearchById(recipient) is not { } target) return false;
            Vector3 delta = target.Position - from;
            float distance = delta.Length();
            destination = distance > .01f ? from + delta / distance * Math.Max(0, distance - target.HitboxRadius) : from;
        }
        else destination = from + new Vector3(MathF.Sin(player.Rotation), 0, MathF.Cos(player.Rotation)) * fixedDistance;
        bool? safe = targeted ? BossForecastProvider.DashSafe(from, destination) : BossForecastProvider.FixedDashSafe(fixedDistance);
        AccordDiagnostics.DiagnosticLog.Event("Execution.DashSafety", () => new { Action = action, Recipient = recipient, From = from, To = destination, Safe = safe });
        return safe != false; // Unsupported hints preserve existing behavior and manual access.
    }
}
