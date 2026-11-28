"""Exercise the production press lookup with many registered custom buttons."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class NativePressLookupTests(unittest.TestCase):
    def test_one_resolution_per_press_preserves_action_and_patient(self):
        source = (ROOT / 'WrathCombo/Data/ActionWatching.cs').read_text(encoding='utf-8-sig')
        start = source.index('            if (actionType is ActionType.Action)', source.index('private unsafe static bool UseActionDetour('))
        start = source.index('{', start) + 1
        end = source.index('                ulong? testingRecipient', start)
        lookup = source[start:end]
        harness = r'''
using System;
using System.Collections.Generic;
using System.Linq;
unsafe struct ActionManager {
    internal static Func<uint, uint> Resolve = null!;
    internal uint GetAdjustedActionId(uint action) => Resolve(action);
}
class Button { internal uint Id; internal Action? OnClick; }
static class P { internal static class CustomActions { internal static class Manager { internal static List<Button> Actions = new(); } } }
static class Extensions {
    internal static bool TryGetFirst(this List<Button> buttons, Func<Button, bool> predicate, out Button result) {
        foreach (var button in buttons) if (predicate(button)) { result = button; return true; }
        result = null!; return false;
    }
}
namespace AccordDiagnostics { static class DiagnosticLog { internal static bool Reject(string reason) => false; } }
unsafe class Program {
    static uint Executed; static ulong Patient, ExecutedPatient;
    static bool Press(uint actionId) {
        ActionManager manager = new(); ActionManager* actionManager = &manager;
LOOKUP
        // The remaining press binds the recipient to this same resolved action.
        Executed = replacedWith; ExecutedPatient = Patient;
        return true;
    }
    static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    static void Main() {
        foreach (var count in new[] { 0, 1, 64, 512 }) {
            P.CustomActions.Manager.Actions = Enumerable.Range(0, count).Select(i => new Button { Id = (uint)(1000000 + i) }).ToList();
            int calls = 0;
            ActionManager.Resolve = input => { calls++; Patient = calls == 1 ? 42UL : 99UL; return calls == 1 ? 120U : 127U; };
            Require(Press(1000016), "Ordinary healer press intercepted");
            Require(calls == 1, $"{count} buttons triggered {calls} resolutions");
            Require(Executed == 120 && ExecutedPatient == 42, "Healer action/patient changed during button search");
            // A later press must resolve fresh rather than retain the prior patient's heal.
            Require(Press(1000016) && calls == 2 && Executed == 127 && ExecutedPatient == 99, "Next input reused a stale heal");
        }
        foreach (bool callback in new[] { false, true }) {
            int clicks = 0, calls = 0;
            P.CustomActions.Manager.Actions = Enumerable.Range(0, 512).Select(i => new Button { Id = (uint)(1000000 + i) }).ToList();
            P.CustomActions.Manager.Actions.Add(new Button { Id = 120, OnClick = callback ? () => clicks++ : null });
            ActionManager.Resolve = input => { calls++; Patient = 42; return 120; };
            Require(Press(1000016) == !callback && calls == 1 && clicks == (callback ? 1 : 0), "Custom-button callback/dispatch changed");
        }
        Console.WriteLine("Single native press lookup passed");
    }
}
'''.replace('LOOKUP', lookup)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Lookup.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><Nullable>enable</Nullable></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(harness, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Lookup.csproj'), '-p:UseSharedCompilation=false'], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Single native press lookup passed', result.stdout)
