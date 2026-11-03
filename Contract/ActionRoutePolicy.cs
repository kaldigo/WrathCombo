using System;
using System.Collections.Generic;

namespace WrathFork.Contract;

// Engine setting: input action IDs own route policies. No Accord intent/variant
// names cross into the engine. Empty configuration preserves upstream behaviour.
public sealed class ActionRoutePolicy
{
    public bool Direct { get; set; }
    public uint Target { get; set; }
    public HashSet<uint> Excluded { get; set; } = new();
}

public static class DirectAction
{
    public const uint Offset = 2000000;
    public static uint Id(uint native) => native is > 0 and < 1000000 ? Offset + native : throw new ArgumentOutOfRangeException(nameof(native));
}

public sealed class ActionRouteContext : IDisposable
{
    [ThreadStatic] private static ActionRoutePolicy? current;
    private readonly ActionRoutePolicy? previous;
    public ActionRouteContext(ActionRoutePolicy? policy) { previous = current; current = policy; }
    public static bool Allowed(uint action) => current?.Excluded.Contains(action) != true;
    public static IEnumerable<uint> Excluded => current?.Excluded ?? (IEnumerable<uint>)Array.Empty<uint>();
    public void Dispose() => current = previous;
}
