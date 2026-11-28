using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using WrathCombo.Engine.Selection;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class TargetLifetime
{
    private static readonly Dictionary<ulong, LifetimeEstimate> histories = new();
    private static long nextPoll;
    private static uint territory;
    internal static void Tick()
    {
        if (!ExperimentalSelection.ActiveBard || !InCombat()) { histories.Clear(); nextPoll = 0; return; }
        long now = Environment.TickCount64;
        if (now < nextPoll) return;
        nextPoll = now + 500;
        using var timing = AccordDiagnostics.PerformanceTimings.Measure(AccordDiagnostics.TimedPath.TargetSelection);
        if (territory != Svc.ClientState.TerritoryType) { territory = Svc.ClientState.TerritoryType; histories.Clear(); }
        foreach (var actor in Svc.Objects)
        {
            if (actor is not IBattleChara { IsDead: false, IsTargetable: true } target || !target.IsHostile() || GetTargetDistance(target) > 60) continue;
            if (!histories.TryGetValue(target.GameObjectId, out var history))
            {
                if (histories.Count == 128) histories.Remove(histories.MinBy(entry => entry.Value.LastObserved).Key);
                histories[target.GameObjectId] = history = new();
            }
            history.Observe(target.CurrentHp, target.MaxHp, now);
        }
        foreach (var id in histories.Where(entry => now - entry.Value.LastObserved > 5000).Select(entry => entry.Key).ToArray()) histories.Remove(id);
    }
    internal static bool AllowsDot(IGameObject? target, float minimumSeconds = 6)
    {
        if (!ExperimentalSelection.ActiveBard || !SelectionRuntime.Automatic || target is null) return true;
        float? remaining = histories.GetValueOrDefault(target.GameObjectId)?.Remaining(Environment.TickCount64);
        TestingApi.RecordDecision("BRD.TargetLifetime", new { Recipient = target.GameObjectId, Minimum = minimumSeconds }, remaining);
        return remaining is null || remaining >= minimumSeconds;
    }
}
