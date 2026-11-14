"""Exercise the solo quest battle engagement rule together with the AoE safety check."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class StoryEngagementTests(unittest.TestCase):
    def test_story_ally_and_idle_bystander(self):
        root = Path(__file__).resolve().parents[1]
        controller = (root / "WrathCombo/AutoRotation/AutoRotationController.cs").read_text()
        method = controller[controller.index("        internal static bool FightingParty("):
                            controller.index("        public static IEnumerable<IBattleChara> BaseSelection")]
        events = (root / "WrathCombo/Engine/OccultEventTargets.cs").read_text()
        predicate = events[events.index("    internal static bool MatchesEvent"):events.index("    internal static unsafe bool Matches")]
        arenas = events[events.index("    internal static bool InEncounterArena"):events.index("    internal static void AcquireVisible")]
        harness = """
using System.Numerics;
using WrathCombo.Engine.Selection;
enum TerritoryIntendedUseEnum { Dungeon, Quest_Battle, Quest_Battle_2 }
unsafe class IBattleChara {
    public ulong GameObjectId, TargetObjectId;
    public IBattleChara? TargetObject;
    public bool IsDead, Hostile;
    public bool IsHostile() => Hostile;
    public Combat* Struct() => combat;
    readonly Combat* combat = (Combat*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)sizeof(Combat));
}
struct Combat { public bool InCombat; }
class IBattleNpc : IBattleChara { }
static class OccultEventTargets {
    public static ushort Fate, EnemyFate, CE;
    public static uint Duty, ActorId;
    public static Vector3 Position, EnemyPosition;
    public static bool Matches(IBattleChara actor) => MatchesEvent(Fate,EnemyFate,CE,Duty,ActorId,Position,EnemyPosition);
    EVENT_POLICY
}
static class Player {
    public static IBattleChara Object = new() {GameObjectId=1};
    public static TerritoryIntendedUseEnum TerritoryIntendedUseEnum;
}
record Member(ulong GameObjectId, IBattleChara? BattleChara);
static unsafe class Replay {
    static Member[] GetPartyMembers() => [];
METHOD
    static void Main() {
        var storyAlly = new IBattleNpc {GameObjectId=2};
        var enemy = new IBattleNpc { GameObjectId=3, TargetObject=storyAlly, TargetObjectId=2, Hostile=true };
        enemy.Struct()->InCombat=true;
        var selected = new TargetActor(4,Vector3.Zero,2,true,false,1);
        ulong? Select(bool manual=false) {
            var nearby = selected with {Id=3, MayAffect=FightingParty(enemy)};
            return TargetSelection.Select(Vector3.Zero,.5f,new(20,5,EffectShape.Circle),
                new(true,false,true,false,true,AllowManualSplash:manual),[selected,nearby],[selected,nearby],[new(TargetSource.Explicit,4)]).Recipient;
        }
        foreach (var duty in new[]{TerritoryIntendedUseEnum.Quest_Battle,TerritoryIntendedUseEnum.Quest_Battle_2}) {
            Player.TerritoryIntendedUseEnum=duty;
            if(Select()!=4) throw new Exception("Story NPC combat blocked the selected target");
        }
        enemy.Struct()->InCombat=false;
        if(Select()!=null) throw new Exception("Idle enemy permitted during automation");
        if(Select(manual:true)!=4) throw new Exception("Nearby idle enemy vetoed a manual attack");
        enemy.Struct()->InCombat=true;
        Player.TerritoryIntendedUseEnum=TerritoryIntendedUseEnum.Dungeon;
        if(Select()!=null) throw new Exception("Solo rule leaked outside quest battles");
        if(Select(manual:true)!=4) throw new Exception("Manual splash blocked outside a solo duty");
        Player.TerritoryIntendedUseEnum=TerritoryIntendedUseEnum.Quest_Battle_2;
        storyAlly.Hostile=true;
        if(Select()!=null) throw new Exception("Hostile NPC treated as ally");
        // No party, no current enemy and another player has aggro.
        ulong? Acquire() {
            var mob = selected with {Id=3, MayAffect=FightingParty(enemy)};
            return TargetSelection.Select(Vector3.Zero,.5f,new(20,0),new(true,false,false,false,true),
                [mob],[mob],[]).Recipient;
        }
        OccultEventTargets.Fate=1971; OccultEventTargets.EnemyFate=1971;
        if(Acquire()!=3) throw new Exception("Solo FATE target was rejected");
        OccultEventTargets.EnemyFate=1970;
        if(Acquire()!=null) throw new Exception("Adjacent FATE target was accepted");
        OccultEventTargets.Fate=0; OccultEventTargets.EnemyFate=0;
        OccultEventTargets.Duty=1018; OccultEventTargets.CE=38;
        OccultEventTargets.Position=OccultEventTargets.EnemyPosition=new(450,0,357);
        foreach(var id in new uint[]{0x46A5,0x46A6,0x46A7}) {
            OccultEventTargets.ActorId=id;
            if(Acquire()!=3) throw new Exception("CE wave member was rejected");
        }
        OccultEventTargets.ActorId=0x46E1;
        if(Acquire()!=null) throw new Exception("Different arena actor was accepted");
        OccultEventTargets.ActorId=0x46A7; OccultEventTargets.CE=0;
        if(Acquire()!=null) throw new Exception("Inactive CE was accepted");
    }
}
""".replace("METHOD", method).replace("EVENT_POLICY", predicate + arenas)
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory)
            (target / "Replay.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup></Project>')
            (target / "Program.cs").write_text(harness)
            (target / "TargetSelection.cs").write_bytes((root / "WrathCombo/Engine/Selection/TargetSelection.cs").read_bytes())
            result = subprocess.run(["dotnet", "run", "--project", str(target / "Replay.csproj")], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
