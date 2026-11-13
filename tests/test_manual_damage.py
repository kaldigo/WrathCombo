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
        gunbreaker = (root / "Combos/PvE/GNB/GNB_Selection.cs").read_text()
        display = gunbreaker[gunbreaker.index("    private static uint DisplayPlannedGcd"):gunbreaker.index("    private static uint PlanTestingGcd")]
        providers = (root / "Combos/PvE/GNB/GNB.cs").read_text()
        for starter in ("KeenEdge", "DemonSlice"):
            self.assertEqual(2, providers.count(f"return DisplayPlannedGcd(actionID, plannedGcd, {starter});"))
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
static class Service { public static class ActionReplacer { public static (uint,uint) Icon; public static void RememberIdleIcon(uint i,uint a)=>Icon=(i,a); } }
static class Replay {
    const int Generation=1;
    static bool automatic, CountdownActive=false;
    static bool InPvP()=>false;
    static bool combat=true; static bool InCombat()=>combat;
    static bool ActionLearned(uint a)=>true;
    static IBattleChara? ManualEnemyForAction(uint action)=>SimpleTarget.HardTarget;
    static ulong recipient;
    static uint Publish(uint i,Decision d){recipient=d.target;return d.action;}
    static uint Resolve(uint action,uint input=1000016,bool damageRotation=false) {
        var row=new {CanTargetSelf=action==2,CanTargetHostile=action is 1 or 16495 or 25783};
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
    static class SelectionRuntime {public static bool Automatic => automatic;}
    static class ActionRoutes {public static bool Allowed(uint id)=>true;}
    DISPLAY
    static void Main(){
        SimpleTarget.HardTarget=new(99,true);
        if(Resolve(1)!=1 || recipient!=99) throw new Exception("Lost explicit target");
        automatic=true;
        if(Resolve(1)!=All.Cease) throw new Exception("Automatic fallback acquired a target");
        automatic=false; SimpleTarget.HardTarget=null;
        if(Resolve(2)!=2 || recipient!=1) throw new Exception("Targetless self AoE was blocked");
        if(Resolve(1)!=All.Cease) throw new Exception("Invented a hostile target");
        combat=false;
        foreach (var (input, action) in new (uint,uint)[] { (97,16495), (106,25783) })
        {
            foreach (var selected in new IBattleChara?[] { null, Player.Object, new(88,false) })
            {
                SimpleTarget.HardTarget=selected; Service.ActionReplacer.Icon=default;
                if(Resolve(action,input,true)!=All.Cease || Service.ActionReplacer.Icon!=(input,action))
                    throw new Exception("Bard native rotation lost its town icon or acquired a recipient");
            }
            CountdownActive=true; Service.ActionReplacer.Icon=default; Resolve(action,input,true); CountdownActive=false;
            if(Service.ActionReplacer.Icon!=default) throw new Exception("Countdown hold disguised as idle");
        }
        SimpleTarget.HardTarget=null; Service.ActionReplacer.Icon=default;
        if(DisplayPlannedGcd(1000016,All.Cease,16137)!=All.Cease || Service.ActionReplacer.Icon!=(1000016u,16137u))
            throw new Exception("Combined Gunbreaker lost its idle icon or made the blocked press executable");
        CountdownActive=true; Service.ActionReplacer.Icon=default;
        DisplayPlannedGcd(1000016,All.Cease,16137); CountdownActive=false;
        if(Service.ActionReplacer.Icon!=default) throw new Exception("Countdown Cease hidden");
        if(FinishOrHold(TechnicalFinish4)!=All.Cease) throw new Exception("Lost Dancer hold");
        remaining=1;
        if(FinishOrHold(TechnicalFinish4)!=TechnicalFinish4) throw new Exception("Lost expiring finish");
    }
}
""".replace("FALLBACK", fallback).replace("FINISH", finish).replace("DISPLAY", display)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "Program.cs").write_text(program)
            (path / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
            result = subprocess.run(["dotnet", "run", "--project", str(path / "Test.csproj")], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
