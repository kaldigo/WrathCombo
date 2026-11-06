using System.IO;
using System;
using System.Collections.Generic;
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
                var root = DiagnosticLog.Root(Svc.PluginInterface.GetPluginConfigDirectory());
                string? duty = input.Value<string>("Duty");
                if (duty != null && !Guid.TryParseExact(duty, "N", out _)) throw new InvalidOperationException("Invalid duty capture identifier.");
                DiagnosticLog.Start(duty == null ? root : Path.Combine(root, "LastDuty", duty), id, component, duty != null);
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
            return JsonConvert.SerializeObject(new { Success = true, Schema = 1, DutyCaptureVersion = 1, Status = DiagnosticLog.Status() });
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
    private static object World()
    {
        var conditions = new List<string>();
        foreach (var flag in Svc.Condition.AsReadOnlySet()) conditions.Add(flag.ToString());
        var party = new List<object>();
        foreach (var member in Svc.Party) party.Add(new { member.ObjectId, Job = member.ClassJob.RowId });
        var actors = new List<object>();
        foreach (var candidate in Svc.Objects)
        {
            if (candidate is not IBattleChara actor || Player.Object != null && System.Numerics.Vector3.Distance(actor.Position, Player.Position) > 60) continue;
            var statuses = new List<object>();
            foreach (var status in actor.StatusList) statuses.Add(new { status.StatusId, status.SourceId, status.RemainingTime, status.Param });
            actors.Add(new { Id = actor.GameObjectId, actor.DataId, Job = actor.ClassJob.RowId, actor.Level, actor.CurrentHp, actor.MaxHp, actor.CurrentMp, actor.MaxMp,
                actor.IsDead, actor.IsTargetable, actor.Position, actor.Rotation, actor.HitboxRadius, actor.TargetObjectId, actor.IsCasting, actor.CastActionId, actor.TotalCastTime, actor.CurrentCastTime,
                Statuses = statuses });
        }
        return new { LoggedIn = Player.Available, PlayerId = Player.Object?.GameObjectId, PlayerEntityId = Player.Object?.EntityId,
            Job = (int)Player.Job, Player.Level, Territory = Svc.ClientState.TerritoryType, PvP = Svc.ClientState.IsPvP, Conditions = conditions,
            Selected = Svc.Targets.Target?.GameObjectId, Focus = Svc.Targets.FocusTarget?.GameObjectId, Soft = Svc.Targets.SoftTarget?.GameObjectId,
            Mouseover = Svc.Targets.MouseOverTarget?.GameObjectId, Party = party, Gauge = Gauge(), Cooldowns = Cooldowns(), Actors = actors };
    }
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
