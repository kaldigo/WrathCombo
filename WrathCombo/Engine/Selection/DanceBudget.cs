using System;

namespace WrathCombo.Engine.Selection;

internal readonly record struct DanceBudgetInput(float StartRecast, float StepRecast, int Steps, float Delay, float? Downtime);
internal static class DanceBudget
{
    internal static bool CanStart(DanceBudgetInput state) =>
        float.IsFinite(state.StartRecast) && state.StartRecast > 0 &&
        float.IsFinite(state.StepRecast) && state.StepRecast > 0 && state.Steps is 2 or 4 &&
        (state.Downtime is not { } remaining || float.IsFinite(remaining) &&
         remaining > Math.Max(0, state.Delay) + state.StartRecast + state.StepRecast * state.Steps + .1f);
    internal static bool ExpiresBeforeNext(float remaining, float delay, float gcd) =>
        float.IsFinite(remaining) && remaining > Math.Max(0, delay) + .1f &&
        remaining <= Math.Max(0, delay) + gcd + .1f;
}
