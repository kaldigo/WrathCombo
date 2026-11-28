using System;
using System.Collections.Immutable;
using System.Linq;
using ECommons.GameHelpers;
using WrathCombo.Combos.PvE;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class PartyMitigationCoverage
{
    // Recipient damage-reduction statuses, not similarly named caster/regeneration
    // statuses. IDs verified against the game Status sheet and pinned RSR enum.
    private static readonly ushort[] reductions = [1873, 2711, 849, 3003, SGE.Buffs.Kerachole, SCH.Buffs.SacredSoil];
    internal static bool Covers(PreventionWindow window)
    {
        if (!ExperimentalSelection.SpaceMitigation || window.DueAt is not { } due) return false;
        float impact = Math.Max(0, (due - Environment.TickCount64) / 1000f);
        var party = GetPartyMembers().Select(member => member.BattleChara).Where(actor => actor is { IsDead: false })
            .Append(Player.Object!).DistinctBy(actor => actor!.GameObjectId);
        var snapshot = new MitigationCoverageInput(party.Select(actor => reductions.Max(status => actor.Status(status, true).RemainingTimeOrZero())).ToImmutableArray(), impact);
        bool covered = MitigationCoverage.CoversHit(snapshot);
        TestingApi.RecordDecision("Healing.MitigationCoverage", snapshot, covered);
        return covered;
    }
}
