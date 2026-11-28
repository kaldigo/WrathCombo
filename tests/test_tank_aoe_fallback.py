"""Execute all new DRK/GNB AoE fallbacks with target, cooldown and range boundaries."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class TankAoeFallbackTests(unittest.TestCase):
    def test_simple_and_advanced_share_permission_and_native_safety(self):
        methods = []
        for job, aoe, ranged in [('DRK', 'Unleash', 'Unmend'), ('GNB', 'DemonSlice', 'LightningShot')]:
            source = (ROOT / f'WrathCombo/Combos/PvE/{job}/{job}.cs').read_text(encoding='utf-8-sig')
            for mode in ['Simple', 'Advanced']:
                start = source.index(f'internal class {job}_AoE_{mode} :')
                start = source.index('            if (Engine.ExperimentalSelection.TankAoeRangedFallback', start)
                end = source.index(f'return {ranged};', start) + len(f'return {ranged};')
                methods.append(f'static uint {job}{mode}() {{' + source[start:end] + f' return {aoe}; }}')
        harness = r'''
using System;
class Program {
 const uint Unleash=1,Unmend=2,DemonSlice=3,LightningShot=4;
 static bool Target,Ready,Near,Reach,Weaving;
 static class Engine { public static class ExperimentalSelection { public static bool TankAoeRangedFallback; } }
 static bool HasBattleTarget()=>Target;
 static bool ActionReady(uint action)=>Ready;
 static bool CanWeave()=>Weaving;
 static bool InActionRange(uint action)=>action is Unleash or DemonSlice?Near:Reach;
 METHODS
 static void Main() {
  foreach(bool allow in new[]{false,true}) foreach(bool target in new[]{false,true})
  foreach(bool ready in new[]{false,true}) foreach(bool near in new[]{false,true}) foreach(bool reach in new[]{false,true}) foreach(bool weaving in new[]{false,true}) {
   Engine.ExperimentalSelection.TankAoeRangedFallback=allow;Target=target;Ready=ready;Near=near;Reach=reach;Weaving=weaving;
   bool expected=allow&&target&&ready&&!near&&reach&&!weaving;
   if(DRKSimple()!=(expected?Unmend:Unleash)||DRKAdvanced()!=(expected?Unmend:Unleash)||
      GNBSimple()!=(expected?LightningShot:DemonSlice)||GNBAdvanced()!=(expected?LightningShot:DemonSlice))
      throw new Exception("Tank AoE fallback ignored permission, target, readiness or range");
  }
 }
}
'''.replace('METHODS', '\n'.join(methods))
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)
            (path / 'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (path / 'Program.cs').write_text(harness)
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Harness.csproj'), '-c', 'Release'], capture_output=True, text=True, timeout=120)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()
