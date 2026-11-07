using System;
using System.Collections.Generic;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace RotationInterop;

// This is a transient execution guard. It never changes Accord's saved runner preference.
internal static unsafe class ManualLimitBreak
{
    internal readonly record struct QueueEntry(ActionType Type, uint Id, ulong Target);
    private static readonly Dictionary<uint, bool> categories = new();
    private static QueueEntry runnerQueue;
    private static long queueExpires, holdUntil;
    [ThreadStatic] private static int runnerDepth;

    private static QueueEntry Queue(ActionManager* am) => am != null && am->ActionQueued
        ? new(am->QueuedActionType, am->QueuedActionId, am->QueuedTargetId.Id) : default;

    private static bool IsLimitBreak(ActionType type, uint id)
    {
        if (type == ActionType.GeneralAction) return id == 3;
        if (type != ActionType.Action || id == 0 || id >= 1_000_000) return false;
        if (!categories.TryGetValue(id, out var found))
        {
            found = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row)
                && row.ActionCategory.RowId is 9 or 15;
            categories[id] = found;
        }
        return found;
    }

    internal static bool Input(ActionManager* am, ActionType type, uint id, bool queueReplay)
    {
        if (am == null || runnerDepth != 0) return false;
        if (queueReplay) return IsLimitBreak(type, id);
        if (!IsLimitBreak(type, id)) { runnerQueue = default; return false; }
        var now = Environment.TickCount64;
        var queued = Queue(am);
        bool clear = queued.Id != 0 && queued == runnerQueue && now < queueExpires && !IsLimitBreak(queued.Type, queued.Id);
        if (clear) am->ActionQueued = false;
        runnerQueue = default;
        holdUntil = now + 1800;
        AccordDiagnostics.DiagnosticLog.Event("LimitBreak.Input", () => new { Action = id, ClearedRunnerQueue = clear, PreviousAction = queued.Id });
        return true;
    }

    // Includes native placement of a ground LB and a job-specific action reached through a replacement.
    internal static void Placement(ActionType type, uint id)
    {
        if (IsLimitBreak(type, id)) holdUntil = Environment.TickCount64 + 1800;
    }

    internal static bool Waiting()
    {
        var am = ActionManager.Instance();
        if (am == null) { holdUntil = 0; runnerQueue = default; return false; }
        long now = Environment.TickCount64;
        var queued = Queue(am);
        if (IsLimitBreak(am->CastActionType, am->CastActionId) || IsLimitBreak(queued.Type, queued.Id)
            || IsLimitBreak(am->AreaTargetingActionType, am->AreaTargetingActionId))
            holdUntil = Math.Max(holdUntil, now + 500);
        if (holdUntil == 0) return false;
        if (am->AnimationLock > 0) holdUntil = Math.Max(holdUntil, now + 100);
        if (now < holdUntil) return true;
        holdUntil = 0;
        AccordDiagnostics.DiagnosticLog.Event("LimitBreak.Resume", () => new { });
        return false;
    }

    internal static RunnerScope TrackRunner() => new(Queue(ActionManager.Instance()));
    internal readonly ref struct RunnerScope
    {
        private readonly QueueEntry before;
        internal RunnerScope(QueueEntry previous) { before = previous; runnerDepth++; }
        public void Dispose()
        {
            var after = Queue(ActionManager.Instance());
            if (after != before)
            {
                runnerQueue = after;
                queueExpires = Environment.TickCount64 + 3000;
            }
            runnerDepth--;
        }
    }
}
