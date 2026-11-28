using System;
using Dalamud.Game.ClientState.Objects.Types;
using WrathCombo.Combos.PvP;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class PvpTargetEligibility
{
    internal static bool GuardedBypass(uint action) => ExperimentalSelection.PvpTargeting && CurrentTarget is { } target &&
        (target.HasStatus(PvPCommon.Buffs.Guard, true) || target.HasStatus(PvPCommon.Buffs.WeakenedGuard, true)) &&
        ActionRoutes.Allowed(action) && CanDamage(action, target) && InActionRange(action, target) &&
        ActionReadyForTarget(action, target.GameObjectId);
    internal static bool CanDamage(uint action, IGameObject target) => Selection.PvpEligibility.CanDamage(
        PvPCommon.TargetImmuneToDamage(false, target), target.HasStatus(VPRPvP.Buffs.HardenedScales, true),
        Math.Max(target.Status(PvPCommon.Buffs.Guard, true).RemainingTimeOrZero(),
            target.Status(PvPCommon.Buffs.WeakenedGuard, true).RemainingTimeOrZero()),
        BypassesGuard(action), GetActionCastTime(action));

    // Explicit action exceptions, reviewed against RSR's action settings. Reduction
    // bypass does not grant permission to hit an invulnerable target or use movement.
    private static bool BypassesGuard(uint action) => action is
        DRGPvP.ChaoticSpring or MCHPvP.Drill or NINPvP.Assassinate or SCHPvP.ChainStratagem or
        RDMPvP.EnchantedRiposte or RDMPvP.EnchantedZwerchhau or RDMPvP.EnchantedRedoublement or
        VPRPvP.DeathRattle or VPRPvP.TwinfangBite or VPRPvP.TwinbloodBite or
        VPRPvP.UncoiledTwinfang or VPRPvP.UncoiledTwinblood or
        VPRPvP.FirstLegacy or VPRPvP.SecondLegacy or VPRPvP.ThirdLegacy or VPRPvP.FourthLegacy or
        PvPPhysRanged.EagleEyeShot or PvPMelee.Smite or PvPTank.FullSwing ||
        LocalPlayer.HasStatus(RPRPvP.Buffs.DeathWarrant) && action != RPRPvP.DeathWarrant && action == OriginalHook(RPRPvP.DeathWarrant) ||
        LocalPlayer.HasStatus(RPRPvP.Buffs.PerfectioParata) && action != RPRPvP.TenebraeLemurum && action == OriginalHook(RPRPvP.TenebraeLemurum);
}
