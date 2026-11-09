"""Run the manual damage fallback and Dancer's intentional hold with game objects stubbed."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class ManualDamageTests(unittest.TestCase):
    def test_native_recipient_and_dance_hold(self):
        root = Path(__file__).resolve().parents[1] / "WrathCombo"
        runtime = (root / "Engine/SelectionRuntime.cs").read_text()
        fallback = runtime[runtime.index("            bool centered = row.CanTargetSelf"):
                           runtime.index("        ulong? evaluatedEnemy = null;")]
        dancer = (root / "Combos/PvE/DNC/DNC_Helper.cs").read_text()
        finish = dancer[dancer.index("    private static uint FinishOrHold"):dancer.index("    // The native replacement is Improvised Finish")]
        program = """
record IBattleChara(ulong GameObjectId, bool hostile, bool IsDead=false, bool IsTargetable=true) {
    public bool IsHostile()=>hostile;
    public IBattleChara GameObject()=>this;
}
record Decision(uint action,uint gcd,ulong target,int prep,string reason,long now,int generation,bool ManualDamage);
static class Player { public static IBattleChara Object=new(1,false); }
static class SimpleTarget { public static IBattleChara? HardTarget; }
static class ActionManager {
    public static int GetActionRange(uint action)=>action==2?0:3;
    public static bool CanUseActionOnTarget(uint action,IBattleChara target)=>true;
}
static class PveIntentRouting {public static bool IsDamageButton(uint input)=>input==1000016;}
namespace Combos.PvE { static class All { public const uint Cease=1000004; } }
static class Service { public static class ActionReplacer { public static void RememberIdleIcon(uint i,uint a){} } }
static class Replay {
    const int Generation=1;
    static bool automatic, CountdownActive=false;
    static bool InPvP()=>false;
    static bool InCombat()=>true;
    static bool ActionLearned(uint a)=>true;
    static IBattleChara? ManualEnemyForAction(uint action)=>SimpleTarget.HardTarget;
    static ulong recipient;
    static uint Publish(uint i,Decision d){recipient=d.target;return d.action;}
    static uint Resolve(uint action,uint input=1000016) {
        var row=new {CanTargetSelf=action==2,CanTargetHostile=action==1};
        {
FALLBACK
    }
    static class Preset { public const int DNC_ST_BlockFinishes=1; }
    static class Buffs { public const int StandardStep=1,TechnicalStep=2; }
    static class All { public const uint Cease=Combos.PvE.All.Cease; }
    static class LocalPlayer { public static StatusData Status(int buff)=>new(); }
    class StatusData {public float RemainingTimeOrZero()=>remaining;}
    static float remaining=10; const float GCD=2.5f;
    const uint StandardFinish2=10, TechnicalFinish4=11;
    static bool EnemyIn15Yalms=false;
    static bool IsNotEnabled(int preset)=>false;
FINISH
    static void Main(){
        SimpleTarget.HardTarget=new(99,true);
        if(Resolve(1)!=1 || recipient!=99) throw new Exception("Lost explicit target");
        automatic=true;
        if(Resolve(1)!=All.Cease) throw new Exception("Automatic fallback acquired a target");
        automatic=false; SimpleTarget.HardTarget=null;
        if(Resolve(2)!=2 || recipient!=1) throw new Exception("Targetless self AoE was blocked");
        if(Resolve(1)!=All.Cease) throw new Exception("Invented a hostile target");
        if(FinishOrHold(TechnicalFinish4)!=All.Cease) throw new Exception("Lost Dancer hold");
        remaining=1;
        if(FinishOrHold(TechnicalFinish4)!=TechnicalFinish4) throw new Exception("Lost expiring finish");
    }
}
""".replace("FALLBACK", fallback).replace("FINISH", finish)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "Program.cs").write_text(program)
            (path / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
            result = subprocess.run(["dotnet", "run", "--project", str(path / "Test.csproj")], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
