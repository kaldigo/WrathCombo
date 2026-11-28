from pathlib import Path
import subprocess
import tempfile
import unittest


class VariantRecoveryTests(unittest.TestCase):
    def test_real_recovery_selection(self):
        root = Path(__file__).resolve().parents[1]
        text = (root / 'WrathCombo/Combos/PvE/Content/Variant/Variant_Helper.cs').read_text(encoding='utf-8-sig')
        start = text.index('        private static bool CheckRecovery(')
        stop = text.index('        private static bool CheckRaise(', start)
        method = text[start:stop]
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            (path / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(r'''
using Engine = Fixture.Engine;
Fixture.Run();
static class Fixture {
    enum Preset { Cure }
    static uint Cure = 46939;
    static bool Enabled = true, CureReady, WindReady = true, Weave;
    internal static float Health = 19;
    static bool IsEnabled(Preset p) => Enabled;
    static bool ActionReady(uint action) => CureReady;
    static bool CanWeave() => Weave;
    static float PlayerHealthPercentageHp() => Health;
    static class RoleActions { public static class Physical {
        public const uint SecondWind = 7541;
        public static bool CanSecondWind(int threshold) => WindReady && Health <= threshold;
    }}
    internal static class Engine {
        internal static class EncounterDamage { public static bool Needed = true; public static bool ShouldSelfHeal(uint a, float t) => Needed; }
        internal static class RotationBehaviour { public static bool? Support; }
        internal static class ActionRoutes { public static uint Excluded; public static bool Allowed(uint a) => Excluded != a; }
        internal static class NativeExecution {
            public static bool Legal = true;
            public static NativeExecutionState Capture() => new();
        }
        internal struct NativeExecutionState { public bool CanSubmit(bool gcd, float queue) => NativeExecution.Legal; }
    }
METHOD
    public static void Run() {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        Check(CheckRecovery(Preset.Cure, 50, 50, out var a) && a == 7541, "GCD-locked Cure hid a legal emergency Second Wind");
        Engine.NativeExecution.Legal = false;
        Check(!CheckRecovery(Preset.Cure, 50, 50, out a), "Native animation/cast lock was bypassed");
        Engine.NativeExecution.Legal = true; Engine.ActionRoutes.Excluded = 7541; CureReady = true;
        Check(CheckRecovery(Preset.Cure, 50, 50, out a) && a == Cure, "Excluded Second Wind overrode Cure");
        Engine.ActionRoutes.Excluded = 0; WindReady = false;
        Check(CheckRecovery(Preset.Cure, 50, 50, out a) && a == Cure, "Unavailable Second Wind blocked Cure");
        WindReady = true;
        Check(CheckRecovery(Preset.Cure, 50, null, out a) && a == Cure, "Disabled job recovery permission was ignored");
        Check(CheckRecovery(Preset.Cure, 50, 0, out a) && a == Cure, "Disabled Second Wind threshold was ignored");
        Engine.RotationBehaviour.Support = false;
        Check(CheckRecovery(Preset.Cure, 50, 50, out a) && a == Cure, "Support off enabled Second Wind");
        Engine.RotationBehaviour.Support = null; Health = 56;
        Check(CheckRecovery(Preset.Cure, 50, 50, out a) && a == Cure, "Predictive Cure silently raised Second Wind threshold");
        Health = 40;
        Check(CheckRecovery(Preset.Cure, 50, 50, out a) && a == 7541, "GCD-ready Cure hid an independently legal Second Wind");
        Weave = true;
        Check(CheckRecovery(Preset.Cure, 50, 50, out a) && a == 7541, "Ordinary legal recovery weave was lost");
        Check(CheckRecovery(Preset.Cure, 50, 25, out a) && a == Cure, "Job's lower custom threshold was changed");
        Enabled = false;
        Check(!CheckRecovery(Preset.Cure, 50, 50, out a), "Disabled Variant route changed selection");
        Enabled = true; Engine.EncounterDamage.Needed = false;
        Check(!CheckRecovery(Preset.Cure, 50, 50, out a), "Healthy player spent recovery");
        Engine.EncounterDamage.Needed = true; Cure = 0;
        Check(!CheckRecovery(Preset.Cure, 50, 50, out a), "Unresolved territory action was queried");
        Console.WriteLine("Production Variant recovery: 14 behavioral checks passed.");
    }
}
'''.replace('METHOD', method), encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', str(path / 'Test.csproj'), '-p:UseSharedCompilation=false'], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_all_existing_physical_providers_pass_permission(self):
        root = Path(__file__).resolve().parents[1] / 'WrathCombo/Combos/PvE'
        for job in ['BRD', 'DNC', 'MCH', 'MNK', 'DRG', 'NIN', 'SAM', 'RPR', 'VPR']:
            source = (root / job / (job + '.cs')).read_text(encoding='utf-8-sig')
            calls = [line for line in source.splitlines() if 'ContentSpecificActions.TryGet(' in line]
            self.assertEqual(sum('secondWindThreshold:' in line for line in calls), 2 if job == 'SAM' else 4, job)
            # Advanced providers pass the same threshold and permission their
            # existing Second Wind branches use; simple SAM has no such branch.
            for line in calls:
                if 'secondWindThreshold:' in line and '? (int)' in line:
                    threshold = line.split('? (int)', 1)[1].split(' : null', 1)[0]
                    self.assertIn('CanSecondWind(' + threshold + ')', source if job != 'BRD' else (root / 'BRD/BRD_Helper.cs').read_text(encoding='utf-8-sig').replace('seconWindThreshold)', threshold + ')'))


if __name__ == '__main__':
    unittest.main()
