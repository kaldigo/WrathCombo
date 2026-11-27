using WrathCombo.Engine;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class DRK
{
    private static uint SelectDamageGcd(Combo flags)
    {
        uint selected = flags.HasFlag(Combo.AoE) ? Unleash : HardSlash;
        bool cooldowns = flags.HasFlag(Combo.Simple) || IsEnabled(flags.HasFlag(Combo.AoE) ? Preset.DRK_AoE_CDs : Preset.DRK_ST_CDs) &&
            (flags.HasFlag(Combo.AoE) || (int)Config.DRK_ST_CDsBossRequirement != (int)Config.BossRequirement.On || InBossEncounter());
        if (cooldowns && TryGetAction<Cooldown>(flags, ref selected, true)) return selected;
        bool spenders = flags.HasFlag(Combo.Simple) || IsEnabled(flags.HasFlag(Combo.AoE) ? Preset.DRK_AoE_Spenders : Preset.DRK_ST_Spenders);
        if (spenders && new Spender().TryGetBloodAction(flags, ref selected)) return selected;
        TryGetAction<Core>(flags, ref selected);
        return selected;
    }
}
