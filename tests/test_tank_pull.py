from pathlib import Path
import subprocess
import tempfile
import unittest


class TankPullTests(unittest.TestCase):
    def test_runner_pull_policy(self):
        root = Path(__file__).resolve().parents[1] / "WrathCombo"
        runner = (root / "AutoRotation/AutoRotationController.cs").read_text()
        guard = runner[runner.index("    internal static bool WaitForPartyTank()"):runner.index("    private static bool ProcessAutoActions")]
        self.assertIn("if (!canHeal && WaitForPartyTank()) return false;", runner)
        event = (root / "Engine/OccultEventTargets.cs").read_text().split("internal static unsafe void EngageSelected")[1]
        self.assertLess(event.index("WaitForPartyTank()"), event.index("AutoAttackState.Set(true)"))
        self.assertLess(event.index("WaitForPartyTank()"), event.index("ExecuteAutomatic(decision)"))
        harness = """
using static World;
enum CombatRole { Tank, Healer, DPS }
class State { public bool InCombat; }
class IBattleChara {
    public bool IsDead, IsTargetable=true;
    public CombatRole Role;
    public State State=new();
    public State Struct()=>State;
}
record Member(IBattleChara? GameObject);
static class Player { public static IBattleChara Object=new(){Role=CombatRole.Healer}; }
static class Svc { public static List<Member> Party=[]; }
static class World {
    public static bool Pvp, Fighting;
    public static bool InPvP()=>Pvp;
    public static bool InCombat()=>Fighting;
}
namespace AccordDiagnostics { static class DiagnosticLog { public static void Exit(string reason){} } }
class Test {
GUARD
    static void Check(bool expected){ if(WaitForPartyTank()!=expected) throw new Exception(); }
    static void Main(){
        Check(false);
        var tank=new IBattleChara(){Role=CombatRole.Tank};
        Svc.Party.Add(new(null)); Svc.Party.Add(new(Player.Object)); Check(false);
        Svc.Party.Add(new(tank)); Check(true);
        Fighting=true; Check(false); Fighting=false;
        tank.State.InCombat=true; Check(false); tank.State.InCombat=false;
        tank.IsDead=true; Check(false); tank.IsDead=false;
        Player.Object.Role=CombatRole.Tank; Check(false);
        Player.Object.Role=CombatRole.DPS; Pvp=true; Check(false);
    }
}
""".replace("GUARD", guard.replace("Struct()->", "Struct()."))
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp)
            (path / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path / "Program.cs").write_text(harness)
            subprocess.run(["dotnet", "run", "--project", str(path / "Test.csproj")], check=True, timeout=120)
