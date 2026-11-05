using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Extensions;
using WrathCombo.Native;
using static WrathCombo.Combos.PvP.MCHPvP.Config;
using static WrathCombo.Window.Functions.UserConfig;

namespace WrathCombo.Combos.PvP;

internal static partial class MCHPvP
{
    #region IDS
    internal class Role : PvPPhysRanged;
    public const uint
        BlastCharge = 29402,
        BlazingShot = 41468,
        Scattergun = 29404,
        Drill = 29405,
        BioBlaster = 29406,
        AirAnchor = 29407,
        ChainSaw = 29408,
        Wildfire = 29409,
        BishopTurret = 29412,
        AetherMortar = 29413,
        Analysis = 29414,
        MarksmanSpite = 29415,
        FullMetalField = 41469;

    public static class Buffs
    {
        public const ushort
            Heat = 3148,
            Overheated = 3149,
            DrillPrimed = 3150,
            BioblasterPrimed = 3151,
            AirAnchorPrimed = 3152,
            ChainSawPrimed = 3153,
            Analysis = 3158;
    }

    public static class Debuffs
    {
        public const ushort
            Wildfire = 1323;
    }
    #endregion

    #region Config
    public static class Config
    {
        public static UserInt
            MCHPvP_MarksmanSpite = new("MCHPvP_MarksmanSpite"),
            MCHPvP_FMFOption = new("MCHPvP_FMFOption"),
            MCHPvP_EagleThreshold = new("MCHPvP_EagleThreshold");

        internal static void Draw(Preset preset)
        {
            switch (preset)
            {
                case Preset.MCHPvP_BurstMode_MarksmanSpite:
                    DrawSliderInt(0, 36000, MCHPvP_MarksmanSpite, "Use Marksman's Spite when the target is below set HP");
                    break;

                case Preset.MCHPvP_BurstMode_FullMetalField:
                    ImGui.Indent();
                    DrawHorizontalRadioButton(MCHPvP_FMFOption, "Full Metal Field Wildfire combo",
                        "Uses Full Metal Field when Wildfire is ready.", 1);

                    DrawHorizontalRadioButton(MCHPvP_FMFOption, "Full Metal Field only when Overheated",
                        "Only uses Full Metal Field while Overheated.", 2);
                    ImGui.Unindent();
                    break;

                case Preset.MCHPvP_Eagle:
                    DrawSliderInt(0, 100, MCHPvP_EagleThreshold,
                        "Target HP percent threshold to use Eagle Eye Shot Below.");
                    break;
            }
        }            
    }
#endregion

    internal class MCHPvP_BurstMode : CustomCombo
    {
        protected internal override Preset Preset => Preset.MCHPvP_BurstMode;
        protected override uint Invoke(uint actionID)
        {
            if (!CustomActionHelper.OneButtonRotationChecker(actionID, CustomActionType.SingleTargetDPS, BlastCharge))
                return actionID;

            if (TryPreparedTool(actionID, out uint preparedTool)) return preparedTool;

            #region Variables
            var canWeave = CanWeave();
            var analysisStacks = GetRemainingCharges(Analysis);
            var bigDamageStacks = GetRemainingCharges(OriginalHook(Drill));
            var overheated = LocalPlayer.HasStatus(Buffs.Overheated);
            uint plannedTool = Engine.ExperimentalSelection.ActivePvpMachinist ? PlannedTool() : 0;
            #endregion

            if (IsEnabled(Preset.MCHPvP_Eagle) && PvPPhysRanged.CanEagleEyeShot() && (PvPCommon.TargetImmuneToDamage() || GetTargetHPPercent() <= MCHPvP_EagleThreshold))
                return PvPPhysRanged.EagleEyeShot;
            if(Engine.PvPIntentRouting.Active && canWeave && CurrentTarget.HasStatus(Debuffs.Wildfire) &&
                CurrentTarget.Status(Debuffs.Wildfire).RemainingTimeOrZero()<=1 && OriginalHook(Wildfire)!=Wildfire)
                return Engine.PvPIntentRouting.BoundRelease(actionID, Wildfire, OriginalHook(Wildfire));

            // Guard is a damage reduction, not absolute immunity. Drill's reviewed
            // PvP exception must remain available without bypassing true immunity.
            if (Engine.ExperimentalSelection.ActivePvpMachinist && plannedTool == Drill &&
                (CurrentTarget.HasStatus(PvPCommon.Buffs.Guard, true) || CurrentTarget.HasStatus(PvPCommon.Buffs.WeakenedGuard, true)) &&
                !CurrentTarget.HasStatus(VPRPvP.Buffs.HardenedScales, true) && !PvPCommon.TargetImmuneToDamage(false))
                return WantsAnalysis(plannedTool, canWeave, overheated) ? PrepareAnalysis(actionID, plannedTool) : plannedTool;

            if (!PvPCommon.TargetImmuneToDamage() && HasBattleTarget())
            {
                // MarksmanSpite execute condition
                if (IsEnabled(Preset.MCHPvP_BurstMode_MarksmanSpite) && HasBattleTarget() && GetTargetCurrentHP() < MCHPvP_MarksmanSpite && IsLB1Ready)
                    return MarksmanSpite;

                if (IsEnabled(Preset.MCHPvP_BurstMode_Wildfire) && canWeave && overheated && IsOffCooldown(Wildfire))
                    return OriginalHook(Wildfire);

                // FullMetalField condition when not overheated or if overheated and FullMetalField is off cooldown
                if (IsEnabled(Preset.MCHPvP_BurstMode_FullMetalField) && IsOffCooldown(FullMetalField))
                {
                    if (MCHPvP_FMFOption == 1)
                    {
                        if (!overheated && IsOffCooldown(Wildfire))
                            return FullMetalField;
                    }
                    if (MCHPvP_FMFOption == 2)
                    {
                        if (overheated)
                            return FullMetalField;
                    }
                }
                // Check if primed buffs and analysis conditions are met
                bool hasPrimedBuffs = LocalPlayer.HasStatus(Buffs.DrillPrimed) ||
                                      (LocalPlayer.HasStatus(Buffs.ChainSawPrimed) && !IsEnabled(Preset.MCHPvP_BurstMode_AltAnalysis)) ||
                                      (LocalPlayer.HasStatus(Buffs.AirAnchorPrimed) && IsEnabled(Preset.MCHPvP_BurstMode_AltAnalysis));

                if (IsEnabled(Preset.MCHPvP_BurstMode_Analysis))
                {
                    if (Engine.ExperimentalSelection.ActivePvpMachinist ? WantsAnalysis(plannedTool, canWeave, overheated) :
                        hasPrimedBuffs && !LocalPlayer.HasStatus(Buffs.Analysis) && !JustUsed(Analysis, 2f) && analysisStacks > 0 &&
                        (!IsEnabled(Preset.MCHPvP_BurstMode_AltDrill) || IsOnCooldown(Wildfire)) &&
                        !canWeave && !overheated && bigDamageStacks > 0)
                    {
                        return Engine.ExperimentalSelection.ActivePvpMachinist ? PrepareAnalysis(actionID, plannedTool) : OriginalHook(Analysis);
                    }
                }
                // BigDamageStacks logic with checks for primed buffs
                if (Engine.ExperimentalSelection.ActivePvpMachinist)
                    return plannedTool != 0 ? plannedTool : BlastCharge;
                if (bigDamageStacks > 0)
                {
                    if (IsEnabled(Preset.MCHPvP_BurstMode_Drill) && LocalPlayer.HasStatus(Buffs.DrillPrimed))
                        return OriginalHook(Drill);

                    if (IsEnabled(Preset.MCHPvP_BurstMode_BioBlaster) && LocalPlayer.HasStatus(Buffs.BioblasterPrimed) && HasBattleTarget() && InActionRange(BioBlaster))
                        return OriginalHook(BioBlaster);

                    if (IsEnabled(Preset.MCHPvP_BurstMode_AirAnchor) && LocalPlayer.HasStatus(Buffs.AirAnchorPrimed))
                        return OriginalHook(AirAnchor);

                    if (IsEnabled(Preset.MCHPvP_BurstMode_ChainSaw) && LocalPlayer.HasStatus(Buffs.ChainSawPrimed))
                        return OriginalHook(ChainSaw);
                }
            }
            return BlastCharge;
        }

        private static uint PlannedTool()
        {
            if (!HasBattleTarget()) return 0;
            (uint Action, ushort Status, Preset Permission)[] tools =
            [
                (Drill, Buffs.DrillPrimed, Preset.MCHPvP_BurstMode_Drill),
                (BioBlaster, Buffs.BioblasterPrimed, Preset.MCHPvP_BurstMode_BioBlaster),
                (AirAnchor, Buffs.AirAnchorPrimed, Preset.MCHPvP_BurstMode_AirAnchor),
                (ChainSaw, Buffs.ChainSawPrimed, Preset.MCHPvP_BurstMode_ChainSaw),
            ];
            foreach (var tool in tools)
                if (IsEnabled(tool.Permission) && LocalPlayer.HasStatus(tool.Status) && Engine.ActionRoutes.Allowed(tool.Action) &&
                    InActionRange(tool.Action, CurrentTarget) && ActionReadyForTarget(tool.Action, CurrentTarget.GameObjectId)) return tool.Action;
            return 0;
        }

        private static bool WantsAnalysis(uint plannedTool, bool canWeave, bool overheated)
        {
            var snapshot = new Engine.Selection.PvpMchPreparationInput(plannedTool,
                IsEnabled(Preset.MCHPvP_BurstMode_Analysis) && ActionReady(Analysis) && !JustUsed(Analysis, 2f) &&
                (!IsEnabled(Preset.MCHPvP_BurstMode_AltDrill) || IsOnCooldown(Wildfire)) && !canWeave,
                LocalPlayer.HasStatus(Buffs.Analysis), overheated, IsEnabled(Preset.MCHPvP_BurstMode_AltAnalysis));
            bool result = Engine.Selection.PvpMchPreparation.Analysis(snapshot);
            Engine.TestingApi.RecordDecision("PvP.MCH.Analysis", snapshot, result);
            return result;
        }
    }
}
