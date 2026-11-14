"""Exercise the combat-only early returns and automatic recipient policy from the runner."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class OutOfCombatRaiseTests(unittest.TestCase):
    def test_combat_only_dispatch(self):
        root = Path(__file__).resolve().parents[1] / "WrathCombo"
        runner = (root / "AutoRotation/AutoRotationController.cs").read_text(encoding="utf-8-sig")
        healer = (root / "Engine/HealerSelection.cs").read_text(encoding="utf-8-sig")
        def block(start):
            pos = runner.index(start)
            return runner[pos:runner.index("        }", pos) + 9]
        managed = block("if (RotationBehaviour.Preferences?.CombatOnly == true && NotInCombat)")
        native = block("if (cfg.InCombatOnly && NotInCombat && !CombatBypass)")
        dispatch = runner[runner.index("private static bool RunOutOfCombatRaise()"):runner.index("    // Note: Similar to Kardia")]
        predicate = healer[healer.index("private static bool AutomaticRaiseRecipient("):healer.index("    private static SelectionDecision? Raise(")]
        harness = """
using static Fixture;
record IGameObject(bool Party, bool Player, bool Hostile = false) {
    public bool IsInParty()=>Party; public bool IsAPlayer()=>Player; public bool IsHostile()=>Hostile;
}
class Fixture {
    public static bool NotInCombat=true, CombatBypass=false;
    public static bool InPvP()=>false;
    public static List<int> Deadtionary = [1];
    public static bool PlayerHasActionPenalty(bool b)=>false;
    public static bool CanUseResurrection(bool h)=>h;
    public static int Damage, Legacy, Managed;
    public static bool RezParty(){ Legacy++; return true; }
}
enum Job { WHM, BLU }
enum CombatRole { Healer }
static class BLU { public static bool HasHealerMimicry=>false; }
static class Player { public static Job Job=>Job.WHM; public static Actor Object=new(); }
class Actor { public bool IsCasting()=>false; public CombatRole Role=>CombatRole.Healer; }
class Pref { public bool CombatOnly; }
static class RotationBehaviour { public static Pref? Preferences=new(); public static bool? Support=true; }
static class ExperimentalSelection { public static bool ActiveHealer=true, IncludeRaise=true, RaiseNearbyPlayers; }
static class HealerSelection { public static bool ExecuteAutomaticRaise(){ Managed++; return true; } }
static class cfg {
    public static bool InCombatOnly;
    public static class DPSSettings { public static float MaxDistance=30; public static bool UnTargetAndDisableForPenalty=false; }
    public static class HealerSettings { public static bool AutoRez; }
}
static class OccultEventTargets { public static bool Active; public static int Starts; public static void EngageSelected(float range){ if(Active) Starts++; } }
class Runner {
    static void Tick() { MANAGED_GATE NATIVE_GATE Damage++; }
    DISPATCH
    PREDICATE
    static void Check(bool yes) { if(!yes) throw new Exception("Raise gate regression"); }
    static void Main() {
        foreach (bool managed in new[]{true,false}) {
            RotationBehaviour.Preferences!.CombatOnly=managed; cfg.InCombatOnly=!managed;
            ExperimentalSelection.IncludeRaise=true; cfg.HealerSettings.AutoRez=false;
            Damage=Managed=Legacy=0; Tick(); Check((Damage,Managed,Legacy)==(0,1,0));
            ExperimentalSelection.IncludeRaise=false; Tick(); Check((Damage,Managed,Legacy)==(0,1,0));
            cfg.HealerSettings.AutoRez=true; Tick(); Check((Damage,Managed,Legacy)==(0,1,1));
            Deadtionary.Clear(); Tick(); Check((Damage,Managed,Legacy)==(0,1,1)); Deadtionary.Add(1);
            NotInCombat=false; Tick(); Check(Damage==1); NotInCombat=true;
        }
        OccultEventTargets.Active=true; NotInCombat=true; cfg.InCombatOnly=true;
        Deadtionary.Clear(); Tick(); Check(OccultEventTargets.Starts==1);
        Deadtionary.Add(1); Tick(); Check(OccultEventTargets.Starts==1);
        Check(AutomaticRaiseRecipient(new(true,true)) && !AutomaticRaiseRecipient(new(false,true)));
        ExperimentalSelection.RaiseNearbyPlayers=true;
        Check(AutomaticRaiseRecipient(new(false,true)) && !AutomaticRaiseRecipient(new(false,false)) &&
              !AutomaticRaiseRecipient(new(false,true,true)));
    }
}
""".replace("MANAGED_GATE", managed).replace("NATIVE_GATE", native).replace("DISPATCH", dispatch).replace("PREDICATE", predicate)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "Replay.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path / "Program.cs").write_text(harness)
            subprocess.run(["dotnet", "run", "--project", str(path / "Replay.csproj")], check=True, timeout=120)
