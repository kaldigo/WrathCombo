namespace WrathCombo.Engine;

// Holds one existing rotation policy during an active mudra sequence.
internal sealed class PvPAreaChoice
{
    internal static uint RouteInput(uint input, bool area) => input == 1000015 ? area ? 1000001u : 1000000u : input;
    private bool area;
    internal bool Select(bool automatic, bool mudraActive, int eligibleEnemies)
    {
        if (!automatic) { Reset(); return false; }
        if (!mudraActive) area = eligibleEnemies >= 2;
        return area;
    }
    internal void Reset() => area = false;
}
