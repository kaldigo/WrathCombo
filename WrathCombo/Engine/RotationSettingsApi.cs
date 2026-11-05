using System;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WrathCombo.Services;

namespace WrathCombo.Engine;

internal sealed class RotationSettingsApi : IDisposable
{
    private readonly ICallGateProvider<string, string> api = Svc.PluginInterface.GetIpcProvider<string, string>("WrathCombo.RotationSettings.v1");
    internal RotationSettingsApi()
    {
        ExperimentalSelection.Apply(Service.Configuration.RotationSelectionSettings, restoring: true);
        api.RegisterFunc(Handle);
    }
    internal static void ApplySaved(JObject desired)
    {
        var previous = ExperimentalSelection.Read();
        var saved = Service.Configuration.RotationSelectionSettings;
        ExperimentalSelection.Apply(desired);
        try
        {
            Service.Configuration.RotationSelectionSettings = ExperimentalSelection.Read();
            Svc.PluginInterface.SavePluginConfig(Service.Configuration);
        }
        catch
        {
            Service.Configuration.RotationSelectionSettings = saved;
            ExperimentalSelection.Apply(previous, restoring: true);
            throw;
        }
    }
    private static string Handle(string request)
    {
        try
        {
            if (!Svc.Framework.IsInFrameworkUpdateThread) throw new InvalidOperationException("Use the framework thread.");
            if (request != "read") ApplySaved(JObject.Parse(request));
            return new JObject { ["Success"] = true, ["Version"] = 1, ["Settings"] = ExperimentalSelection.Read() }.ToString(Formatting.None);
        }
        catch (Exception ex) { return new JObject { ["Success"] = false, ["Error"] = ex.Message }.ToString(Formatting.None); }
    }
    public void Dispose() => api.UnregisterFunc();
}
