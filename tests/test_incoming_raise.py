"""Replay visible casts changing between a raise preview, Swiftcast and Raise."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class IncomingRaiseTests(unittest.TestCase):
    def test_cast_changes_between_dispatches(self):
        root = Path(__file__).resolve().parents[1]
        source = (root / "WrathCombo/Engine/IncomingRaise.cs").read_text(encoding="utf-8-sig")
        source = source[source.index("internal static class IncomingRaise"):]
        harness = """
using System;
using System.Collections.Generic;
using IPlayerCharacter = Caster;
using IGameObject = Actor;
using IBattleChara = Actor;
class Actor {
    public ulong GameObjectId; public bool IsDead, HasRaiseStatus; public bool IsTargetable = true;
    public bool HasStatus(int id, bool any) => false;
}
class Caster : Actor {
    public bool Hostile, IsCasting; public uint CastActionId; public ulong CastTargetObjectId;
    public bool IsHostile() => Hostile;
}
class Objects : List<Actor> { public Actor? SearchById(ulong id) => Find(a=>a.GameObjectId==id); }
class Svc {public static Objects Objects=new(); public class Framework {public static DateTime LastUpdateUTC=DateTime.UtcNow;} }
class Player {public static Caster Object=new(){GameObjectId=1};}
enum ActionType { Action }
unsafe struct ActionManager {
    public static ActionManager* Ptr; public static ActionManager* Instance()=>Ptr;
    public bool UseAction(ActionType t,uint action,ulong target) {
        if(IncomingRaise.DispatchPatient!=5) throw new Exception("Lost patient during submission");
        return true;
    }
}
class AccordDiagnostics {public class DiagnosticLog {public static bool Reject(string text)=>false;} }
class All {public class Buffs {public const int Raised=148;}}
class WHM {public const uint Raise=125;} class SCH {public const uint Resurrection=173;}
class AST {public const uint Ascend=3603;} class SGE {public const uint Egeiro=24287;}
class RDM {public const uint Verraise=7523;} class BLU {public const uint AngelWhisper=18317;}
class Variant {public const uint Raise=29731,Raise2=29734;}
class OccultCrescent {public static uint OccultRaise=49070,Revive=41634;}
class Bozja {public const uint LostArise=20730,LostSacrifice=22345;}
unsafe class Replay {
    static void Check(bool value){if(!value)throw new Exception("Incoming raise regression");}
    static void Main(){
        var am=new ActionManager(); ActionManager.Ptr=&am;
        var patient=new Actor{GameObjectId=5,IsDead=true};
        var caster=new Caster{GameObjectId=8,CastTargetObjectId=5};
        Svc.Objects.AddRange([patient,caster,Player.Object]);
        foreach(uint spell in new uint[]{125,173,3603,24287,7523,18317,29731,29734,49070,41634,20730,22345,12996,14481}){
            caster.CastActionId=spell; caster.IsCasting=false;
            Check(IncomingRaise.Available(patient,true));
            caster.IsCasting=true; Check(!IncomingRaise.Submit(7561,5,1));
            caster.IsCasting=false; Check(IncomingRaise.Submit(7561,5,1));
            caster.IsCasting=true; Check(!IncomingRaise.Submit(125,5,5));
            caster.CastTargetObjectId=6; Check(IncomingRaise.Submit(125,5,5)); caster.CastTargetObjectId=5;
            caster.Hostile=true; Check(IncomingRaise.Available(patient,true)); caster.Hostile=false;
        }
        caster.CastActionId=120; Check(IncomingRaise.Available(patient,true));
        Player.Object.IsCasting=true;Player.Object.CastActionId=125;Player.Object.CastTargetObjectId=5;
        Check(IncomingRaise.Available(patient,true));
        patient.HasRaiseStatus=true;Check(!IncomingRaise.Submit(125,5,5));
        patient.HasRaiseStatus=false;patient.IsDead=false;Check(!IncomingRaise.Submit(125,5,5));
        Check(IncomingRaise.DispatchPatient==0);
    }
}
"""
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp)
            (path / "Replay.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup></Project>')
            (path / "Program.cs").write_text(harness + source)
            subprocess.run(["dotnet", "run", "--project", str(path / "Replay.csproj")], check=True, timeout=120)
