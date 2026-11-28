using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using WrathCombo.Engine.Selection;

namespace WrathCombo.Engine;

internal static class BossForecastProvider
{
    internal static EncounterForecast? Current { get; private set; }
    internal static long Generation { get; private set; }
    internal static float? Downtime => ExperimentalSelection.Forecasts && Current is { } forecast
        ? forecast.Remaining(forecast.Downtime, Environment.TickCount64, Generation) : null;
    internal static float? Raidwide => ExperimentalSelection.Forecasts && Current is { } forecast
        ? forecast.Remaining(forecast.Raidwide, Environment.TickCount64, Generation) : null;
    internal static float? Tankbuster => ExperimentalSelection.Forecasts && Current is { } forecast
        ? forecast.Remaining(forecast.Tankbuster, Environment.TickCount64, Generation) : null;
    internal static EncounterForecast? Fresh => ExperimentalSelection.Forecasts && Current is { } forecast &&
        forecast.Generation == Generation && Environment.TickCount64 - forecast.ObservedAt is >= 0 and <= 1500 ? forecast : null;
    internal static float? DowntimeEnd => ExperimentalSelection.Forecasts && Current is { } forecast
        ? forecast.Remaining(forecast.DowntimeEnd, Environment.TickCount64, Generation) : null;
    internal static float? MaxCastTime => ExperimentalSelection.Forecasts && Current is { } forecast
        ? forecast.RemainingLimit(forecast.MaxCastTime, Environment.TickCount64, Generation) : null;
    private static long nextPoll;
    private static readonly Dictionary<string, long> unavailable = new();
    private static class Readers<T> where T : struct
    {
        internal static readonly Dictionary<string, Func<T>> Calls = new();
    }
    private static string context = "";
    internal static void Reset() { Current = null; context = ""; nextPoll = 0; Generation++; }
    private static T? Read<T>(string endpoint) where T : struct
    {
        if (unavailable.TryGetValue(endpoint, out long retry) && Environment.TickCount64 < retry) return null;
        try
        {
            if (!Readers<T>.Calls.TryGetValue(endpoint, out var call))
                Readers<T>.Calls[endpoint] = call = Svc.PluginInterface.GetIpcSubscriber<T>("BossMod." + endpoint).InvokeFunc;
            var value = AccordDiagnostics.IntegrationTrace.Call("BossMod." + endpoint, call);
            unavailable.Remove(endpoint);
            AccordDiagnostics.DiagnosticLog.Event("Forecast.Read", () => new { Endpoint = endpoint, Available = true, Value = value }, "Forecast.Read:" + endpoint);
            return value;
        }
        catch (Exception ex)
        {
            unavailable[endpoint] = Environment.TickCount64 + 5000;
            AccordDiagnostics.DiagnosticLog.Event("Forecast.Read", () => new { Endpoint = endpoint, Available = false, Error = ex.Message }, "Forecast.Read:" + endpoint);
            return null;
        }
    }
    private static float? Seconds(string endpoint) => EncounterForecast.Normalize(Read<float>(endpoint));
    internal static bool? DashSafe(Vector3 from, Vector3 to) => CheckDash("Hints.IsDashSafe",
        () => Svc.PluginInterface.GetIpcSubscriber<Vector3, Vector3, bool>("BossMod.Hints.IsDashSafe").InvokeFunc(from, to), new { From = from, To = to });
    internal static bool? FixedDashSafe(float signedDistance) => CheckDash("Hints.IsFixedDashSafe",
        () => Svc.PluginInterface.GetIpcSubscriber<float, bool, bool>("BossMod.Hints.IsFixedDashSafe").InvokeFunc(Math.Abs(signedDistance), signedDistance < 0),
        new { Range = Math.Abs(signedDistance), Backwards = signedDistance < 0 });
    private static bool? CheckDash(string endpoint, Func<bool> invoke, object input)
    {
        if (Fresh == null) return null;
        if (unavailable.TryGetValue(endpoint, out long retry) && Environment.TickCount64 < retry) return null;
        try { return AccordDiagnostics.IntegrationTrace.Call("BossMod." + endpoint, invoke, input: input); }
        catch { unavailable[endpoint] = Environment.TickCount64 + 5000; return null; }
    }
    internal static void Tick()
    {
        long now = Environment.TickCount64;
        if (now < nextPoll) return;
        using var timing = AccordDiagnostics.PerformanceTimings.Measure(AccordDiagnostics.TimedPath.Forecast);
        nextPoll = now + 250;
        string module = "";
        if (Player.Available && !Player.IsDead && !Svc.ClientState.IsPvP && Read<bool>("HasActiveModule") == true)
            try { module = AccordDiagnostics.IntegrationTrace.Call("BossMod.ActiveModuleName",
                Svc.PluginInterface.GetIpcSubscriber<string>("BossMod.ActiveModuleName").InvokeFunc) ?? ""; } catch { }
        string currentContext = $"{Player.Object?.GameObjectId}:{Svc.ClientState.TerritoryType}:{Player.IsDead}:{CustomComboNS.Functions.CustomComboFunctions.InCombat()}:{module}";
        if (context != currentContext)
        {
            AccordDiagnostics.DiagnosticLog.Event("Integration.Context", () => new { Previous = context, Current = currentContext, Module = module });
            context = currentContext; Generation++; Current = null;
        }
        if (module.Length == 0) { Current = null; return; }
        float? damage = Seconds("Hints.NextDamageIn");
        int damageType = Read<int>("Hints.NextDamageType") ?? 0;
        Current = new(now, Generation, module,
            EncounterForecast.Earliest(EncounterForecast.Earliest(Seconds("Timeline.NextRaidwideIn"), Seconds("Hints.NextRaidwideDamageIn")), damageType == 2 ? damage : null),
            EncounterForecast.Earliest(EncounterForecast.Earliest(Seconds("Timeline.NextTankbusterIn"), Seconds("Hints.NextTankbusterDamageIn")), damageType == 1 ? damage : null),
            Seconds("Timeline.NextDowntimeIn"), Seconds("Timeline.NextKnockbackIn"), Seconds("Timeline.NextDowntimeEndIn"), Seconds("Hints.MaxCastTime"));
        Current = Current with { Damage = damage, DamageType = damageType, DamagePlayers = Read<ulong>("Hints.PredictedDamagePlayers") ?? 0,
            ForceCancel = Read<bool>("Hints.ForceCancelCastMechanic"), ForceCancelAI = Read<bool>("Hints.ForceCancelCastMechanicAI"),
            Moving = Read<bool>("Movement.IsMoving"), Vulnerable = Seconds("Timeline.NextVulnerableIn"), VulnerableEnd = Seconds("Timeline.NextVulnerableEndIn"),
            SpecialModeIn = Seconds("Hints.SpecialModeIn"), SpecialMode = Read<int>("Hints.SpecialModeType") ?? 0 };
    }
}
