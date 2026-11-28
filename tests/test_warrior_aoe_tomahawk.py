"""Exercise the production Warrior ranged-uptime gate and Tomahawk branch."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class TankAoeRangedFallbackTests(unittest.TestCase):
    def test_explicit_aoe_permission_preserves_st_and_native_gates(self):
        source = (ROOT / 'WrathCombo/Combos/PvE/WAR/WAR_Helper.cs').read_text(encoding='utf-8-sig')
        start = source.index('        bool rangedUptimeEnabled =')
        gate = source[start:source.index(';', start) + 1]
        paladin = (ROOT / 'WrathCombo/Combos/PvE/PLD/PLD_Helper.cs').read_text(encoding='utf-8-sig')
        start = paladin.index('        bool rangedUptimeEnabled =')
        paladin_gate = paladin[start:paladin.index(';', start) + 1].replace('PLD_ST_AdvancedMode_ShieldLob', 'WAR_ST_RangedUptime').replace('PLD_AoE_AdvancedMode_ShieldLob', 'WAR_AoE_RangedUptime')
        start = source.index('            if (rangedUptimeEnabled &&')
        branch = source[start:source.index('            #endregion', start)]
        program = r'''
using System;
class Program {
 [Flags] enum Combo { ST=1, AoE=2, Simple=4, Adv=8 }
 enum Preset { WAR_ST_RangedUptime, WAR_AoE_RangedUptime }
 static class Engine { internal static class ExperimentalSelection { internal static bool TankAoeRangedFallback; } }
 const uint Tomahawk=46, Overpower=41;
 static bool Enabled,Ready,Melee;
 static bool IsSTEnabled(Combo f,Preset p)=>f.HasFlag(Combo.ST)&&Enabled;
 static bool IsAoEEnabled(Combo f,Preset p)=>f.HasFlag(Combo.AoE)&&Enabled;
 static bool ActionReady(uint action)=>Ready;
 static bool InMeleeRange()=>Melee;
 static bool PaladinGate(Combo flags) { PALADIN_GATE return rangedUptimeEnabled; }
 static bool Select(Combo flags,ref uint actionID) {
 GATE
 BRANCH
 return false;
 }
 static void Main() {
  int checks=0;
  foreach(bool allow in new[]{false,true}) foreach(bool aoe in new[]{false,true})
  foreach(bool simple in new[]{false,true}) foreach(bool enabled in new[]{false,true})
  foreach(bool ready in new[]{false,true}) foreach(bool melee in new[]{false,true}) {
   Engine.ExperimentalSelection.TankAoeRangedFallback=allow; Enabled=enabled; Ready=ready; Melee=melee;
   uint action=Overpower;
   bool selected=Select((aoe?Combo.AoE:Combo.ST)|(simple?Combo.Simple:Combo.Adv),ref action);
   bool expected=(!aoe||allow)&&(simple||enabled)&&ready&&!melee;
   if(PaladinGate((aoe?Combo.AoE:Combo.ST)|(simple?Combo.Simple:Combo.Adv))!=((!aoe||allow)&&(simple||enabled))) throw new Exception("Paladin AoE fallback gate mismatch");
   if(selected!=expected || action!=(expected?Tomahawk:Overpower)) throw new Exception($"allow={allow}, aoe={aoe}, simple={simple}, enabled={enabled}, ready={ready}, melee={melee}");
   checks++;
  }
  Console.WriteLine($"Warrior Tomahawk: {checks} selection cases passed.");
 }
}
'''.replace('PALADIN_GATE', paladin_gate).replace('GATE', gate).replace('BRANCH', branch)
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)
            (path / 'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (path / 'Program.cs').write_text(program)
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Harness.csproj'), '-c', 'Release'], capture_output=True, text=True, timeout=120)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()
