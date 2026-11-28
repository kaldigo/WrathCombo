using System;
using System.Collections.Generic;
using System.Linq;

namespace WrathCombo.Engine.Selection;

internal sealed record PreventionWindow(long Id, long Generation, ulong Recipient, long? DueAt, string SourceKey = "");
internal readonly record struct IncomingHit(ulong Recipient, float? Seconds, string SourceKey, float? Age = null);
internal enum PreventionPurpose { All, Reduction, Shield, Reactive, Recovery, Enhancement }
internal sealed class PreventionBudget
{
    private readonly Dictionary<string, PreventionWindow> windows = new();
    private readonly HashSet<(long Event, PreventionPurpose Purpose, ulong Recipient)> handled = new();
    private long serial;
    internal void Observe(string kind, bool active, long now, long generation, ulong recipient, float? seconds, string sourceKey = "", int recoveryGraceMs = 0)
    {
        if (!active)
        {
            if (recoveryGraceMs > 0 && windows.TryGetValue(kind, out var recent) && recent.Generation == generation &&
                recent.DueAt is { } impact && now >= impact - 500 && now - impact <= recoveryGraceMs) return;
            if (windows.Remove(kind, out var old)) Forget(old.Id);
            return;
        }
        long? due = EncounterForecast.Normalize(seconds) is { } time ? now + (long)(time * 1000) : null;
        if (windows.TryGetValue(kind, out var previous) && previous.Generation == generation && previous.Recipient == recipient &&
            (sourceKey.Length == 0 || previous.SourceKey.Length == 0 || sourceKey == previous.SourceKey) &&
            (due is null || previous.DueAt is null || Math.Abs(due.Value - previous.DueAt.Value) <= 1500) &&
            // A newly forecast hit after the previous impact has its own budget,
            // even when the same boss reuses a cast within the recovery grace.
            !(previous.DueAt is { } impactAt && due is { } nextAt && now >= impactAt && nextAt - impactAt > 750))
        {
            if (previous.DueAt is null && due is not null || previous.SourceKey.Length == 0 && sourceKey.Length != 0)
                windows[kind] = previous with { DueAt = previous.DueAt ?? due, SourceKey = sourceKey.Length == 0 ? previous.SourceKey : sourceKey };
            return;
        }
        if (previous is not null) Forget(previous.Id);
        windows[kind] = new(++serial, generation, recipient, due, sourceKey);
    }
    internal void ObserveMany(string kind, IEnumerable<IncomingHit> hits, long now, long generation, int recoveryGraceMs = 0)
    {
        var active = new HashSet<string>();
        foreach (var hit in hits.Take(8))
        {
            if (hit.Recipient == 0) continue;
            string key = kind + ":" + hit.Recipient;
            active.Add(key);
            Observe(key, true, now, generation, hit.Recipient, hit.Seconds, hit.SourceKey, recoveryGraceMs);
        }
        foreach (string key in windows.Keys.Where(key => (key == kind || key.StartsWith(kind + ":", StringComparison.Ordinal)) && !active.Contains(key)).ToArray())
            Observe(key, false, now, generation, 0, null, recoveryGraceMs: recoveryGraceMs);
    }
    internal IEnumerable<PreventionWindow> Active(string kind) => windows
        .Where(entry => entry.Key == kind || entry.Key.StartsWith(kind + ":", StringComparison.Ordinal))
        .Select(entry => entry.Value).OrderBy(window => window.DueAt ?? long.MaxValue);
    private ulong EventRecipient(long id) => windows.Values.FirstOrDefault(window => window.Id == id)?.Recipient ?? 0;
    private void Forget(long id) => handled.RemoveWhere(entry => entry.Event == id);
    internal bool Fulfilled(long id, PreventionPurpose purpose, ulong recipient = 0)
    {
        if (recipient == 0) recipient = EventRecipient(id);
        return handled.Contains((id, PreventionPurpose.All, 0)) || handled.Contains((id, PreventionPurpose.All, recipient)) ||
            handled.Contains((id, purpose, 0)) || handled.Contains((id, purpose, recipient));
    }
    internal PreventionWindow? Available(string kind, PreventionPurpose purpose = PreventionPurpose.All, ulong recipient = 0) =>
        windows.TryGetValue(kind, out var value) && !Fulfilled(value.Id, purpose, recipient) ? value : null;
    internal bool Contains(long id) => windows.Values.Any(window => window.Id == id);
    internal void Confirm(long id, PreventionPurpose purpose = PreventionPurpose.All, ulong recipient = 0)
    { if (id != 0 && windows.Values.Any(w => w.Id == id)) handled.Add((id, purpose, recipient == 0 ? EventRecipient(id) : recipient)); }
    internal void Reset() { windows.Clear(); handled.Clear(); }
}
