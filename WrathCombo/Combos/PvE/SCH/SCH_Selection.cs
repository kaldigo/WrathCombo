using WrathCombo.Engine;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;
internal partial class SCH
{
    private static bool KeepHealingAetherflow => ExperimentalSelection.ActiveHealer &&
        RotationBehaviour.Support != false && Gauge.Aetherflow == 1 && AetherflowCD > 3 &&
        (EncounterDamage.Tankbuster() is { Recipient: not 0, Seconds: >= 0 and <= 5 } ||
         BossForecastProvider.Raidwide is >= 0 and <= 5);
}
