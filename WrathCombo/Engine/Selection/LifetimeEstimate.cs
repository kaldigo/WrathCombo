using System;
using System.Collections.Generic;

namespace WrathCombo.Engine.Selection;

internal sealed class LifetimeEstimate
{
    private readonly Queue<(long At, double Ratio)> samples = new();
    private uint maximum;
    private double lastRatio;
    internal long LastObserved { get; private set; }
    internal void Observe(uint health, uint max, long now)
    {
        if (max == 0 || health > max) { samples.Clear(); return; }
        if (samples.Count != 0 && now - LastObserved is >= 0 and < 500) return;
        double ratio = (double)health / max;
        if (max != maximum || now - LastObserved is < 0 or > 1500 || ratio > lastRatio + .01 || ratio >= 1) samples.Clear();
        maximum = max; lastRatio = ratio; LastObserved = now;
        if (samples.Count == 16) samples.Dequeue();
        samples.Enqueue((now, ratio));
    }
    internal float? Remaining(long now)
    {
        if (samples.Count < 4 || now - LastObserved is < 0 or > 1500 || LastObserved - samples.Peek().At < 2000) return null;
        double tMean = 0, hMean = 0;
        long origin = samples.Peek().At;
        foreach (var sample in samples) { tMean += (sample.At - origin) / 1000d; hMean += sample.Ratio; }
        tMean /= samples.Count; hMean /= samples.Count;
        double covariance = 0, variance = 0;
        foreach (var sample in samples)
        {
            double t = (sample.At - origin) / 1000d - tMean;
            covariance += t * (sample.Ratio - hMean); variance += t * t;
        }
        double rate = variance > 0 ? -covariance / variance : 0;
        double seconds = rate > 0 ? lastRatio / rate : double.NaN;
        return double.IsFinite(seconds) && seconds >= 0 && seconds <= float.MaxValue ? (float)seconds : null;
    }
}
