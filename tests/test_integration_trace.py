"""Run the real transport and integration recorder with a deterministic clock."""
from pathlib import Path
import os
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]


class IntegrationTraceTests(unittest.TestCase):
    def test_endpoint_sampling_frame_correlation_and_failure_isolation(self):
        source_root = ROOT / 'WrathCombo/Diagnostics'
        if not source_root.exists():
            source_root = ROOT / 'Accord.Plugin/Diagnostics'
        reference = Path(os.environ.get('DALAMUD_HOME') or (Path(os.environ['APPDATA']) / 'XIVLauncher/addon/Hooks/dev')) / 'Newtonsoft.Json.dll'
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory)
            (p / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>' + escape(str(reference)) + '</HintPath></Reference></ItemGroup></Project>', encoding='utf-8')
            for name in ['DiagnosticLog.cs', 'IntegrationTrace.cs']:
                source = (source_root / name).read_text(encoding='utf-8-sig').replace('Environment.TickCount64', 'Clock.Now').replace('System.Diagnostics.Stopwatch.GetTimestamp()', 'Clock.Now').replace('Stopwatch.GetTimestamp()', 'Clock.Now').replace('Stopwatch.Frequency', '1000L')
                (p / name).write_text(source, encoding='utf-8')
            (p / 'Program.cs').write_text(r'''
using System; using System.IO; using System.Linq; using System.Threading; using Newtonsoft.Json.Linq; using AccordDiagnostics;
static class Clock { internal static long Now=1000; }
namespace ECommons.DalamudServices { static class Svc { internal static class Log { internal static void Information(string format,string rows){} } } }
namespace AccordDiagnostics { enum TimedPath { DiagnosticCapture } static class PerformanceTimings { internal static IDisposable? Measure(TimedPath p)=>null; } }
class Program {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Drain(){Check(SpinWait.SpinUntil(()=>!(bool)JObject.FromObject(DiagnosticLog.Status())["Flushing"]!,5000),"Writer did not flush");}
 static void Main(string[] args){
  var id=Guid.NewGuid().ToString("N");DiagnosticLog.Start(args[0],id,"Test",false);
  int contexts=0;object? Context(){contexts++;return new{Module="Boss",Combat=true};}
  IntegrationTrace.Frame(Context);
  Check(IntegrationTrace.Call("BossMod.A",()=>{Clock.Now++;return 1;})==1,"IPC result changed");
  Check(!IntegrationTrace.Call("BossMod.B",()=>{Clock.Now++;return false;}),"False IPC result changed");
  IntegrationTrace.Call("BossMod.A",()=>2);
  var expected=new InvalidOperationException("endpoint failed");
  try{IntegrationTrace.Call<bool>("BossMod.Failed",()=>throw expected);throw new Exception("Failure swallowed");}
  catch(InvalidOperationException e){Check(ReferenceEquals(e,expected),"Exception replaced");}
  IntegrationTrace.Call("BossMod.Write",()=>false,true,new{Previous="Rear",Requested="Flank",Preset="AutoDuty"});
  Clock.Now+=70;IntegrationTrace.Frame(Context);
  for(int i=0;i<64;i++){Clock.Now+=16;IntegrationTrace.Frame(Context);}
  Check(contexts<10,"Context factory ran every frame");
  for(int i=0;i<100;i++)IntegrationTrace.Call("Bounded."+i,()=>true);
  Clock.Now+=30000;IntegrationTrace.Frame(Context);
  DiagnosticLog.Stop("Done");Drain();
  var records=Directory.GetFiles(Path.Combine(args[0],id),"events-*.jsonl",SearchOption.AllDirectories).SelectMany(File.ReadLines).Select(JObject.Parse).ToArray();
  var reads=records.Where(r=>(string?)r["Kind"]=="Integration.Read").ToArray();
  Check(reads.Count(r=>(string?)r["Data"]!["Endpoint"]=="BossMod.A")==1,"Read sampling ignored endpoint or repeated fast reads");
  Check(reads.Any(r=>(string?)r["Data"]!["Endpoint"]=="BossMod.B"),"Second endpoint masked by first");
  Check(records.Any(r=>(string?)r["Kind"]=="Integration.Error" && (string?)r["Data"]!["Error"]=="endpoint failed"),"Failure evidence lost");
  var write=records.Single(r=>(string?)r["Kind"]=="Integration.Write")["Data"]!;
  Check((string?)write["Result"]=="False" && (string?)write["Input"]!["Previous"]=="Rear","Write result/input lost");
  var window=records.First(r=>(string?)r["Kind"]=="Frame.Window")["Data"]!;
  Check((double)window["MaxMs"]!>=70 && (int)window["Over50Ms"]!>0,"Frame spike missing");
  Check((string?)window["PeakContext"]!["Module"]=="Boss","Peak context missing");
  Check(((JArray)window["RecentCallsAtPeak"]!).Any(c=>(string?)c["Endpoint"]=="BossMod.Write"),"Write not correlated with next frame spike");
  Check(records.Where(r=>(string?)r["Kind"]=="Frame.Window").All(r=>((JArray)r["Data"]!["RecentCallsAtPeak"]!).Count<=64),"Recent call ring unbounded");
  Check(records.Any(r=>(string?)r["Kind"]=="Integration.Timings" && r["Data"]!.ToString().Contains("BossMod.Failed n=1") && r["Data"]!.ToString().Contains("errors=1")),"Endpoint counters lost failures");
  var second=Guid.NewGuid().ToString("N");DiagnosticLog.Start(args[0],second,"Test",false);
  IntegrationTrace.Frame(()=>new{Module="NextBoss"});Clock.Now+=60;IntegrationTrace.Frame(()=>new{Module="NextBoss"});
  for(int i=0;i<64;i++){Clock.Now+=16;IntegrationTrace.Frame(()=>new{Module="NextBoss"});}
  DiagnosticLog.Stop("Next");Drain();
  var fresh=Directory.GetFiles(Path.Combine(args[0],second),"events-*.jsonl",SearchOption.AllDirectories).SelectMany(File.ReadLines).Select(JObject.Parse).First(r=>(string?)r["Kind"]=="Frame.Window")["Data"]!;
  Check(((JArray)fresh["RecentCallsAtPeak"]!).Count==0,"Prior duty/session calls leaked into new correlation");
  DiagnosticLog.Stop("Already stopped");
  Check(IntegrationTrace.Call("NoCapture",()=>42)==42,"Disabled logging blocked IPC");
  IntegrationTrace.Frame(()=>throw new Exception("Context unavailable"));
  Console.WriteLine("Integration trace boundaries passed");
 }
}
''', encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', str(p / 'Test.csproj'), '-p:UseSharedCompilation=false', '--', str(p / 'capture')], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Integration trace boundaries passed', result.stdout)
