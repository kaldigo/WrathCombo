namespace WrathCombo.Engine.Selection;

internal enum SamuraiTransitionChoice { None, RepeatAcceptedAttack, SpendThreeSen }

internal readonly record struct SamuraiTransitionInput(
    bool Area, bool SingleRepeatReady, bool AreaRepeatReady, int Sen,
    bool AllowIaijutsu, bool CastReady, bool InRange, bool Moving);

internal static class SamuraiTransition
{
    internal static SamuraiTransitionChoice Choose(SamuraiTransitionInput state)
    {
        // The accepted Iaijutsu owns its follow-up even if the player changes buttons.
        if (state.Area ? state.SingleRepeatReady : state.AreaRepeatReady)
            return SamuraiTransitionChoice.RepeatAcceptedAttack;
        // Never silently convert Sen to Kenki. Spending preserves the player's preparation.
        return state.Area && state.Sen == 3 && state.AllowIaijutsu && state.CastReady &&
               state.InRange && !state.Moving
            ? SamuraiTransitionChoice.SpendThreeSen : SamuraiTransitionChoice.None;
    }

    internal static bool PreferFlank(bool missingGetsu, bool missingKa, bool gekkoAllowed,
        bool kashaAllowed, float damageBuff, float speedBuff, float gcd, bool flank, bool rear) =>
        missingGetsu && missingKa && gekkoAllowed && kashaAllowed && flank && !rear &&
        float.IsFinite(gcd) && gcd > 0 && damageBuff > gcd * 2 && speedBuff > gcd * 2;
}
