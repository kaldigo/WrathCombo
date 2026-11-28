using System;
using System.Linq;
using System.Collections.Generic;
using WrathCombo.Engine.Selection;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using WrathCombo.Data.BattleData;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class EncounterDamage
{
    private static DateTime frame;
    private static IncomingHit[] cachedBusters = [];
    internal static IReadOnlyList<IncomingHit> Busters()
    {
        if (frame == Svc.Framework.LastUpdateUTC) return cachedBusters;
        frame = Svc.Framework.LastUpdateUTC;
        var hits = new List<IncomingHit>();
        foreach (var actor in Svc.Objects)
        {
            if (actor is not IBattleChara { IsCasting: true } caster || !caster.IsHostile() || !BattleData.IsTankbuster(caster.CastActionId)) continue;
            if (Svc.Objects.SearchById(caster.CastTargetObjectId) is not IBattleChara { IsTargetable: true, IsDead: false } victim ||
                !(victim.IsInParty() || victim.GameObjectId == Player.Object?.GameObjectId)) continue;
            hits.Add(new(victim.GameObjectId, Math.Max(0, caster.TotalCastTime - caster.CurrentCastTime),
                $"cast:{caster.GameObjectId}:{caster.CastActionId}", caster.CurrentCastTime));
        }
        foreach (var victim in GetPartyMembers().Select(member => member.BattleChara).Append(Player.Object).Where(actor => actor is { IsTargetable: true, IsDead: false }).DistinctBy(actor => actor!.GameObjectId))
            if (HasIncomingTankBusterEffect(out var age, victim))
                hits.Add(new(victim!.GameObjectId, BossForecastProvider.Tankbuster, "marker:" + victim.GameObjectId, age));
        // One shared snapshot, known recipients only. A global timer/party bit mask
        // alone is never interpreted as a buster on the local player.
        return cachedBusters = hits.OrderBy(hit => hit.Seconds ?? float.MaxValue).GroupBy(hit => hit.Recipient)
            .Select(group => group.First()).Take(8).ToArray();
    }
    internal static (ulong Recipient, float? Seconds, string Key) Tankbuster(ulong? recipient = null)
    {
        var hit = Busters().FirstOrDefault(hit => recipient is null || hit.Recipient == recipient);
        return (hit.Recipient, hit.Seconds, hit.SourceKey ?? "");
    }

    private static readonly Selection.HealthTrend selfHealth = new();
    private static (ulong? Player, uint? Maximum, uint Territory, bool Combat) healthContext;
    internal static void ObserveSelfHealth()
    {
        if (!Player.Available || Svc.ClientState.IsPvP) { selfHealth.Reset(); healthContext = default; return; }
        var current = (Player.Object?.GameObjectId, Player.Object?.MaxHp, Svc.ClientState.TerritoryType, InCombat());
        if (healthContext != current) { healthContext = current; selfHealth.Reset(); }
        selfHealth.Observe(PlayerHealthPercentageHp(), Environment.TickCount64);
    }
    internal static bool ShouldSelfHeal(uint action, float threshold)
    {
        ObserveSelfHealth();
        float health = PlayerHealthPercentageHp();
        long now = Environment.TickCount64;
        var native = NativeExecution.Capture();
        bool gcd = Data.ActionWatching.ActionSheet.TryGetValue(action, out var row) && row.ActionCategory.RowId is 2 or 3;
        float cast = NativeExecution.CastTime(action);
        float available = Math.Max(native.CastRemaining, Math.Max(native.AnimationLock, gcd ? native.GcdRemaining : 0));
        float period = native.GcdTotal > 0 ? native.GcdTotal : 2.5f;
        float horizon = available + cast + .7f + period;
        float? hit = SelfHitWindow();
        var plan = SelfHealPlan.Evaluate(health, threshold, ExperimentalSelection.Forecasts && InCombat(),
            available, cast, period, hit, selfHealth.Project(health, horizon, now));
        TestingApi.RecordDecision("Survival.SelfCare", new { Action = action, Health = health, Threshold = threshold, Hit = hit, Cast = cast, Available = available, NextGcd = period, Horizon = horizon }, plan);
        return plan.Needed;
    }
    private static float? SelfHitWindow()
    {
        var raidwide = Selection.EncounterForecast.Earliest(BossForecastProvider.Raidwide, ObservedRaidwide().Seconds);
        var buster = Tankbuster(Player.Object?.GameObjectId);
        return Selection.EncounterForecast.Earliest(raidwide, buster.Recipient == Player.Object?.GameObjectId ? buster.Seconds : null);
    }
    internal static bool SelfHitWithin(float seconds) => SelfHitWindow() is { } hit && hit <= seconds;

    internal static bool EmergencyAbilityWindow => SelectionRuntime.Automatic && NativeExecution.Capture().CanSubmit(false, 0) &&
        (PlayerHealthPercentageHp() <= 25 || SelfHitWithin(1.5f));

    internal static bool ShortDefense(float duration, bool fallback)
    {
        if (!ExperimentalSelection.Forecasts) return fallback;
        var buster = Tankbuster(Player.Object?.GameObjectId);
        float? known = Selection.EncounterForecast.Earliest(Selection.EncounterForecast.Earliest(BossForecastProvider.Raidwide, ObservedRaidwide().Seconds),
            buster.Recipient == Player.Object?.GameObjectId ? buster.Seconds : null);
        // A known later event must not be consumed early merely because a solo
        // boss is targeting us. Low health still permits immediate protection.
        return known is { } due ? due <= duration - .5f || PlayerHealthPercentageHp() <= 60 : fallback;
    }

    internal static bool TankbusterWindow(float configuredDelay, float coverage)
    {
        var buster = Tankbuster(Player.Object?.GameObjectId);
        if (ExperimentalSelection.Forecasts && buster.Recipient == Player.Object?.GameObjectId && buster.Seconds is { } due)
        {
            float age = Busters().FirstOrDefault(hit => hit.Recipient == buster.Recipient).Age ?? (HasIncomingTankBusterEffect(out var markerAge) ? markerAge : 0);
            return age >= configuredDelay && due <= coverage - .5f;
        }
        return HasIncomingTankBusterEffect(out var ageFallback) && ageFallback >= configuredDelay;
    }
}
