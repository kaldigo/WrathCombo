"""Replay the Ifrit runner branch with deterministic provider and native boundaries."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class IfritRotationHealTests(unittest.TestCase):
    def test_runner_uses_rotation_input_and_submits_its_recipient(self):
        root = Path(__file__).resolve().parents[1]
        source = (root / 'WrathCombo/AutoRotation/AutoRotationController.cs').read_text(encoding='utf-8-sig')
        start = source.index('    private static bool TryRotationButtonHeal()')
        end = source.index('    public static IEnumerable<uint> TankbusterActions', start)
        runner = source[source.index('    internal static void Run()'):start]
        self.assertLess(runner.index('TryRotationButtonHeal()'), runner.index('ProcessAutoActions(ref _, canHeal, false)'))
        harness = """
using static Fixture;
class Fixture {
    public const ulong Enemy = 1073767858, Tank = 268709579;
    public static bool Injured = true, Allow = true, CastingWhileMoving, Area;
    public static float RemainingGCD, AnimationLock;
    public static TimeSpan TimeMoving => CastingWhileMoving ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;
    public record Config(float QueueWindow = .5f, bool OrbwalkerIntegration = false);
    public static Config cfg = new();
    public record Preset(bool IsHeal, bool IsAoE) {
        public Preset Attributes() => this;
        public Preset AutoAction => this;
    }
    public static Dictionary<Preset,bool> _autoActions = new() { [new(true,false)] = true, [new(false,false)] = true, [new(false,true)] = true };
}
record Decision(uint Action, ulong Recipient);
class Scope : IDisposable { public void Dispose() {} }
class ActionRouteContext(object? policy) : Scope {}
record Policy(bool Direct);
static class ActionRoutes { public static Policy? For(uint input) => null; public static bool Allowed(uint action) => Allow; }
static class PveIntentRouting { public const uint FullRotation = 1000016; public static bool Area => Fixture.Area; public static Scope Enter(uint input) => new(); }
static class HealerSelection {
    public static bool TrySelect(uint input, Preset preset, out uint action) {
        if (preset.IsHeal || preset.IsAoE != Area || input != PveIntentRouting.FullRotation) throw new Exception("Wrong rotation input/provider");
        action = 120;
        if (Injured) SelectionRuntime.Current = new(action, Tank);
        return Injured;
    }
}
static class SGE { public static bool TryTestingSelection(uint i, Preset p, out uint a) { a = 0; return false; } }
enum ActionAttackType { Ability, Spell }
static class Ext { public static ActionAttackType ActionAttackType(this uint a) => global::ActionAttackType.Spell; }
enum ActionType { Action }
static class ActionManager { public static int GetAdjustedCastTime(ActionType t, uint a) => 1500; }
static class OrbwalkerIPC { public static bool CanOrbwalk => false; }
static class SelectionRuntime {
    public static Decision? Current, Submitted;
    public static void BeginPreview(uint i) => Current = null;
    public static Decision? Preview(uint i) => Current;
    public static bool ExecuteAutomatic(Decision d) { Submitted = d; return true; }
}
namespace AccordDiagnostics { static class DiagnosticLog { public static void Event(string k, Func<object> f) {} } }
class Runner {
METHOD
    static void Check(bool ok) { if (!ok) throw new Exception("Rotation-heal regression"); }
    static void Main() {
        // Legacy manual targeting yields no heal with the enemy selected.
        Check(TryRotationButtonHeal() && SelectionRuntime.Submitted == new Decision(120, Tank));
        SelectionRuntime.Submitted = null;
        Injured = false; Check(!TryRotationButtonHeal() && SelectionRuntime.Submitted == null);
        Injured = true; Allow = false; Check(!TryRotationButtonHeal());
        Allow = true; RemainingGCD = 1; Check(!TryRotationButtonHeal());
        RemainingGCD = 0; CastingWhileMoving = true; Check(!TryRotationButtonHeal());
        CastingWhileMoving = false; Area = true; Check(TryRotationButtonHeal());
    }
}
""".replace('METHOD', source[start:end])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Replay.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path / 'Program.cs').write_text(harness)
            subprocess.run(['dotnet', 'run', '--project', str(path / 'Replay.csproj')], check=True, timeout=120)
