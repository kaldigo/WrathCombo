using System;
using System.Collections.Generic;
using System.Linq;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;

namespace WrathCombo.Combos.PvE;

internal partial class NIN
{
    private static readonly List<MudraCasting> testingMudras = new();
    private sealed record MudraProposal(uint Action, MudraCasting.MudraState Intent, long At, long Generation);
    private static readonly Dictionary<uint, MudraProposal> mudraPreviews = new();
    private static readonly AcceptedProposal<MudraProposal> mudraPress = new();
    private static MudraProposal? automaticMudra;
    private static long mudraGeneration = -1;

    private static void TestingMudraContext()
    {
        if (mudraGeneration == SelectionRuntime.Generation) return;
        mudraGeneration = SelectionRuntime.Generation;
        mudraPreviews.Clear(); automaticMudra = null; mudraPress.Reset();
        foreach (var state in testingMudras) state.CurrentMudra = MudraCasting.MudraState.None;
    }

    internal static MudraPreviewScope? BeginTestingMudra(uint input)
    {
        if (!ExperimentalSelection.ActiveNinja) return null;
        TestingMudraContext();
        mudraPreviews.Remove(input);
        return new MudraPreviewScope(input);
    }

    internal sealed class MudraPreviewScope : IDisposable
    {
        private readonly uint input;
        private readonly (MudraCasting Instance, MudraCasting.MudraState Intent)[] previous;
        internal MudraPreviewScope(uint input)
        {
            this.input = input;
            previous = testingMudras.Select(m => (m, m.CurrentMudra)).ToArray();
        }
        internal void Complete(uint action)
        {
            if (!MudraSigns.Contains(action)) return;
            var changed = previous.FirstOrDefault(p => p.Instance.CurrentMudra != p.Intent);
            var intent = changed.Instance?.CurrentMudra ?? previous.Select(p => p.Intent)
                .FirstOrDefault(i => i != MudraCasting.MudraState.None);
            if (intent == MudraCasting.MudraState.None) return;
            var proposal = new MudraProposal(action, intent, Environment.TickCount64, mudraGeneration);
            mudraPreviews[input] = proposal;
            if (SelectionRuntime.Automatic) automaticMudra = proposal;
        }
        public void Dispose()
        {
            foreach (var state in previous) state.Instance.CurrentMudra = state.Intent;
        }
    }

    internal static void PressTestingMudra(uint input, uint action)
    {
        if (!ExperimentalSelection.ActiveNinja) return;
        TestingMudraContext();
        var proposal = mudraPreviews.GetValueOrDefault(input) ??
            (SelectionRuntime.Automatic && automaticMudra?.Action == action ? automaticMudra : null);
        mudraPress.Reset();
        if (proposal is not null && proposal.Action == action && proposal.Generation == mudraGeneration &&
            Environment.TickCount64 - proposal.At is >= 0 and < 500)
            mudraPress.Press(action, proposal, Environment.TickCount64, mudraGeneration);
    }

    internal static void RejectTestingMudra() => mudraPress.Reset();

    internal static void ConfirmTestingMudra(uint action)
    {
        if (!ExperimentalSelection.ActiveNinja) return;
        TestingMudraContext();
        var accepted = mudraPress.Confirm(action, Environment.TickCount64, mudraGeneration);
        if (accepted is not null)
        {
            // The accepted sequence belongs to the job, not whichever ST/AoE icon happened to be queried.
            foreach (var state in testingMudras) state.CurrentMudra = accepted.Intent;
            TestingApi.Observe("MudraAccepted:" + accepted.Intent, action, 0, 0);
        }
        else if (!MudraSigns.Contains(action))
        {
            foreach (var state in testingMudras) state.CurrentMudra = MudraCasting.MudraState.None;
        }
    }
}
