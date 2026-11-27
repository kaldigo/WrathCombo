"""Exercise the Pull button's real action choice with GCD and range availability."""
from pathlib import Path
import subprocess
import tempfile
import unittest

class TankPullTests(unittest.TestCase):
    def test_manual_pull_choices(self):
        src=(Path(__file__).resolve().parents[1]/"WrathCombo/Engine/TankPull.cs").read_text(encoding="utf-8-sig")
        start=src.index("    private static uint Choose(")
        end=src.index("    internal static unsafe bool Press()",start)
        method=src[start:end]
        fixture="""
using System;
using System.Collections.Generic;
class IBattleChara {}
class Player {public static IBattleChara Object=new();}
class RoleActions {public class Tank {public const uint Provoke=7533;}}
class Fixture {
 static (uint Area,uint Ranged,uint Basic) Actions;
 static HashSet<uint> Ready=new(),Learned=new();
 static bool Near;
 static bool InArea(IBattleChara e)=>Near;
 static bool Usable(uint a,IBattleChara e)=>Ready.Contains(a);
 static bool ActionLearned(uint a)=>Learned.Contains(a);
 METHOD
 static void Check(uint result,uint expected){if(result!=expected)throw new Exception($"Expected {expected}, got {result}");}
 static void Main(){
 foreach(var actions in new[]{(7381u,24u,9u),(41u,46u,31u),(3621u,3624u,3617u),(16141u,16143u,16137u)}){
 Actions=actions;
 var enemy=new IBattleChara();
 Learned=new(){Actions.Area,Actions.Ranged,Actions.Basic,7533};
 Near=true;Ready=new(){Actions.Area,Actions.Ranged,7533};Check(Choose(enemy),Actions.Area);
 Near=false;Check(Choose(enemy),Actions.Ranged);
 Ready=new(){7533};Check(Choose(enemy),7533);
 Near=true;Check(Choose(enemy),7533);
 Ready.Clear();Check(Choose(enemy),Actions.Ranged);
 Learned.Remove(Actions.Ranged);Ready.Add(Actions.Basic);Check(Choose(enemy),Actions.Basic);
 }
 }
}
""".replace("METHOD",method)
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)
            (p/"Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (p/"Program.cs").write_text(fixture)
            subprocess.run(["dotnet","run","--project",str(p/"Test.csproj")],check=True,timeout=120)


    def test_party_and_alliance_pickup(self):
        src=(Path(__file__).resolve().parents[1]/"WrathCombo/Engine/TankPull.cs").read_text(encoding="utf-8-sig")
        methods=src[src.index("    private static unsafe bool Protected("):src.index("    // Returns true")]
        methods=methods.replace("unsafe ", "").replace("->", ".")
        fixture="""
using System;
class State {public bool InCombat=true,IsAllianceMember;}
class Job {public uint RowId;}
class IBattleChara {
 public bool IsDead,IsCasting,Boss,Hostile,Party,IsTargetable=true;public ulong GameObjectId;public System.Numerics.Vector3 Position;
 public IBattleChara? TargetObject;public Job ClassJob=new();public State State=new();
 public State Struct()=>State;public bool IsBoss()=>Boss;public bool IsHostile()=>Hostile;public bool IsInParty()=>Party;
}
class Player {public static IBattleChara Object=new(){GameObjectId=1};}
class Fixture {
METHODS
static float AreaRange()=>5;
static void Check(bool value){if(!value)throw new Exception("Tank ownership regression");}
static void Main(){
var ally=new IBattleChara{GameObjectId=2,Party=true};
var enemy=new IBattleChara{GameObjectId=3,Hostile=true,TargetObject=ally};
Check(ThreatensGroup(enemy));
ally.Party=false;ally.State.IsAllianceMember=true;Check(ThreatensGroup(enemy));
ally.State.IsAllianceMember=false;Check(!ThreatensGroup(enemy));
ally.Party=true;
foreach(uint tank in new uint[]{19,21,32,37}){ally.ClassJob.RowId=tank;Check(!ThreatensGroup(enemy));}
ally.ClassJob.RowId=24;enemy.Boss=true;Check(!ThreatensGroup(enemy));enemy.Boss=false;
enemy.IsCasting=true;Check(!ThreatensGroup(enemy));enemy.IsCasting=false;
enemy.State.InCombat=false;Check(!ThreatensGroup(enemy));enemy.State.InCombat=true;
enemy.TargetObject=Player.Object;Check(ThreatensGroup(enemy));
var selected=enemy;
var idle=new IBattleChara{GameObjectId=4,Hostile=true};idle.State.InCombat=false;
PACK_POLICY
Check(Eligible(idle));
selected.TargetObject=ally;ally.ClassJob.RowId=21;Check(!Eligible(idle));

}
}
""".replace("METHODS",methods).replace("PACK_POLICY",
            src[src.index("        bool Eligible("):src.index("        if (AccordDiagnostics.DiagnosticLog.Active")].replace("->", "."))
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)
            (p/"Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>')
            (p/"Program.cs").write_text(fixture)
            subprocess.run(["dotnet","run","--project",str(p/"Test.csproj")],check=True,timeout=120)


    def test_pull_requires_native_ready_status(self):
        src=(Path(__file__).resolve().parents[1]/"WrathCombo/Engine/TankPull.cs").read_text(encoding="utf-8-sig")
        method=src[src.index("    private static unsafe bool Usable("):src.index("    internal static uint Preview()")]
        method=method.replace("unsafe ", "").replace("->", ".")
        fixture="""
using System;
class IBattleChara {public ulong GameObjectId=1;}
enum ActionType {Action}
class ActionManager {
 public static uint Status;
 public static ActionManager Instance()=>new();
 public uint GetActionStatus(ActionType type,uint action,ulong target)=>Status;
}
class Fixture {
 static bool ActionLearned(uint action)=>true;
 static bool InActionRange(uint action,IBattleChara target)=>true;
 static bool ActionReadyForTarget(uint action,ulong target,bool recast,bool cast)=>true;
 METHOD
 static void Main(){
 var target=new IBattleChara();
 foreach(uint status in new uint[]{582,580,0}){
 ActionManager.Status=status;
 if(Usable(16143,target)!=(status==0))throw new Exception("Pull accepted an unavailable native action");
 }
 }
}
""".replace("METHOD",method)
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)
            (p/"Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (p/"Program.cs").write_text(fixture)
            subprocess.run(["dotnet","run","--project",str(p/"Test.csproj")],check=True,timeout=120)
