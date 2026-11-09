"""Replay configured hostile-target ordering through Wrath's final press override."""
import itertools
from pathlib import Path
import subprocess
import tempfile
import unittest


class EnemyPriorityTests(unittest.TestCase):
    def test_manual_preview_and_delivery(self):
        root = Path(__file__).resolve().parents[1] / "WrathCombo"
        runtime = (root / "Engine/SelectionRuntime.cs").read_text()
        order = runtime[runtime.index("    private static ImmutableArray<TargetOverride> EnemyOverrides"):
                        runtime.index("    private static unsafe IBattleChara? ManualEnemyForAction")]
        native = (root / "Data/ActionWatching.cs").read_text()
        dispatch = native[native.index("                if (testingRecipient is { } boundRecipient)"):
                          native.index("                // If retargeting kicks in")]
        program = """
using System.Collections.Immutable;
using System.Numerics;
using WrathCombo.Engine.Selection;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
namespace FFXIVClientStructs.FFXIV.Client.Game.Object { struct GameObject {} }
record IBattleChara(ulong GameObjectId) {
    public nint Address=>1; public bool IsHostile()=>true;
    public bool IsTargetable=>true; public bool IsDead=>false;
}
static class SimpleTarget {public static IBattleChara ModelMouseOverTarget=new(2),FocusTarget=new(3);}
static class Svc {public static class Targets {public static IBattleChara Target=new(1);}}
static class ExperimentalSelection {
    public static bool OverridesInAutomation=false, AutomaticEnemies=true, EnemyMouseover=true, EnemyFocus=true;
    public static string[] EnemyOverrideOrder=[];
}
static class Engine {public static class SelectionRuntime {public static bool Automatic=false;}}
static unsafe class ActionManager {
    public enum UseActionMode { None,Queue }
    public static bool CanUseActionOnTarget(uint action,GameObject* ptr)=>true;
}
record Row(bool CanTargetHostile,bool TargetArea);
static unsafe class Replay {
    static bool automatic;
    static Dictionary<uint,Row> ActionSheet=new(){[7]=new(true,false)};
ORDER
    static ulong Deliver(ulong? testingRecipient) {
        bool strictDecision=testingRecipient.HasValue,changed=false;
        ulong originalTargetId=1,changedTargetId=originalTargetId; uint replacedWith=7;
        var mode=ActionManager.UseActionMode.None;
DISPATCH
        return changed ? changedTargetId : originalTargetId;
    }
    static void Main(){
        var actors=Enumerable.Range(1,3).Select(i=>new TargetActor((ulong)i,Vector3.Zero,1,true,false,1)).ToImmutableArray();
        foreach (string[] priorities in new string[][] { ORDERS }) {
            ExperimentalSelection.EnemyOverrideOrder=priorities;
            var overrides=EnemyOverrides(Svc.Targets.Target);
            var chosen=TargetSelection.Select(Vector3.Zero,1,new(25,0),new(true,false,false,false,true),
                actors,actors,overrides).Recipient;
            ulong expected=priorities[0] switch {"Selected"=>1,"WorldMouseover"=>2,_=>3};
            if(chosen!=expected || Deliver(chosen)!=expected) throw new Exception(string.Join(",",priorities));
        }
        ExperimentalSelection.EnemyFocus=ExperimentalSelection.EnemyMouseover=false;
        if(EnemyOverrides(Svc.Targets.Target).Single().Id!=1 || Deliver(null)!=1) throw new Exception("direct selection");
        automatic=true;
        if(EnemyOverrides(Svc.Targets.Target).Length!=0) throw new Exception("automation ignored opt-out");
    }
}
""".replace("ORDER\n", order + "\n").replace("DISPATCH", dispatch)
        orders = itertools.permutations(("WorldMouseover", "Focus", "Selected"))
        program = program.replace("ORDERS", ", ".join("[" + ",".join(f'"{s}"' for s in order) + "]" for order in orders))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "Program.cs").write_text(program)
            for name in ("TargetOrder.cs", "TargetSelection.cs"):
                (path / name).write_bytes((root / "Engine/Selection" / name).read_bytes())
            (path / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
            result = subprocess.run(["dotnet","run","--project",str(path / "Test.csproj")],capture_output=True,text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
