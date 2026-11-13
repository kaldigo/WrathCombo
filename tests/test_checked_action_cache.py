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
        opener = (ROOT / 'WrathCombo/CustomCombo/WrathOpener.cs').read_text(encoding='utf-8-sig')
        opener = opener[opener.index('    public unsafe bool FullOpener('):opener.index('    internal void ObserveForTesting')].replace('unsafe ', '')
        display = source[source.index('    private readonly Dictionary<uint, (uint Action'):source.index('    /// <summary>', source.index('    private uint DisplayAction'))]
        harness = r'''
using System;
using System.Collections.Generic;
static class All { public const uint Cease = 1000004; }
static class Engine {
    public static class ExperimentalSelection { public static bool ActiveDrg, ActiveSage; }
    public static class PveIntentRouting { public static bool IsDamageButton(uint id) => id == 1000016; }
    public static class EmergencyRotation { public const uint Single = 1000017, Area = 1000018; }
    public static class RotationBehaviour { public static uint CheckedAction; }
    public static class AutomationOwnership { public static bool ExternalAction; }
    public static class SelectionRuntime {
        public static bool Refresh, HandlingPress, Automatic; public static bool IdleDamageDisplayAllowed=true;
        public static bool ConsumePressRefresh() { bool refresh = Refresh; Refresh = false; return refresh; }
    }
}
namespace AccordDiagnostics {
    enum TimedPath { Preview }
    static class PerformanceTimings { internal static System.IDisposable? Measure(TimedPath _) => null; }
}
enum Job { GNB, WHM }
static class Player { public static bool Available = true; public static Job Job; public static int Level=100; }
static class Svc { public static class ClientState { public static uint TerritoryType=100; } }
static class Service { public static class Configuration { public static int Throttle = 50; } }
static class EzThrottler { public static bool Throttle(string key, int interval) => false; }
static class Logs { public static void Log(this Exception e) => throw e; }
class Hook {
    public object FilteredCombos = new();
    public Dictionary<uint, uint> LastActionInvokeFor = new() { [100] = 200, [101] = 201 };
DISPLAY
    void UpdateFilteredCombos() { }
    uint OriginalHook(uint action) => action;
    uint GetAdjustedAction(uint action) => 999;
METHOD
}
class Opener {
    public bool CountdownActive, Combat, testingObserved = true, testingHasAction = true;
    public uint testingAction = All.Cease;
    public int OpenerStep = 1;
    public List<Func<uint>> OpenerActions = [() => All.Cease];
    bool InCombat() => Combat;
    bool ObserveOpener(ref uint action) { action = All.Cease; return true; }
OPENER
}
class Program {
    static void Main() {
        var hook = new Hook();
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 100) != 200) throw new Exception("Normal cache changed");
        foreach (var backup in new[] { Engine.EmergencyRotation.Single, Engine.EmergencyRotation.Area }) {
            hook.LastActionInvokeFor[backup] = 123;
            if (hook.GetAdjustedActionDetour(IntPtr.Zero, backup) != backup) throw new Exception("Backup hotbar action reused a cached rotation");
        }
        Engine.RotationBehaviour.CheckedAction = 100;
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 100) != 100) throw new Exception("Stale targeted replacement escaped checked execution");
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 101) != 201) throw new Exception("Unrelated action affected");
        if (hook.LastActionInvokeFor[100] != 200) throw new Exception("Checked execution mutated the hotbar cache");
        Engine.RotationBehaviour.CheckedAction = 0;
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 100) != 200) throw new Exception("Normal behaviour did not resume");
        Engine.AutomationOwnership.ExternalAction = true;
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 100) != 100 || hook.LastActionInvokeFor[100] != 200)
            throw new Exception("External execution reused or mutated a Wrath replacement");
        Engine.AutomationOwnership.ExternalAction = false;
        Engine.SelectionRuntime.Refresh = true;
        if (hook.GetAdjustedActionDetour(IntPtr.Zero, 100) != 999 || Engine.SelectionRuntime.Refresh)
            throw new Exception("Real press did not refresh the selected action exactly once");
        hook.LastActionInvokeFor[1000016] = All.Cease;
        hook.RememberIdleIcon(1000016, 25859);
        uint Read() => hook.GetAdjustedActionDetour(IntPtr.Zero, 1000016);
        if (Read() != 25859) throw new Exception("Idle WHM button did not display Glare");
        Engine.SelectionRuntime.HandlingPress = true;
        if (Read() != All.Cease) throw new Exception("A targetless press executed the idle icon");
        Engine.SelectionRuntime.HandlingPress = false;
        Engine.SelectionRuntime.Automatic = true;
        if (Read() != All.Cease) throw new Exception("Automatic execution received the idle icon");
        Engine.SelectionRuntime.Automatic = false;
        if (Read() != 25859) throw new Exception("Press changed the cached idle display");
        Player.Job=Job.WHM;
        if(Read()!=All.Cease)throw new Exception("Job switch reused an idle icon");
        Player.Job=Job.GNB; Engine.SelectionRuntime.IdleDamageDisplayAllowed=false;
        if(Read()!=All.Cease)throw new Exception("A stale icon hid a new hold");
        var opener = new Opener();
        foreach (bool sage in new[] { false, true }) {
            Engine.ExperimentalSelection.ActiveSage = sage;
            void Check(bool expectHold, uint input = 1000016) {
                uint action = input;
                if (opener.FullOpener(ref action) != expectHold || action != (expectHold ? All.Cease : input))
                    throw new Exception("Unexpected prepull result for " + input);
            }
            Check(false);
            Check(true, 119);
            opener.CountdownActive = true; Check(true); opener.CountdownActive = false;
            opener.Combat = true; Check(true); opener.Combat = false;
            Engine.SelectionRuntime.Automatic = true; Check(true); Engine.SelectionRuntime.Automatic = false;
            opener.OpenerStep = 2; Check(true); opener.OpenerStep = 1;
        }
        Console.WriteLine("Checked-action cache boundary passed");
    }
}
'''.replace('METHOD', method).replace('DISPLAY', display).replace('OPENER', opener)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Cache.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (path / 'Program.cs').write_text(harness)
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Cache.csproj'), '-p:UseSharedCompilation=false'], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Checked-action cache boundary passed', result.stdout)
