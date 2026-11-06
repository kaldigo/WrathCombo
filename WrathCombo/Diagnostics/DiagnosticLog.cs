using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AccordDiagnostics;

// Identical, locally authored source in all three plugins. No runtime dependency between engines.
internal static class DiagnosticLog
{
    private static readonly object Gate = new();
    [ThreadStatic] private static long scope;
    private static long scopes;
    [ThreadStatic] private static int pressDepth;
    private const long ChunkBytes = 4L * 1024 * 1024;
    private const int RetainedParts = 8;
    private sealed class Scope(long previous, bool press) : IDisposable
    {
        private readonly long started = System.Diagnostics.Stopwatch.GetTimestamp();
        public void Dispose()
        {
            Event("Scope.End", () => new { DurationMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds });
            if (press) pressDepth--;
            scope = previous;
        }
    }
    internal static IDisposable? Begin(string kind, Func<object?> payload)
    {
        if (!Active) return null;
        long previous = scope; scope = Interlocked.Increment(ref scopes);
        bool press = kind == "Native.Press";
        if (press) pressDepth++;
        Event(kind, payload); return new Scope(previous, press);
    }
    internal static bool Predicate(bool result, [System.Runtime.CompilerServices.CallerMemberName] string member = "", [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { Event("Predicate.Result", () => new { Result = result, Member = member, File = Path.GetFileName(file), Line = line }); return result; }
    internal static bool Reject(string reason, [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { Event("Rejected", () => new { Reason = reason, File = Path.GetFileName(file), Line = line }, "Rejected:" + reason); return false; }
    internal static void Exit(string reason, [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { Event("Runner.Exit", () => new { Reason = reason, File = Path.GetFileName(file), Line = line }, "Runner.Exit:" + reason); }
    private static Session? current;
    private static Session? last;
    internal static bool Active => current is { Accepting: true };
    internal static string Root(string configDirectory) => Path.Combine(Directory.GetParent(configDirectory)!.FullName, "AccordDiagnostics");
    private sealed class Session
    {
        internal readonly Channel<string> Queue = Channel.CreateBounded<string>(new BoundedChannelOptions(8192) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
        internal readonly Dictionary<string, long> Counts = new();
        internal readonly Dictionary<string, long> NextSample = new();
        internal long SampledOut;
        internal int ExpiredParts;
        internal readonly string Id, Component, Directory;
        internal bool Compact;
        internal readonly long Started = Environment.TickCount64;
        internal long Sequence, Dropped, Written, Bytes;
        internal volatile bool Accepting = true;
        internal string Error = "", StopReason = "";
        internal Task Writer = Task.CompletedTask;
        internal Session(string root, string id, string component) { Id = id; Component = component; Directory = Path.Combine(root, id, component); }
    }
    internal static void Start(string root, string id, string component, bool compact = false)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid diagnostic session.");
        lock (Gate)
        {
            if (current?.Id == id && Active) return;
            if (current != null) throw new InvalidOperationException("Stop the previous diagnostic session first.");
            var session = new Session(root, id, component) { Compact = compact };
            System.IO.Directory.CreateDirectory(session.Directory);
            // Fail before reporting success if this destination is not writable.
            using (new FileStream(Path.Combine(session.Directory, "session.lock"), FileMode.CreateNew, FileAccess.Write)) { }
            current = last = session;
            session.Writer = Task.Run(() => WriteSession(session));
            Event("Session.Start", () => new { Schema = 1, Component = component, Build = typeof(DiagnosticLog).Assembly.ManifestModule.ModuleVersionId,
                Version = typeof(DiagnosticLog).Assembly.GetName().Version?.ToString(), Frequency = "World/state every 500 ms; settings every 2 seconds when changed", Limits = "Duty capture: up to eight 4 MiB compressed parts per component; repeated background checks sampled each second" });
        }
    }
    private static bool BackgroundCheck(string kind) =>
        kind is "Feature.Evaluated" or "Runner.Tick" or "Runner.Exit" or "Rejected" or "Resolver.Rejected" or "Predicate.Result"
        or "Scope.End" or "Selection" or "Provider.Evaluate" or "Route.Permission" or "Decision.Target" or "Decision.Publish"
        or "Resolver.Begin" or "Resolver.Chosen" or "Resolver.Try" or "Resolver.Accepted" or "Readiness.Check" or "Readiness.Result" or "Forecast.Read" ||
        kind.StartsWith("Provider.Result:", StringComparison.Ordinal) || kind.StartsWith("PlannedGcd:", StringComparison.Ordinal)
        || kind.StartsWith("Decision.Cast.", StringComparison.Ordinal);

    internal static void Event(string kind, Func<object?> data, string? sampleKey = null)
    {
        if (!Active) return;
        try
        {
            // Resolve game objects on the caller thread, never on the file writer.
            var observed = current;
            if (observed == null) return;
            lock (Gate)
            {
                if (current != observed || !observed.Accepting) return;
                var key = sampleKey ?? kind;
                observed.Counts[key] = observed.Counts.GetValueOrDefault(key) + 1;
                if (pressDepth == 0 && BackgroundCheck(kind))
                {
                    long now = Environment.TickCount64;
                    if (now < observed.NextSample.GetValueOrDefault(key)) { observed.SampledOut++; return; }
                    observed.NextSample[key] = now + 1000;
                }
            }
            var payload = data();
            lock (Gate)
            {
                var s = current;
                if (s != observed || !observed.Accepting) return;
                var line = JsonConvert.SerializeObject(new { Schema = 1, Session = s.Id, Component = s.Component,
                    Scope = scope, Sequence = ++s.Sequence, Utc = DateTime.UtcNow, MonotonicMs = Environment.TickCount64,
                    ElapsedMs = Environment.TickCount64 - s.Started, Kind = kind, Data = payload }, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                if (!s.Queue.Writer.TryWrite(line)) Interlocked.Increment(ref s.Dropped);
            }
        }
        catch (Exception ex) { lock (Gate) { if (current is { } s) { Interlocked.Increment(ref s.Dropped); s.Error = "Capture: " + ex.Message; } } }
    }
    internal static object Status()
    {
        lock (Gate)
        {
            var s = current ?? last;
            return new { Active, Session = s?.Id, Directory = s?.Directory, Dropped = s?.Dropped ?? 0,
                Pending = s?.Queue.Reader.Count ?? 0, Written = s?.Written ?? 0, Bytes = s?.Bytes ?? 0, SampledOut = s?.SampledOut ?? 0, ExpiredParts = s?.ExpiredParts ?? 0, Error = s?.Error ?? "", StopReason = s?.StopReason ?? "", Flushing = s is not null && !s.Writer.IsCompleted && !Active };
        }
    }
    internal static void Checkpoint() => Event("Coverage", () => { lock (Gate) return new { Counts = current is { } active ? new Dictionary<string, long>(active.Counts) : null, Status = Status() }; });
    internal static void Stop(string reason)
    {
        lock (Gate)
        {
            if (current is not { } s) return;
            Checkpoint();
            Event("Session.Stop", () => new { Reason = reason, Dropped = s.Dropped, Error = s.Error });
            s.StopReason = reason;
            s.Accepting = false;
            s.Queue.Writer.TryComplete();
            current = null;
        }
    }
    private static void TrimDutyParts(Session s, string currentFile)
    {
        // A duty may span several plugin reloads. Enforce the budget over all its segments.
        var duty = Directory.GetParent(s.Directory)!.Parent!;
        var parts = duty.EnumerateDirectories().Where(d => Guid.TryParseExact(d.Name, "N", out _) && !d.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Select(d => new DirectoryInfo(Path.Combine(d.FullName, s.Component)))
            .Where(d => d.Exists && !d.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .SelectMany(d => d.EnumerateFiles("events-*.jsonl.gz"))
            .OrderBy(f => f.CreationTimeUtc).ThenBy(f => f.FullName, StringComparer.Ordinal).ToArray();
        foreach (var old in parts.Skip(1).Where(f => f.FullName != currentFile).Take(Math.Max(0, parts.Length - RetainedParts)))
        {
            old.Delete();
            Interlocked.Increment(ref s.ExpiredParts);
        }
    }
    private static async Task WriteSession(Session s)
    {
        StreamWriter? writer = null;
        FileStream? file = null;
        long lastFlush = Environment.TickCount64;
        int part = 0;
        long chunk = 0;
        try
        {
            while (await s.Queue.Reader.WaitToReadAsync())
            {
                while (s.Queue.Reader.TryRead(out var line))
                {
                    if (writer == null || (s.Compact ? file!.Position : chunk) >= ChunkBytes)
                    {
                        if (writer != null) await writer.DisposeAsync();
                        var filename = Path.Combine(s.Directory, $"events-{++part:D4}.jsonl" + (s.Compact ? ".gz" : ""));
                        file = new FileStream(filename, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                        writer = new StreamWriter(s.Compact ? new GZipStream(file, CompressionLevel.Fastest) : file);
                        if (s.Compact) TrimDutyParts(s, filename);
                        chunk = 0;
                    }
                    await writer.WriteLineAsync(line);
                    long size = System.Text.Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                    chunk += size; Interlocked.Add(ref s.Bytes, size); Interlocked.Increment(ref s.Written);
                    if (!s.Compact && s.Bytes >= 2L * 1024 * 1024 * 1024) throw new IOException("Legacy capture limit reached.");

                }
                if (!s.Compact || Environment.TickCount64 - lastFlush >= 1000)
                {
                    await writer!.FlushAsync();
                    lastFlush = Environment.TickCount64;
                }
            }
        }
        catch (Exception ex) { lock (Gate) { s.Error = "Writer: " + ex.Message; s.Accepting = false; s.Queue.Writer.TryComplete(); } }
        finally
        {
            try { if (writer != null) await writer.DisposeAsync(); }
            catch (Exception ex) { s.Error = "Flush: " + ex.Message; }
            try
            {
                Dictionary<string, long> counts;
                lock (Gate) counts = new(s.Counts);
                await File.WriteAllTextAsync(Path.Combine(s.Directory, "summary.json"), JsonConvert.SerializeObject(new { s.Id, s.Component, s.Sequence, s.Written, s.Dropped, s.SampledOut, s.ExpiredParts, s.Bytes, s.Error, s.StopReason, Counts = counts }, Formatting.Indented));
            }
            catch (Exception ex) { s.Error = "Summary: " + ex.Message; }
        }
    }
}
