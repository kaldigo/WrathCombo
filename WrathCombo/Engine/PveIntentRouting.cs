using System;
using System.Linq;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using WrathCombo.Combos.PvE;
using WrathCombo.Core;
using WrathCombo.Extensions;
using WrathCombo.Native;
using WrathCombo.Services;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class PveIntentRouting
{
    internal const uint FullRotation = 1_000_016;
    private static readonly Selection.AdaptiveDamage choice = new();
    private static string context = "";
    private static bool active, area, previewArea;
    private static (uint Action, bool Area, long At) pending;
    internal static bool IsDamageButton(uint input) => input is All.SingleTargetDPS or All.AoEDPS or FullRotation;
    internal static bool Area => active && area;
    internal static uint RouteInput(uint input) => input == FullRotation && !InPvP() ? ((active ? area : previewArea) ? All.AoEDPS : All.SingleTargetDPS) : input;
    private static void Context()
    {
        string current = $"{Player.Object?.GameObjectId}:{Options.Job}:{Player.Level}:{InPvP()}:{Player.IsDead}:{Svc.ClientState.TerritoryType}";
        if (current == context) return;
        context = current; choice.Reset(); pending = default;
    }
    internal static IDisposable Enter(uint input)
    {
        Context();
        var previous = (active, area);
        active = input == FullRotation && !InPvP();
        if (active)
        {
            uint root = Options.Job switch
            {
                19 => PLD.TotalEclipse, 21 => WAR.Overpower, 32 => DRK.Unleash, 37 => GNB.DemonSlice,
                24 => WHM.Holy, 28 => SCH.ArtOfWar, 33 => AST.Gravity, 40 => SGE.Dyskrasia,
                20 => MNK.ArmOfTheDestroyer, 22 => DRG.DoomSpike, 30 => NIN.DeathBlossom,
                34 => SAM.Fuga, 39 => RPR.SpinningScythe, 41 => VPR.SteelMaw,
                23 => BRD.QuickNock, 31 => MCH.SpreadShot, 38 => DNC.Windmill,
                25 => BLM.Fire2, 27 => SMN.Outburst, 35 => RDM.Scatter, 42 => PCT.FireIIinRed,
                _ => 0,
            };
            bool available = root != 0 && ActionLearned(root) && ActionReplacer.FilteredCombos?.Any(c =>
                c.Preset.Attributes().AutoAction is { IsAoE: true, IsHeal: false } && IsEnabled(c.Preset)) == true;
            int hits = available ? SelectionRuntime.DamageCoverage(root) ?? NumberOfEnemiesInRange(OriginalHook(root), CurrentTarget, true) : 0;
            int threshold = Service.Configuration.RotationConfig.DPSSettings.DPSAoETargets ?? int.MaxValue;
            area = choice.Select(available, hits, threshold, Options.Job == 30 && NIN.InMudra);
            previewArea = area;
        }
        return new Scope(() => (active, area) = previous);
    }
    private sealed class Scope(Action exit) : IDisposable { public void Dispose() => exit(); }
    internal static bool? Matches(uint input, CustomActionType type) => active && input == FullRotation
        ? type == (area ? CustomActionType.AoEDPS : CustomActionType.SingleTargetDPS) : null;
    internal static void Press(uint input, uint action)
    {
        Context();
        if (input == FullRotation && !InPvP()) pending = (action, previewArea, Environment.TickCount64);
    }
    internal static void RejectPress(uint input)
    {
        if (input == FullRotation) pending = default;
    }
    internal static void Confirm(uint action)
    {
        Context();
        if (pending.Action != action || Environment.TickCount64 - pending.At is < 0 or > 3000) return;
        choice.Confirm(pending.Area); pending = default;
    }
}
