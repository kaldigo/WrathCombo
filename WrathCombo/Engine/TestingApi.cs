using System;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WrathCombo.Engine;

internal sealed class TestingApi : IDisposable
{
    private static readonly object Gate = new();
    private static readonly DecisionTrace Trace = new();
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
                            ["Settings"] = settings().DeepClone() };
                        Trace.Start(Environment.TickCount64, Context);
                    }
                    else if (command == "stop") Trace.Stop("Stopped by user");
                    else if (command != "read") throw new InvalidOperationException("Unknown trace command.");
                    Trace.Check(Environment.TickCount64, Context);
                    return new JObject { ["Success"] = true, ["Schema"] = 1, ["Active"] = Trace.Active,
                        ["StopReason"] = Trace.StopReason, ["Dropped"] = Trace.Dropped, ["RepeatedSelections"] = Trace.RepeatedSelections,
                        ["SettingsAtRead"] = settings().DeepClone(), ["Baseline"] = baseline.DeepClone(),
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
    internal void Tick() { lock (Gate) { if (Trace.Active) Trace.Check(Environment.TickCount64, Context); } }
    public void Dispose() { api.UnregisterFunc(); lock (Gate) Trace.Stop("Plugin unloaded"); }
}
