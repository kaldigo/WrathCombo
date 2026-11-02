using WrathCombo.Combos.PvE;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class ActivityAutomation
{
    internal static bool? PresetOverride(Preset preset) => ActivityPresetPolicy.Resolve(
        preset.ToString(), InPvP(), Options.Enabled,
        preset == Preset.Phantom_Necromancer_DrainTouch && OccultCrescent.Config.Phantom_Necromancer_DrainTouch_Mode != 0);
}
