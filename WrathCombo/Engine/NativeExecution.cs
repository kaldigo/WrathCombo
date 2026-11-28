using System;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Combos.PvE;
using WrathCombo.Engine.Selection;
using WrathCombo.Services;

namespace WrathCombo.Engine;

internal static unsafe class NativeExecution
{
    private static DateTime frame;
    private static ActionReadiness snapshot;
    private static readonly CastLifecycle casts = new();
    private static long interruptedAt;
    private static bool releaseObservation;
    internal static long CastGeneration { get; private set; }
    [ThreadStatic] private static AttemptState? attempt;
    private sealed class AttemptState { internal bool Submitted; }
    internal static bool AttemptSubmitted => attempt?.Submitted == true;
    internal static AttemptScope BeginAttempt() => new();
    internal readonly struct AttemptScope : IDisposable
    {
        private readonly AttemptState? previous;
        public AttemptScope() { previous = attempt; attempt = new(); }
        public void Dispose() => attempt = previous;
    }
    internal static void ObserveResult(bool accepted)
    {
        Invalidate();
        if (accepted && attempt != null) attempt.Submitted = true;
    }
    internal static bool Accepted(bool accepted, uint action, ulong recipient)
    {
        var am = ActionManager.Instance();
        bool queued = am != null && am->ActionQueued && am->QueuedActionId == action && am->QueuedTargetId.Id == recipient;
        return accepted || queued || AttemptSubmitted;
    }
    internal static void Interrupted(bool releaseOnly = false) { interruptedAt = Environment.TickCount64; releaseObservation = releaseOnly; }
    internal static void Completed(uint action) => casts.Complete(action, Environment.TickCount64);
    internal static void ObserveCast()
    {
        var state = Capture(fresh: true);
        var transition = casts.Observe(state.Cast, state.CastRemaining, state.GcdActive, Environment.TickCount64);
        if (!transition.Changed) return;
        CastGeneration++;
        if (transition.RecastReleasedEarly) SelectionRuntime.Interrupt(transition.Previous, releaseOnly: true);
        AccordDiagnostics.DiagnosticLog.Event(state.Cast == 0 ? "Execution.CastEnded" : "Execution.CastStarted",
            () => new { Transition = transition, State = state });
    }
    internal static void Submission(uint action, ulong recipient, bool accepted)
    {
        if (!accepted || interruptedAt == 0) return;
        long elapsed = Environment.TickCount64 - interruptedAt;
        interruptedAt = 0;
        AccordDiagnostics.DiagnosticLog.Event(releaseObservation ? "Execution.ResumedAfterCastRelease" : "Execution.ResumedAfterInterruption", () => new { Action = action, Recipient = recipient, Milliseconds = elapsed, State = Capture(fresh: true) });
    }
    internal static void Invalidate() => frame = default;
    internal static float CastTime(uint action) => ActionManager.GetAdjustedCastTime(ActionType.Action, action) / 1000f;
    internal static bool BaseAvailable(uint action, ulong recipient)
    {
        var am = ActionManager.Instance();
        return am != null && am->GetActionStatus(ActionType.Action, action, recipient,
            checkRecastActive: false, checkCastingActive: false) == 0;
    }
    internal static bool CanSubmit(uint action) => Capture(fresh: true).CanSubmit(
        Data.ActionWatching.ActionSheet.TryGetValue(action, out var row) && row.ActionCategory.RowId is 2 or 3,
        Service.Configuration.RotationConfig.QueueWindow);
    internal static ActionReadiness Capture(bool fresh = false)
    {
        if (!fresh && frame != default && frame == Svc.Framework.LastUpdateUTC) return snapshot;
        frame = Svc.Framework.LastUpdateUTC;
        var am = ActionManager.Instance();
        if (am == null) return snapshot = default;
        var group = am->GetRecastGroupDetail(57);
        bool active = group != null && group->IsActive;
        float total = am->GetRecastTime(ActionType.Action, MCH.HeatedSplitShot);
        float elapsed = am->GetRecastTimeElapsed(ActionType.Action, MCH.HeatedSplitShot);
        if (!float.IsFinite(total) || total <= 0)
        {
            total = group == null ? 0 : group->Total;
            elapsed = group == null ? 0 : group->Elapsed;
        }
        snapshot = new(true, active, total, ActionReadiness.Remaining(active, total, elapsed),
            am->CastActionId, ActionReadiness.Remaining(am->CastActionId != 0, am->CastTimeTotal, am->CastTimeElapsed),
            float.IsFinite(am->AnimationLock) ? Math.Max(0, am->AnimationLock) : float.MaxValue,
            am->ActionQueued, am->QueuedActionId);
        return snapshot;
    }
}
