using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using ECommons.DalamudServices;

namespace AccordDiagnostics;

// Identical source in the two plugins; no shared runtime assembly or game calls.
internal static class IntegrationTrace
{
    private sealed class Counter { internal long Calls, Errors, Ticks, Max; }
    private sealed record CallRecord(long AtMs, DateTime Utc, string Endpoint, double DurationMs,
        bool Write, bool? Success, string? Result, string? Error, object? Input);
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Counter> Counters = new();
    private static readonly Queue<CallRecord> Recent = new(64);
    private static string? captureSession;
    private static long reportAt = Environment.TickCount64 + 30000;
    private static long lastFrame, windowAt;
    private static int frames, over25, over50;
    private static double totalMs, maxMs;
    private static long peakAt;
    private static object? peakContext;
    private static CallRecord[] peakCalls = [];
    internal readonly struct EndpointScope(string endpoint) : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        public void Dispose() => Record(endpoint, started, false, "Elapsed scope", null, null, outcomeKnown: false);
    }
    internal static EndpointScope Measure(string endpoint) => new(endpoint);

    internal static T Call<T>(string endpoint, Func<T> invoke, bool write = false, object? input = null)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            T result = invoke();
            Record(endpoint, started, write, result, null, input);
            return result;
        }
        catch (Exception ex)
        {
            Record(endpoint, started, write, default(T), ex, input);
            throw; // Instrumentation must preserve the caller's failure policy.
        }
    }

    private static void Record<T>(string endpoint, long started, bool write, T result, Exception? error, object? input, bool outcomeKnown = true)
    {
        long elapsed = Stopwatch.GetTimestamp() - started;
        try
        {
            lock (Gate)
            {
                string key = Counters.ContainsKey(endpoint) || Counters.Count < 64 ? endpoint : "Other";
                if (!Counters.TryGetValue(key, out var counter)) Counters[key] = counter = new();
                counter.Calls++; counter.Ticks += elapsed; counter.Max = Math.Max(counter.Max, elapsed);
                if (error != null) counter.Errors++;
            }
            if (!DiagnosticLog.Active) return;
            string? summary = result is IEnumerable<string> strings ? string.Join("; ", strings.Take(4))
                : result is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture) : result?.ToString();
            if (summary?.Length > 256) summary = summary[..256];
            string? message = error?.Message;
            if (message?.Length > 512) message = message[..512];
            double ms = elapsed * 1000d / Stopwatch.Frequency;
            var record = new CallRecord(Environment.TickCount64, DateTime.UtcNow, endpoint, ms, write, outcomeKnown ? error == null : null, summary, message, input);
            lock (Gate)
            {
                RefreshSession();
                if (Recent.Count == 64) Recent.Dequeue();
                Recent.Enqueue(record);
            }
            string kind = error != null ? "Integration.Error" : write ? "Integration.Write" : ms >= 2 ? "Integration.Slow" : "Integration.Read";
            DiagnosticLog.Event(kind, () => record, kind + ":" + endpoint);
        }
        catch { /* Diagnostics never change an IPC result, exception or automation state. */ }
    }

    private static void RefreshSession()
    {
        string? session = DiagnosticLog.SessionId;
        if (captureSession == session) return;
        captureSession = session;
        Recent.Clear(); lastFrame = 0; windowAt = 0;
        ClearWindow();
    }

    private static void ClearWindow()
    {
        frames = over25 = over50 = 0; totalMs = maxMs = 0;
        peakAt = 0; peakContext = null; peakCalls = [];
    }

    // Called at callback entry: measures time between game updates, including time
    // outside this plugin. The context factory must only read already available state.
    internal static void Frame(Func<object?> context)
    {
        try
        {
            long ticks = Stopwatch.GetTimestamp(), now = Environment.TickCount64;
            object? payload = null;
            List<string>? report = null;
            lock (Gate)
            {
                RefreshSession();
                if (lastFrame != 0)
                {
                    double gap = (ticks - lastFrame) * 1000d / Stopwatch.Frequency;
                    frames++; totalMs += gap;
                    if (gap > 25) over25++;
                    if (gap > 50) over50++;
                    if (gap > maxMs)
                    {
                        maxMs = gap; peakAt = now;
                        if (DiagnosticLog.Active && gap > 25)
                        {
                            peakContext = context();
                            peakCalls = Recent.Where(c => now - c.AtMs is >= 0 and <= 2000).ToArray();
                        }
                    }
                }
                lastFrame = ticks;
                if (windowAt == 0) windowAt = now;
                if (now - windowAt >= 1000)
                {
                    if (DiagnosticLog.Active)
                        payload = new { FromMs = windowAt, ToMs = now, Frames = frames,
                            MeanMs = frames == 0 ? 0 : totalMs / frames, MaxMs = maxMs,
                            Over25Ms = over25, Over50Ms = over50, PeakAtMs = peakAt,
                            PeakContext = peakContext, RecentCallsAtPeak = peakCalls, Context = context() };
                    ClearWindow(); windowAt = now;
                }
                if (now >= reportAt)
                {
                    reportAt = now + 30000; report = new();
                    foreach (var entry in Counters)
                    {
                        var c = entry.Value;
                        if (c.Calls == 0) continue;
                        report.Add(FormattableString.Invariant($"{entry.Key} n={c.Calls} total={c.Ticks * 1000d / Stopwatch.Frequency:F3} max={c.Max * 1000d / Stopwatch.Frequency:F3} errors={c.Errors}"));
                        c.Calls = c.Errors = c.Ticks = c.Max = 0;
                    }
                }
            }
            if (payload != null) DiagnosticLog.Event("Frame.Window", () => payload);
            if (report is { Count: > 0 })
            {
                Svc.Log.Information("Integration timings (ms) {Timings}", string.Join(" | ", report));
                DiagnosticLog.Event("Integration.Timings", () => report);
            }
        }
        catch { /* A missing diagnostic context cannot block a game update. */ }
    }
}
