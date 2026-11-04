using System;
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
    internal static bool Paladin { get; private set; }
    internal static bool Warrior { get; private set; }
    internal static bool Gunbreaker { get; private set; }
    internal static bool ActiveGunbreaker => Gunbreaker && !Svc.ClientState.IsPvP && (uint)Player.Job == 37;
    internal static bool DarkKnight { get; private set; }
    internal static bool ActiveWarrior => Warrior && !Svc.ClientState.IsPvP && (uint)Player.Job == 21;
    internal static bool ActiveDarkKnight => DarkKnight && !Svc.ClientState.IsPvP && (uint)Player.Job == 32;
    internal static bool ActivePaladin => Paladin && !Svc.ClientState.IsPvP && (uint)Player.Job == 19;
    internal static bool ActiveMachinist => Machinist && !Svc.ClientState.IsPvP && (uint)Player.Job == 31;
    internal static bool CompareDrg { get; private set; }
    internal static bool Sage { get; private set; }
    internal static bool Scholar { get; private set; }
    internal static bool Astrologian { get; private set; }
    internal static bool WhiteMage { get; private set; }
    internal static bool ActiveHealer => !Svc.ClientState.IsPvP && ((uint)Player.Job switch
    { 24 => WhiteMage, 28 => Scholar, 33 => Astrologian, 40 => Sage, _ => false });
    internal static bool Forecasts { get; private set; }
    internal static bool IncludeHealing { get; private set; }
    internal static bool HealingGcds { get; private set; }
    internal static bool PartyMouseover { get; private set; } = true;
    internal static bool WorldMouseover { get; private set; } = true;
    internal static bool Focus { get; private set; }
    internal static bool Fallback { get; private set; }
    internal static bool AutomaticAllies { get; private set; } = true;
    internal static bool AutomaticEnemies { get; private set; }
    internal static bool EnemyTargeting { get; private set; }
    internal static bool EnemyMouseover { get; private set; }
    internal static bool EnemyFocus { get; private set; }
    internal static bool AcquireEnemy { get; private set; }
    internal static bool OverridesInAutomation { get; private set; }
    internal static bool ActiveTargets => EnemyTargeting && !Svc.ClientState.IsPvP;
    internal static bool ActiveDrg => Drg && !Svc.ClientState.IsPvP && (uint)Player.Job == 22;
    internal static bool ActiveSage => Sage && !Svc.ClientState.IsPvP && (uint)Player.Job == 40;
    internal static JObject Read() => new() { ["Drg"] = Drg, ["Machinist"] = Machinist, ["Paladin"] = Paladin, ["CompareDrg"] = CompareDrg, ["Sage"] = Sage,
        ["Scholar"] = Scholar, ["Astrologian"] = Astrologian, ["WhiteMage"] = WhiteMage, ["Forecasts"] = Forecasts,
        ["Warrior"] = Warrior, ["DarkKnight"] = DarkKnight, ["Gunbreaker"] = Gunbreaker,
        ["IncludeHealing"] = IncludeHealing, ["HealingGcds"] = HealingGcds, ["PartyMouseover"] = PartyMouseover, ["WorldMouseover"] = WorldMouseover,
        ["Focus"] = Focus, ["Fallback"] = Fallback, ["AutomaticAllies"] = AutomaticAllies, ["AutomaticEnemies"] = AutomaticEnemies,
        ["EnemyTargeting"] = EnemyTargeting, ["AcquireEnemy"] = AcquireEnemy, ["OverridesInAutomation"] = OverridesInAutomation,
        ["EnemyMouseover"] = EnemyMouseover, ["EnemyFocus"] = EnemyFocus };
    internal static void Apply(JObject request)
    {
        if (CustomComboFunctions.InCombat() || AutomationOwnership.Busy || SelectionRuntime.Commitment is not null)
            throw new InvalidOperationException("Change testing providers outside combat after the current preparation finishes.");
        var current = Read();
        foreach (var property in request.Properties())
            if (!current.ContainsKey(property.Name) || property.Value.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Unknown testing setting or non-boolean value: " + property.Name);
        foreach (var property in request.Properties()) current[property.Name] = property.Value;
        Drg = current.Value<bool>("Drg"); Sage = current.Value<bool>("Sage"); Forecasts = current.Value<bool>("Forecasts");
        Machinist = current.Value<bool>("Machinist");
        Paladin = current.Value<bool>("Paladin");
        Warrior = current.Value<bool>("Warrior"); DarkKnight = current.Value<bool>("DarkKnight");
        Gunbreaker = current.Value<bool>("Gunbreaker");
        Scholar = current.Value<bool>("Scholar"); Astrologian = current.Value<bool>("Astrologian"); WhiteMage = current.Value<bool>("WhiteMage");
        CompareDrg = current.Value<bool>("CompareDrg");
        IncludeHealing = current.Value<bool>("IncludeHealing"); PartyMouseover = current.Value<bool>("PartyMouseover");
        HealingGcds = current.Value<bool>("HealingGcds");
        WorldMouseover = current.Value<bool>("WorldMouseover"); Focus = current.Value<bool>("Focus");
        Fallback = current.Value<bool>("Fallback"); AutomaticAllies = current.Value<bool>("AutomaticAllies");
        AutomaticEnemies = current.Value<bool>("AutomaticEnemies");
        EnemyTargeting = current.Value<bool>("EnemyTargeting"); AcquireEnemy = current.Value<bool>("AcquireEnemy");
        EnemyMouseover = current.Value<bool>("EnemyMouseover"); EnemyFocus = current.Value<bool>("EnemyFocus");
        OverridesInAutomation = current.Value<bool>("OverridesInAutomation");
        SelectionRuntime.Reset();
        BossForecastProvider.Reset();
    }
}
