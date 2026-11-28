"""Exercise the actual diagnostic transport: feature counts, transitions and presses."""
from pathlib import Path
import os
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]


class FeatureDiagnosticsTests(unittest.TestCase):
    def test_feature_coalescing_keeps_transitions_counts_and_action_detail(self):
        reference = Path(os.environ.get('DALAMUD_HOME') or (Path(os.environ['APPDATA']) / 'XIVLauncher/addon/Hooks/dev')) / 'Newtonsoft.Json.dll'
        self.assertTrue(reference.is_file(), 'Dalamud JSON reference missing')
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>' + escape(str(reference)) + '</HintPath></Reference></ItemGroup></Project>', encoding='utf-8')
            source = (ROOT / 'WrathCombo/Diagnostics/DiagnosticLog.cs').read_text(encoding='utf-8-sig')
            (path / 'DiagnosticLog.cs').write_text(source.replace('Environment.TickCount64', 'Clock.Now'), encoding='utf-8')
            (path / 'Program.cs').write_text(r'''
using System; using System.IO; using System.Linq; using System.Threading; using Newtonsoft.Json.Linq; using AccordDiagnostics;
static class Clock { internal static long Now=100; }
namespace AccordDiagnostics { enum TimedPath { DiagnosticCapture } static class PerformanceTimings { internal static IDisposable? Measure(TimedPath p)=>null; } }
class Program {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Main(string[] args){
  string id=Guid.NewGuid().ToString("N");
  DiagnosticLog.Start(args[0],id,"WrathCombo",false);
  using(DiagnosticLog.Begin("Native.Press",()=>new{Action=120})){
   for(int i=0;i<512;i++) DiagnosticLog.Feature("ActiveHealer",false,41,false);
   DiagnosticLog.Feature("ActiveHealer",true,41,false);
   DiagnosticLog.Feature("ActiveHealer",true,24,false);
   DiagnosticLog.Feature("ActiveHealer",true,24,true);
   Clock.Now+=1001;
   DiagnosticLog.Feature("ActiveHealer",true,24,true);
   DiagnosticLog.Feature("ActiveHealer",false,24,true);
   for(int i=0;i<2;i++) DiagnosticLog.Event("Readiness.Result",()=>new{Action=120,Target=42,NativeStatus=0});
   DiagnosticLog.Event("Native.Result",()=>new{Action=120,Target=42,Submitted=true});
  }
  DiagnosticLog.Checkpoint(); DiagnosticLog.Stop("Test complete");
  Check(SpinWait.SpinUntil(()=>!(bool)JObject.FromObject(DiagnosticLog.Status())["Flushing"]!,5000),"Writer did not flush");
  var records=Directory.GetFiles(args[0],"events-*.jsonl",SearchOption.AllDirectories).SelectMany(File.ReadLines).Select(JObject.Parse).ToArray();
  var features=records.Where(r=>(string?)r["Kind"]=="Feature.Evaluated").ToArray();
  Check(features.Length==6,"Unchanged gates were serialized per press, or a transition was lost");
  Check((uint)features[2]["Data"]!["Job"]! ==24 && (bool)features[3]["Data"]!["PvP"]!,"Job/PvP transitions lost");
  var summary=JObject.Parse(File.ReadAllText(Directory.GetFiles(args[0],"summary.json",SearchOption.AllDirectories).Single()));
  Check((int)summary["Counts"]!["ActiveHealer:inactive"]! ==513 && (int)summary["Counts"]!["ActiveHealer:active"]! ==4,"Feature call counts changed");
  Check(records.Count(r=>(string?)r["Kind"]=="Readiness.Result")==2,"Readiness details sampled inside native press");
  Check(records.Any(r=>(string?)r["Kind"]=="Native.Result" && (ulong)r["Data"]!["Target"]! ==42),"Submission patient missing");
  DiagnosticLog.Start(args[0],Guid.NewGuid().ToString("N"),"WrathCombo",false);
  DiagnosticLog.Feature("ActiveHealer",false,24,true);DiagnosticLog.Stop("Second session");
  Check(SpinWait.SpinUntil(()=>!(bool)JObject.FromObject(DiagnosticLog.Status())["Flushing"]!,5000),"Second session did not flush");
  Check(Directory.GetFiles(args[0],"events-*.jsonl",SearchOption.AllDirectories).SelectMany(File.ReadLines).Select(JObject.Parse).Count(r=>(string?)r["Kind"]=="Feature.Evaluated")==7,"New session retained stale feature state");
  Console.WriteLine("Feature capture boundaries passed");
 }
}
''', encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Test.csproj'), '-p:UseSharedCompilation=false', '--', str(path / 'capture')], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Feature capture boundaries passed', result.stdout)
