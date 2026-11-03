using System;
using System.Collections.Generic;
using ECommons.GameHelpers;
using WrathCombo.Services;
using WrathCombo.Data;
using WrathFork.Contract;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class RotationBehaviour
{
    [ThreadStatic] internal static uint CheckedAction;
    internal sealed class ExecuteChecked(uint action) : IDisposable
    {
        private readonly uint previous = Set(action);
        private static uint Set(uint action) { var old = CheckedAction; CheckedAction = action; return old; }
        public void Dispose() => CheckedAction = previous;
    }
    internal static bool Managed => !InPvP() && Options.Enabled(Feature.RunnerAutomation);
    internal static RotationPreferences? Preferences => Managed && Service.Configuration.RotationPreferences.TryGetValue(Options.Job, out var p) ? p : null;
    internal static bool? Support => Managed ? Options.Enabled(Feature.RunnerSupport) : null;
    internal static bool? Protection => Managed ? Options.Enabled(Feature.RunnerProtection) : null;
    internal static bool? Responses => Managed ? Support == true || Protection == true : null;
    internal static bool Activity => !InPvP() && (Content.ContentType is ContentType.Dungeon or ContentType.DeepDungeon or ContentType.Variant or ContentType.Criterion or ContentType.Trial or ContentType.FieldOperations or ContentType.FieldRaid or ContentType.ARaid or ContentType.Raid);
    internal static readonly HashSet<string> Openers = new("AST_ST_DPS_Opener BLM_ST_Opener BRD_ST_Adv_Balance_Standard DNC_ST_BalanceOpener DRK_ST_BalanceOpener DRG_ST_Opener GNB_ST_Opener MCH_ST_Adv_Opener MNK_STUseOpener NIN_ST_AdvancedMode_BalanceOpener PCT_ST_Advanced_Openers PLD_ST_AdvancedMode_BalanceOpener RPR_ST_Opener RDM_Balance_Opener SGE_ST_Adv_DPS_Opener SAM_ST_Adv_Opener SCH_ST_ADV_DPS_Balance_Opener SMN_ST_Advanced_Combo_Balance_Opener VPR_ST_Opener WAR_ST_BalanceOpener WHM_ST_MainCombo_Opener".Split(' '));
    internal static bool? PresetOverride(Preset preset) => Openers.Contains(preset.ToString()) ? Preferences?.Openers : null;
}
