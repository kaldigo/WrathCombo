using System;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using WrathCombo.Engine.Selection;

namespace WrathCombo.Engine;

internal static class BossForecastProvider
{
    internal static EncounterForecast? Current { get; private set; }
    internal static long Generation { get; private set; }
    private static long nextPoll;
    private static string context = "";
    internal static void Reset() { Current = null; context = ""; nextPoll = 0; Generation++; }
    private static T? Read<T>(string endpoint) where T : struct
    {
        try { return Svc.PluginInterface.GetIpcSubscriber<T>("BossMod." + endpoint).InvokeFunc(); }
        catch { return null; }
    }
    private static float? Seconds(string endpoint) => EncounterForecast.Normalize(Read<float>(endpoint));
    internal static void Tick()
    {
        long now = Environment.TickCount64;
        if (now < nextPoll) return;
        nextPoll = now + 250;
        string module = "";
        if (Player.Available && !Player.IsDead && !Svc.ClientState.IsPvP && Read<bool>("HasActiveModule") == true)
            try { module = Svc.PluginInterface.GetIpcSubscriber<string>("BossMod.ActiveModuleName").InvokeFunc() ?? ""; } catch { }
        string currentContext = $"{Player.Object?.GameObjectId}:{Svc.ClientState.TerritoryType}:{Player.IsDead}:{CustomComboNS.Functions.CustomComboFunctions.InCombat()}:{module}";
        if (context != currentContext) { context = currentContext; Generation++; Current = null; }
        if (module.Length == 0) { Current = null; return; }
        Current = new(now, Generation, module,
            EncounterForecast.Earliest(Seconds("Timeline.NextRaidwideIn"), Seconds("Hints.NextRaidwideDamageIn")),
            EncounterForecast.Earliest(Seconds("Timeline.NextTankbusterIn"), Seconds("Hints.NextTankbusterDamageIn")),
            Seconds("Timeline.NextDowntimeIn"), Seconds("Timeline.NextKnockbackIn"));
    }
}
