using System;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class BST
{
    private static readonly LimitedSequence testingFinisher = new();
    private static long testingGeneration = -1;
    private static uint testingExpected;
    private static bool testingWasReady;
    private static void TickTestingFinisher()
    {
        ulong capability = CurrentPetIsBMPet ? (ulong)(CurrentPet?.DataId ?? 0) : 0;
        if (testingGeneration != SelectionRuntime.Generation)
        { testingGeneration = SelectionRuntime.Generation; testingFinisher.Reset(); testingWasReady = false; }
        bool eligible = capability != 0 && ActionLearned(CounterClockwiseInstinctualAction) &&
            ActionRoutes.Allowed(Trick) && ActionRoutes.Allowed(CounterClockwiseInstinctualAction) &&
            ActionRoutes.Allowed(Rally) && ActionRoutes.Allowed(RallyingCheer);
        testingFinisher.Validate(capability, eligible, Environment.TickCount64);
        bool ready = eligible && JobGauge.MasterInstinct >= 2 && JobGauge.PetInstinct >= 1 &&
            ActionReady(Rally) && ActionReady(RallyingCheer) && JobGauge.PlayerTP >= 100 && JobGauge.BeastTP >= 100;
        if (ready && !testingWasReady && testingFinisher.Current == 0)
            testingFinisher.Begin([Trick, CounterClockwiseInstinctualAction, Rally, RallyingCheer, Trick], capability, Environment.TickCount64);
        testingWasReady = ready;
        FinisherActions.Clear();
        if (testingFinisher.Current != 0) FinisherActions.Add(testingFinisher.Current);
        FinisherReady = testingFinisher.Current != 0;
        testingExpected = FinisherReady ? OriginalHook(testingFinisher.Current) : 0;
    }
    internal static void ConfirmTestingFinisher(uint action)
    {
        if (!ExperimentalSelection.ActiveBeastmaster || testingGeneration != SelectionRuntime.Generation) return;
        testingFinisher.Confirm(action, testingExpected);
    }
}
