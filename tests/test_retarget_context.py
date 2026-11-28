from pathlib import Path
import subprocess
import tempfile
import unittest

class RetargetContextTests(unittest.TestCase):
    def test_production_retarget_storage_is_context_bound(self):
        source=(Path(__file__).resolve().parents[1]/'WrathCombo/Core/ActionRetargeting.cs').read_text(encoding='utf-8-sig')
        def method(name):
            start=source.index('    '+name)
            end=source.index('{',start)+1
            depth=1
            while depth:
                depth+=(source[end]=='{')-(source[end]=='}');end+=1
            return source[start:end]
        fields=source[source.index('    internal readonly Dictionary<uint, Retargeting> Retargets'):source.index('    public ActionRetargeting()')]
        code=r'''using System; using System.Collections.Generic; using System.Linq;
Fixture.Run();
static class Engine { public static class SelectionRuntime { public static bool Automatic; } }
class Retargeting(int id,uint action,uint[] replaced,ulong recipient) {
 public int ID=id; public uint Action=action; public uint[] ReplacedActions=replaced; public ulong Recipient=recipient;
}
class Fixture {
 static uint[] _actionsAllowedToBeReplacedAction=[];
 static void log(string text,int logLevel=0){}
FIELDS
ADD
REMOVE
CLEAR
 public static void Run(){
  var f=new Fixture();
  void Check(bool b,string m){if(!b)throw new Exception(m);}
  Engine.SelectionRuntime.Automatic=false;f.AddRetarget(new(1,124,[124],42));
  Engine.SelectionRuntime.Automatic=true;f.AddRetarget(new(1,124,[124],99));
  Check(f.ActiveRetargets[124].Recipient==99,"automatic target missing");
  Engine.SelectionRuntime.Automatic=false;
  Check(f.ActiveRetargets[124].Recipient==42,"automatic resolver replaced manual target");
  Engine.SelectionRuntime.Automatic=true;f.RemoveRetarget(1);
  Engine.SelectionRuntime.Automatic=false;
  Check(f.ActiveRetargets[124].Recipient==42,"automatic cleanup removed manual target");
  Engine.SelectionRuntime.Automatic=true;
  Check(!f.ActiveRetargets.ContainsKey(124),"automatic cleanup missed its target");
  f.AddRetarget(new(3,124,[124],100));f.ClearCachedRetargets();
  Check(f.ActiveRetargets.Count==0,"reset retained automatic target");
  Engine.SelectionRuntime.Automatic=false;
  Check(f.ActiveRetargets.Count==0,"reset retained manual target");
  Console.WriteLine("Retarget context isolation passed");
 }
}
'''.replace('FIELDS',fields).replace('ADD',method('private void AddRetarget(')).replace('REMOVE',method('private void RemoveRetarget(')).replace('CLEAR',method('internal void ClearCachedRetargets('))
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)
            (path/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>',encoding='utf-8')
            (path/'Program.cs').write_text(code,encoding='utf-8')
            result=subprocess.run(['dotnet','run','--project',str(path/'Test.csproj'),'-p:UseSharedCompilation=false'],capture_output=True,text=True,timeout=120)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
            self.assertIn('Retarget context isolation passed',result.stdout)
