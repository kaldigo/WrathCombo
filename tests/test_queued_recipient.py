"""Exercise the native hook's target selection and queue write with game state stubbed."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class QueuedRecipientTests(unittest.TestCase):
    def test_auto_targets_boss_after_healing_and_preserves_active_queue(self):
        root = Path(__file__).resolve().parents[1] / 'WrathCombo'
        hook = (root / 'Data/ActionWatching.cs').read_text(encoding='utf-8-sig')
        select = hook[hook.index('ulong queuedRecipient ='):hook.index('var areaTargeted =', hook.index('ulong queuedRecipient ='))]
        write = hook[hook.index('if (queuedRecipient == 0'):hook.index('actionManager->QueuedTargetId = targetId;') + len('actionManager->QueuedTargetId = targetId;')]
        harness = """
using Engine = WrathCombo.Engine;
namespace WrathCombo.Engine { static class SelectionRuntime { public static bool Automatic = true; } }
namespace AccordDiagnostics { static class DiagnosticLog { public static bool Reject(string reason) => false; } }
static class Service { public static class Configuration { public static bool OverwriteQueue; } }
struct Target { public ulong Id; public static implicit operator Target(ulong id) => new() { Id = id }; }
struct ActionManager { public enum UseActionMode { None, Queue } public bool ActionQueued; public Target QueuedTargetId; }
static unsafe class Replay {
    const ulong healer = 268761923, boss = 1073833297;
    static bool Press(ActionManager* actionManager, bool strictDecision, ActionManager.UseActionMode mode, bool willQueue, out ulong targetId) {
        targetId = boss;
        ulong? testingRecipient = strictDecision ? boss : null;
        bool manualEnemy = false, changed = true;
        SELECT
        if (willQueue && changed) { WRITE }
        return true;
    }
    static void Check(bool ok) { if (!ok) throw new Exception("Unexpected queue delivery"); }
    static void Main() {
        foreach (bool overwrite in new[] { false, true })
        foreach (bool strict in new[] { false, true }) {
            Service.Configuration.OverwriteQueue = overwrite;
            ActionManager am = new() { ActionQueued = false, QueuedTargetId = healer };
            Check(Press(&am, strict, ActionManager.UseActionMode.None, false, out var delivered) && delivered == boss);
            Check(Press(&am, strict, ActionManager.UseActionMode.None, true, out delivered) && delivered == boss && am.QueuedTargetId.Id == boss);
        }
        ActionManager active = new() { ActionQueued = true, QueuedTargetId = healer };
        Service.Configuration.OverwriteQueue = false;
        Check(!Press(&active, true, ActionManager.UseActionMode.None, true, out _));
        Check(Press(&active, false, ActionManager.UseActionMode.Queue, false, out var queued) && queued == healer);
        Service.Configuration.OverwriteQueue = true;
        Check(Press(&active, true, ActionManager.UseActionMode.None, true, out queued) && queued == boss && active.QueuedTargetId.Id == boss);
    }
}
""".replace('SELECT', select).replace('WRITE', write)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
            (path / 'Program.cs').write_text(harness)
            (path / 'RetargetDelivery.cs').write_bytes((root / 'Engine/Selection/RetargetDelivery.cs').read_bytes())
            subprocess.run(['dotnet', 'run', '--project', str(path / 'Test.csproj')], check=True, timeout=120)
