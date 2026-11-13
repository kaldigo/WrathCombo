from pathlib import Path
import subprocess, tempfile, unittest
ROOT=Path(__file__).resolve().parents[1]

def method(text, signature):
    start=text.index(signature); brace=text.index('{',start); level=1; end=brace+1
    while level:
        level += (text[end]=='{')-(text[end]=='}'); end+=1
    return text[start:end]

class LeaseCoexistenceTests(unittest.TestCase):
    def test_saved_settings_and_runner_with_external_lease(self):
        wrapper=(ROOT/'WrathCombo/AutoRotation/AutoRotationConfigIPCWrapper.cs').read_text()
        enabled=wrapper[wrapper.index('    public bool Enabled =>'):wrapper.index('    public bool InCombatOnly')]
        apply=method((ROOT/'WrathCombo/Engine/SettingsApi.cs').read_text(),'    private JObject Apply(JObject request)')
        harness=r"""
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using WrathFork.Contract;
class Program {
 static void Check(bool b,string why) { if(!b) throw new Exception(why); }
 static void Main() {
  var wrapper=new Wrapper();
  AutomationOwnership.Allowed=true; AutomationOwnership.Managed=true;
  P.UIHelper.State=false;
  Check(wrapper.Enabled,"External off lease overrode Accord on");
  wrapper.config.Enabled=false; P.UIHelper.State=true;
  Check(!wrapper.Enabled,"External on lease overrode Accord off");
  wrapper.config.Enabled=true; AutomationOwnership.Allowed=false;
  Check(!wrapper.Enabled,"Blocked engine executed");
  AutomationOwnership.Allowed=true; AutomationOwnership.Managed=false; P.UIHelper.State=false;
  Check(!wrapper.Enabled,"Standalone Wrath lost normal lease behavior");
  var api=new Api(); WrathCombo.P.IPC.Leasing.Registrations.Add(1);
  var request=new JObject { ["OperationId"]=Guid.NewGuid().ToString(),["Instance"]="instance",["ConfigurationId"]="config",["ExpectedRevision"]=SettingsTransaction.Hash(api.Current),["ExpectedSequence"]=0,["Patch"]=new JObject { ["Value"]=true } };
  bool blocked=false; try { api.Call(request); } catch(InvalidOperationException) { blocked=true; }
  Check(blocked && api.Saves==0,"Uncoordinated lease protection changed");
  AutomationOwnership.Managed=true;
  api.Call(request);
  Check(api.Saves==1 && api.Current.Value<bool>("Value"),"Coordinated lease blocked settings");
  var replay=api.Call(request);
  Check(api.Saves==1 && replay["Receipt"]!.Value<bool>("Replay"),"Pending retry duplicated settings commit");
  Console.WriteLine("Managed runner priority, standalone lease behavior and settings replay with an external lease passed.");
 }
}
static class AutomationOwnership { public static bool Allowed, Managed; }
class Config { public bool Enabled=true; }
class UI { public bool? State; public (bool state,int extra)? AutoRotationStateControlled()=>State is {} s ? (s,0) : null; }
class Plugin { public UI UIHelper=new(); public IPC IPC=new(); }
class IPC { public Leasing Leasing=new(); } class Leasing { public List<int> Registrations=new(); }
static class WrathCombo { public static Plugin P=new(); }
static class P { public static UI UIHelper=WrathCombo.P.UIHelper; }
class Wrapper {
 public Config config=new();
 static Plugin? P=>WrathCombo.P;
__ENABLED__
}
class SavedConfig { public string ForkConfigurationId="config"; public JObject ForkOperationReceipts=new(); }
static class Service { public static SavedConfig Configuration=new(); }
class Api {
 string observedRevision="";
 public JObject Current=new() { ["Value"]=false }; public int Saves; string instance="instance";
 JObject Snapshot()=>(JObject)Current.DeepClone();
 JObject ValidatePatch(JObject current,JObject patch)=>(JObject)patch.DeepClone();
 void Persist(JObject updated,JObject receipts) { Current=updated;Service.Configuration.ForkOperationReceipts=receipts; Saves++; }
 JObject Envelope()=>new() { ["Revision"]=SettingsTransaction.Hash(Current) }; void Notify(string state) {}
 public JObject Call(JObject request)=>Apply(request);
__APPLY__
}
""".replace('__ENABLED__',enabled).replace('__APPLY__',apply)
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp)
            (path/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup></Project>')
            (path/'Program.cs').write_text(harness)
            (path/'SettingsTransaction.cs').write_text((ROOT/'Contract/SettingsTransaction.cs').read_text())
            result=subprocess.run(['dotnet','run','--project',str(path/'Test.csproj'),'-c','Release'],capture_output=True,text=True,timeout=120)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
