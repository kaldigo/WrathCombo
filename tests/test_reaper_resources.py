"""Exercise Reaper's production gauge and earned-spender decisions."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class ReaperResourceTests(unittest.TestCase):
    def test_gauge_and_earned_spenders(self):
        root = Path(__file__).resolve().parents[1] / "WrathCombo"
        source = (root / "Combos/PvE/RPR/RPR_Helper.cs").read_text(encoding="utf-8-sig")

        def method(name):
            start = source.index("    private static bool " + name + "(")
            body = source.index("{", start)
            arrow = source.find("=>", start, body)
            if arrow >= 0:
                return source[start:source.index(";", arrow) + 1]
            depth = 1
            end = body + 1
            while depth:
                depth += (source[end] == "{") - (source[end] == "}")
                end += 1
            return source[start:end]

        methods = "\n".join(method(name) for name in (
            "UseEnshroud", "UseGuillotine", "UseGibbetGallowsGCD",
            "UseBloodStalkGrimSwatheSoulReaverGCD", "UseSoulOverflow",
            "UseBloodstalk", "UseGrimSwathe", "UseGluttony",
            "ShouldSpendSoulOvercapST", "ShouldSpendSoulOvercapAoE",
            "MaintainsDeathsDesign", "NeedsDeathsDesignFor", "NeedsCommittedDeathsDesign",
            "ProtectBurstFinishers", "UseShadowOfDeath", "UseWhorlOfDeath",
            "UseSacrificium", "IsDebuffExpiring"))
        for name in ("IsShroudCapped", "IsShroudOvercapping", "InNormalRotation", "SacrificiumDeadline"):
            start = source.index("    private static bool " + name)
            methods += "\n" + source[start:source.index(";", start) + 1]
        start = source.index("    private static float EnshroudFinishDuration(")
        methods += "\n" + source[start:source.index(";", start) + 1]
        window = (root / "Engine/Selection/ResourceWindow.cs").read_text().replace(
            "namespace WrathCombo.Engine.Selection;", "")
        harness = """
using System;
using System.Collections.Generic;
static class Extensions { public static bool InRange(this float value,float low,float high)=>value>=low&&value<=high; }
namespace Engine {
 static class ExperimentalSelection {public static bool ActiveReaper=true;}
 static class BossForecastProvider {public static float? Downtime;}
 static class TestingApi {public static void RecordDecision(string name,object input,object output){}}
 namespace Selection {
 static class EncounterForecast {public static float? Normalize(float? value)=>value;}
 WINDOW
 }
}
class Replay {
 const uint Enshroud=1,Communio=2,PlentifulHarvest=3,VoidReaping=4,Guillotine=5,Gibbet=6,Gallows=7,GrimSwathe=8,BloodStalk=9,Gluttony=10,ShadowOfDeath=11,WhorlOfDeath=12,Perfectio=13,ArcaneCircle=14,CrossReaping=15;
 const byte MaxShroud=100;
 static byte Soul,Shroud;
 static float GCD=2.5f,RemainingGCD=.5f,AcCD=30;
 static int Lemure=4,VoidShroud=0;
 static bool Maintain=false,Boss=true,Simple=true,DesignEnabled=true,CanApply=true;
 static int Hp=100;
 static int RPR_WoDHPThreshold=0,RPR_SoDHPThreshold=0;
 enum Preset {RPR_AoE_SimpleMode,RPR_AoE_WoD,RPR_ST_SimpleMode,RPR_ST_SoD}
 static bool IsEnabled(Preset preset)=>Maintain && (Simple
     ? preset is Preset.RPR_AoE_SimpleMode or Preset.RPR_ST_SimpleMode
     : DesignEnabled && preset is Preset.RPR_AoE_WoD or Preset.RPR_ST_SoD);
 static int GetTargetHPPercent()=>Hp;
 static bool InBossEncounter()=>Boss;
 static bool UsesBurstAlignment=>Boss;
 static bool JustUsed(uint action)=>false;
 static bool Expiring,Learned=true,InRange=true,EnshroudLearned=true;
 static bool ActionLearned(uint action)=>Learned && (action!=Enshroud||EnshroudLearned);
 static bool ActionReady(uint action)=>ActionLearned(action) && (action!=Enshroud||Shroud>=50);
 static bool HasBattleTarget()=>true;
 static bool InPostBurstSequence=>false;
 static bool ShouldHoldSoulOverflowWeave=>false;
 static bool IsOnCooldown(uint action)=>false;
 static bool IsOffCooldown(uint action)=>GetCooldownRemainingTime(action)<=0;
 static float GetCooldownRemainingTime(uint action)=>action==ArcaneCircle?AcCD:0;
 static bool UseBurstGluttony(bool enabled,bool area)=>false;
 static bool JustUsed(uint action,float seconds)=>false;
 static bool IsComboExpiring(float count)=>Expiring;
 static bool InActionRange(uint action)=>InRange;
 static uint OriginalHook(uint action)=>action;
 static float GetActionCastTime(uint action)=>1.3f;
 record Cooldown(float CooldownTotal);
 static Cooldown GetCooldown(uint action)=>new(1.5f);
 static class Buffs {public const int Enshrouded=1,SoulReaver=2,Executioner=3,PerfectioParata=4,IdealHost=5,ArcaneCircle=6,EnhancedGibbet=7,EnhancedGallows=8,ImmortalSacrifice=9,Oblatio=10;}
 static class Debuffs {public const int DeathsDesign=100;}
 record StatusTime(float Time) {public float RemainingTimeOrZero()=>Time;}
 class Actor {
 public HashSet<int> Statuses=new(); public Dictionary<int,float> Times=new();
 public bool HasStatus(int status)=>Statuses.Contains(status);
 public StatusTime Status(int status)=>new(HasStatus(status)?Times.GetValueOrDefault(status):0);
 public bool CanApplyStatus(int status)=>CanApply;
 public void Set(int status,float seconds){Statuses.Add(status);Times[status]=seconds;}
 }
 static Actor LocalPlayer=new(),CurrentTarget=new();
 METHODS
 static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
 static void Main(){
 foreach(bool v2 in new[]{false,true}){
 Engine.ExperimentalSelection.ActiveReaper=v2;
 Shroud=100;Soul=50;
 Check(UseEnshroud()&&UseEnshroud(true),"Capped Shroud was held for Soul");
 Shroud=90;Check(UseEnshroud(),"Shroud anti-overcap threshold failed");
 Shroud=50;Soul=100;Check(!UseEnshroud(),"Soul incorrectly triggered Shroud anti-overcap");
 Shroud=100;Soul=50;
 foreach(int status in new[]{Buffs.SoulReaver,Buffs.Executioner})
 foreach(bool enabled in new[]{false,true}){
 LocalPlayer.Statuses.Add(status);
 Check(!UseEnshroud(),"Entered Enshroud before completing earned spenders");
 Check(UseGibbetGallowsGCD(enabled)&&UseGuillotine(enabled),"Capped Shroud discarded earned spenders");
 uint st=BloodStalk,area=GrimSwathe;
 Check(UseBloodStalkGrimSwatheSoulReaverGCD(ref st,enabled)&&st==Gallows,"Direct ST spender blocked");
 Check(UseBloodStalkGrimSwatheSoulReaverGCD(ref area,enabled)&&area==Guillotine,"Direct AoE spender blocked");
 LocalPlayer.Statuses.Clear();
 Check(!UseGibbetGallowsGCD(enabled)&&!UseGuillotine(enabled),"Spender used without earned status");
 Check(UseEnshroud(),"Enshroud failed after the spender cleared");
 }
 }
 foreach(bool enshroudEnabled in new[]{false,true}){
 Shroud=100;Soul=100;EnshroudLearned=false;
 Check(!IsShroudOvercapping(enshroudEnabled),"Locked Enshroud blocked Soul spending");
 Check(UseBloodstalk(true,enshroudEnabled)&&UseGrimSwathe(true,enshroudEnabled)&&UseGluttony(enshroudEnabled),
       "Locked Enshroud blocked a ready Soul spender");
 LocalPlayer.Statuses.Add(Buffs.IdealHost);
 Check(UseBloodstalk(true,enshroudEnabled)&&UseGluttony(enshroudEnabled),
       "Unusable Ideal Host blocked Soul spending");
 LocalPlayer.Statuses.Clear();EnshroudLearned=true;
 LocalPlayer.Statuses.Add(Buffs.IdealHost);Check(!InNormalRotation,"Unlocked Ideal Host lost preparation protection");
 LocalPlayer.Statuses.Clear();
 Shroud=0;EnshroudLearned=false;Check(UseBloodstalk(true,enshroudEnabled),"Below-80 Soul spending failed");
 EnshroudLearned=true;Shroud=100;
 }
 Check(IsShroudOvercapping(false),"Learned manually held Enshroud lost overcap protection");
 Engine.ExperimentalSelection.ActiveReaper=true;
 Engine.BossForecastProvider.Downtime=2;Check(!UseEnshroud(),"Forecast entry protection failed");
 Engine.BossForecastProvider.Downtime=null;Expiring=true;Check(!UseEnshroud(),"Combo protection failed");Expiring=false;
 LocalPlayer.Statuses.Add(Buffs.SoulReaver);InRange=false;Check(!UseGuillotine(),"AoE ignored range");
 InRange=true;Learned=false;Check(!UseGuillotine()&&!UseGibbetGallowsGCD(),"Spender ignored level availability");
 Learned=true;LocalPlayer.Statuses.Clear();Maintain=true;Soul=100;Shroud=100;
 // Missing Design must refresh before any automatic Soul spender or Enshroud.
 Check(IsDebuffExpiring(5),"Missing Design was treated as safe");
 Check(!UseEnshroud()&&!UseEnshroud(true),"Entered Enshroud without Design");
 Check(!UseGluttony()&&!UseGluttony(onAoE:true)&&!UseBloodstalk()&&!UseGrimSwathe(true),"Started spender chain without Design");
 Expiring=true;Check(UseShadowOfDeath()&&UseWhorlOfDeath(),"Combo protection suppressed urgent Design");Expiring=false;
 Soul=0;Expiring=true;
 Check(UseShadowOfDeath()&&UseWhorlOfDeath(),"Empty Soul hid the capped-Shroud Design commitment behind combo protection");
 Soul=100;Expiring=false;
 CurrentTarget.Set(Debuffs.DeathsDesign,30);
 Check(UseEnshroud()&&UseGluttony(),"Healthy Design blocked rotation");
 Check(!UseShadowOfDeath()&&!UseWhorlOfDeath(),"Healthy Design refreshed unnecessarily");
 CurrentTarget.Set(Debuffs.DeathsDesign,4);
 LocalPlayer.Statuses.Add(Buffs.Executioner);
 Check(!UseShadowOfDeath()&&!UseWhorlOfDeath(),"Design discarded earned spender");LocalPlayer.Statuses.Clear();
 LocalPlayer.Set(Buffs.Enshrouded,25);CurrentTarget.Set(Debuffs.DeathsDesign,2);
 Check(UseShadowOfDeath()&&UseWhorlOfDeath(),"Manual Enshroud did not recover Design before expiry");
 // Sacrificium is held early for Circle, but released before the phase ends.
 LocalPlayer.Set(Buffs.Oblatio,25);AcCD=0;Lemure=4;
 Check(!UseSacrificium(),"Early Sacrificium ignored buff alignment");
 Lemure=2;Check(UseSacrificium()&&UseSacrificium(true),"Circle hold lost Sacrificium at phase deadline");
 Lemure=1;Check(UseSacrificium()&&UseSacrificium(true),"Last-GCD Sacrificium recovery failed");
 Lemure=4;LocalPlayer.Set(Buffs.Oblatio,1);Check(UseSacrificium(),"Expiring Oblatio ignored deadline");
 LocalPlayer.Statuses.Clear();Lemure=4;AcCD=30;CurrentTarget.Set(Debuffs.DeathsDesign,40);
 // Captured inter-Enshroud case: a filler costs finishers their raid buff.
 LocalPlayer.Statuses.Add(Buffs.IdealHost);LocalPlayer.Set(Buffs.ArcaneCircle,11);Expiring=true;
 Check(EnshroudFinishDuration(true)<11&&EnshroudFinishDuration(true)+GCD>11,"Burst fixture did not cross the filler deadline");
 Check(ProtectBurstFinishers()&&UseEnshroud()&&UseEnshroud(true),"Combo filler delayed buffed finishers");
 LocalPlayer.Set(Buffs.ArcaneCircle,20);
 Check(!ProtectBurstFinishers()&&!UseEnshroud(),"Healthy buff window discarded combo protection");
 // The feature gate and disabled maintenance retain their existing behavior.
 Engine.ExperimentalSelection.ActiveReaper=false;Check(!ProtectBurstFinishers(),"Scheduling leaked outside v2");
 Engine.ExperimentalSelection.ActiveReaper=true;Maintain=false;CurrentTarget.Statuses.Clear();Expiring=false;
 Check(!NeedsDeathsDesignFor(10)&&UseEnshroud(),"Disabled maintenance blocked Enshroud");
 Maintain=true;Simple=false;LocalPlayer.Statuses.Clear();Soul=100;Shroud=100;
 Check(NeedsDeathsDesignFor(10)&&NeedsDeathsDesignFor(10,true),"Advanced maintenance was ignored");
 DesignEnabled=false;Check(!NeedsDeathsDesignFor(10)&&!NeedsDeathsDesignFor(10,true)&&UseEnshroud(),"Disabled advanced maintenance blocked rotation");
 DesignEnabled=true;RPR_SoDHPThreshold=25;RPR_WoDHPThreshold=25;Hp=20;
 Check(!NeedsDeathsDesignFor(10)&&!NeedsDeathsDesignFor(10,true),"Configured HP cutoff was overridden");
 Hp=100;CanApply=false;Check(!NeedsDeathsDesignFor(10)&&!NeedsDeathsDesignFor(10,true),"Unapplicable Design blocked rotation");CanApply=true;
 LocalPlayer.Set(Buffs.Enshrouded,25);LocalPlayer.Set(Buffs.Oblatio,25);AcCD=0;Lemure=2;
 Engine.ExperimentalSelection.ActiveReaper=false;Check(!UseSacrificium(),"Deadline leaked into baseline Sacrificium hold");

 }
}
""".replace("WINDOW", window.replace("using System;", "")).replace("METHODS", methods)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "Replay.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path / "Program.cs").write_text(harness)
            subprocess.run(["dotnet", "run", "--project", str(path / "Replay.csproj")], check=True, timeout=120)
