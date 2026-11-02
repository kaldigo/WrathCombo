"""Exercise the production action hook's checked-execution/cache boundary."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class CheckedActionCacheTests(unittest.TestCase):
    def test_checked_action_bypasses_stale_replacement_without_changing_cache(self):
        source = (ROOT / 'WrathCombo/Core/ActionReplacer.cs').read_text(encoding='utf-8-sig')
        start = source.index('    private uint GetAdjustedActionDetour(')
        end = source.index('{', start) + 1
        depth = 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        method = source[start:end].replace('private uint', 'public uint', 1)
        harness = r'''
using System;
using System.Collections.Generic;
static class Engine { public static class RotationBehaviour { public static uint CheckedAction; } }
static class Player { public static bool Available = true; }
static class Service { public static class Configuration { public static int Throttle = 50; } }
static class EzThrottler { public static bool Throttle(string key, int interval) => false; }
static class Logs { public static void Log(this Exception e) => throw e; }
class Hook {
    public object FilteredCombos = new();
    public Dictionary<uint, uint> LastActionInvokeFor = new() { [100] = 200, [101] = 201 };
    void UpdateFilteredCombos() { }
    uint OriginalHook(uint action) => action;
    uint GetAdjustedAction(uint action) => 999;
METHOD
}
class Program {
    static void Main() {
        var hook = new Hook();
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 100) != 200) throw new Exception("Normal cache changed");
        Engine.RotationBehaviour.CheckedAction = 100;
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 100) != 100) throw new Exception("Stale targeted replacement escaped checked execution");
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 101) != 201) throw new Exception("Unrelated action affected");
        if (hook.LastActionInvokeFor[100] != 200) throw new Exception("Checked execution mutated the hotbar cache");
        Engine.RotationBehaviour.CheckedAction = 0;
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 100) != 200) throw new Exception("Normal behaviour did not resume");
        Console.WriteLine("Checked-action cache boundary passed");
    }
}
'''.replace('METHOD', method)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Cache.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (path / 'Program.cs').write_text(harness)
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Cache.csproj'), '-p:UseSharedCompilation=false'], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Checked-action cache boundary passed', result.stdout)
