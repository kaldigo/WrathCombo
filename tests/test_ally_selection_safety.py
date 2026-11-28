"""Exercise the actual ally adapter with stale objects and transition-time previews."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class AllySelectionSafetyTests(unittest.TestCase):
    def test_live_objects_transitions_and_deliberate_targets(self):
        root = Path(__file__).resolve().parents[1] / "WrathCombo"
        source = (root / "Engine/SelectionRuntime.cs").read_text()
        adapter = source[source.index("    internal static bool AllySelectionAvailable"):source.rfind("}")]
        program = r'''
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using WrathCombo.Engine.Selection;
interface IGameObject {
    bool IsValid(); nint Address {get;} int ObjectIndex {get;} ulong GameObjectId {get;}
}
class IBattleChara : IGameObject {
    public bool Valid=true,Friendly=true,IsDead,IsTargetable=true,InRange=true;
    public float Health=1,HitboxRadius=.5f; public Vector3 Position;
    public nint Address {get;set;} public int ObjectIndex {get;set;} public ulong GameObjectId {get;set;}
    public bool IsValid()=>Valid;
    public bool IsHostile()=>throw new Exception("Forbidden native nameplate query");
    public IBattleChara GameObject()=>this;
}
class IPlayerCharacter : IBattleChara {}
enum ConditionFlag {BetweenAreas,BetweenAreas51}
class Conditions {public HashSet<ConditionFlag> Flags=new(); public bool this[ConditionFlag f]=>Flags.Contains(f);}
class ObjectTable {
    public IBattleChara?[] Slots=new IBattleChara?[32]; public int Length=>Slots.Length;
    public IBattleChara? this[int index]=>Slots[index];
    public IGameObject? SearchById(ulong id)=>Slots.FirstOrDefault(a=>a?.GameObjectId==id);
}
static class Svc {
    public static ObjectTable Objects=new(); public static Conditions Condition=new();
    public static class ClientState {public static bool IsLoggedIn=true;}
}
static class GenericHelpers {public static bool Screen=true;public static bool IsScreenReady()=>Screen;}
static class Player {public static bool Available=true; public static IPlayerCharacter Object=new();}
static class ExperimentalSelection {
    public static bool PartyMouseover=true,WorldMouseover=true,Focus=true,OverridesInAutomation=false,
        AutomaticAllies=true,Fallback=false;
    public static string[] AllyOverrideOrder=["PartyMouseover","WorldMouseover","Selected","Focus"];
}
static class SimpleTarget {
    public static IBattleChara? UIMouseOverTarget,ModelMouseOverTarget,SoftTarget,HardTarget,FocusTarget;
    public static class Stack {public static IBattleChara? NearbyRaiseTarget(uint action)=>null;}
}
static class IncomingRaise {public static bool IsRaise(uint action)=>action==2;}
static class TestingApi {public static void RecordDecision(string name,object input,object result){}}
static class ActionManager {
    public static int NativeCalls;
    public static float GetActionRange(uint action)=>30;
    public static bool CanUseActionOnTarget(uint action,IBattleChara actor) {
        NativeCalls++;
        if(!Replay.AllySelectionAvailable || !actor.Valid || Svc.Objects[actor.ObjectIndex]?.Address!=actor.Address)
            throw new Exception("Native action check reached unavailable state");
        return actor.Friendly && actor.IsDead==(action==2);
    }
}
record Member(IBattleChara BattleChara);
class Replay {
    static bool automatic,Pvp; static int FactionCalls;
    static List<Member> Party=new();
    static IEnumerable<Member> GetPartyMembers()=>Party;
    static bool InPvP()=>Pvp;
    static bool TargetIsFriendly(IBattleChara actor) {
        FactionCalls++;
        if(!AllySelectionAvailable || !actor.Valid || Svc.Objects[actor.ObjectIndex]?.Address!=actor.Address)
            throw new Exception("Faction check reached unavailable state");
        return actor.Friendly;
    }
    static bool TargetLifeAllowed(uint action,IBattleChara actor)=>actor.IsDead==(action==2);
    static bool InActionRange(uint action,IBattleChara actor)=>actor.InRange;
    static float GetTargetHPPercent(IBattleChara actor)=>actor.Health*100;
ADAPTER
    static void Check(bool yes,string reason){if(!yes)throw new Exception(reason);}
    static IPlayerCharacter Add(int index,float health=1) {
        var a=new IPlayerCharacter {ObjectIndex=index,Address=100+index,GameObjectId=(ulong)index+1,Health=health};
        Svc.Objects.Slots[index]=a; return a;
    }
    static void AssertUnavailable() {
        ActionManager.NativeCalls=FactionCalls=0;
        Check(SelectAlly(1).Recipient==null,"Preview selected during transition");
        Check(CurrentFriendlyActor(Party[0].BattleChara)==null,"Transition exposed an ally");
        Check(ActionManager.NativeCalls==0&&FactionCalls==0,"Transition reached native state");
    }
    static void Main() {
        Player.Object=Add(0);var tank=Add(1,.2f);var other=Add(2,.5f);Party.Add(new(tank));Party.Add(new(other));
        foreach(bool auto in new[]{false,true}) {
            automatic=auto;Check(SelectAlly(1).Recipient==tank.GameObjectId,"Party heal priority changed");
            Svc.Condition.Flags.Add(ConditionFlag.BetweenAreas);AssertUnavailable();Svc.Condition.Flags.Clear();
            Svc.Condition.Flags.Add(ConditionFlag.BetweenAreas51);AssertUnavailable();Svc.Condition.Flags.Clear();
            GenericHelpers.Screen=false;AssertUnavailable();GenericHelpers.Screen=true;
            Svc.ClientState.IsLoggedIn=false;AssertUnavailable();Svc.ClientState.IsLoggedIn=true;
            Player.Available=false;AssertUnavailable();Player.Available=true;
            Player.Object.Valid=false;AssertUnavailable();Player.Object.Valid=true;
            Player.Object.GameObjectId=0;AssertUnavailable();Player.Object.GameObjectId=1;
            Svc.Objects.Slots[0]=null;AssertUnavailable();Svc.Objects.Slots[0]=Player.Object;
        }
        automatic=false;
        var stale=new IPlayerCharacter {ObjectIndex=1,Address=999,GameObjectId=tank.GameObjectId};
        Check(CurrentFriendlyActor(stale)==null,"Stale address accepted");
        stale.Address=tank.Address;stale.GameObjectId=500;
        Check(CurrentFriendlyActor(stale)==null,"Reused slot accepted old identity");
        stale.GameObjectId=tank.GameObjectId;stale.Valid=false;
        Check(CurrentFriendlyActor(stale)==null,"Invalid wrapper accepted");
        stale.Valid=true;stale.ObjectIndex=100;
        Check(CurrentFriendlyActor(stale)==null,"Out-of-table index accepted");
        var dead=Add(3,.0f);dead.IsDead=true;
        Check(SelectAlly(2,dead.GameObjectId).Recipient==dead.GameObjectId,"Dead-player raise was lost");
        Check(SelectAlly(1,dead.GameObjectId).Recipient==null,"Heal redirected away from explicit dead player");
        ExperimentalSelection.Fallback=true;
        Check(SelectAlly(1,999).Recipient==null,"Missing explicit recipient fell back to another ally");
        ExperimentalSelection.Fallback=false;
        SimpleTarget.UIMouseOverTarget=other;
        Check(SelectAlly(1).Recipient==other.GameObjectId,"Party mouseover override changed");
        other.InRange=false;Check(SelectAlly(1).Recipient==null,"Invalid override ignored fallback opt-out");
        ExperimentalSelection.Fallback=true;Check(SelectAlly(1).Recipient==tank.GameObjectId,"Configured fallback failed");
        other.InRange=true;ExperimentalSelection.Fallback=false;SimpleTarget.UIMouseOverTarget=null;
        SimpleTarget.ModelMouseOverTarget=other;
        Check(SelectAlly(1).Recipient==other.GameObjectId,"World mouseover override changed");
        SimpleTarget.ModelMouseOverTarget=null;
        automatic=true;SimpleTarget.UIMouseOverTarget=other;
        Check(SelectAlly(1).Recipient==tank.GameObjectId,"Automation ignored override opt-out");
        ExperimentalSelection.OverridesInAutomation=true;
        Check(SelectAlly(1).Recipient==other.GameObjectId,"Automation override opt-in failed");
        automatic=false;SimpleTarget.UIMouseOverTarget=null;
        var enemy=new IBattleChara {ObjectIndex=4,Address=104,GameObjectId=5,Friendly=false};Svc.Objects.Slots[4]=enemy;
        var npc=new IBattleChara {ObjectIndex=5,Address=105,GameObjectId=6,Friendly=true};Svc.Objects.Slots[5]=npc;
        SimpleTarget.SoftTarget=enemy;SimpleTarget.HardTarget=npc;
        Check(SelectAlly(1).Recipient==npc.GameObjectId,"Hostile soft target blocked friendly NPC hard target");
        SimpleTarget.SoftTarget=null;SimpleTarget.HardTarget=enemy;
        Check(SelectAlly(1).Recipient==tank.GameObjectId,"Hostile selected target blocked party heal");
        SimpleTarget.HardTarget=null;SimpleTarget.FocusTarget=other;
        Check(SelectAlly(1).Recipient==other.GameObjectId,"Focus override changed");
        SimpleTarget.FocusTarget=null;Pvp=true;other.Friendly=false;
        Check(CurrentFriendlyActor(other)==null,"PvP enemy player classified as friendly");
        Check(SelectAlly(1).Recipient==tank.GameObjectId,"PvP party heal changed");
        Pvp=false;other.Friendly=true;
        Check(SelectAlly(1).Recipient==tank.GameObjectId,"Selection failed to resume after transition");
    }
}
'''.replace("ADAPTER", adapter)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "Program.cs").write_text(program)
            for name in ("TargetSelection.cs", "TargetOrder.cs"):
                (path / name).write_bytes((root / "Engine/Selection" / name).read_bytes())
            (path / "Replay.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup></Project>')
            subprocess.run(["dotnet", "run", "--project", str(path / "Replay.csproj")], check=True, timeout=120)
