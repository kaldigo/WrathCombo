"""Exercise the production automatic proposal submission boundary."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]

class AutomaticProposalTests(unittest.TestCase):
    def test_proposal_is_retained_through_ground_selection_and_native_dispatch(self):
        source = (ROOT / 'WrathCombo/Engine/SelectionRuntime.cs').read_text(encoding='utf-8-sig')
        start = source.index('    internal static unsafe bool ExecuteAutomatic(')
        end = source.index('    internal static unsafe bool Press(', start)
        method = source[start:end]
        follow = source[source.index("    internal static void FollowAcceptedEnemy("):source.index("    internal static uint Publish(")]
        harness = r"""
using System;
namespace AccordDiagnostics { static class DiagnosticLog { public static void Event(string kind, Func<object?> data) {} public static bool Reject(string reason) => false; } }
record SelectionDecision(uint Action, ulong Recipient, uint Preparation, int Generation, ulong? EvaluatedEnemy = null, long ObservedAt = 0);
static class Combos { public static class PvE { public static class All { public const uint SingleTargetDPS = 1000000; } } }
namespace WrathFork.Contract { sealed class ActionRouteContext(object policy) : IDisposable { public void Dispose() {} } }
static class ActionRoutes { public static object? For(uint input) => null; }
static class Player { public static Actor Object = new(); public class Actor { public ulong GameObjectId = 9; } }
class IBattleChara(ulong id, bool hostile) {
    public ulong GameObjectId = id;
    public bool IsDead, IsTargetable = true;
    public bool IsHostile() => hostile;
}
static class ExperimentalSelection { public static bool ActiveTargets = true, AcquireVisibleEnemy = true, SwitchEnemyTarget; }
static class Svc {
    public static class Targets { public static IBattleChara? Target; }
    public static class Objects {
        public static IBattleChara Boss = new(42, true), Add = new(43, true), Ally = new(44, false), Self = new(9, false);
        public static IBattleChara? SearchById(ulong id) => id switch { 42 => Boss, 43 => Add, 44 => Ally, 9 => Self, _ => null };
    }
}
enum ActionType { Action }
static class RotationBehaviour {
    public static uint Checked;
    public sealed class ExecuteChecked(uint action) : IDisposable {
        readonly uint previous = Set(action);
        static uint Set(uint action) { var old = Checked; Checked = action; return old; }
        public void Dispose() => Checked = previous;
    }
}
static class HealingGroundSelection {
    public static bool Reject;
    public static uint Input;
    public static uint Select(uint input, uint action) {
        Input = input;
        if (Reject) return 1000000;
        Runtime.automaticDecision = Runtime.automaticDecision! with { Recipient = 42 };
        return action;
    }
}
unsafe struct ActionManager {
    public static int Calls;
    public static ulong Recipient;
    public static bool Accepted = true, Throw;
    public static ActionManager* Instance() => null;
    public bool UseAction(ActionType type, uint action, ulong recipient) {
        if (RotationBehaviour.Checked != action) throw new Exception("Proposal was not protected from replacement");
        Calls++; Recipient = recipient;
        if (Throw) throw new InvalidOperationException();
        return Accepted;
    }
}
static class Runtime {
    public static bool automatic = true, Ready = true;
    public static SelectionDecision? automaticDecision;
    public static uint automaticInput = 1000002;
    public static int Generation = 1;
    static class Data { public static class ActionWatching { public static System.Collections.Generic.Dictionary<uint, ActionRow> ActionSheet = new() { [100] = new(true), [101] = new(false) }; } }
    record ActionRow(bool CanTargetHostile);
    static bool ActionReadyForTarget(uint action, ulong recipient) => Ready;
METHOD
    static void Check(bool condition) { if (!condition) throw new Exception("Automatic submission regression"); }
    public static void Main() {
        var boss = Svc.Objects.Boss;
        var add = Svc.Objects.Add;
        var ally = Svc.Objects.Ally;
        var self = Svc.Objects.Self;
        void Follow(IBattleChara? selected, ulong recipient, IBattleChara? expected) {
            Svc.Targets.Target = selected;
            FollowAcceptedEnemy(100, recipient);
            Check(Svc.Targets.Target == expected);
        }
        Follow(null, 42, boss); Follow(self, 42, boss); Follow(add, 42, add); Follow(ally, 42, ally);
        Svc.Targets.Target = null; FollowAcceptedEnemy(101, 42); Check(Svc.Targets.Target == null);
        Follow(null, 44, null); Follow(null, 9, null);
        boss.IsDead = true; Follow(null, 42, null); boss.IsDead = false;
        boss.IsTargetable = false; Follow(null, 42, null); boss.IsTargetable = true;
        automatic = false; Follow(null, 42, null); automatic = true;
        ExperimentalSelection.AcquireVisibleEnemy = false; ExperimentalSelection.SwitchEnemyTarget = true;
        Follow(null, 42, null); Follow(self, 42, self); Follow(add, 42, boss);
        ExperimentalSelection.AcquireVisibleEnemy = true; ExperimentalSelection.SwitchEnemyTarget = false;
        automaticDecision = new(100, 9, 0, Generation, 42, Environment.TickCount64);
        Follow(self, 9, boss); Follow(add, 9, add);
        automaticDecision = automaticDecision with { ObservedAt = Environment.TickCount64 - 1000 };
        Follow(self, 9, self);
        var proposal = new SelectionDecision(100, 2, 0, 1);
        automaticDecision = proposal;
        Check(ExecuteAutomatic(proposal) && ActionManager.Calls == 1 && ActionManager.Recipient == 42 && HealingGroundSelection.Input == automaticInput);
        Check(RotationBehaviour.Checked == 0);
        automaticDecision = proposal with { Preparation = 100 };
        Check(ExecuteAutomatic(automaticDecision) && ActionManager.Recipient == 9);
        int before = ActionManager.Calls;
        automaticDecision = proposal; automatic = false; Check(!ExecuteAutomatic(proposal)); automatic = true;
        Generation = 2; Check(!ExecuteAutomatic(proposal)); Generation = 1;
        Check(!ExecuteAutomatic(proposal with { Action = 101 }));
        HealingGroundSelection.Reject = true; Check(!ExecuteAutomatic(proposal)); HealingGroundSelection.Reject = false;
        Ready = false; Check(!ExecuteAutomatic(proposal)); Ready = true;
        Check(ActionManager.Calls == before);
        automaticDecision = proposal; ActionManager.Accepted = false; Check(!ExecuteAutomatic(proposal));
        automaticDecision = proposal; ActionManager.Throw = true;
        try { ExecuteAutomatic(proposal); throw new Exception("Expected native exception"); } catch (InvalidOperationException) { }
        Check(RotationBehaviour.Checked == 0);
    }
}
""".replace('METHOD', follow + method)
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            (directory / 'Review.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><Nullable>enable</Nullable></PropertyGroup></Project>')
            (directory / 'Program.cs').write_text(harness)
            result = subprocess.run(['dotnet', 'run', '--project', str(directory / 'Review.csproj'), '-c', 'Release'], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
