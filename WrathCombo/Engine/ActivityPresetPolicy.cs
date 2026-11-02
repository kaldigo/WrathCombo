using System;
using System.Collections.Generic;
using System.Linq;
using WrathFork.Contract;

namespace WrathCombo.Engine;

// Only reviewed presets are controlled. Unknown/new upstream options retain their saved values.
internal static class ActivityPresetPolicy
{
    internal static readonly Dictionary<string, Feature> Actions = new();
    internal static readonly HashSet<string> Parents = new();
    internal static readonly HashSet<string> DirectDamage = new();
    internal static readonly HashSet<string> Preserved = new()
    {
        "Phantom_RestrictToBuff", "Phantom_TimeMage_OccultSlowga_Wait",
        "Phantom_RedMage_OccultCureII_Retarget", "Phantom_RedMage_OccultLibra_Refresh",
    };

    static ActivityPresetPolicy()
    {
        foreach (string role in new[] { "Tank", "Healer", "Melee", "PhysRanged", "Magic" })
        {
            string variant = "Variant_" + role;
            Parents.Add(variant);
            Add(variant, Feature.ActivityRecovery, "Cure Raise");
            Add(variant, Feature.ActivityDefense, "Rampart Ultimatum");
            Add(variant, Feature.ActivityOffense, "SpiritDart EagleEyeShot");
            if (role != "Tank") continue; // The upstream shared Bozja runner only implements Tank.
            string bozja = "Bozja_" + role;
            Parents.Add(bozja);
            Add(bozja, Feature.ActivityOffense, "LostFocus LostFontOfPower LostSlash LostFairTrade LostAssassination LostDeath LostBravery BannerOfNobleEnds BannerOfHonoredSacrifice");
            Add(bozja, Feature.ActivityDefense, "LostProtect LostShell LostBubble LostParalyze3 BannerOfHonedAcuity");
            Add(bozja, Feature.ActivityRecovery, "LostCure LostArise LostReraise");
            Add(bozja, Feature.ActivityUtility, "LostStealth LostSpellforge LostSteelsting");
            foreach (string unused in "LostManawall BannerOfTirelessConviction LostBloodRage BannerOfSolemnClarity LostCure2 LostCure3 LostCure4 LostSacrifice LostReflect LostAethershield LostProtect2 LostShell2 LostSwift LostFontOfSkill LostImpetus LostRampage".Split(' '))
                Preserved.Add(bozja + "_" + unused); // Listed upstream, but no execution branch exists.
            Preserved.Add(bozja + "_PowerEnds");
            Preserved.Add(bozja + "_PowerSacrifice");
        }
        foreach (string job in "Freelancer Knight Monk Thief Samurai Berserker Ranger TimeMage Chemist Bard Oracle Cannoneer Geomancer Dancer MysticKnight Gladiator Ninja WhiteMage BlackMage Dragoon Summoner BlueMage RedMage Necromancer".Split(' '))
            Parents.Add("Phantom_" + job);
        // Weather is an umbrella for independent healing, protection, damage and movement options.
        Parents.Add("Phantom_Geomancer_Weather");
        Add("Phantom", Feature.ActivityRecovery,
            "Freelancer_OccultResuscitation Knight_Pray Knight_OccultHeal Monk_OccultChakra Ranger_OccultUnicorn Chemist_OccultPotion Chemist_OccultEther Chemist_Revive Chemist_OccultElixir Bard_MightyMarch Oracle_Cleansing Oracle_Blessing Oracle_Recuperation Oracle_PhantomRejuvenation Geomancer_Sunbath Geomancer_CloudyCaress Geomancer_RingingRespite WhiteMage_OccultCureII WhiteMage_OccultCureIII WhiteMage_OccultRaise BlueMage_OccultWhiteWind RedMage_OccultCureII");
        Add("Phantom", Feature.ActivityDefense,
            "Knight_PhantomGuard Knight_Pledge Monk_Counterstance Thief_Vigilance Thief_PilferWeapon Samurai_Shirahadori Samurai_Mineuchi TimeMage_OccultSlowga TimeMage_OccultMageMasher Bard_RomeosBallad Oracle_Invulnerability Geomancer_BlessedRain Geomancer_MistyMirage Dancer_Mesmerize Dancer_SteadfastStance MysticKnight_MagicShell Gladiator_Defend Ninja_Smoke Ninja_Image WhiteMage_OccultBlink BlackMage_OccultToad Summoner_EarthenWall BlueMage_OccultMightyGuard");
        Add("Phantom", Feature.ActivityUtility,
            "Thief_OccultSprint Thief_Steal TimeMage_OccultDispel Oracle_Predict Geomancer_Suspend Geomancer_HastyMirage Dancer_Dance Dancer_QuickStep Gladiator_LongReach RedMage_OccultLibra");
        Add("Phantom", Feature.ActivityOffense,
            "Monk_PhantomKick Monk_OccultCounter Samurai_Iainuki Samurai_Zeninage Berserker_Rage Berserker_DeadlyBlow Ranger_PhantomAim TimeMage_OccultComet TimeMage_OccultQuick Bard_OffensiveAria Bard_HerosRime Oracle_PhantomJudgment Oracle_Starfall Oracle_PhantomDoom Cannoneer_PhantomFire Cannoneer_HolyCannon Cannoneer_DarkCannon Cannoneer_ShockCannon Cannoneer_SilverCannon Geomancer_BattleBell Geomancer_AetherialGain MysticKnight_SunderingSpellblade MysticKnight_HolySpellblade MysticKnight_BlazingSpellblade Gladiator_Finisher Gladiator_BladeBlitz Ninja_FumaShuriken Ninja_LightningScroll Ninja_FlameScroll WhiteMage_OccultHoly BlackMage_OccultFireIII BlackMage_OccultBlizzardIII BlackMage_OccultThunderIII BlackMage_OccultFlare Dragoon_OccultJump Dragoon_Lance Summoner_Hellfire Summoner_JudgmentBolt Summoner_Thunderstorm Summoner_Megaflare BlueMage_OccultAero BlueMage_OccultMissile BlueMage_OccultAquaBreath BlueMage_OccultAeroII BlueMage_OccultAeroIII RedMage_OccultFireII RedMage_OccultBlizzardII RedMage_OccultThunderII Necromancer_DrainTouch Necromancer_DeepFreeze Necromancer_HellWind Necromancer_ChaosDrive Necromancer_Doomsday");
        foreach (string potion in new[] { "PoTD_SustainingPotion", "HoH_EmpyreanPotion", "EO_OrthosPotion", "PT_PilgrimsPotion" })
            Actions.Add(potion, Feature.ActivityRecovery);
        Actions["Phantom_Dancer_QuickStep"] = Feature.ActivityDefense; // Evasion, despite the movement-sounding name.
        foreach (string role in new[] { "Tank", "Healer", "Melee", "PhysRanged", "Magic" })
            AddDamage("Variant_" + role, "SpiritDart EagleEyeShot");
        AddDamage("Bozja_Tank", "LostSlash LostFairTrade LostAssassination LostDeath");
        AddDamage("Phantom",
            "Monk_OccultCounter Samurai_Iainuki Samurai_Zeninage Berserker_DeadlyBlow TimeMage_OccultComet Oracle_PhantomJudgment Oracle_Starfall Oracle_PhantomDoom Cannoneer_PhantomFire Cannoneer_HolyCannon Cannoneer_DarkCannon Cannoneer_ShockCannon Cannoneer_SilverCannon MysticKnight_SunderingSpellblade MysticKnight_HolySpellblade MysticKnight_BlazingSpellblade Gladiator_Finisher Gladiator_BladeBlitz Ninja_FumaShuriken Ninja_LightningScroll Ninja_FlameScroll WhiteMage_OccultHoly BlackMage_OccultFireIII BlackMage_OccultBlizzardIII BlackMage_OccultThunderIII BlackMage_OccultFlare Dragoon_OccultJump Dragoon_Lance Summoner_Hellfire Summoner_JudgmentBolt Summoner_Thunderstorm Summoner_Megaflare BlueMage_OccultAero BlueMage_OccultMissile BlueMage_OccultAquaBreath BlueMage_OccultAeroII BlueMage_OccultAeroIII RedMage_OccultFireII RedMage_OccultBlizzardII RedMage_OccultThunderII Necromancer_DeepFreeze Necromancer_HellWind Necromancer_ChaosDrive Necromancer_Doomsday");
    }

    private static void AddDamage(string prefix, string suffixes)
    {
        foreach (string suffix in suffixes.Split(' '))
        {
            string name = prefix + "_" + suffix;
            Actions.Remove(name);
            DirectDamage.Add(name);
        }
    }

    private static void Add(string prefix, Feature owner, string suffixes)
    {
        foreach (string suffix in suffixes.Split(' ')) Actions.Add(prefix + "_" + suffix, owner);
    }

    internal static bool? Resolve(string preset, bool pvp, Func<Feature, bool> enabled, bool drainTouchHealing = false)
    {
        if (pvp || !enabled(Feature.ActivityAutomation)) return null;
        if (DirectDamage.Contains(preset)) return true;
        if (Parents.Contains(preset)) return DirectDamage.Any(name => name.StartsWith(preset + "_", StringComparison.Ordinal)) ||
            new[] { Feature.ActivityOffense, Feature.ActivityDefense, Feature.ActivityRecovery, Feature.ActivityUtility }.Any(enabled);
        if (!Actions.TryGetValue(preset, out var owner)) return null;
        if (preset == "Phantom_Necromancer_DrainTouch" && drainTouchHealing) owner = Feature.ActivityRecovery;
        return enabled(owner);
    }
}
