using System;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;

namespace WrathCombo.Engine.Selection;

internal enum TargetSource { Explicit, PartyMouseover, WorldMouseover, Selected, Focus, Automatic }
internal enum EffectShape { Single, Circle, Cone, Line, Donut }
internal sealed record TargetActor(ulong Id, Vector3 Position, float Radius, bool Eligible, bool Party, float Health, bool MayAffect = true);
internal sealed record TargetOverride(TargetSource Source, ulong Id);
internal sealed record TargetAction(float Range, float Radius, EffectShape Shape = EffectShape.Single, bool CenterOnCaster = false,
    float HalfAngle = MathF.PI / 4, float Width = 4, float InnerRadius = 0);
internal sealed record TargetPolicy(bool Automatic, bool FallbackOnInvalidOverride, bool PreferCoverage, bool PreferInjured, bool AvoidUnengaged, bool AllowManualSplash = false);
internal sealed record TargetSelectionInput(Vector3 Caster, float CasterRadius, TargetAction Action, TargetPolicy Policy,
    ImmutableArray<TargetActor> Candidates, ImmutableArray<TargetActor> Affected, ImmutableArray<TargetOverride> Overrides);
internal sealed record TargetDecision(ulong? Recipient, ImmutableArray<ulong> Affected, TargetSource Source, string Reason)
{
    internal static TargetDecision None(string reason, TargetSource source = TargetSource.Automatic) => new(null, [], source, reason);
}

// RSR-derived structure: action-specific direct candidates and affected actors are
// distinct. Inputs/results are values; no game object, target mutation or resolver cache.
internal static class TargetSelection
{
    internal static TargetDecision Select(TargetSelectionInput input) => Select(input.Caster, input.CasterRadius,
        input.Action, input.Policy, input.Candidates, input.Affected, input.Overrides);
    internal static TargetDecision Select(Vector3 caster, float casterRadius, TargetAction action, TargetPolicy policy,
        ImmutableArray<TargetActor> candidates, ImmutableArray<TargetActor> affected, ImmutableArray<TargetOverride> overrides)
    {
        bool Eligible(TargetActor actor) => actor.Eligible &&
            Vector3.Distance(caster, actor.Position) <= action.Range + casterRadius + actor.Radius;
        ImmutableArray<ulong> Hits(TargetActor actor) => action.Shape == EffectShape.Single ? [actor.Id] : affected.Where(a => Covers(caster, actor.Position, action, a)).Select(a => a.Id).ToImmutableArray();
        bool Safe(TargetActor actor) => !policy.AvoidUnengaged || (action.Shape == EffectShape.Single || policy.AllowManualSplash ? actor.MayAffect : !affected.Any(a => !a.MayAffect && Covers(caster, actor.Position, action, a)));
        foreach (var target in overrides)
        {
            if (target.Id == 0) continue;
            var actor = candidates.FirstOrDefault(a => a.Id == target.Id);
            if (actor is not null && Eligible(actor) && Safe(actor))
                return new(actor.Id, Hits(actor), target.Source, "Eligible deliberate recipient");
            if (target.Source == TargetSource.Explicit || !policy.FallbackOnInvalidOverride)
                return TargetDecision.None("Deliberate recipient is unavailable, ineligible or unsafe", target.Source);
        }
        if (!policy.Automatic) return TargetDecision.None("Automatic recipient selection disabled");
        var best = candidates.Where(Eligible).Where(Safe)
            .OrderByDescending(a => policy.PreferCoverage ? Hits(a).Length : 0)
            .ThenByDescending(a => a.Party)
            .ThenBy(a => policy.PreferInjured ? a.Health : 0)
            .ThenBy(a => Vector3.DistanceSquared(caster, a.Position)).ThenBy(a => a.Id).FirstOrDefault();
        return best is null ? TargetDecision.None("No eligible recipient")
            : new(best.Id, Hits(best), TargetSource.Automatic, policy.PreferCoverage ? "Eligible maximum coverage" : "Eligible recipient priority");
    }

    internal static bool Covers(Vector3 caster, Vector3 target, TargetAction action, TargetActor actor)
    {
        if (action.Shape == EffectShape.Single) return Vector3.DistanceSquared(target, actor.Position) < .0001f;
        var origin = action.CenterOnCaster || action.Shape is EffectShape.Cone or EffectShape.Line ? caster : target;
        var delta = new Vector2(actor.Position.X - origin.X, actor.Position.Z - origin.Z);
        float distance = Vector3.Distance(actor.Position, origin);
        if (!float.IsFinite(distance)) return false;
        if (distance > action.Radius + actor.Radius) return false;
        if (action.Shape == EffectShape.Circle) return true;
        if (action.Shape == EffectShape.Donut) return distance + actor.Radius >= action.InnerRadius;
        var direction = new Vector2(target.X - caster.X, target.Z - caster.Z);
        if (direction.LengthSquared() < .0001f) return false;
        direction = Vector2.Normalize(direction);
        float forward = Vector2.Dot(delta, direction);
        if (action.Shape == EffectShape.Line)
            return forward >= -actor.Radius && forward <= action.Radius + actor.Radius &&
                MathF.Abs(delta.X * direction.Y - delta.Y * direction.X) <= action.Width / 2 + actor.Radius;
        if (distance <= actor.Radius) return true;
        float allowance = MathF.Asin(Math.Clamp(actor.Radius / distance, 0, 1));
        return forward / distance >= MathF.Cos(Math.Min(MathF.PI, action.HalfAngle + allowance));
    }
}
