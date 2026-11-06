"""Exercise the production rotation settings API and feature gates across reload and failure."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]

class PersistentSettingsTests(unittest.TestCase):
    def test_save_reload_and_rejected_updates(self):
        harness = r"""
using System;
using Newtonsoft.Json.Linq;
using ECommons.DalamudServices;
using WrathCombo.Engine;
using WrathCombo.Services;
class Program {
 static void Check(bool value,string why) { if(!value) throw new Exception(why); }
 static JObject Call(string request) => JObject.Parse(Svc.PluginInterface.Handler!(request));
 static void Main() {
  using(var api = new RotationSettingsApi()) {
   var expected = ExperimentalSelection.Read();
   expected["BlackMage"]=true; expected["Forecasts"]=true; expected["EnemyTargeting"]=true;
   expected["EnemyFocus"]=true; expected["EnemyOverrideOrder"]=new JArray("Focus","Selected","WorldMouseover");
   Check(Call(expected.ToString()).Value<bool>("Success"),"valid settings rejected");
   Check(ExperimentalSelection.ActiveBlackMage && ExperimentalSelection.ActiveTargets && ExperimentalSelection.Forecasts,"runtime gates remained off");
   Check(JToken.DeepEquals(expected,JObject.Parse(Svc.PluginInterface.Disk)),"settings not saved");
   var previous=ExperimentalSelection.Read();
   Svc.PluginInterface.FailSave=true;
   Check(!Call("{\"BlackMage\":false}").Value<bool>("Success"),"save failure accepted");
   Check(JToken.DeepEquals(previous,ExperimentalSelection.Read()),"save failure changed runtime");
   Check(JToken.DeepEquals(previous,Service.Configuration.RotationSelectionSettings),"save failure changed saved config");
   Svc.PluginInterface.FailSave=false;
   WrathCombo.CustomComboNS.Functions.CustomComboFunctions.Combat=true;
   SelectionRuntime.Commitment=new object(); int resets=SelectionRuntime.Resets;
   Check(!Call("{\"BlackMage\":false}").Value<bool>("Success"),"combat write accepted");
   Check(SelectionRuntime.Resets==resets && SelectionRuntime.Commitment!=null,"rejected change disrupted an action");
   Check(Call("read").Value<bool>("Success") && ExperimentalSelection.ActiveBlackMage,"combat read failed");
   WrathCombo.CustomComboNS.Functions.CustomComboFunctions.Combat=false; SelectionRuntime.Commitment=null;
   Check(!Call("{\"BlackMage\":false,\"Unknown\":true}").Value<bool>("Success") && ExperimentalSelection.ActiveBlackMage,"invalid batch partially applied");
   Check(!Call("{\"EnemyOverrideOrder\":[\"Focus\",\"Focus\",\"Selected\"]}").Value<bool>("Success"),"invalid order accepted");
  }
  var disk=JObject.Parse(Svc.PluginInterface.Disk);
  ExperimentalSelection.Apply(new JObject { ["BlackMage"]=false, ["Forecasts"]=false, ["EnemyTargeting"]=false },restoring:true);
  Service.Configuration=new Configuration { RotationSelectionSettings=disk };
  WrathCombo.CustomComboNS.Functions.CustomComboFunctions.Combat=true;
  using(var reloaded = new RotationSettingsApi()) {
   Check(ExperimentalSelection.ActiveBlackMage && ExperimentalSelection.ActiveTargets && ExperimentalSelection.Forecasts,"reload lost runtime settings");
   Check(JToken.DeepEquals(disk,Call("read")["Settings"]),"reload lost saved overrides");
  }
  Console.WriteLine("Persistent engine settings: saved runtime activation, reload, combat rejection, malformed batches and save rollback passed.");
 }
}
namespace Dalamud.Plugin.Ipc { interface ICallGateProvider<T,R> { void RegisterFunc(Func<T,R> fn); void UnregisterFunc(); } }
namespace ECommons.DalamudServices {
 static class Svc { public static Plugin PluginInterface=new(); public static Framework Framework=new(); public static Client ClientState=new(); }
 class Framework { public bool IsInFrameworkUpdateThread=true; } class Client { public bool IsPvP; }
 class Plugin {
  public Func<string,string>? Handler; public bool FailSave; public string Disk="{}";
  public Dalamud.Plugin.Ipc.ICallGateProvider<T,R> GetIpcProvider<T,R>(string name) => new Provider<T,R>(this);
  public void SavePluginConfig(Configuration config) { if(FailSave) throw new Exception("disk failure"); Disk=config.RotationSelectionSettings.ToString(); }
 }
 class Provider<T,R>(Plugin plugin):Dalamud.Plugin.Ipc.ICallGateProvider<T,R> {
  public void RegisterFunc(Func<T,R> fn) => plugin.Handler=s=>(string)(object)fn((T)(object)s)!;
  public void UnregisterFunc() => plugin.Handler=null;
 }
}
namespace ECommons.GameHelpers { static class Player { public static uint Job=25; } }
namespace WrathCombo.Services { static class Service { public static Configuration Configuration=new(); } class Configuration { public JObject RotationSelectionSettings=new(); } }
namespace WrathCombo.CustomComboNS.Functions { static class CustomComboFunctions { public static bool Combat; public static bool InCombat()=>Combat; } }
namespace AccordDiagnostics { static class DiagnosticLog { public static bool Active => false; public static void Event(string kind, Func<object> data, string? sampleKey=null) {} } }
namespace WrathCombo.Engine {
 static class AutomationOwnership { public static bool Busy; }
 static class SelectionRuntime { public static object? Commitment; public static int Resets; public static void Reset() { Resets++; Commitment=null; } }
 static class BossForecastProvider { public static void Reset() {} }
 static class PvPIntentRouting { public static bool Active; }
 static class RotationBehaviour { public static Prefs? Preferences; }
 class Prefs { public bool IncludeHealing; public bool HealingGcds; }
}
namespace WrathCombo.Engine.Selection {
 enum TargetSource { Explicit, PartyMouseover, WorldMouseover, Selected, Focus }
 record TargetOverride(TargetSource Source);
}
"""
        with tempfile.TemporaryDirectory() as temporary:
            path=Path(temporary)
            (path/'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup></Project>')
            for name in ['Engine/RotationSettingsApi.cs','Engine/ExperimentalSelection.cs','Engine/Selection/TargetOrder.cs']:
                (path/Path(name).name).write_text((ROOT/'WrathCombo'/name).read_text())
            (path/'Program.cs').write_text(harness)
            result=subprocess.run(['dotnet','run','--project',str(path/'Harness.csproj'),'-c','Release'],capture_output=True,text=True,timeout=120)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
