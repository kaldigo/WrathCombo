using System;
using System.Collections.Generic;
using System.Linq;

namespace WrathCombo.Engine.Selection;

internal sealed record PreventionWindow(long Id, long Generation, ulong Recipient, long? DueAt, string SourceKey = "");
internal sealed class PreventionBudget
{
    private readonly Dictionary<string, PreventionWindow> windows = new();
    private readonly HashSet<long> handled = new();
    private long serial;
    internal void Observe(string kind, bool active, long now, long generation, ulong recipient, float? seconds, string sourceKey = "")
    {
        if (!active) { if (windows.Remove(kind, out var old)) handled.Remove(old.Id); return; }
        long? due = EncounterForecast.Normalize(seconds) is { } time ? now + (long)(time * 1000) : null;
        if (windows.TryGetValue(kind, out var previous) && previous.Generation == generation && previous.Recipient == recipient &&
            (sourceKey.Length == 0 || previous.SourceKey.Length == 0 || sourceKey == previous.SourceKey) &&
            (due is null || previous.DueAt is null || Math.Abs(due.Value - previous.DueAt.Value) <= 1500))
        {
            if (previous.DueAt is null && due is not null || previous.SourceKey.Length == 0 && sourceKey.Length != 0)
                windows[kind] = previous with { DueAt = previous.DueAt ?? due, SourceKey = sourceKey.Length == 0 ? previous.SourceKey : sourceKey };
            return;
        }
        if (previous is not null) handled.Remove(previous.Id);
        windows[kind] = new(++serial, generation, recipient, due, sourceKey);
    }
    internal PreventionWindow? Available(string kind) => windows.TryGetValue(kind, out var value) && !handled.Contains(value.Id) ? value : null;
    internal bool Contains(long id) => windows.Values.Any(window => window.Id == id);
    internal void Confirm(long id) { if (id != 0 && windows.Values.Any(w => w.Id == id)) handled.Add(id); }
    internal void Reset() { windows.Clear(); handled.Clear(); }
}
