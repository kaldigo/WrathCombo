using Dalamud.Game.ClientState.Objects.Types;
using WrathCombo.Core;
using All = WrathCombo.Combos.PvE.All;
using WrathCombo.Engine;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Extensions;
using WrathCombo.Native;
using static WrathCombo.Combos.PvP.SCHPvP.Config;
using static WrathCombo.Window.Functions.UserConfig;

namespace WrathCombo.Combos.PvP;

internal static class SCHPvP
{
    #region IDS
    internal class Role : PvPHealer;
    public const uint
        Broil = 29231,
        Adloquium = 29232,
        Biolysis = 29233,
        DeploymentTactics = 29234,
        Expedient = 29236,
        ChainStratagem = 29716;


    internal class Buffs
    {
        internal const ushort
            Catalyze = 3088,
            Recitation = 3094;
    }
    internal class Debuffs
    {
        internal const ushort
            Biolysis = 3089,
            Biolytic = 3090;
    }
    #endregion

    #region Config
    public static class Config
    {
        public static UserBool
            SCHPvP_Adlo_Retarget = new("SCHPvP_Adlo_Retarget");
        public static UserInt
            SCHPvP_DiabrosisThreshold = new("SCHPvP_DiabrosisThreshold"),
            SCHPvP_AdloThreshold = new("SCHPvP_AdloThreshold");

        internal static void Draw(Preset preset)
        {
            switch (preset)
            {
                case Preset.SCHPvP_Diabrosis:
                    DrawSliderInt(0, 100, SCHPvP_DiabrosisThreshold, $"Target HP% to use {PvPHealer.Diabrosis.ActionName()}");
                    break;

                case Preset.SCHPvP_Adlo:
                    DrawAdditionalBoolChoice(SCHPvP_Adlo_Retarget, $"Retarget {Adloquium.ActionName()}", "");
                    DrawSliderInt(1, 100, SCHPvP_AdloThreshold, $"HP% to use {Adloquium.ActionName()}");
                    break;
                
            }
        }
    }
    #endregion
          
    internal class SCHPvP_Burst : CustomCombo
    {
        protected internal override Preset Preset => Preset.SCHPvP_Burst;

        protected override uint Invoke(uint actionID)
        {

            if (!CustomActionHelper.OneButtonRotationChecker(actionID, CustomActionType.SingleTargetDPS, Broil))
                return actionID;

            if (PvpAllySelection.Active && IsEnabled(Preset.SCHPvP_Adlo))
            {
                var recipient = PvpAllySelection.Resolve(Adloquium, ally => !ally.HasStatus(Buffs.Catalyze) &&
                    GetTargetHPPercent(ally) <= SCHPvP_AdloThreshold);
                if (recipient is not null && ActionReadyForTarget(Adloquium, recipient.GameObjectId))
                    return PvpAllySelection.Publish(actionID, Adloquium, recipient, "Scholar recovery on an eligible unshielded ally");
            }

            // Uses Chain Stratagem when available
            if (IsEnabled(Preset.SCHPvP_ChainStratagem) && IsOffCooldown(ChainStratagem) &&
                (!PvpAllySelection.Active || CurrentTarget is { } chainTarget && PvpTargetEligibility.CanDamage(ChainStratagem, chainTarget)))
                return ChainStratagem;                    

            // Uses Expedient when available and target isn't affected with Biolysis
            if (IsEnabled(Preset.SCHPvP_Expedient) && IsOffCooldown(Expedient) && GetCooldownRemainingTime(Biolysis) < 3 &&
                (!PvpAllySelection.Active || ActionRoutes.Allowed(Biolysis) && CurrentTarget is { } dotTarget && PvpTargetEligibility.CanDamage(Biolysis, dotTarget)))
                return Expedient;

            // Uses Biolysis on cooldown or with Recitation when Expedient is enabled with safety for too long of an expedient cooldown. 
            if (IsEnabled(Preset.SCHPvP_Biolysis) && IsOffCooldown(Biolysis) &&
                (!PvpAllySelection.Active || CurrentTarget is { } bioTarget && PvpTargetEligibility.CanDamage(Biolysis, bioTarget)))
            {
                if (IsNotEnabled(Preset.SCHPvP_Expedient) ||(LocalPlayer.HasStatus(Buffs.Recitation) || GetCooldownRemainingTime(Expedient) > 5))
                    return Biolysis;
            }
            //Uses Diabrosis when below set health
            if (IsEnabled(Preset.SCHPvP_Diabrosis) && PvPHealer.CanDiabrosis() && HasTarget() && 
                GetTargetHPPercent() <= SCHPvP_DiabrosisThreshold)
                return PvPHealer.Diabrosis;

            // Uses Deployment Tactics when available
            if ((IsEnabled(Preset.SCHPvP_DeploymentTactics) || PvPIntentRouting.Area) &&
                GetRemainingCharges(DeploymentTactics) > 1 && CurrentTarget.HasStatus(Debuffs.Biolysis))
                return DeploymentTactics;

            // Adds Adloquium when at or below threshold, will not Overwrite the 10% damage reduction buff to prevent waste
            if (!PvpAllySelection.Active && IsEnabled(Preset.SCHPvP_Adlo))
            {
                IGameObject? healTarget = SCHPvP_Adlo_Retarget ? SimpleTarget.Stack.AllyToHealPVP : SimpleTarget.Stack.Allies;
                
                if (!healTarget.HasStatus(Buffs.Catalyze) && GetTargetHPPercent(healTarget) <= SCHPvP_AdloThreshold && ActionReady(Adloquium))
                    return SCHPvP_Adlo_Retarget
                        ? Adloquium.Retarget(actionID, healTarget)
                        : Adloquium;
            }

            return Broil;
        }
    }
    
    internal class SCHPvP_RetargetAdlo : CustomCombo
    {
        protected internal override Preset Preset => Preset.SCHPvP_RetargetAdlo;

        protected override uint Invoke(uint actionID)
        {
            if (PvpAllySelection.Active && actionID == Adloquium)
            {
                var recipient = PvpAllySelection.Resolve(Adloquium);
                if (recipient is null) return All.Cease;
                if (PvPIntentRouting.Enabled && recipient.HasStatus(Buffs.Catalyze) && GetRemainingCharges(DeploymentTactics) > 1 &&
                    ActionRoutes.Allowed(DeploymentTactics) && recipient.CanUseOn(DeploymentTactics) && NumberOfAlliesInRange(DeploymentTactics, recipient) > 1)
                    return PvpAllySelection.Publish(actionID, DeploymentTactics, recipient, "Spread the selected ally's shield");
                return PvpAllySelection.Publish(actionID, Adloquium, recipient, "Explicit Scholar healing recipient");
            }
            if(PvPIntentRouting.Enabled && actionID==Adloquium)
            {
                var ally=SimpleTarget.Stack.AllyToHealPVP;
                if(ally is not null && ally.HasStatus(Buffs.Catalyze) && GetRemainingCharges(DeploymentTactics)>1 &&
                    ally.CanUseOn(DeploymentTactics) && NumberOfAlliesInRange(DeploymentTactics,ally)>1)
                    return DeploymentTactics.Retarget(actionID,ally);
            }
            return actionID is not Adloquium 
                ? actionID 
                : Adloquium.Retarget(SimpleTarget.Stack.AllyToHealPVP);
        }
    }
}
