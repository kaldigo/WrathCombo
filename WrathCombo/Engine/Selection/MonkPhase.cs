namespace WrathCombo.Engine.Selection;

internal enum MonkForm { None, Opo, Raptor, Coeurl }
internal readonly record struct MonkPhaseInput(bool Active, bool Lunar, bool Solar, bool Area,
    MonkForm First, MonkForm Second, MonkForm Third, bool RaptorLearned, bool CoeurlLearned);

internal static class MonkPhase
{
    internal static MonkForm Next(MonkPhaseInput state)
    {
        if (!state.Active || state.Third != MonkForm.None) return MonkForm.None;
        bool Has(MonkForm form) => state.First == form || state.Second == form;
        bool mixed = state.First != MonkForm.None && state.Second != MonkForm.None && state.First != state.Second;
        // Actual chakra, rather than the number of presses, determines recovery after a manual deviation.
        bool solar = state.Lunar && !state.Solar || mixed;
        if (!solar) return state.First == MonkForm.None ? MonkForm.Opo : state.First;
        if (state.Area && !Has(MonkForm.Opo)) return MonkForm.Opo;
        if (!state.Area && !Has(MonkForm.Coeurl) && state.CoeurlLearned) return MonkForm.Coeurl;
        if (!Has(MonkForm.Raptor) && state.RaptorLearned) return MonkForm.Raptor;
        if (!Has(MonkForm.Opo)) return MonkForm.Opo;
        if (!Has(MonkForm.Coeurl) && state.CoeurlLearned) return MonkForm.Coeurl;
        return MonkForm.Opo; // Level-sync fallback; do not suggest an unavailable form action.
    }
}
