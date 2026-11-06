from pathlib import Path
import tempfile, subprocess, unittest

class EmergencyTests(unittest.TestCase):
    def test_manual_dispatch(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)
            (path/"Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path/"EmergencyRotation.cs").write_text((Path(__file__).resolve().parents[1]/"WrathCombo/Engine/EmergencyRotation.cs").read_text())
            (path/"Program.cs").write_text(r'''using ECommons.DalamudServices;
using WrathCombo.Engine;
using WrathCombo.Core;
using FFXIVClientStructs.FFXIV.Client.Game;
using var ipc=new EmergencyRotation();
string Press(bool area)=>Svc.PluginInterface.Call!(area);
void Check(bool ok){if(!ok) throw new Exception("Unexpected backup dispatch");}
AutomationOwnership.Allowed=true;Press(false);Check(ActionManager.Calls==0);
AutomationOwnership.Allowed=false;
foreach(bool area in new[]{false,true}) {
 uint input=area?1000001u:1000000u;
 ActionReplacer.FilteredCombos=[new Combo(input,156)];
 Press(area);Check(ActionManager.Last==(156u,77ul) && !EmergencyRotation.Resolving && !ExperimentalSelection.LegacyDamage && !AutomationOwnership.Bypass);
}
Check(ActionManager.Calls==2);
ActionReplacer.FilteredCombos=[new Combo(1000000,1000004)];Press(false);Check(ActionManager.Calls==2);
Svc.ClientState.IsPvP=true;Press(true);Check(ActionManager.Calls==2);
namespace WrathCombo.Engine {
 static class ExperimentalSelection { public static bool LegacyDamage; }
 static class AutomationOwnership { public static bool Managed=true,Allowed,Bypass; public static IDisposable QueuedExternalScope(){Bypass=true;return new End();} class End:IDisposable {public void Dispose()=>Bypass=false;} }
}
namespace WrathCombo.Core {
 class Combo(uint expected,uint output) { public bool TryInvoke(uint input,out uint action){if(!EmergencyRotation.Resolving || !ExperimentalSelection.LegacyDamage || expected!=input)throw new Exception();action=output;return true;} }
 class ActionReplacer {public static Combo[]? FilteredCombos; public void UpdateFilteredCombos()=>FilteredCombos=[];}
}
namespace WrathCombo.Services {static class Service {public static ActionReplacer ActionReplacer=new();}}
namespace WrathCombo.Combos.PvE {static class All {public const uint SingleTargetDPS=1000000,AoEDPS=1000001;}}
namespace ECommons.GameHelpers {static class Player {public static bool Available=true,IsDead;}}
namespace ECommons.DalamudServices {
 static class Svc {public static PI PluginInterface=new(); public static Framework Framework=new();public static Client ClientState=new();public static Targets Targets=new();}
 class Targets {public Target? Target=new();} class Target {public ulong GameObjectId=77;}
 class Client {public bool IsPvP;} class Framework {public bool IsInFrameworkUpdateThread=true;}
 class PI {public Func<bool,string>? Call;public Dalamud.Plugin.Ipc.ICallGateProvider<T,R> GetIpcProvider<T,R>(string name)=>new Gate<T,R>(this);}
 class Gate<T,R>(PI plugin):Dalamud.Plugin.Ipc.ICallGateProvider<T,R>{public void RegisterFunc(Func<T,R> f)=>plugin.Call=x=>(string)(object)f((T)(object)x)!;public void UnregisterFunc()=>plugin.Call=null;}
}
namespace Dalamud.Plugin.Ipc {interface ICallGateProvider<T,R>{void RegisterFunc(Func<T,R> f);void UnregisterFunc();}}
namespace FFXIVClientStructs.FFXIV.Client.Game {
 enum ActionType {Action}
 unsafe struct ActionManager {public static int Calls; public static (uint,ulong) Last;
 public static ActionManager* Instance()=>(ActionManager*)1;
 public bool UseAction(ActionType type,uint action,ulong target){if(!AutomationOwnership.Bypass || EmergencyRotation.Resolving)throw new Exception();Calls++;Last=(action,target);return true;} }
}
namespace AccordDiagnostics {static class DiagnosticLog {public static void Event(string k,Func<object> payload){} }}
''')
            project=path/"Test.csproj"
            project.write_text(project.read_text().replace("<Nullable>","<AllowUnsafeBlocks>true</AllowUnsafeBlocks><Nullable>"))
            run=subprocess.run(["dotnet","run","--project",str(project)],capture_output=True,text=True,timeout=120)
            self.assertEqual(run.returncode,0,run.stdout+run.stderr)
