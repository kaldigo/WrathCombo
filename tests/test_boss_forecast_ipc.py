"""Exercise the real forecast adapter against BossMod's typed IPC contracts."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class BossForecastIpcTests(unittest.TestCase):
    def test_fixed_dash_forward_backward_targeted_and_unavailable(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory)
            (p / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>', encoding='utf-8')
            source = (ROOT / 'WrathCombo/Engine/BossForecastProvider.cs').read_text(encoding='utf-8-sig')
            (p / 'BossForecastProvider.cs').write_text(source.replace('Environment.TickCount64', 'Clock.Now'), encoding='utf-8')
            (p / 'EncounterForecast.cs').write_bytes((ROOT / 'WrathCombo/Engine/Selection/EncounterForecast.cs').read_bytes())
            (p / 'Program.cs').write_text(r'''
using System; using System.Numerics; using System.Collections.Generic; using WrathCombo.Engine;
static class Clock { internal static long Now=1000; }
namespace ECommons.GameHelpers { static class Player { internal static bool Available=true,IsDead; internal static Actor Object=new(); } class Actor { internal ulong GameObjectId=1; } }
namespace ECommons.DalamudServices {
 static class Svc { internal static Host PluginInterface=new(); internal static class ClientState { internal static uint TerritoryType=1;internal static bool IsPvP; } }
 class Host { internal Gate<T> GetIpcSubscriber<T>(string name)=>new(name); internal Gate<A,B,R> GetIpcSubscriber<A,B,R>(string name)=>new(name); }
 class Gate<T>(string name){internal T InvokeFunc(){object value=name switch{ "BossMod.HasActiveModule"=>true,"BossMod.ActiveModuleName"=>"Boss",_=>typeof(T)==typeof(float)?float.MaxValue:typeof(T)==typeof(bool)?false:typeof(T)==typeof(ulong)?0UL:0};return (T)value;} }
 class Gate<A,B,R>(string name){internal R InvokeFunc(A a,B b){Fixture.Calls++;if(Fixture.Fail)throw new InvalidOperationException("missing");if(name.EndsWith("IsFixedDashSafe")){if(typeof(A)!=typeof(float)||typeof(B)!=typeof(bool))throw new Exception("Wrong fixed dash signature");Fixture.Range=(float)(object)a!;Fixture.Back=(bool)(object)b!;}else{if(typeof(A)!=typeof(Vector3)||typeof(B)!=typeof(Vector3))throw new Exception("Wrong targeted signature");Fixture.From=(Vector3)(object)a!;Fixture.To=(Vector3)(object)b!;}return (R)(object)Fixture.Safe;} }
}
namespace WrathCombo.Engine { static class ExperimentalSelection { internal static bool Forecasts=true; } }
namespace WrathCombo.CustomComboNS.Functions { static class CustomComboFunctions { internal static bool InCombat()=>true; } }
namespace AccordDiagnostics {
 enum TimedPath { Forecast }
 static class PerformanceTimings { internal static IDisposable? Measure(TimedPath p)=>null; }
 static class DiagnosticLog { internal static void Event(string name,Func<object?> data,string? key=null){} }
 static class IntegrationTrace { internal static T Call<T>(string endpoint,Func<T> call,bool write=false,object? input=null)=>call(); }
}
static class Fixture { internal static int Calls;internal static bool Back,Fail,Safe;internal static float Range;internal static Vector3 From,To; }
class Program {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Main(){
  Check(BossForecastProvider.FixedDashSafe(15)==null && Fixture.Calls==0,"Missing forecast attempted a dash IPC");
  BossForecastProvider.Tick();
  foreach(float distance in new[]{10f,15f,-15f,-10f}){
   Check(BossForecastProvider.FixedDashSafe(distance)==false,"Unsafe fixed dash was not blocked");
   Check(Fixture.Range==Math.Abs(distance) && Fixture.Back==(distance<0),"Signed distance contract changed");
  }
  Fixture.Safe=true;
  Check(BossForecastProvider.FixedDashSafe(15)==true,"Safe dash rejected");
  var from=new Vector3(1,2,3);var to=new Vector3(4,5,6);
  Check(BossForecastProvider.DashSafe(from,to)==true && Fixture.From==from && Fixture.To==to,"Targeted dash contract changed");
  Fixture.Fail=true;int before=Fixture.Calls;
  Check(BossForecastProvider.FixedDashSafe(-10)==null && BossForecastProvider.FixedDashSafe(-10)==null && Fixture.Calls==before+1,"Unavailable IPC lost backoff/unknown semantics");
  Clock.Now+=5001;Fixture.Fail=false;BossForecastProvider.Tick();
  Check(BossForecastProvider.FixedDashSafe(-10)==true,"Recovered endpoint remained blocked");
  Clock.Now+=1501;before=Fixture.Calls;
  Check(BossForecastProvider.DashSafe(from,to)==null && Fixture.Calls==before,"Stale forecast queried native safety");
  BossForecastProvider.Reset();Check(BossForecastProvider.FixedDashSafe(15)==null,"Reset kept stale safety data");
  Console.WriteLine("Boss forecast IPC boundaries passed");
 }
}
''', encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', str(p / 'Test.csproj'), '-p:UseSharedCompilation=false'], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Boss forecast IPC boundaries passed', result.stdout)
        execution = (ROOT / 'WrathCombo/Engine/ExecutionSafety.cs').read_text(encoding='utf-8-sig')
        self.assertIn('targeted ? BossForecastProvider.DashSafe(from, destination) : BossForecastProvider.FixedDashSafe(fixedDistance)', execution)
