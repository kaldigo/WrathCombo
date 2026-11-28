using System;
using System.Collections.Generic;
using System.Linq;

namespace WrathCombo.Engine.Selection;

/// <summary>Confirmed effects awaiting their recipient-specific HP result. No potency guesses.
/// Observers insert/acknowledge effects; preview projection never consumes them.</summary>
internal sealed class PendingHealth
{
    private readonly Dictionary<(uint Sequence, ulong Recipient, int Effect), (long Delta, long At)> entries = new();
    internal int Count => entries.Count;
    internal void Observe(uint sequence, ulong recipient, int effect, long delta, long now)
    {
        if (recipient == 0 || delta == 0) return;
        var key = (sequence, recipient, effect);
        if (entries.ContainsKey(key)) return;
        if (entries.Count >= 2048) entries.Remove(entries.MinBy(item => item.Value.At).Key);
        entries.Add(key, (delta, now));
    }
    internal uint Project(uint observed, uint maximum, ulong recipient, long now)
    {
        long delta = entries.Where(item => item.Key.Recipient == recipient && now - item.Value.At is >= 0 and < 1500).Sum(item => item.Value.Delta);
        return (uint)Math.Clamp((long)observed + delta, 0, maximum);
    }
    internal void Acknowledge(uint sequence, ulong recipient)
    {
        foreach (var key in entries.Keys.Where(key => key.Sequence == sequence && key.Recipient == recipient).ToArray()) entries.Remove(key);
    }
    internal void Tick(long now)
    {
        foreach (var key in entries.Where(item => now - item.Value.At is < 0 or >= 1500).Select(item => item.Key).ToArray()) entries.Remove(key);
    }
    internal void Reset() => entries.Clear();
}
