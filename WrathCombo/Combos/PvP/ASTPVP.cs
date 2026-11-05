using WrathCombo.Engine;
using WrathFork.Contract;
using All = WrathCombo.Combos.PvE.All;
using Dalamud.Game.ClientState.Objects.Types;
using WrathCombo.Core;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Extensions;
using WrathCombo.Native;
using static WrathCombo.Combos.PvP.ASTPvP.Config;
using static WrathCombo.Window.Functions.UserConfig;

namespace WrathCombo.Combos.PvP;

internal static class ASTPvP
{
    #region IDS
    internal class Role : PvPHealer;

    internal const uint
        Malefic = 29242,
        AspectedBenefic = 29243,
        Gravity = 29244,
        DoubleCast = 29245,
        DoubleMalefic = 29246,
        NocturnalBenefic = 29247,
        DoubleBenefic = NocturnalBenefic,
        DoubleGravity = 29248,
        Draw = 29249,
        Macrocosmos = 29253,
        Microcosmos = 29254,
        MinorArcana = 41503,
        Epicycle = 41506,
        Retrograde = 41507,
        Oracle = 41508;

    internal class Buffs
    {
        internal const ushort
            DiurnalBenefic = 3099, 
            LadyOfCrowns = 4328,
            LordOfCrowns = 4329,
            RetrogradeReady = 4331,
            Divining = 4332;
    }
    #endregion

    #region Config
    public static class Config
    {
        internal static UserBool
            ASTPvP_Heal_DoubleCast = new("ASTPvP_Heal_DoubleCast"),
            ASTPvP_BurstHealRetarget = new("ASTPvP_BurstHealRetarget"),
            ASTPvP_BurstHeal_DoubleCast = new("ASTPvP_BurstHeal_DoubleCast"),
            ASTPvP_Heal_Retarget = new("ASTPvP_Heal_Retarget");
        internal static UserInt
            ASTPvP_Burst_PlayCardOption = new("ASTPvP_Burst_PlayCardOption"),
            ASTPvP_Burst_HealThreshold = new("ASTPvP_Burst_HealThreshold"),
            ASTPvP_DiabrosisThreshold = new("ASTPvP_DiabrosisThreshold");

        internal static void Draw(Preset preset)
        {
            switch (preset)
            {
                case Preset.ASTPvP_Burst_PlayCard:
                    DrawHorizontalRadioButton(ASTPvP_Burst_PlayCardOption, "Lord and Lady card play",
                        "Uses Lord and Lady of Crowns when available.", 1);

                    DrawHorizontalRadioButton(ASTPvP_Burst_PlayCardOption, "Lord of Crowns card play",
                        "Only uses Lord of Crowns when available.", 2);

                    DrawHorizontalRadioButton(ASTPvP_Burst_PlayCardOption, "Lady of Crowns card play",
                        "Only uses Lady of Crowns when available.", 3); break;

                case Preset.ASTPvP_Diabrosis:
                    DrawSliderInt(0, 100, ASTPvP_DiabrosisThreshold, "Target HP% to use Diabrosis");
                    break;
                
                case Preset.ASTPvP_Burst_Heal:
                    DrawAdditionalBoolChoice(ASTPvP_BurstHealRetarget, "Retarget", "Retargets Aspected Benefic to the Heal Stack(In Wrath Settings)");
                    DrawAdditionalBoolChoice(ASTPvP_BurstHeal_DoubleCast, "Double Cast", "Adds Doublecast to Aspected Benefic");
                    DrawSliderInt(1, 100, ASTPvP_Burst_HealThreshold, "HP% to use Aspected Benefic");
                    break;
                    
                
                case Preset.ASTPvP_Heal:
                    DrawAdditionalBoolChoice(ASTPvP_Heal_DoubleCast, "Double Cast", "Adds Doublecast to Aspected Benefic");
                    DrawAdditionalBoolChoice(ASTPvP_Heal_Retarget, "Retarget", "Retargets Aspected Benefic to the Heal Stack(In Wrath Settings)");
                    break;
            }
        }
    }
    #endregion

    internal class ASTPvP_Burst : CustomCombo
    {
        protected internal override Preset Preset => Preset.ASTPvP_Burst;

        protected override uint Invoke(uint actionID)
        {
            if (!CustomActionHelper.OneButtonRotationChecker(actionID, CustomActionType.SingleTargetDPS, Malefic))
                return actionID;

            if (PvpAllySelection.Active && IsEnabled(Preset.ASTPvP_Burst_Heal) &&
                TryTestingHealing(actionID, ASTPvP_BurstHeal_DoubleCast, true, out uint healing)) return healing;

            // Card Draw
            if (IsEnabled(Preset.ASTPvP_Burst_DrawCard) && IsOffCooldown(MinorArcana) &&
                (!LocalPlayer.HasStatus(Buffs.LadyOfCrowns) && !LocalPlayer.HasStatus(Buffs.LordOfCrowns)))
                return MinorArcana;

            if (IsEnabled(Preset.ASTPvP_Burst_PlayCard))
            {
                int cardPlayOption = ASTPvP_Burst_PlayCardOption;
                bool hasLadyOfCrowns = LocalPlayer.HasStatus(Buffs.LadyOfCrowns);
                bool hasLordOfCrowns = LocalPlayer.HasStatus(Buffs.LordOfCrowns);

                // Card Playing Split so Lady can still be used if target is immune
                if ((cardPlayOption == 1 && hasLordOfCrowns && !PvPCommon.TargetImmuneToDamage()) ||
                    (cardPlayOption == 1 && hasLadyOfCrowns) ||
                    (cardPlayOption == 2 && hasLordOfCrowns && !PvPCommon.TargetImmuneToDamage()) ||
                    (cardPlayOption == 3 && hasLadyOfCrowns))
                    return OriginalHook(MinorArcana);
            }

            if (!PvPCommon.TargetImmuneToDamage())
            {
                if (IsEnabled(Preset.ASTPvP_Burst_Oracle) && LocalPlayer.HasStatus(Buffs.Divining) && HasBattleTarget())
                    return Oracle;
                
                if (IsEnabled(Preset.ASTPvP_Diabrosis) && PvPHealer.CanDiabrosis() && HasTarget() &&
                    GetTargetHPPercent() <= ASTPvP_DiabrosisThreshold)
                    return PvPHealer.Diabrosis;

                // Macrocosmos only with double gravity or on cooldown when double gravity is disabled
                if (IsEnabled(Preset.ASTPvP_Burst_Macrocosmos) && IsOffCooldown(Macrocosmos) &&
                    (ComboAction == DoubleGravity || !IsEnabled(Preset.ASTPvP_Burst_DoubleGravity)))
                    return Macrocosmos;

                // Double Gravity
                if (IsEnabled(Preset.ASTPvP_Burst_DoubleGravity) && ComboAction == Gravity && HasCharges(DoubleCast))
                    return DoubleGravity;

                // Gravity on cd
                if (IsEnabled(Preset.ASTPvP_Burst_Gravity) && Options.Enabled(Feature.PvPAstGravityInitiation) && IsOffCooldown(Gravity))
                    return Gravity;

                // Double Malefic logic to not leave gravity without a charge
                if (IsEnabled(Preset.ASTPvP_Burst_DoubleMalefic) && ComboAction == Malefic &&
                    (GetRemainingCharges(DoubleCast) > 1 || GetCooldownRemainingTime(Gravity) > 7.5f) && CanWeave())
                    return DoubleMalefic;
                
                if (!PvpAllySelection.Active && !PvPIntentRouting.Active && IsEnabled(Preset.ASTPvP_Burst_Heal))
                {
                    IGameObject? healTarget = ASTPvP_BurstHealRetarget ? SimpleTarget.Stack.AllyToHealPVP : SimpleTarget.Stack.Allies;
                    
                    if (ASTPvP_BurstHeal_DoubleCast && CanWeave() && ComboAction == AspectedBenefic && HasCharges(DoubleCast))
                        return ASTPvP_BurstHealRetarget
                            ? OriginalHook(DoubleCast).Retarget(Malefic, SimpleTarget.Stack.AllyToHealPVP)
                            : OriginalHook(DoubleCast);
                    
                
                    if (!healTarget.HasStatus(Buffs.DiurnalBenefic) && GetTargetHPPercent(healTarget) <= ASTPvP_Burst_HealThreshold && ActionReady(AspectedBenefic))
                        return ASTPvP_BurstHealRetarget
                            ? AspectedBenefic.Retarget(Malefic, healTarget)
                            : AspectedBenefic;
                }
            }
            if(!PvpAllySelection.Active && PvPIntentRouting.Active && IsEnabled(Preset.ASTPvP_Burst_Heal))
            {
                var ally=ASTPvP_BurstHealRetarget ? SimpleTarget.Stack.AllyToHealPVP : SimpleTarget.Stack.Allies;
                if(ASTPvP_BurstHeal_DoubleCast && CanWeave() && ComboAction==AspectedBenefic && HasCharges(DoubleCast))
                {
                    var recipient=PvPIntentRouting.HealingRecipient;
                    if(recipient is not null && recipient.CanUseOn(DoubleBenefic))return DoubleBenefic.Retarget(actionID,recipient);
                }
                if(ally is not null && !ally.HasStatus(Buffs.DiurnalBenefic) && GetTargetHPPercent(ally)<=ASTPvP_Burst_HealThreshold &&
                    ActionReadyForTarget(AspectedBenefic,ally.GameObjectId) && ally.CanUseOn(AspectedBenefic))
                    return AspectedBenefic.Retarget(actionID,ally);
            }
            return OriginalHook(Malefic);
        }
    }

    internal class ASTPvP_Epicycle : CustomCombo
    {
        protected internal override Preset Preset => Preset.ASTPvP_Epicycle;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not Epicycle) 
                return actionID;
            
            if (IsOffCooldown(MinorArcana))
                return MinorArcana;

            if (LocalPlayer.HasStatus(Buffs.RetrogradeReady))
            {
                if (LocalPlayer.HasStatus(Buffs.LordOfCrowns))
                    return OriginalHook(MinorArcana);
                if (IsOffCooldown(Macrocosmos))
                    return Macrocosmos;
            }
            return actionID;
        }
    }

    internal class ASTPvP_Heal : CustomCombo
    {
        protected internal override Preset Preset => Preset.ASTPvP_Heal;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not AspectedBenefic) 
                return actionID;
            if (PvpAllySelection.Active)
                return TryTestingHealing(actionID, ASTPvP_Heal_DoubleCast, false, out uint healing) ? healing : All.Cease;
            if(PvPIntentRouting.Enabled && ASTPvP_Heal_DoubleCast && CanWeave() && ComboAction==AspectedBenefic && HasCharges(DoubleCast))
            {
                var recipient=PvPIntentRouting.HealingRecipient;
                if(recipient is not null && recipient.CanUseOn(DoubleBenefic))return DoubleBenefic.Retarget(actionID,recipient);
                return actionID;
            }
            
            if (ASTPvP_Heal_DoubleCast && CanWeave() && ComboAction == AspectedBenefic && HasCharges(DoubleCast))
                return ASTPvP_Heal_Retarget
                ? OriginalHook(DoubleCast).Retarget(AspectedBenefic, SimpleTarget.Stack.AllyToHealPVP)
                : OriginalHook(DoubleCast);
            
            return ASTPvP_Heal_Retarget
                ? actionID.Retarget(SimpleTarget.Stack.AllyToHealPVP)
                : actionID;
        }
    }

    private static bool TryTestingHealing(uint input, bool doubleCast, bool threshold, out uint result)
    {
        result = 0;
        if (doubleCast && CustomComboFunctions.CanWeave() && CustomComboFunctions.ComboAction == AspectedBenefic && CustomComboFunctions.HasCharges(DoubleCast))
        {
            var retained = PvPIntentRouting.HealingRecipient;
            var recipient = retained is null ? null : PvpAllySelection.Resolve(DoubleBenefic, retained: retained.GameObjectId);
            if (recipient is not null && CustomComboFunctions.ActionReadyForTarget(DoubleBenefic, recipient.GameObjectId))
            { result = PvpAllySelection.Publish(input, DoubleBenefic, recipient, "Double Benefic retains the confirmed healing recipient"); return true; }
        }
        var ally = PvpAllySelection.Resolve(AspectedBenefic, target => !threshold ||
            !target.HasStatus(Buffs.DiurnalBenefic) && CustomComboFunctions.GetTargetHPPercent(target) <= ASTPvP_Burst_HealThreshold);
        if (ally is null || !CustomComboFunctions.ActionReadyForTarget(AspectedBenefic, ally.GameObjectId)) return false;
        result = PvpAllySelection.Publish(input, AspectedBenefic, ally, "Astrologian recovery uses the resolved ally");
        return true;
    }
}
