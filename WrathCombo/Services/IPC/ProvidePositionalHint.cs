using ECommons.EzIpcManager;

namespace WrathCombo.Services.IPC;

public partial class Provider
{
    /// <summary>
    ///     Returns the current upcoming positional hint, or <see langword="null" />
    ///     when no hint is active.
    /// </summary>
    [EzIPC]
    public uint[]? GetUpcomingPositionalHint() =>
        UpcomingPositionalHintService.GetWireSnapshot();

    /// <summary>Returns the near-term positional direction for the specified target, or zero.</summary>
    [EzIPC]
    public byte GetMovementPositional(ulong targetId) =>
        (byte)UpcomingPositionalHintService.GetMovementPositional(targetId);
}
