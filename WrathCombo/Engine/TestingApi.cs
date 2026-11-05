using System;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WrathCombo.Engine;

internal sealed class TestingApi : IDisposable
{
    private readonly AutomationOwnership ownership = new();
    private static readonly object Gate = new();
    private static readonly DecisionTrace Trace = new();
    private static readonly System.Collections.Generic.Queue<JObject> Decisions = new();
    private static long droppedDecisions;
    private readonly ICallGateProvider<string, string> api = Svc.PluginInterface.GetIpcProvider<string, string>("WrathCombo.Testing.Trace.v1");
    private JObject baseline = new();
    private static string Context => $"{Player.Object?.GameObjectId}:{Player.Job}:{Player.Level}:{Svc.ClientState.TerritoryType}:{Svc.ClientState.IsPvP}";
    internal TestingApi(Func<JObject> settings)
    {
        api.RegisterFunc(command =>
        {
            try
            {
                if (!Svc.Framework.IsInFrameworkUpdateThread) throw new InvalidOperationException("Use the framework thread.");
                lock (Gate)
                {
                    if (command == "start")
                    {
                        if (!Player.Available) throw new InvalidOperationException("Log in before capturing.");
                        baseline = new JObject { ["Job"] = (int)Player.Job, ["Level"] = Player.Level,
                            ["Territory"] = Svc.ClientState.TerritoryType, ["PvP"] = Svc.ClientState.IsPvP,
                            ["StartedUtc"] = DateTime.UtcNow, ["EngineBuild"] = typeof(TestingApi).Assembly.ManifestModule.ModuleVersionId.ToString(),
                            ["Settings"] = settings().DeepClone(), ["Experimental"] = ExperimentalSelection.Read(),
                            ["CaptureId"] = Guid.NewGuid().ToString(), ["StartedMonotonicMs"] = Environment.TickCount64,
                            ["EngineVersion"] = typeof(TestingApi).Assembly.GetName().Version?.ToString(),
                            ["Encounter"] = BossForecastProvider.Current is { } initialForecast ? JObject.FromObject(initialForecast) : null };
                        Trace.Start(Environment.TickCount64, Context);
                        Decisions.Clear(); droppedDecisions = 0;
                    }
                    else if (command == "stop") Trace.Stop("Stopped by user");
                    else if (command.StartsWith("configure:")) ExperimentalSelection.Apply(JObject.Parse(command[10..]));
                    else if (command != "read") throw new InvalidOperationException("Unknown trace command.");
                    Trace.Check(Environment.TickCount64, Context);
                    return new JObject { ["Success"] = true, ["Schema"] = 1, ["Active"] = Trace.Active,
                        ["StopReason"] = Trace.StopReason, ["Dropped"] = Trace.Dropped, ["RepeatedSelections"] = Trace.RepeatedSelections,
                        ["SettingsAtRead"] = settings().DeepClone(), ["Baseline"] = baseline.DeepClone(),
                        ["Experimental"] = ExperimentalSelection.Read(),
                        ["Encounter"] = BossForecastProvider.Current is { } forecast ? JObject.FromObject(forecast) : null,
                        ["DecisionSamples"] = new JArray(Decisions), ["DroppedDecisionSamples"] = droppedDecisions,
                        ["Entries"] = JArray.FromObject(Trace.Snapshot()) }.ToString(Formatting.None);
                }
            }
            catch (Exception ex) { return new JObject { ["Success"] = false, ["Error"] = ex.Message }.ToString(); }
        });
    }
    internal static void Observe(string kind, uint input, uint output, ulong? recipient = null)
    {
        // Diagnostics must never interrupt an action hook, even during teardown.
        try { lock (Gate) { if (Trace.Active) Trace.Add(Environment.TickCount64, Context, kind, input, output, recipient); } }
        catch { lock (Gate) Trace.Stop("Observation failed"); }
    }
    internal static void RecordDecision(string provider, object input, object? output)
    {
        try
        {
            lock (Gate)
            {
                if (!Trace.Active || !Trace.Check(Environment.TickCount64, Context)) return;
                if (Decisions.Count == 128) { Decisions.Dequeue(); droppedDecisions++; }
                Decisions.Enqueue(new JObject { ["Provider"] = provider, ["Input"] = JToken.FromObject(input),
                    ["Output"] = output is null ? JValue.CreateNull() : JToken.FromObject(output) });
            }
        }
        catch { /* Observation must not affect selection. */ }
    }
    internal void Tick()
    {
        if (ExperimentalSelection.Forecasts) BossForecastProvider.Tick();
        SelectionRuntime.Tick();
        lock (Gate) { if (Trace.Active) Trace.Check(Environment.TickCount64, Context); }
    }
    public void Dispose() { ownership.Dispose(); api.UnregisterFunc(); lock (Gate) Trace.Stop("Plugin unloaded"); }
}
