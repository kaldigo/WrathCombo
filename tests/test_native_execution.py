"""Exercise production native snapshots against inactive/stale/cancelled state."""
from pathlib import Path
import subprocess
import tempfile
import unittest
ROOT = Path(__file__).resolve().parents[1]
class NativeExecutionTests(unittest.TestCase):
    def test_authoritative_native_snapshot_and_attempt_ownership(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)
            (path/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup></Project>')
            (path/'NativeExecution.cs').write_bytes((ROOT/'WrathCombo/Engine/NativeExecution.cs').read_bytes())
            for name in ['ActionReadiness','CastLifecycle']:
                (path/(name+'.cs')).write_bytes((ROOT/('WrathCombo/Engine/Selection/'+name+'.cs')).read_bytes())
            (path/'Program.cs').write_text(HARNESS,encoding='utf-8')
            result=subprocess.run(['dotnet','run','--project',str(path/'Test.csproj'),'-c','Release','-p:UseSharedCompilation=false','-m:1','-nr:false'],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
            self.assertIn('Native readiness and ownership passed',result.stdout)
HARNESS=r"""
using WrathCombo.Engine;
using FFXIVClientStructs.FFXIV.Client.Game;
unsafe class Program {
 static void Check(bool ok,string why) { if(!ok) throw new Exception(why); }
 static void Main() {
  var am=ActionManager.Instance(); var group=am->GetRecastGroupDetail(57);
  group->Total=2.5f; group->Elapsed=.5f; group->IsActive=false;
  ActionManager.Total=2.5f; ActionManager.Elapsed=.5f;
  Check(NativeExecution.Capture(true).GcdRemaining==0 && NativeExecution.CanSubmit(10),"inactive recast must ignore retained total/elapsed");
  group->IsActive=true;
  Check(!NativeExecution.CanSubmit(10) && NativeExecution.CanSubmit(11),"real GCD must not block independent native-legal ability");
  am->CastActionId=10; am->CastTimeTotal=2;am->CastTimeElapsed=.1f;
  Check(!NativeExecution.CanSubmit(11),"real cast cannot be interrupted to weave");
  am->CastActionId=0;group->IsActive=false;am->AnimationLock=.3f;
  Check(!NativeExecution.CanSubmit(10),"real animation lock remains authoritative after cancel");
  am->AnimationLock=0; ActionManager.Total=0;
  Check(NativeExecution.Capture(true).GcdTotal==2.5f,"unsupported native reference falls back to actual recast group");
  using(NativeExecution.BeginAttempt()) {
   Check(!NativeExecution.AttemptSubmitted,"attempt starts empty");
   NativeExecution.ObserveResult(false);Check(!NativeExecution.AttemptSubmitted,"rejection must not consume attempt");
   NativeExecution.ObserveResult(true);Check(NativeExecution.AttemptSubmitted,"accepted action owns this attempt");
   NativeExecution.ObserveResult(false);Check(NativeExecution.AttemptSubmitted,"nested rejection must not erase earlier acceptance");
  }
  Check(!NativeExecution.AttemptSubmitted,"ownership must not leak into the next pass");
  am->ActionQueued=true;am->QueuedActionId=10;am->QueuedTargetId.Id=42;
  Check(NativeExecution.Accepted(false,10,42) && !NativeExecution.Accepted(false,10,43),"queued submission retains its actual recipient");
  am->ActionQueued=false;
  Check(!NativeExecution.Accepted(false,10,42),"stale queue fields are not an accepted submission");
  Check(NativeExecution.BaseAvailable(10,42) && !ActionManager.CheckedRecast && !ActionManager.CheckedCast,"base viability ignores only transient recast/cast locks");
  Console.WriteLine("Native readiness and ownership passed");
 }
}
namespace ECommons.DalamudServices {
 static class Svc { public static FrameworkState Framework=new(); public class FrameworkState { public DateTime LastUpdateUTC=DateTime.UtcNow; } }
}
namespace WrathCombo.Combos.PvE { static class MCH { public const uint HeatedSplitShot=7411; } }
namespace WrathCombo.Services {
 static class Service { public static Config Configuration=new(); public class Config { public Rotation RotationConfig=new(); } public class Rotation { public float QueueWindow=.5f; } }
}
namespace WrathCombo.Data {
 static class ActionWatching { public record Category(uint RowId); public record Row(Category ActionCategory); public static Dictionary<uint,Row> ActionSheet=new(){[10]=new(new(2)),[11]=new(new(4))}; }
}
namespace AccordDiagnostics { static class DiagnosticLog { public static void Event(string k,Func<object> v) {} } }
namespace WrathCombo.Engine { static class SelectionRuntime { public static void Interrupt(uint action,bool releaseOnly=false) {} } }
namespace FFXIVClientStructs.FFXIV.Client.Game {
 enum ActionType { Action }
 unsafe struct RecastDetail { public bool IsActive; public float Total,Elapsed; }
 struct TargetId { public ulong Id; }
 unsafe struct ActionManager {
  static ActionManager* singleton=(ActionManager*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)sizeof(ActionManager));
  static RecastDetail* group=(RecastDetail*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)sizeof(RecastDetail));
  public static float Total,Elapsed;public static bool CheckedRecast,CheckedCast;
  public bool ActionQueued; public uint QueuedActionId,CastActionId;public TargetId QueuedTargetId;public float CastTimeTotal,CastTimeElapsed,AnimationLock;
  public static ActionManager* Instance()=>singleton;
  public RecastDetail* GetRecastGroupDetail(int groupId)=>group;
  public float GetRecastTime(ActionType t,uint id)=>Total;
  public float GetRecastTimeElapsed(ActionType t,uint id)=>Elapsed;
  public static uint GetAdjustedCastTime(ActionType t,uint id)=>2000;
  public uint GetActionStatus(ActionType t,uint id,ulong target,bool checkRecastActive,bool checkCastingActive) { CheckedRecast=checkRecastActive;CheckedCast=checkCastingActive;return 0; }
 }
}
"""
