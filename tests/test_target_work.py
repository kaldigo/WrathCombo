"""Exercise production target evaluation, freshness and lazy legacy fallback."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def method(source, signature):
    start = source.index(signature)
    end = source.index('{', start) + 1
    depth = 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


class TargetWorkTests(unittest.TestCase):
    def test_snapshot_scope_native_checks_and_legacy_fallback(self):
        source = (ROOT / 'WrathCombo/Engine/SelectionRuntime.cs').read_text(encoding='utf-8-sig')
        fields = source[source.index('    private sealed record EnemyActor('):source.index('    private static EnemyActor[] EnemyActors(')]
        evaluation = method(source, '    private sealed class EnemyEvaluation(')
        actor_method = method(source, '    private static EnemyActor[] EnemyActors(')
        selector = method(source, '    private static unsafe TargetDecision SelectEnemy(')
        resolver = method(source, '    internal static bool TryResolveAutomaticEnemy(')
        controller = (ROOT / 'WrathCombo/AutoRotation/AutoRotationController.cs').read_text(encoding='utf-8-sig')
        start = controller.index('            var manualTarget = SimpleTarget.HardTarget;', controller.index('public static bool ExecuteAoE('))
        routing = controller[start:controller.index('            if ((target is not IBattleChara', start)]
        start = controller.index('            IBattleChara? target = null;', controller.index('public static bool ExecuteST('))
        st_routing = controller[start:controller.index('            if ((target is not', start)]
        harness = r'''
using System; using System.Linq; using System.Collections.Generic; using System.Collections.Immutable; using System.Numerics;
using WrathCombo.Engine.Selection;
class IBattleChara {
 public ulong GameObjectId; public uint BaseId; public Vector3 Position; public float HitboxRadius=.5f;
 public bool IsDead, IsInvincible, Engaged=true, CanUse=true, IsTargetable=true;
 public bool IsHostile()=>true; public IBattleChara GameObject()=>this;
}
static class Player { public static bool Available=true; public static IBattleChara Object=new(){GameObjectId=999}; public static Vector3 Position=>Object.Position; public static IBattleChara GameObject=>Object; }
namespace Lumina.Excel.Sheets { class Action { public bool CanTargetSelf=true,CanTargetHostile; public byte CastType=2; public float EffectRange=5; public byte XAxisModifier; } }
class Sheet { public Lumina.Excel.Sheets.Action GetRow(uint id)=>TryGetRow(id,out var row)?row:throw new Exception(); public bool TryGetRow(uint id,out Lumina.Excel.Sheets.Action row){row=Fixture.Row;return id!=0;} }
static class Svc {
 public static class Data { public static Sheet GetExcelSheet<T>()=>new(); }
 public static class ClientState { public static bool IsPvP; }
 public static class Framework { public static DateTime LastUpdateUTC=DateTime.UtcNow; }
 public static class Objects {
  public static IBattleChara[] Actors=[]; public static int Reads;
  public static IEnumerable<IBattleChara> GetBattleCharas(){Reads++;return Actors;}
  public static object? SearchById(ulong id)=>Actors.FirstOrDefault(a=>a.GameObjectId==id);
 }
}
static class Fixture { public static Lumina.Excel.Sheets.Action Row=new(); public static float Range; public static int PartyReads,NativeReads; public static bool SelfAllowed=true; }
static class ActionManager {
 public static float GetActionRange(uint action)=>Fixture.Range;
 public static bool CanUseActionOnTarget(uint action,IBattleChara actor){Fixture.NativeReads++;return actor==Player.Object?Fixture.SelfAllowed:actor.CanUse;}
}
static class Service { public static class Configuration { public static Dictionary<uint,bool> IgnoredNPCs=new(); } }
static class ExperimentalSelection { public static bool ActiveTargets=true,AutomaticEnemies=true,AcquireEnemy,Fallback; }
static class SimpleTarget { public static IBattleChara? HardTarget; }
static class PvpTargetEligibility { public static bool CanDamage(uint action,IBattleChara actor)=>actor.CanUse; }
static class TestingApi { public static void RecordDecision(string name,object input,object output){} }
namespace AutoRotation { static class AutoRotationController { public static class DPSTargeting { public static bool FightingParty(IBattleChara a,int[] party)=>a.Engaged; } } }
namespace AccordDiagnostics { enum TimedPath { TargetSelection,EnemySnapshot,ActionEligibility,LegacyTargetRanking } static class PerformanceTimings { public static IDisposable? Measure(TimedPath path)=>null; } }
static class Runtime {
 static bool automatic=true; internal static bool HandlingPress=true;
 static EnemyEvaluation? enemyEvaluation;
EVALUATION
FIELDS
ACTORS
SELECTOR
RESOLVER
 static uint OriginalHook(uint id)=>id;
 static int[] GetPartyMembers(){Fixture.PartyReads++;return [];}
 static float GetTargetHPPercent(IBattleChara a)=>100;
 static bool InActionRange(uint action,IBattleChara a)=>true;
 static ImmutableArray<TargetOverride> EnemyOverrides(IBattleChara? selected)=>selected==null?[]:[new(TargetSource.Selected,selected.GameObjectId)];
 internal static TargetDecision Select(bool execution=true)=>SelectEnemy(1,execution:execution);
 internal static IDisposable Scope(){var scope=new EnemyEvaluation(enemyEvaluation);enemyEvaluation=scope;return scope;}
}
enum DPSRotationMode { Manual,Automatic }
class AutoAction { public bool IsHeal; }
class Attributes { public AutoAction AutoAction=new(); }
class Config { public DPSRotationMode DPSRotationMode; public Settings DPSSettings=new(); }
class Settings { public bool AoEIgnoreManual,AoEOnlyWhenTargeting; }
static class SelectionRuntime { public static bool Handled=true; public static IBattleChara? Result; public static bool TryResolveAutomaticEnemy(uint id,out IBattleChara? target){target=Result;return Handled;} }
static class DPSTargeting { public static int Reads; public static IBattleChara[] Targets=[]; public static IEnumerable<IBattleChara> BaseSelection {get{Reads++;return Targets;}} }
static class Routing {
 static uint OriginalHook(uint id)=>id; static int NumberOfEnemiesInRange(uint id,IBattleChara target,bool ignored)=>1;
 static IBattleChara? GetSingleTarget(DPSRotationMode mode){DPSTargeting.Reads++;return SimpleTarget.HardTarget;}
 internal static IBattleChara? Run(bool heal,DPSRotationMode mode){var attributes=new Attributes{AutoAction=new(){IsHeal=heal}};var cfg=new Config{DPSRotationMode=mode};uint gameAct=1;
ROUTING
 return target as IBattleChara;
 }
 internal static IBattleChara? Single(bool heal){var attributes=new Attributes{AutoAction=new(){IsHeal=heal}};uint gameAct=1;var mode=DPSRotationMode.Automatic;
SINGLE_ROUTING
 return target;
 }
}
class Program {
 static void Check(bool b,string text){if(!b)throw new Exception(text);}
 static void Main(){
  Svc.Objects.Actors=Enumerable.Range(1,58).Select(i=>new IBattleChara{GameObjectId=(ulong)i,Position=new(i<=3?i:100+i,0,0)}).ToArray();
  using(Runtime.Scope()){
   Runtime.Select();Runtime.Select();
   Check(Svc.Objects.Reads==1 && Fixture.PartyReads==1,"Actor/party snapshot repeated inside one evaluation");
   Check(Fixture.NativeReads==2,"Self eligibility must run once per action evaluation");
   Fixture.SelfAllowed=false;
   Check(Runtime.Select().Recipient==null,"Native action eligibility was cached across checks");
  }
  Fixture.SelfAllowed=true;Svc.Objects.Actors[0].IsDead=true;
  Check(Runtime.Select().Recipient==2 && Svc.Objects.Reads==2,"Execution reused the disposed selection snapshot");
  Fixture.Range=3;Fixture.Row.CanTargetSelf=false;Fixture.Row.CanTargetHostile=true;Fixture.Row.CastType=1;
  Fixture.NativeReads=0;
  Check(Runtime.Select().Recipient==2 && Fixture.NativeReads==2,"Distant candidates still trigger native eligibility");
  // Outside direct-target range but within the affected area: retain collateral safety.
  Svc.Objects.Actors=[new(){GameObjectId=1,Position=new(2,0,0)},new(){GameObjectId=2,Position=new(8,0,0),Engaged=false}];
  Fixture.Row.CastType=2;Fixture.Row.EffectRange=6;
  Check(Runtime.Select().Recipient==null,"Range pruning dropped unsafe affected actors");
  Check(Runtime.TryResolveAutomaticEnemy(1,out var empty) && empty==null,"Empty managed result became a legacy fallback");
  Check(!Runtime.TryResolveAutomaticEnemy(0,out _),"Unsupported root suppressed legacy fallback");
  ExperimentalSelection.ActiveTargets=false;
  Check(!Runtime.TryResolveAutomaticEnemy(1,out _),"Disabled targeting suppressed legacy fallback");
  var chosen=new IBattleChara{GameObjectId=42};DPSTargeting.Targets=[chosen];SimpleTarget.HardTarget=chosen;
  SelectionRuntime.Result=chosen;
  Check(Routing.Run(false,DPSRotationMode.Automatic)==chosen && DPSTargeting.Reads==0,"Managed target invoked legacy ranking");
  SelectionRuntime.Result=null;
  Check(Routing.Run(false,DPSRotationMode.Automatic)==null && DPSTargeting.Reads==0,"Empty managed target invoked legacy ranking");
  SelectionRuntime.Handled=false;
  Check(Routing.Run(false,DPSRotationMode.Automatic)==chosen && DPSTargeting.Reads==1,"Legacy damage fallback lost");
  SelectionRuntime.Handled=true;
  Check(Routing.Run(true,DPSRotationMode.Automatic)==chosen && DPSTargeting.Reads==2,"Legacy healer routing changed");
  Check(Routing.Run(false,DPSRotationMode.Manual)==null && DPSTargeting.Reads==2,"Managed/manual policy changed");
  SelectionRuntime.Handled=false;
  Check(Routing.Run(false,DPSRotationMode.Manual)==chosen && DPSTargeting.Reads==2,"Manual fallback unnecessarily ranked targets");
  DPSTargeting.Reads=0;SelectionRuntime.Handled=true;SelectionRuntime.Result=chosen;
  Check(Routing.Single(false)==chosen && DPSTargeting.Reads==0,"Managed ST still ranked legacy targets");
  SelectionRuntime.Result=null;
  Check(Routing.Single(false)==null && DPSTargeting.Reads==0,"Empty managed ST fell back");
  Check(Routing.Single(true)==chosen && DPSTargeting.Reads==1,"ST healer routing changed");
  SelectionRuntime.Handled=false;
  Check(Routing.Single(false)==chosen && DPSTargeting.Reads==2,"Legacy ST fallback lost");
  Console.WriteLine("Target work boundaries passed");
 }
}
'''.replace('EVALUATION', evaluation).replace('FIELDS', fields).replace('ACTORS', actor_method).replace('SELECTOR', selector).replace('RESOLVER', resolver).replace('SINGLE_ROUTING', st_routing).replace('ROUTING', routing)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><Nullable>enable</Nullable></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(harness, encoding='utf-8')
            (path / 'TargetSelection.cs').write_bytes((ROOT / 'WrathCombo/Engine/Selection/TargetSelection.cs').read_bytes())
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Test.csproj'), '-p:UseSharedCompilation=false'], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Target work boundaries passed', result.stdout)
