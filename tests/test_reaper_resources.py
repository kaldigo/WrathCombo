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
            "UseBloodStalkGrimSwatheSoulReaverGCD"))
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
 const uint Enshroud=1,Communio=2,PlentifulHarvest=3,VoidReaping=4,Guillotine=5,Gibbet=6,Gallows=7,GrimSwathe=8,BloodStalk=9;
 static byte Soul,Shroud;
 static float GCD=2.5f,RemainingGCD=.5f,AcCD=30;
 static bool Expiring,Learned=true,InRange=true;
 static bool ActionLearned(uint action)=>Learned;
 static bool ActionReady(uint action)=>action!=Enshroud||Shroud>=50;
 static bool HasBattleTarget()=>true;
 static bool JustUsed(uint action,float seconds)=>false;
 static bool IsComboExpiring(float count)=>Expiring;
 static bool IsDebuffExpiring(float count)=>false;
 static bool InActionRange(uint action)=>InRange;
 static uint OriginalHook(uint action)=>action;
 static float GetActionCastTime(uint action)=>1.3f;
 record Cooldown(float CooldownTotal);
 static Cooldown GetCooldown(uint action)=>new(1.5f);
 static class Buffs {public const int Enshrouded=1,SoulReaver=2,Executioner=3,PerfectioParata=4,IdealHost=5,ArcaneCircle=6,EnhancedGibbet=7,EnhancedGallows=8;}
 class Actor {public HashSet<int> Statuses=new();public bool HasStatus(int status)=>Statuses.Contains(status);}
 static Actor LocalPlayer=new();
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
 Engine.ExperimentalSelection.ActiveReaper=true;
 Engine.BossForecastProvider.Downtime=2;Check(!UseEnshroud(),"Forecast entry protection failed");
 Engine.BossForecastProvider.Downtime=null;Expiring=true;Check(!UseEnshroud(),"Combo protection failed");Expiring=false;
 LocalPlayer.Statuses.Add(Buffs.SoulReaver);InRange=false;Check(!UseGuillotine(),"AoE ignored range");
 InRange=true;Learned=false;Check(!UseGuillotine()&&!UseGibbetGallowsGCD(),"Spender ignored level availability");
 }
}
""".replace("WINDOW", window.replace("using System;", "")).replace("METHODS", methods)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "Replay.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path / "Program.cs").write_text(harness)
            subprocess.run(["dotnet", "run", "--project", str(path / "Replay.csproj")], check=True, timeout=120)
