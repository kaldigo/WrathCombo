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
    internal static bool CompareDrg { get; private set; }
    internal static bool Sage { get; private set; }
    internal static bool Forecasts { get; private set; }
    internal static bool IncludeHealing { get; private set; }
    internal static bool HealingGcds { get; private set; }
    internal static bool PartyMouseover { get; private set; } = true;
    internal static bool WorldMouseover { get; private set; } = true;
    internal static bool Focus { get; private set; }
    internal static bool Fallback { get; private set; }
    internal static bool AutomaticAllies { get; private set; } = true;
    internal static bool AutomaticEnemies { get; private set; }
    internal static bool ActiveDrg => Drg && !Svc.ClientState.IsPvP && (uint)Player.Job == 22;
    internal static bool ActiveSage => Sage && !Svc.ClientState.IsPvP && (uint)Player.Job == 40;
    internal static JObject Read() => new() { ["Drg"] = Drg, ["CompareDrg"] = CompareDrg, ["Sage"] = Sage, ["Forecasts"] = Forecasts,
        ["IncludeHealing"] = IncludeHealing, ["HealingGcds"] = HealingGcds, ["PartyMouseover"] = PartyMouseover, ["WorldMouseover"] = WorldMouseover,
        ["Focus"] = Focus, ["Fallback"] = Fallback, ["AutomaticAllies"] = AutomaticAllies, ["AutomaticEnemies"] = AutomaticEnemies };
    internal static void Apply(JObject request)
    {
        if (CustomComboFunctions.InCombat() || SelectionRuntime.Commitment is not null)
            throw new InvalidOperationException("Change testing providers outside combat after the current preparation finishes.");
        var current = Read();
        foreach (var property in request.Properties())
            if (!current.ContainsKey(property.Name) || property.Value.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Unknown testing setting or non-boolean value: " + property.Name);
        foreach (var property in request.Properties()) current[property.Name] = property.Value;
        Drg = current.Value<bool>("Drg"); Sage = current.Value<bool>("Sage"); Forecasts = current.Value<bool>("Forecasts");
        CompareDrg = current.Value<bool>("CompareDrg");
        IncludeHealing = current.Value<bool>("IncludeHealing"); PartyMouseover = current.Value<bool>("PartyMouseover");
        HealingGcds = current.Value<bool>("HealingGcds");
        WorldMouseover = current.Value<bool>("WorldMouseover"); Focus = current.Value<bool>("Focus");
        Fallback = current.Value<bool>("Fallback"); AutomaticAllies = current.Value<bool>("AutomaticAllies");
        AutomaticEnemies = current.Value<bool>("AutomaticEnemies");
        SelectionRuntime.Reset();
        BossForecastProvider.Reset();
    }
}
