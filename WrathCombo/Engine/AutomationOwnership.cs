using System;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json.Linq;
using RotationInterop;
using WrathCombo.AutoRotation;
using WrathCombo.Core;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Services.ActionRequestIPC;
using WrathCombo.Services;

namespace WrathCombo.Engine;

/// <summary>Optional session authority, independent of saved rotation preferences.</summary>
internal sealed class AutomationOwnership : IDisposable
{
    private static readonly AutomationLease Lease = new();
    [ThreadStatic] private static int externalDepth;
    [ThreadStatic] private static int queuedDepth;
    [ThreadStatic] private static Guid externalOwner;
    internal static bool ExternalAction => queuedDepth > 0 || externalDepth > 0 && !Allowed && Lease.OwnedBy(externalOwner, Environment.TickCount64);
    private readonly ICallGateProvider<string, string> api = Svc.PluginInterface.GetIpcProvider<string, string>("WrathCombo.AutomationOwnership.v1");
    private readonly ICallGateProvider<Guid, bool> begin = Svc.PluginInterface.GetIpcProvider<Guid, bool>("WrathCombo.AutomationExternal.Begin.v1");
    private readonly ICallGateProvider<Guid, bool> end = Svc.PluginInterface.GetIpcProvider<Guid, bool>("WrathCombo.AutomationExternal.End.v1");
    private readonly ICallGateProvider<uint, uint> nativeAction = Svc.PluginInterface.GetIpcProvider<uint, uint>("WrathCombo.AutomationNativeAction.v1");
    internal static bool Allowed => (Service.Configuration?.AccordAutomationManaged != true || Lease.Managed) && Lease.Allowed(Environment.TickCount64);
    internal static unsafe bool Busy => ActionManager.Instance() is var am && am != null &&
        (am->ActionQueued || am->CastActionId != 0 || am->AnimationLock > 0);

    internal AutomationOwnership()
    {
        api.RegisterFunc(Handle);
        nativeAction.RegisterFunc(id => Service.ActionReplacer.OriginalHook(id));
        begin.RegisterFunc(token =>
        {
            if (!Svc.Framework.IsInFrameworkUpdateThread || Allowed || !Lease.OwnedBy(token, Environment.TickCount64)) return false;
            if (externalDepth > 0 && externalOwner != token) return false;
            externalOwner = token;
            externalDepth++;
            return true;
        });
        end.RegisterFunc(token =>
        {
            if (externalDepth == 0 || externalOwner != token) return false;
            externalDepth--;
            return true;
        });
    }

    internal static IDisposable QueuedExternalScope()
    {
        queuedDepth++;
        return new QueueScope();
    }
    private sealed class QueueScope : IDisposable
    {
        public void Dispose() => queuedDepth--;
    }

    private static string Handle(string json)
    {
        try
        {
            if (!Svc.Framework.IsInFrameworkUpdateThread) throw new InvalidOperationException("Use the framework thread.");
            var request = JObject.Parse(json);
            string command = request.Value<string>("Command") ?? "status";
            Guid.TryParse(request.Value<string>("Token"), out var token);
            long now = Environment.TickCount64;
            bool success = command switch
            {
                "status" => true,
                "block" => Lease.Block(token, now),
                "renew" => Lease.Renew(token, now),
                "start" => Lease.Start(token, now, Busy),
                "release" => Lease.Release(token, now),
                _ => false,
            };
            if (command == "block" && success)
            {
                ClearPending();
                Service.Configuration.RotationConfig.Enabled = false;
                Service.Configuration.AccordAutomationManaged = true;
                Service.Configuration.Save();
            }
            if (command == "release" && success)
            {
                Service.Configuration.AccordAutomationManaged = false;
                try { Service.Configuration.Save(); }
                catch { Service.Configuration.AccordAutomationManaged = true; throw; }
            }
            if (command == "start" && success)
            {
                AutoRotationController.ToggleAutoRotation(true);
                success = new AutoRotationConfigIPCWrapper(Service.Configuration.RotationConfig).Enabled;
                if (!success) Lease.Stop();
            }
            return new JObject { ["Success"] = success, ["Version"] = 1, ["Managed"] = Lease.Managed,
                ["Allowed"] = Allowed, ["Owned"] = Lease.OwnedBy(token, now), ["Busy"] = Busy,
                ["Build"] = typeof(AutomationOwnership).Assembly.ManifestModule.ModuleVersionId.ToString() }.ToString();
        }
        catch (Exception ex) { Lease.Stop(); return new JObject { ["Success"] = false, ["Error"] = ex.Message }.ToString(); }
    }

    private static void ClearPending()
    {
        // Do not clear the native queue: the coordinator waits for it to settle.
        ActionRequestIPCProvider.ActionRequests.Clear();
        CustomComboFunctions.OverrideTarget = null;
        AutoRotationController.AutorotHealTarget = null;
        P.ActionRetargeting.ClearCachedRetargets();
        SelectionRuntime.Reset();
    }

    public void Dispose() { Lease.Stop(); api.UnregisterFunc(); begin.UnregisterFunc(); end.UnregisterFunc(); nativeAction.UnregisterFunc(); externalDepth = 0; }
}
