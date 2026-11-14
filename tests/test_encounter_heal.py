"""Replay actual CE triage predicates with deterministic game boundaries."""
import pathlib
import subprocess
import tempfile
import unittest


class EncounterHealTests(unittest.TestCase):
    def test_patient_and_party_rules(self):
        root = pathlib.Path(__file__).resolve().parents[1] / 'WrathCombo'
        src = (root / 'Engine/OccultEventTargets.cs').read_text()
        body = src[src.index('    private const uint HoofingIt'):src.index('    internal static SelectionDecision? HealPatient')]
        program = """
using static Runtime;
using System.Numerics;
class IBattleChara {
 public bool IsDead, IsTargetable=true, Marked=true, Party, Visible=true, Legal=true, Hostile, PlayerType=true;
 public uint CurrentHp=40, BaseId=1;
 public ulong GameObjectId=2;
 public Vector3 Position;
 public bool IsAPlayer()=>PlayerType;
 public bool IsInParty()=>Party;
 public bool IsHostile()=>Hostile;
 public bool HasStatus(uint id,bool any)=>Marked && id==1778;
 public IBattleChara GameObject()=>this;
}
class IBattleNpc:IBattleChara {}
record Member(IBattleChara BattleChara);
static class Player { public static IBattleChara Object=new(){GameObjectId=1,CurrentHp=100}; public static Vector3 Position=>Object.Position; }
static class Svc { public static class Objects { public static List<IBattleChara> GetBattleCharas()=>[new IBattleNpc()]; } }
static class ExperimentalSelection { public static bool AutomaticAllies=true, HealEncounterPlayers; }
static class ActionManager { public static bool CanUseActionOnTarget(uint spell,IBattleChara a)=>a.Legal; }
static class Runtime {
 public static DateTime frame=DateTime.UtcNow; public static ushort encounter=1; public static uint duty=1;
 public static bool Active()=>true;
 public static bool InEncounterArena(uint d,uint id,Vector3 p,Vector3 e)=>Vector3.Distance(p,e)<25;
 public static bool InActionRange(uint s,IBattleChara a)=>true;
 public static bool IsInLineOfSight(IBattleChara a)=>a.Visible;
 public static float GetTargetHPPercent(IBattleChara a,bool forceUsePending)=>a.CurrentHp;
 public static IBattleChara Ally=new(){CurrentHp=90};
 public static List<Member> GetPartyMembers()=>[new(Ally),new(Player.Object)];
}
class Replay {
BODY
 static void Main(){
  var other=new IBattleChara();
  void Check(bool yes){if(PatientStillEligible(120,other)!=yes)throw new Exception();}
  Check(false); ExperimentalSelection.HealEncounterPlayers=true; Check(true);
  Ally.CurrentHp=70; Check(false); Ally.CurrentHp=71; Check(true);
  other.CurrentHp=50; Check(false); other.CurrentHp=49; Check(true);
  other.IsDead=true; Check(false); other.IsDead=false;
  other.Party=true; Check(false); other.Party=false; other.Marked=false; Check(false); other.Marked=true;
  other.Position=new(100,0,0); Check(false); other.Position=default;
  other.Visible=false; Check(false); other.Visible=true; other.Legal=false; Check(false); other.Legal=true;
  Player.Object.Marked=false; Check(false); Player.Object.Marked=true;
  encounter=0; Check(false);
 }
}
""".replace("BODY", body)
        with tempfile.TemporaryDirectory() as temp:
            path = pathlib.Path(temp)
            (path / 'Replay.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path / 'Program.cs').write_text(program)
            subprocess.run(['dotnet','run','--project',str(path / 'Replay.csproj')],check=True,timeout=120)
