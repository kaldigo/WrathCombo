using System;

namespace RotationInterop;

/// <summary>Session-only authority above native automation flags and third-party leases.
/// A lost controller stops execution; it never restores an earlier native setting.</summary>
internal sealed class AutomationLease
{
    internal const long LifetimeMilliseconds = 3000;
    private Guid owner;
    private long renewedAt;
    private bool permitted;
    internal bool Managed { get; private set; }

    internal bool Allowed(long now) => !Managed || permitted && Live(now);
    internal bool OwnedBy(Guid token, long now) => Managed && token != Guid.Empty && token == owner && Live(now);
    private bool Live(long now) => now >= renewedAt && now - renewedAt < LifetimeMilliseconds;

    // Acquisition always stops first, including takeover from a disconnected controller.
    internal bool Block(Guid token, long now)
    {
        if (token == Guid.Empty) return false;
        Managed = true;
        permitted = false;
        owner = token;
        renewedAt = now;
        return true;
    }

    internal bool Renew(Guid token, long now)
    {
        if (!OwnedBy(token, now)) return false;
        renewedAt = now;
        return true;
    }

    internal bool Start(Guid token, long now, bool busy)
    {
        if (busy || !OwnedBy(token, now)) return false;
        permitted = true;
        return true;
    }

    internal void Stop() => permitted = false;

    internal bool Release(Guid token, long now)
    {
        if (permitted || !OwnedBy(token, now)) return false;
        Managed = false;
        owner = Guid.Empty;
        return true;
    }
}
