using System;
using System.Collections.Generic;

namespace WrathCombo.Engine;

// Observations only: this type has no game, resolver, targeting or execution dependencies.
internal sealed class DecisionTrace
{
    internal sealed record Entry(long Sequence, long ElapsedMs, string Kind, uint Input, uint Output, ulong? Recipient);
    private readonly Queue<Entry> entries = new();
    private readonly Dictionary<uint, uint> selections = new();
    private long started, deadline, sequence;
    private string context = "";
    internal bool Active { get; private set; }
    internal string StopReason { get; private set; } = "Not started";
    internal long Dropped { get; private set; }
    internal long RepeatedSelections { get; private set; }
    internal void Start(long now, string currentContext)
    {
        entries.Clear(); selections.Clear(); sequence = Dropped = RepeatedSelections = 0;
        started = now; deadline = now + 60000; context = currentContext;
        Active = true; StopReason = "";
    }
    internal void Stop(string reason) { Active = false; StopReason = reason; }
    internal bool Check(long now, string currentContext)
    {
        if (Active && currentContext != context) Stop("Player or duty context changed");
        if (Active && now >= deadline) Stop("60 second limit reached");
        return Active;
    }
    internal void Add(long now, string currentContext, string kind, uint input, uint output, ulong? recipient = null)
    {
        if (!Check(now, currentContext)) return;
        if (kind == "Selection")
        {
            if (selections.TryGetValue(input, out var previous) && previous == output) { RepeatedSelections++; return; }
            selections[input] = output;
        }
        if (entries.Count == 2048) { entries.Dequeue(); Dropped++; }
        entries.Enqueue(new Entry(++sequence, now - started, kind, input, output, recipient));
    }
    internal Entry[] Snapshot() => entries.ToArray();
}
