namespace WrathCombo.Engine.Selection;

internal readonly record struct GunbreakerPlanInput(uint AcceptedChain, uint ExpiringProc, uint BurstEntry,
    uint DoubleDown, uint SonicBreak, uint FillerChain, uint CartridgeSpend, uint BasicCombo);
internal static class GunbreakerPlan
{
    internal static uint Choose(GunbreakerPlanInput state) =>
        state.AcceptedChain != 0 ? state.AcceptedChain :
        state.ExpiringProc != 0 ? state.ExpiringProc :
        state.BurstEntry != 0 ? state.BurstEntry :
        state.DoubleDown != 0 ? state.DoubleDown :
        state.SonicBreak != 0 ? state.SonicBreak :
        state.FillerChain != 0 ? state.FillerChain :
        state.CartridgeSpend != 0 ? state.CartridgeSpend : state.BasicCombo;
}
