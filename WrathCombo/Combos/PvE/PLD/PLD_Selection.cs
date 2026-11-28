using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class PLD
{
    private static uint SelectDamageGcd(Combo flags)
    {
        uint action = flags.HasFlag(Combo.AoE) ? AoECombo : STCombo;
        TryGCDAttacks(flags, ref action);
        return action;
    }
}
