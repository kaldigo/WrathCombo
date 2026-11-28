using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class MNK
{
    private static uint RuminationDeadline(bool area, bool simple)
    {
        if (!ExperimentalSelection.ActiveMonk || LocalPlayer.HasStatus(Buffs.PerfectBalance) ||
            LocalPlayer.HasStatus(Buffs.FormlessFist) || !IsOriginal(MasterfulBlitz)) return 0;
        bool Eligible(uint action, Preset st, Preset aoe) => (simple || IsEnabled(area ? aoe : st)) &&
            ActionRoutes.Allowed(action) && ActionReady(action) && InActionRange(action);
        // Starting another PB sequence must not bury a reply that cannot survive it.
        int slots = ActionReady(PerfectBalance) ? 4 : 1;
        return EarnedActionDeadline.Choose([
            new(FiresReply, LocalPlayer.Status(Buffs.FiresRumination).RemainingTimeOrZero(),
                Eligible(FiresReply, Preset.MNK_STUseFiresReply, Preset.MNK_AoEUseFiresReply), slots),
            new(WindsReply, LocalPlayer.Status(Buffs.WindsRumination).RemainingTimeOrZero(),
                Eligible(WindsReply, Preset.MNK_STUseWindsReply, Preset.MNK_AoEUseWindsReply), slots)
        ], RemainingGCD, GCDTotal > 0 ? GCDTotal : 2.5f);
    }

    private static bool ReplyBeforeBalance => ExperimentalSelection.ActiveMonk &&
        SelectionRuntime.PlannedGcd is FiresReply or WindsReply;
}
