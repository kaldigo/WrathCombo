using System;
using System.Linq;
using WrathCombo.Engine.Selection;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json.Linq;
using WrathCombo.CustomComboNS.Functions;

namespace WrathCombo.Engine;

// Session-only during testing. No stable configuration migration or automatic enablement.
internal static class ExperimentalSelection
{
    internal static bool Drg { get; private set; }
    internal static bool Machinist { get; private set; }
    internal static bool Bard { get; private set; }
    internal static bool Viper { get; private set; }
    internal static bool Reaper { get; private set; }
    internal static bool Summoner { get; private set; }
    internal static bool RedMage { get; private set; }
    internal static bool Pictomancer { get; private set; }
    internal static bool BlackMage { get; private set; }
    internal static bool Samurai { get; private set; }
    internal static bool Monk { get; private set; }
    internal static bool Ninja { get; private set; }
    internal static bool Dancer { get; private set; }
    internal static bool BlueMage { get; private set; }
    internal static bool Beastmaster { get; private set; }
    internal static bool ActiveBlueMage => Observed("ActiveBlueMage", BlueMage && !Svc.ClientState.IsPvP && (uint)Player.Job == 36);
    internal static bool ActiveBeastmaster => Observed("ActiveBeastmaster", Beastmaster && !Svc.ClientState.IsPvP && (uint)Player.Job == 43);
    internal static bool ActiveDancer => Observed("ActiveDancer", Dancer && !Svc.ClientState.IsPvP && (uint)Player.Job == 38);
    internal static bool ActiveMonk => Observed("ActiveMonk", Monk && !Svc.ClientState.IsPvP && (uint)Player.Job == 20);
    internal static bool ActiveNinja => Observed("ActiveNinja", Ninja && !Svc.ClientState.IsPvP && (uint)Player.Job == 30);
    internal static bool ActiveSamurai => Observed("ActiveSamurai", Samurai && !Svc.ClientState.IsPvP && (uint)Player.Job == 34);
    internal static bool ActiveBlackMage => Observed("ActiveBlackMage", BlackMage && !Svc.ClientState.IsPvP && (uint)Player.Job == 25);
    internal static bool ActivePictomancer => Observed("ActivePictomancer", Pictomancer && !Svc.ClientState.IsPvP && (uint)Player.Job == 42);
    internal static bool ActiveRedMage => Observed("ActiveRedMage", RedMage && !Svc.ClientState.IsPvP && (uint)Player.Job == 35);
    internal static bool ActivePreparedCaster => Observed("ActivePreparedCaster", ActiveSummoner || ActiveRedMage);
    internal static bool ActivePreparedAction => Observed("ActivePreparedAction", ActivePreparedCaster || ActiveMachinist || ActiveBlueMage || ActivePvpMachinist);
    internal static bool ActiveSummoner => Observed("ActiveSummoner", Summoner && !Svc.ClientState.IsPvP && (uint)Player.Job == 27);
    internal static bool ActiveViper => Observed("ActiveViper", Viper && !Svc.ClientState.IsPvP && (uint)Player.Job == 41);
    internal static bool ActiveReaper => Observed("ActiveReaper", Reaper && !Svc.ClientState.IsPvP && (uint)Player.Job == 39);
    internal static bool ActiveBard => Observed("ActiveBard", Bard && !Svc.ClientState.IsPvP && (uint)Player.Job == 23);
    internal static bool Paladin { get; private set; }
    internal static bool Warrior { get; private set; }
    internal static bool Gunbreaker { get; private set; }
    internal static bool PvpMachinist { get; private set; }
    internal static bool PvpScheduling { get; private set; }
    internal static bool ActivePvpScheduling => Observed("ActivePvpScheduling", PvpScheduling && PvPIntentRouting.Active);
    internal static bool ActivePvpMachinist => Observed("ActivePvpMachinist", PvpMachinist && Svc.ClientState.IsPvP && (uint)Player.Job == 31);
    internal static bool ActiveGunbreaker => Observed("ActiveGunbreaker", Gunbreaker && !Svc.ClientState.IsPvP && (uint)Player.Job == 37);
    internal static bool DarkKnight { get; private set; }
    internal static bool ActiveWarrior => Observed("ActiveWarrior", Warrior && !Svc.ClientState.IsPvP && (uint)Player.Job == 21);
    internal static bool ActiveDarkKnight => Observed("ActiveDarkKnight", DarkKnight && !Svc.ClientState.IsPvP && (uint)Player.Job == 32);
    internal static bool ActivePaladin => Observed("ActivePaladin", Paladin && !Svc.ClientState.IsPvP && (uint)Player.Job == 19);
    internal static bool ActiveMachinist => Observed("ActiveMachinist", Machinist && !Svc.ClientState.IsPvP && (uint)Player.Job == 31);
    internal static bool CompareDrg { get; private set; }
    internal static bool Sage { get; private set; }
    internal static bool Scholar { get; private set; }
    internal static bool Astrologian { get; private set; }
    internal static bool WhiteMage { get; private set; }
    internal static bool ActiveHealer => Observed("ActiveHealer", !Svc.ClientState.IsPvP && ((uint)Player.Job switch
    { 24 => WhiteMage || ManagedHealing, 28 => Scholar || ManagedHealing, 33 => Astrologian || ManagedHealing, 40 => Sage || ManagedHealing, _ => false }));
    private static bool ManagedHealing => RotationBehaviour.Preferences?.IncludeHealing == true;
    internal static bool EffectiveHealing => RotationBehaviour.Preferences?.IncludeHealing ?? IncludeHealing;
    internal static bool EffectiveHealingGcds => RotationBehaviour.Preferences?.HealingGcds ?? HealingGcds;
    internal static bool Forecasts { get; private set; }
    internal static bool IncludeHealing { get; private set; }
    internal static bool IncludeRaise { get; private set; }
    internal static bool IncludeCleanse { get; private set; }
    internal static bool PreventiveHealing { get; private set; } = true;
    internal static bool HardCastRaise { get; private set; } = true;
    internal static bool HealingGcds { get; private set; }
    internal static bool PartyMouseover { get; private set; } = true;
    internal static bool WorldMouseover { get; private set; } = true;
    internal static bool Focus { get; private set; }
    internal static bool GroundTargets { get; private set; }
    internal static bool SpaceMitigation { get; private set; }
    internal static bool Fallback { get; private set; }
    internal static bool AutomaticAllies { get; private set; } = true;
    internal static bool AutomaticEnemies { get; private set; }
    internal static bool EnemyTargeting { get; private set; }
    internal static bool PvpTargeting { get; private set; }
    internal static bool EnemyMouseover { get; private set; }
    internal static bool EnemyFocus { get; private set; }
    internal static bool AcquireEnemy { get; private set; }
    internal static bool OverridesInAutomation { get; private set; }
    internal static string[] AllyOverrideOrder { get; private set; } = [.. TargetOrder.AllySources];
    internal static string[] EnemyOverrideOrder { get; private set; } = [.. TargetOrder.EnemySources];
    internal static bool SwitchEnemyTarget { get; private set; }
    internal static bool SwitchAllyTarget { get; private set; }
    internal static bool ActiveTargets => Observed("ActiveTargets", Svc.ClientState.IsPvP ? PvpTargeting : EnemyTargeting);
    internal static bool ActiveDrg => Observed("ActiveDrg", Drg && !Svc.ClientState.IsPvP && (uint)Player.Job == 22);
    internal static bool ActiveSage => Observed("ActiveSage", (Sage || ManagedHealing) && !Svc.ClientState.IsPvP && (uint)Player.Job == 40);
    private static bool Observed(string feature, bool active)
    {
        AccordDiagnostics.DiagnosticLog.Event("Feature.Evaluated", () => new { Feature = feature, Active = active, Job = (uint)Player.Job, PvP = Svc.ClientState.IsPvP });
        return active;
    }
    internal static JObject Read() => new() { ["Drg"] = Drg, ["Machinist"] = Machinist, ["Paladin"] = Paladin, ["CompareDrg"] = CompareDrg, ["Sage"] = Sage,
        ["Scholar"] = Scholar, ["Astrologian"] = Astrologian, ["WhiteMage"] = WhiteMage, ["Forecasts"] = Forecasts, ["Bard"] = Bard, ["Dancer"] = Dancer, ["BlueMage"] = BlueMage, ["Beastmaster"] = Beastmaster,
        ["Viper"] = Viper, ["Reaper"] = Reaper, ["Summoner"] = Summoner, ["RedMage"] = RedMage, ["Pictomancer"] = Pictomancer, ["BlackMage"] = BlackMage, ["Samurai"] = Samurai, ["Monk"] = Monk, ["Ninja"] = Ninja,
        ["Warrior"] = Warrior, ["DarkKnight"] = DarkKnight, ["Gunbreaker"] = Gunbreaker, ["PvpMachinist"] = PvpMachinist, ["PvpScheduling"] = PvpScheduling,
        ["IncludeHealing"] = IncludeHealing, ["IncludeRaise"] = IncludeRaise, ["HardCastRaise"] = HardCastRaise,
        ["IncludeCleanse"] = IncludeCleanse, ["PreventiveHealing"] = PreventiveHealing,
        ["HealingGcds"] = HealingGcds, ["PartyMouseover"] = PartyMouseover, ["WorldMouseover"] = WorldMouseover,
        ["Focus"] = Focus, ["GroundTargets"] = GroundTargets, ["SpaceMitigation"] = SpaceMitigation, ["Fallback"] = Fallback, ["AutomaticAllies"] = AutomaticAllies, ["AutomaticEnemies"] = AutomaticEnemies,
        ["EnemyTargeting"] = EnemyTargeting, ["AcquireEnemy"] = AcquireEnemy, ["OverridesInAutomation"] = OverridesInAutomation,
        ["EnemyMouseover"] = EnemyMouseover, ["EnemyFocus"] = EnemyFocus, ["PvpTargeting"] = PvpTargeting,
        ["AllyOverrideOrder"] = new JArray(AllyOverrideOrder), ["EnemyOverrideOrder"] = new JArray(EnemyOverrideOrder),
        ["SwitchEnemyTarget"] = SwitchEnemyTarget, ["SwitchAllyTarget"] = SwitchAllyTarget };
    internal static void Apply(JObject request)
    {
        if (CustomComboFunctions.InCombat() || AutomationOwnership.Busy || SelectionRuntime.Commitment is not null)
            throw new InvalidOperationException("Change testing providers outside combat after the current preparation finishes.");
        var current = Read();
        foreach (var property in request.Properties())
        {
            if (property.Name is "AllyOverrideOrder" or "EnemyOverrideOrder")
            {
                if (property.Value is not JArray array || array.Any(item => item.Type != JTokenType.String) ||
                    !TargetOrder.Valid(array.Values<string>().Select(item => item!).ToArray(), property.Name == "AllyOverrideOrder"))
                    throw new InvalidOperationException("Target order must contain each supported source exactly once.");
                continue;
            }
            if (!current.ContainsKey(property.Name) || property.Value.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Unknown testing setting or non-boolean value: " + property.Name);
        }
        foreach (var property in request.Properties()) current[property.Name] = property.Value;
        Drg = current.Value<bool>("Drg"); Sage = current.Value<bool>("Sage"); Forecasts = current.Value<bool>("Forecasts");
        Machinist = current.Value<bool>("Machinist");
        Bard = current.Value<bool>("Bard");
        Viper = current.Value<bool>("Viper"); Reaper = current.Value<bool>("Reaper");
        Summoner = current.Value<bool>("Summoner");
        RedMage = current.Value<bool>("RedMage");
        Pictomancer = current.Value<bool>("Pictomancer");
        BlackMage = current.Value<bool>("BlackMage");
        Samurai = current.Value<bool>("Samurai");
        Monk = current.Value<bool>("Monk"); Ninja = current.Value<bool>("Ninja");
        Dancer = current.Value<bool>("Dancer");
        BlueMage = current.Value<bool>("BlueMage"); Beastmaster = current.Value<bool>("Beastmaster");
        Paladin = current.Value<bool>("Paladin");
        Warrior = current.Value<bool>("Warrior"); DarkKnight = current.Value<bool>("DarkKnight");
        Gunbreaker = current.Value<bool>("Gunbreaker");
        PvpMachinist = current.Value<bool>("PvpMachinist");
        PvpScheduling = current.Value<bool>("PvpScheduling");
        Scholar = current.Value<bool>("Scholar"); Astrologian = current.Value<bool>("Astrologian"); WhiteMage = current.Value<bool>("WhiteMage");
        CompareDrg = current.Value<bool>("CompareDrg");
        IncludeHealing = current.Value<bool>("IncludeHealing"); PartyMouseover = current.Value<bool>("PartyMouseover");
        IncludeRaise = current.Value<bool>("IncludeRaise"); HardCastRaise = current.Value<bool>("HardCastRaise");
        IncludeCleanse = current.Value<bool>("IncludeCleanse"); PreventiveHealing = current.Value<bool>("PreventiveHealing");
        HealingGcds = current.Value<bool>("HealingGcds");
        WorldMouseover = current.Value<bool>("WorldMouseover"); Focus = current.Value<bool>("Focus");
        GroundTargets = current.Value<bool>("GroundTargets");
        SpaceMitigation = current.Value<bool>("SpaceMitigation");
        Fallback = current.Value<bool>("Fallback"); AutomaticAllies = current.Value<bool>("AutomaticAllies");
        AutomaticEnemies = current.Value<bool>("AutomaticEnemies");
        EnemyTargeting = current.Value<bool>("EnemyTargeting"); AcquireEnemy = current.Value<bool>("AcquireEnemy");
        PvpTargeting = current.Value<bool>("PvpTargeting");
        EnemyMouseover = current.Value<bool>("EnemyMouseover"); EnemyFocus = current.Value<bool>("EnemyFocus");
        OverridesInAutomation = current.Value<bool>("OverridesInAutomation");
        AllyOverrideOrder = current["AllyOverrideOrder"]!.Values<string>().Select(item => item!).ToArray();
        EnemyOverrideOrder = current["EnemyOverrideOrder"]!.Values<string>().Select(item => item!).ToArray();
        SwitchEnemyTarget = current.Value<bool>("SwitchEnemyTarget"); SwitchAllyTarget = current.Value<bool>("SwitchAllyTarget");
        SelectionRuntime.Reset();
        BossForecastProvider.Reset();
    }
}
