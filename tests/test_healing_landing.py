"""Run the production landing adapter with deterministic game/clock boundaries."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]

class HealingLandingTests(unittest.TestCase):
    def test_all_healer_landing_and_permission_boundaries(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path / 'Landing.cs').write_bytes((ROOT / 'WrathCombo/Engine/HealerSelection.Landing.cs').read_bytes())
            for name in ['HealLanding', 'PreventionBudget', 'PreparationSequence', 'EncounterForecast']:
                (path / (name + '.cs')).write_bytes((ROOT / ('WrathCombo/Engine/Selection/' + name + '.cs')).read_bytes())
            (path / 'Program.cs').write_text(HARNESS, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Test.csproj'), '-c', 'Release', '-p:UseSharedCompilation=false', '-m:1', '-nr:false'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('All healer landing boundaries passed', result.stdout)

HARNESS = r"""
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Combos.PvE;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
class Program {
 static void Check(bool ok,string why) { if(!ok) throw new Exception(why); }
 static void Main() {
  HealerSelection.Job=24;
  SelectionRuntime.Prevention.Observe("raidwide",true,10000,1,0,4);
  var hit=SelectionRuntime.Prevention.Available("raidwide")!;
  Check(HealerSelection.Run(1,hit,true)==null && HealerSelection.ReservesDamageGcd,"full-health Medica must reserve its immediate cast slot");
  WrathCombo.Engine.Environment.TickCount64=11600;
  var medica=HealerSelection.Run(1,hit,true);
  Check(medica is {Action:WHM.Medica2,ExecuteAfter:>11600},"Medica must be scheduled to land after damage");
  RotationBehaviour.Support=false;
  Check(HealerSelection.Run(1,hit,true)==null && !HealerSelection.ReservesDamageGcd,"support-off must not schedule or reserve a heal");
  RotationBehaviour.Support=true;
  Check(HealerSelection.Run(1,hit,false)==null,"GCD exclusion must exclude Medica");
  RemainingGCD=0; WrathCombo.Engine.Environment.TickCount64=10000;
  Enabled.Add(Preset.WHM_Raidwide_PlenaryIndulgence);
  Check(HealerSelection.Run(1,new(2,1,0,15000),true)==null,"a distant full-health cast must not lock the whole forecast horizon");
  RemainingGCD=1.7f; WrathCombo.Engine.Environment.TickCount64=10800;
  var plenary=HealerSelection.Run(1,new(2,1,0,15000),true);
  Check(plenary is {Action:WHM.PlenaryIndulgence,PlannedGcd:WHM.Medica2,FollowupAfter:>10800,FollowupPurpose:PreventionPurpose.Recovery},"Plenary must bind its actual timed follow-up");
  Enabled.Clear(); RemainingGCD=0;
  HealerSelection.Job=28; WrathCombo.Engine.Environment.TickCount64=13700;
  Check(HealerSelection.Run(1,hit,false) is {Action:SCH.Indomitability},"GCD exclusion must still allow timed Indom");
  HealerSelection.Job=33; LocalPlayer.Macro=true; Enabled.Add(Preset.AST_AOE_DPS_MacroCosmos);
  Check(HealerSelection.Run(1,hit,false) is {Action:AST.MicroCosmos},"active Microcosmos must release after impact without requiring healing GCD inclusion");
  LocalPlayer.Macro=false;
  Check(HealerSelection.Run(1,hit,false) is {Action:AST.StellarDetonation},"mature Star must retain ability-only access");
  HealerSelection.Job=40; WrathCombo.Engine.Environment.TickCount64=12000;
  Enabled.Add(Preset.SGE_AoE_Adv_Heal_Zoe);
  Check(HealerSelection.ScheduleLanding(1,SGE.Pneuma,42,hit) is {Action:SGE.Zoe,PlannedGcd:SGE.Pneuma,Recipient:42},"Pneuma must use its healing circle despite a zero damage-line ally count, and Zoe must retain its enemy recipient");
  ExecutionSafety.Fits=false;
  Check(HealerSelection.ScheduleLanding(1,SGE.Pneuma,42,hit)==null,"known unsafe future cast cannot reserve a landing");
  Console.WriteLine("All healer landing boundaries passed");
 }
}
namespace ECommons.GameHelpers { static class Player { public static WrathCombo.CustomComboNS.Functions.Actor Object=new(); } }
namespace WrathCombo.Extensions { }
namespace WrathCombo.CustomComboNS.Functions {
 class Actor { public bool IsDead; public bool Macro; public bool HasStatus(ushort id)=>id==AST.Buffs.Macrocosmos && Macro; }
 class Member { public Actor BattleChara=new(); }
 static class CustomComboFunctions {
  public static float RemainingGCD=0,GCDTotal=2.5f;
  public static Actor LocalPlayer=ECommons.GameHelpers.Player.Object;
  public static HashSet<Preset> Enabled=new();
  public static bool IsEnabled(Preset p)=>Enabled.Contains(p);
  public static bool IsMoving()=>false;
  public static bool CanWeave()=>true;
  public static bool ActionLearned(uint a)=>true;
  public static bool ActionReadyForTarget(uint a,ulong r)=>true;
  public static uint OriginalHook(uint a)=>a==AST.Macrocosmos?AST.MicroCosmos:a;
  public static List<Member> GetPartyMembers()=>[new(),new()];
  public static int NumberOfAlliesInRange(uint a,Actor p)=>a==SGE.Pneuma?0:2;
  public static float GetTargetDistance(Actor p)=>10;
 }
}
namespace WrathCombo.Combos.PvE {
 enum Preset { SGE_AoE_Simple_Heal, AST_AOE_DPS_MacroCosmos, WHM_Raidwide_PlenaryIndulgence, SGE_AoE_Adv_Heal_Zoe }
 static class WHM {
  public const uint Medica1=11,Medica2=12,AfflatusRapture=13,PlenaryIndulgence=14;
  public static bool CanLily=false,BloodLilyReady=false;
  public static class Buffs { public const ushort Medica2=1,Medica3=2; }
  public static void GetMatchingConfigAoE(int i,object p,out uint s,out bool e) { s=i==0?Medica2:AfflatusRapture;e=i==0; }
 }
 static class SCH {
  public const uint Indomitability=20; public static bool HasAetherflow=true;
  public static void GetMatchingConfigAoE(int i,out uint s,out bool e) { s=Indomitability;e=true; }
 }
 static class AST {
  public const uint StellarDetonation=30,Macrocosmos=31,MicroCosmos=32,AspectedHelios=33;
  public static class Buffs { public const ushort GiantDominance=3,Macrocosmos=4; }
  public static void GetMatchingConfigAoE(int i,out uint s,out bool e) { s=StellarDetonation;e=true; }
 }
 static class SGE { public const uint Pneuma=40,Zoe=41; }
}
namespace WrathCombo.Engine {
 static class Environment { public static long TickCount64=10000; }
 static class ExperimentalSelection { public static bool ActiveHealer=true; }
 static class RotationBehaviour { public static bool? Support=true; }
 static class BossForecastProvider { public static Forecast? Fresh=null; public record Forecast(bool Moving); }
 static class NativeExecution { public static float CastTime(uint a)=>a is WHM.Medica1 or WHM.Medica2 or SGE.Pneuma or AST.AspectedHelios?2:0; }
 static class ActionRoutes { public static bool Allowed(uint a)=>true; }
 static class TestingApi { public static void RecordDecision(string k,object i,object o){} }
 static class ExecutionSafety { public static bool Fits=true; public static bool HealingCastFits(float c,float s)=>Fits; }
 static class SelectionRuntime {
  public static bool Automatic=true; public const long Generation=1;
  public static PreventionBudget Prevention=new(); public static SelectionDecision? Commitment;
  public static uint Publish(uint i,SelectionDecision d)=>d.Action;
 }
 internal static partial class HealerSelection {
  internal static uint Job; private const ulong Self=9;
  private static bool SimplePartyHealing=>false;
  internal static SelectionDecision? Run(uint input,PreventionWindow w,bool gcds)=>ScheduledRecovery(input,w,gcds);
  private static bool IsGcd(uint a)=>NativeExecution.CastTime(a)>0 || a==WHM.AfflatusRapture;
  private static SelectionDecision? Ready(uint i,uint a,ulong r,string reason,long e)=>new(a,a,r,0,reason,Environment.TickCount64,1,e);
 }
}
"""
