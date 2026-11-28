using ECommons.GameHelpers;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.CustomComboNS;
using WrathCombo.Engine;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class PCT
{
    private static uint DeadlineGcd(Combo flags)
    {
        if (!ExperimentalSelection.ActivePictomancer) return 0;
        bool simple = flags.HasFlag(Combo.Simple), area = flags.HasFlag(Combo.AoE);
        bool Ready(uint action, Preset single, Preset aoe) => (simple || IsEnabled(area ? aoe : single)) &&
            ActionRoutes.Allowed(action) && ActionReady(action) && InActionRange(action);
        uint hammer = OriginalHook(HammerStamp);
        int steps = hammer == PolishingHammer ? 1 : hammer == HammerBrush ? 2 : 3;
        return EarnedActionDeadline.Choose([
            new(StarPrism, LocalPlayer.Status(Buffs.Starstruck).RemainingTimeOrZero(),
                Ready(StarPrism, Preset.PCT_ST_AdvancedMode_StarPrism, Preset.PCT_AoE_AdvancedMode_StarPrism)),
            new(RainbowDrip, LocalPlayer.Status(Buffs.RainbowBright).RemainingTimeOrZero(),
                Ready(RainbowDrip, Preset.PCT_ST_AdvancedMode_RainbowDrip, Preset.PCT_AoE_AdvancedMode_RainbowDrip)),
            new(hammer, LocalPlayer.Status(Buffs.HammerTime).RemainingTimeOrZero(),
                Ready(hammer, Preset.PCT_ST_AdvancedMode_HammerStampCombo, Preset.PCT_AoE_AdvancedMode_HammerStampCombo), steps)
        ], RemainingGCD, GCDTotal > 0 ? GCDTotal : 2.5f);
    }

    internal static uint BindSwiftcast(uint input, uint result, Preset preset)
    {
        if (!ExperimentalSelection.ActivePictomancer || SelectionRuntime.ScriptedOpener || result != Role.Swiftcast ||
            preset is not (Preset.PCT_ST_SimpleMode or Preset.PCT_AoE_SimpleMode or Preset.PCT_ST_AdvancedMode or Preset.PCT_AoE_AdvancedMode)) return result;
        bool area = preset is Preset.PCT_AoE_SimpleMode or Preset.PCT_AoE_AdvancedMode;
        bool simple = preset is Preset.PCT_ST_SimpleMode or Preset.PCT_AoE_SimpleMode;
        Combo flags = (area ? Combo.AoE : Combo.ST) | (simple ? Combo.Simple : Combo.Adv);
        uint motif = 0;
        return TryDrawMotif(flags, ref motif, preparingSwiftcast: true)
            ? PreparedCast.Prepare(input, motif, Player.Object!.GameObjectId) : All.Cease;
    }

    internal static uint ForecastAlternative(uint input, uint blocked, Preset preset)
    {
        bool area = preset is Preset.PCT_AoE_SimpleMode or Preset.PCT_AoE_AdvancedMode;
        bool simple = preset is Preset.PCT_ST_SimpleMode or Preset.PCT_AoE_SimpleMode;
        Combo flags = (area ? Combo.AoE : Combo.ST) | (simple ? Combo.Simple : Combo.Adv);
        uint action = blocked;
        if (!TryMovementOption(flags, ref action, forecast: true)) return All.Cease;
        return action == Role.Swiftcast ? BindSwiftcast(input, action, preset) : action;
    }
}
