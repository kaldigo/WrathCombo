using System;
using System.Diagnostics;
using ECommons.DalamudServices;

namespace AccordDiagnostics;

internal enum TimedPath { Framework, LiveLayout, Plan, HotbarApply, SettingsStamp, SettingsRead, SettingsApply, RunnerStart, RunnerAttempt, TargetSelection, Preview, Serialize }

// Instrumentation is independent of detailed capture. Use fixed buckets to bound memory.
internal static class PerformanceTimings
{
    private sealed class Counter
    {
        internal long Calls, Total, Max;
        internal readonly long[] Histogram = new long[11];
    }
    private static readonly double[] LimitsMs = [.01, .05, .1, .25, .5, 1, 2, 4, 8, 16];
    private static readonly Counter[] Counters = Array.ConvertAll(Enum.GetValues<TimedPath>(), _ => new Counter());
    private static long next = Environment.TickCount64 + 30000;
    static PerformanceTimings() => DiagnosticLog.ObserveSerialization = ticks => Record(TimedPath.Serialize, ticks);
    internal readonly struct Sample(TimedPath path) : IDisposable
    {
        private readonly long start = Stopwatch.GetTimestamp();
        public void Dispose() => Record(path, Stopwatch.GetTimestamp() - start);
    }
    private static void Record(TimedPath path, long elapsed)
    {
        double ms = elapsed * 1000d / Stopwatch.Frequency;
        var c = Counters[(int)path];
        lock (c)
        {
            c.Calls++;
            c.Total += elapsed;
            c.Max = Math.Max(c.Max, elapsed);
            int i = 0;
            while (i < LimitsMs.Length && ms > LimitsMs[i]) i++;
            c.Histogram[i]++;
        }
    }
    internal static Sample Measure(TimedPath path) => new(path);
    internal static void Report()
    {
        long now = Environment.TickCount64;
        if (now < next) return;
        next = now + 30000;
        var rows = new System.Collections.Generic.List<string>();
        foreach (var path in Enum.GetValues<TimedPath>())
        {
            var c = Counters[(int)path];
            lock (c)
            {
                if (c.Calls == 0) continue;
                long cumulative = 0;
                int index = 0;
                while (index < LimitsMs.Length && (cumulative += c.Histogram[index]) < c.Calls * .95) index++;
                string percentile = index == LimitsMs.Length ? ">16" : "<=" + LimitsMs[index];
                rows.Add($"{path} n={c.Calls} total={c.Total * 1000d / Stopwatch.Frequency:F2} p95={percentile} max={c.Max * 1000d / Stopwatch.Frequency:F2}");
                c.Calls = c.Max = c.Total = 0;
                Array.Clear(c.Histogram);
            }
        }
        Svc.Log.Information("Performance (ms) capture={Capture} {Timings}", DiagnosticLog.Active, string.Join(" | ", rows));
        DiagnosticLog.Event("Performance.Timings", () => rows);
    }
}
