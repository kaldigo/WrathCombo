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
        ulong? Select() {
            var nearby = selected with {Id=3, MayAffect=FightingParty(enemy)};
            return TargetSelection.Select(Vector3.Zero,.5f,new(20,5,EffectShape.Circle),
                new(true,false,true,false,true),[selected,nearby],[selected,nearby],[new(TargetSource.Explicit,4)]).Recipient;
        }
        foreach (var duty in new[]{TerritoryIntendedUseEnum.Quest_Battle,TerritoryIntendedUseEnum.Quest_Battle_2}) {
            Player.TerritoryIntendedUseEnum=duty;
            if(Select()!=4) throw new Exception("Story NPC combat blocked the selected target");
        }
        enemy.Struct()->InCombat=false;
        if(Select()!=null) throw new Exception("Idle enemy permitted");
        enemy.Struct()->InCombat=true;
        Player.TerritoryIntendedUseEnum=TerritoryIntendedUseEnum.Dungeon;
        if(Select()!=null) throw new Exception("Solo rule leaked outside quest battles");
        Player.TerritoryIntendedUseEnum=TerritoryIntendedUseEnum.Quest_Battle_2;
        storyAlly.Hostile=true;
        if(Select()!=null) throw new Exception("Hostile NPC treated as ally");
    }
}
""".replace("METHOD", method)
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory)
            (target / "Replay.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup></Project>')
            (target / "Program.cs").write_text(harness)
            (target / "TargetSelection.cs").write_bytes((root / "WrathCombo/Engine/Selection/TargetSelection.cs").read_bytes())
            result = subprocess.run(["dotnet", "run", "--project", str(target / "Replay.csproj")], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
