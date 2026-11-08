from pathlib import Path
import subprocess
import tempfile
import unittest

class LimitBreakTests(unittest.TestCase):
    def test_manual_priority(self):
        root = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
            (path/'ManualLimitBreak.cs').write_bytes((root/'WrathCombo/Engine/ManualLimitBreak.cs').read_bytes())
            hook = (root/'WrathCombo/Data/ActionWatching.cs').read_text(encoding='utf-8-sig')
            pending = hook[hook.index('    private sealed record PendingLimitBreak'):hook.index('    /// <summary> Handles logic when an action is used.')]
            (path/'Program.cs').write_text(r'''using FFXIVClientStructs.FFXIV.Client.Game;
using RotationInterop;
using Clock = RotationInterop.Environment;
unsafe {
    var am = ActionManager.Instance();
    void Check(bool ok, string reason) { if(!ok) throw new Exception(reason); }
    void Press() => ManualLimitBreak.Input(am, ActionType.GeneralAction, 3, false);
    void Queue(uint id, ulong target) { am->ActionQueued=true; am->QueuedActionType=ActionType.Action; am->QueuedActionId=id; am->QueuedTargetId.Id=target; }
    PendingTest.Run();
    Clock.TickCount64=10000;
    using (ManualLimitBreak.TrackRunner()) Queue(34606,42);
    Press(); Check(!am->ActionQueued && ManualLimitBreak.Waiting(),"Viper queue beat manual LB");
    am->CastActionType=ActionType.Action; am->CastActionId=201;
    Clock.TickCount64+=2500; Check(ManualLimitBreak.Waiting(),"cast lost protection after input window");
    am->CastActionId=0;am->AnimationLock=1.5f;
    Clock.TickCount64+=800;Check(ManualLimitBreak.Waiting(),"animation lock lost protection");
    am->AnimationLock=0;Clock.TickCount64+=1500;Check(!ManualLimitBreak.Waiting(),"failed to resume");
    Queue(34606,42); Press();Check(am->ActionQueued,"cleared manual queue");
    am->ActionQueued=false;
    using (ManualLimitBreak.TrackRunner()) Queue(34606,42);
    Queue(34606,99);Press();Check(am->ActionQueued,"cleared a different recipient");
    am->ActionQueued=false;
    Clock.TickCount64+=2000;Check(!ManualLimitBreak.Waiting(),"rejected input paused indefinitely");
    Press();am->AreaTargetingActionType=ActionType.Action;am->AreaTargetingActionId=201;
    Clock.TickCount64+=6000;Check(ManualLimitBreak.Waiting(),"ground placement timed out");
    am->AreaTargetingActionId=0;Clock.TickCount64+=700;Check(!ManualLimitBreak.Waiting(),"canceled ground target never resumed");
    Queue(201,42);Clock.TickCount64+=2000;Check(ManualLimitBreak.Waiting(),"queued LB lost protection");
    am->ActionQueued=false;Clock.TickCount64+=2000;Check(!ManualLimitBreak.Waiting(),"abandoned queue did not recover");
    ManualLimitBreak.Input(am,ActionType.Action,777,false);Check(ManualLimitBreak.Waiting(),"direct PvP LB bypassed input guard");
    Clock.TickCount64+=2000;Check(!ManualLimitBreak.Waiting(),"direct input stuck");
    ManualLimitBreak.Input(am,ActionType.Action,3,false);Check(!ManualLimitBreak.Waiting(),"ordinary action 3 mistaken for general LB");
    using (ManualLimitBreak.TrackRunner()) {ManualLimitBreak.Input(am,ActionType.GeneralAction,3,false);Check(!ManualLimitBreak.Waiting(),"runner input mistaken for manual");}
}
static unsafe class PendingTest {
    static class Environment { public static long TickCount64; }
    record Actor(ulong GameObjectId); static class Player { public static Actor Object = new(1); public static bool IsDead; }
    static class Svc { public static class ClientState { public static uint TerritoryType = 2; }
        public static class Targets { public static Actor Target = new(42); } }
    static bool InCombat()=>true;
    static int LimitBreakLevel=2;
    static uint LimitBreakAction=201;
    static class Engine { public static class AutomationOwnership { public static IDisposable? QueuedExternalScope()=>null; } }
    static FakeHook UseActionHook=new();
    class FakeHook {
        public int Calls;
        public bool Original(ActionManager* am,ActionType type,uint id,ulong target,uint extra,ActionManager.UseActionMode mode,uint route,bool* area) {
            if(id!=201 || target!=42) throw new Exception("LB changed action or target");
            return ++Calls == 2;
        }
    }
PENDING
    public static void Run(){
        var am=ActionManager.Instance();
        void Click()=>pendingLimitBreak=new(201,42,42,1,2,0,0,Environment.TickCount64+1800);
        Click(); am->AnimationLock=0.5f; TickManualLimitBreak();
        if(UseActionHook.Calls!=0 || pendingLimitBreak==null) throw new Exception("Locked click was lost");
        am->AnimationLock=0; TickManualLimitBreak(); TickManualLimitBreak();
        if(UseActionHook.Calls!=1 || pendingLimitBreak==null) throw new Exception("Retry throttle failed");
        Environment.TickCount64+=200; TickManualLimitBreak(); TickManualLimitBreak();
        if(UseActionHook.Calls!=2 || pendingLimitBreak!=null) throw new Exception("Accepted click replayed");
        Click(); Svc.Targets.Target=new(99); TickManualLimitBreak(); Svc.Targets.Target=new(42);
        if(pendingLimitBreak!=null) throw new Exception("Stale target retained");
        Click(); Environment.TickCount64+=1801; TickManualLimitBreak();
        if(pendingLimitBreak!=null || UseActionHook.Calls!=2) throw new Exception("Expired LB fired");
    }
}
namespace RotationInterop { static class Environment { public static long TickCount64; } }
namespace AccordDiagnostics { static class DiagnosticLog { public static void Event(string k,Func<object> f){} } }
namespace Lumina.Excel.Sheets { class Action {public Category ActionCategory;} struct Category {public uint RowId;} }
namespace ECommons.DalamudServices {
 static class Svc {public static Data Data=new();}
 class Data {public Sheet GetExcelSheet<T>()=>new();}
 class Sheet {public bool TryGetRow(uint id,out Lumina.Excel.Sheets.Action row){row=new(){ActionCategory=new(){RowId=id==201?9u:id==777?15u:2u}};return true;}}
}
namespace FFXIVClientStructs.FFXIV.Client.Game {
 enum ActionType { Action=1, GeneralAction=5 }
 struct Target {public ulong Id;}
 unsafe struct ActionManager {
   public enum UseActionMode {None}
   public uint GetActionStatus(ActionType type,uint action,ulong target)=>0;
   static ActionManager* pointer=(ActionManager*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)sizeof(ActionManager));
   public static ActionManager* Instance()=>pointer;
   public ActionType QueuedActionType,CastActionType,AreaTargetingActionType;
   public uint QueuedActionId,CastActionId,AreaTargetingActionId;
   public Target QueuedTargetId;public bool ActionQueued;public float AnimationLock;
 }
}
'''.replace('PENDING',pending))
            result = subprocess.run(['dotnet','run','--project',str(path/'Test.csproj')],capture_output=True,text=True,timeout=120)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
