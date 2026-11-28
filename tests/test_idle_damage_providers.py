"""Replay the display fallback for each PvE damage preset using its declared native buttons."""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1] / "WrathCombo"


class IdleDamageProviders(unittest.TestCase):
    def test_declared_damage_presets(self):
        data = (ROOT / "Combos/CustomComboPreset.cs").read_text()
        hook = (ROOT / "Core/ActionReplacer.cs").read_text()
        method = hook[hook.index("    private bool RememberProviderStarter"):hook.index("    internal static bool DisableJobCheck")]
        blocks = re.findall(r"((?:[ \t]*\[[^\]]+\]\s*)+)(\w+)\s*=\s*\d+", data)
        jobs = set()
        runs = []
        # The runner uses a distinct numeric action for each declared native skill.
        ids = {}
        for attributes, name in blocks:
            damage = re.search(r"AutoAction\((true|false),\s*false\)", attributes)
            if not damage or "PvP" in attributes or "PvP" in name:
                continue
            job = re.search(r"JobInfo\(Job\.(\w+)", attributes)[1]
            jobs.add(job)
            roots = re.search(r"ReplaceSkill\((.*?)\)\]", attributes, re.S)
            self.assertIsNotNone(roots, name)
            actions = [ids.setdefault(s.strip(), len(ids)+1) for s in roots[1].split(",")]
            runs.append(f'Check("{name}",{damage[1]},[{",".join(map(str,actions))}]);')
        self.assertEqual(jobs, set("AST BLM BLU BST BRD DNC DRK DRG GNB MCH MNK NIN PCT PLD RPR RDM SGE SAM SCH SMN VPR WAR WHM".split()))
        self.assertGreaterEqual(len(runs), 91)
        program = """
using System;
using System.Collections.Generic;
using System.Linq;
enum Job { GNB, BLU }
static class Player { public static Job Job; }
static class All { public const uint SingleTargetDPS=1000000; }
record Damage(bool IsAoE,bool IsHeal=false);
record Skills(List<uint> ActionIDs);
record Preset(bool IsPvP, Damage AutoAction, Skills ReplaceSkill){public Preset Attributes()=>this;}
record CustomCombo(Preset Preset);
static class Native {
 public enum CustomActionType { SingleTargetDPS,AoEDPS }
 public static class CustomActionHelper {
  public static bool Custom; public static bool CustomActionEnabled(CustomActionType t)=>Custom;
  public static uint GetActionId(CustomActionType t)=>1000000+(uint)t;
 }
}
static class Engine {
 public static class SelectionRuntime {public static bool IdleDamageDisplayAllowed=true;}
 public static class ActionRoutes { public static bool Allowed(uint id)=>id!=0; }
 public static class PveIntentRouting {
  public static bool Area;
  public static bool IsDamageButton(uint id)=>id is 1000000 or 1000001 or 1000016;
  public static bool? Matches(uint id,Native.CustomActionType t)=>id==1000016 ? Area==(t==Native.CustomActionType.AoEDPS) : null;
 }
}
static class CustomComboFunctions {public static bool ActionLearned(uint id)=>true; public static bool IsSpellActive(uint id)=>false;}
class Program {
 uint shown; uint OriginalHook(uint root)=>root+100;
 void RememberIdleIcon(uint input,uint icon)=>shown=icon;
METHOD
 void Check(string name,bool area,uint[] roots) {
  var provider=new CustomCombo(new(false,new(area),new(roots.ToList())));
  void Expect(uint input,uint expected) {
   shown=0;
   if(RememberProviderStarter(input,provider)!=(expected!=0) || shown!=expected)throw new Exception(name+" input "+input);
  }
  Engine.PveIntentRouting.Area=area;
  Native.CustomActionHelper.Custom=false;
  foreach(var root in roots) Expect(root,root+100);
  Expect(1000016,roots[0]+100);
  Engine.PveIntentRouting.Area=!area; Expect(1000016,0); Engine.PveIntentRouting.Area=area;
  Native.CustomActionHelper.Custom=true;
  Expect(area?1000001u:1000000u,roots[0]+100); Expect(area?1000000u:1000001u,0);
  Expect(1000002,0);
  Player.Job=Job.BLU; Expect(1000016,0); Player.Job=Job.GNB;
  Engine.SelectionRuntime.IdleDamageDisplayAllowed=false; Expect(1000016,0); Engine.SelectionRuntime.IdleDamageDisplayAllowed=true;
 }
 void Run(){RUNS}
 static void Main()=>new Program().Run();
}
""".replace("METHOD", method).replace("RUNS", "\n".join(runs))
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            (path/"Program.cs").write_text(program)
            (path/"Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            result = subprocess.run(["dotnet","run","--project",str(path/"Test.csproj")],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
