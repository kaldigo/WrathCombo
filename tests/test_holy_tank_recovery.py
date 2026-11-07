"""Replay the healer routing and GCD fallback from the Wanderer's Palace pull."""
from pathlib import Path
import subprocess
import tempfile
import unittest


class HolyRecoveryTests(unittest.TestCase):
    def test_aoe_damage_checks_injured_ally_before_holy(self):
        root = Path(__file__).resolve().parents[1]
        source = (root / 'WrathCombo/Engine/HealerSelection.cs').read_text(encoding='utf-8-sig')
        routing = source[source.index('    internal static unsafe SelectionDecision? Recovery('):
                         source.index('    private static unsafe SelectionDecision? RecoveryFor(')]
        fallback = source[source.index('        if (!gcds || !explicitHeal && hp >= 80)'):
                          source.index('    private static uint PlannedWhiteMagePartyHeal')]
        harness = """
record SelectionDecision(uint Action, ulong Recipient);
record Target(ulong? Recipient);
static class SelectionRuntime {
    public static Target SelectAlly(uint action) => new(268581823);
}
static class Replay {
    const uint BasicHeal = 135, PartyHeal = 124, Holy = 139, Input = 1000016;
    const ulong Tank = 268581823, Healer = 268761923;
    static float tankHP = 100f * 3472 / 7168, otherHP = 100;
    static bool allowed = true;
    static bool RecipientFor(uint spell, ulong id, out ulong target) { target = spell == PartyHeal ? Healer : id; return true; }
    static SelectionDecision? Ready(uint input, uint action, ulong target, string reason) =>
        allowed ? new(action, target) : null;
    ROUTING
    static SelectionDecision? RecoveryFor(uint input, ulong targetId, bool area, bool gcds, bool explicitHeal) {
        // Model a synced job with no higher-priority ability ready.
        float hp = area ? (tankHP + 3 * otherHP) / 4 : tankHP;
        FALLBACK
    static void Check(bool pass) { if (!pass) throw new Exception("Healer routing regression"); }
    static void Main() {
        Check(Recovery(Input, true, true, false) == new SelectionDecision(BasicHeal, Tank));
        Check(Recovery(Input, false, true, false) == new SelectionDecision(BasicHeal, Tank));
        Check(Recovery(Input, true, true, true) == new SelectionDecision(PartyHeal, Healer));
        Check(Recovery(Input, true, false, false) == null);
        allowed = false; Check(Recovery(Input, true, true, false) == null); allowed = true;
        otherHP = 60; Check(Recovery(Input, true, true, false) == new SelectionDecision(PartyHeal, Healer));
        otherHP = tankHP = 100; Check((Recovery(Input, true, true, false)?.Action ?? Holy) == Holy);
    }
}
""".replace('ROUTING', routing).replace('FALLBACK', fallback)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'Replay.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
            (path / 'Program.cs').write_text(harness)
            subprocess.run(['dotnet', 'run', '--project', str(path / 'Replay.csproj')], check=True, timeout=120)
