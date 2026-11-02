namespace WrathCombo.Engine;

internal static class UntargetedAoePolicy
{
    internal static bool Eligible(bool managed, bool enabled, bool pvp, bool activity, bool hasTarget,
        bool combatOnly, bool inCombat) => managed && enabled && !pvp && activity && !hasTarget && (!combatOnly || inCombat);
    internal static bool Suitable(bool targetsSelf, int castType, bool groundTargeted, int eligibleEnemies) =>
        targetsSelf && castType == 2 && !groundTargeted && eligibleEnemies > 0;
}
