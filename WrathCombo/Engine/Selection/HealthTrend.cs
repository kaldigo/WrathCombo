using System;
using System.Collections.Generic;

namespace WrathCombo.Engine.Selection;

// Observed loss only; no guessed enemy potency or unbounded combat history.
internal sealed class HealthTrend
{
    private readonly Queue<(long At, float Loss)> losses = new();
    private long lastAt;
    private float lastHealth;
    internal void Reset() { losses.Clear(); lastAt = 0; }
    internal void Observe(float percent, long now)
    {
        if (!float.IsFinite(percent) || percent is < 0 or > 100) { Reset(); return; }
        if (lastAt != 0 && now - lastAt is >= 0 and < 250) return;
        if (lastAt == 0 || now - lastAt is < 0 or > 1500) losses.Clear();
        else if (lastHealth > percent)
        {
            if (losses.Count == 12) losses.Dequeue();
            losses.Enqueue((now, lastHealth - percent));
        }
        lastAt = now; lastHealth = percent;
        while (losses.TryPeek(out var loss) && now - loss.At > 3000) losses.Dequeue();
    }
    internal float Project(float percent, float seconds, long now)
    {
        if (!float.IsFinite(seconds) || seconds <= 0 || now - lastAt is < 0 or > 1500) return percent;
        float loss = 0;
        foreach (var entry in losses)
            if (now - entry.At is >= 0 and <= 3000) loss += entry.Loss;
        return Math.Max(0, percent - loss / 3 * seconds);
    }
}
