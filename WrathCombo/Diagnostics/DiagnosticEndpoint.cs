using System;
using System.Linq;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AccordDiagnostics;

internal sealed class DiagnosticEndpoint : IDisposable
{
    private readonly ICallGateProvider<string, string>? api;
    private readonly string component;
    private readonly Func<object> state, settings;
    private long heartbeat, nextState, nextSettings, nextCheckpoint;
    private string previousSettings = "";
    internal DiagnosticEndpoint(string component, string? endpoint, Func<object> state, Func<object> settings)
    {
        this.component = component; this.state = state; this.settings = settings;
        if (endpoint != null) { api = Svc.PluginInterface.GetIpcProvider<string, string>(endpoint); api.RegisterFunc(Handle); }
        Svc.Framework.Update += Tick;
    }
    internal string Handle(string request)
    {
        try
        {
            if (!Svc.Framework.IsInFrameworkUpdateThread) throw new InvalidOperationException("Use the framework thread.");
            var input = JObject.Parse(request);
            var command = input.Value<string>("Command");
            string id = input.Value<string>("Session") ?? "";
            var status = JObject.FromObject(DiagnosticLog.Status());
            if (command == "start")
            {
                DiagnosticLog.Start(DiagnosticLog.Root(Svc.PluginInterface.GetPluginConfigDirectory()), id, component);
                previousSettings = ""; nextState = nextSettings = nextCheckpoint = 0;
                heartbeat = Environment.TickCount64;
                DiagnosticLog.Event("Settings.Initial", settings);
            }
            else if (command is "heartbeat" or "stop")
            {
                if (status.Value<string>("Session") != id) throw new InvalidOperationException("Session does not match.");
                heartbeat = Environment.TickCount64;
                if (command == "stop") DiagnosticLog.Stop("User stopped diagnostics");
            }
            else if (command != "status") throw new InvalidOperationException("Unknown diagnostic command.");
            return JsonConvert.SerializeObject(new { Success = true, Schema = 1, Status = DiagnosticLog.Status() });
        }
        catch (Exception ex) { return JsonConvert.SerializeObject(new { Success = false, Error = ex.Message, Status = DiagnosticLog.Status() }); }
    }
    private void Tick(IFramework _)
    {
        if (!DiagnosticLog.Active) return;
        long now = Environment.TickCount64;
        if (now - heartbeat > 15000) { DiagnosticLog.Stop("Controller heartbeat expired"); return; }
        if (now >= nextState)
        {
            nextState = now + 500;
            DiagnosticLog.Event("State", state);
            DiagnosticLog.Event("World", World);
        }
        if (now >= nextSettings)
        {
            nextSettings = now + 2000;
            DiagnosticLog.Event("Settings.Check", () => {
                string serialized = JsonConvert.SerializeObject(settings(), new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                bool changed = serialized != previousSettings; previousSettings = serialized;
                return new { Changed = changed, Values = changed ? JToken.Parse(serialized) : null };
            });
        }
        if (now >= nextCheckpoint) { nextCheckpoint = now + 10000; DiagnosticLog.Checkpoint(); }
    }
    private static object World() => new {
        LoggedIn = Player.Available, PlayerId = Player.Object?.GameObjectId, PlayerEntityId = Player.Object?.EntityId, Job = (int)Player.Job, Player.Level, Territory = Svc.ClientState.TerritoryType, PvP = Svc.ClientState.IsPvP,
        Conditions = Svc.Condition.AsReadOnlySet().Select(x => x.ToString()).ToArray(),
        Selected = Svc.Targets.Target?.GameObjectId, Focus = Svc.Targets.FocusTarget?.GameObjectId,
        Soft = Svc.Targets.SoftTarget?.GameObjectId, Mouseover = Svc.Targets.MouseOverTarget?.GameObjectId,
        Party = Svc.Party.Select(x => new { x.ObjectId, x.ClassJob.RowId }).ToArray(),
        Gauge = Gauge(), Cooldowns = Cooldowns(),
        Actors = Svc.Objects.OfType<IBattleChara>().Where(x => Player.Object == null || System.Numerics.Vector3.Distance(x.Position, Player.Position) <= 60)
            .Select(x => new { Id = x.GameObjectId, x.DataId, Job = x.ClassJob.RowId, x.Level, x.CurrentHp, x.MaxHp, x.CurrentMp, x.MaxMp,
                x.IsDead, x.IsTargetable, x.Position, x.Rotation, x.HitboxRadius, x.TargetObjectId, x.IsCasting, x.CastActionId, x.TotalCastTime, x.CurrentCastTime,
                Statuses = x.StatusList.Select(s => new { s.StatusId, s.SourceId, s.RemainingTime, s.Param }).ToArray() }).ToArray()
    };
    private static object? Gauge()
    {
        if (!Player.Available) return null;
        try
        {
            var type = typeof(IJobGauges).Assembly.GetType("Dalamud.Game.ClientState.JobGauge.Types." + Player.Job + "Gauge");
            if (type == null) return new { Available = false, Reason = "No gauge type for this job" };
            return typeof(IJobGauges).GetMethod("Get")!.MakeGenericMethod(type).Invoke(Svc.Gauges, null);
        }
        catch (Exception ex) { return new { Available = false, Error = ex.Message }; }
    }
    private static unsafe object? Cooldowns()
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (manager == null) return null;
        var result = new JArray();
        for (int i = 0; i < 80; i++)
        {
            var recast = manager->GetRecastGroupDetail(i);
            if (recast != null) result.Add(new JObject { ["Group"] = i, ["Total"] = recast->Total, ["Elapsed"] = recast->Elapsed });
        }
        return new { Groups = result, Queued = manager->ActionQueued, QueueAction = manager->QueuedActionId, QueueTarget = manager->QueuedTargetId.Id,
            AnimationLock = manager->AnimationLock, Cast = manager->CastActionId, CastElapsed = manager->CastTimeElapsed, CastTotal = manager->CastTimeTotal };
    }
    public void Dispose() { Svc.Framework.Update -= Tick; DiagnosticLog.Stop("Plugin unloaded"); api?.UnregisterFunc(); }
}
