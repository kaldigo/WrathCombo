using System;
using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using WrathCombo.Engine;
using WrathCombo.Engine.Selection;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class BST
{
    private static readonly LimitedSequence testingFinisher = new();
    private static long testingGeneration = -1;
    private static uint testingExpected;
    private static ulong testingRecipient;
    private static long testingProgressAt;
    private static bool testingWasReady;
    private static void TickTestingFinisher()
    {
        using var route = new WrathFork.Contract.ActionRouteContext(ActionRoutes.For(All.SingleTargetDPS));
        ulong capability = CurrentPetIsBMPet ? (ulong)(CurrentPet?.DataId ?? 0) : 0;
        if (testingGeneration != SelectionRuntime.Generation)
        { testingGeneration = SelectionRuntime.Generation; testingFinisher.Reset(); testingWasReady = false; testingRecipient = 0; }
        bool eligible = capability != 0 && ActionLearned(CounterClockwiseInstinctualAction) &&
            ActionRoutes.Allowed(Trick) && ActionRoutes.Allowed(CounterClockwiseInstinctualAction) &&
            ActionRoutes.Allowed(Rally) && ActionRoutes.Allowed(RallyingCheer);
        if (testingRecipient != 0 && Svc.Objects.SearchById(testingRecipient) is not IBattleChara { IsDead: false, IsTargetable: true })
            testingFinisher.Reset();
        testingFinisher.Validate(capability, eligible, Environment.TickCount64);
        bool ready = eligible && JobGauge.MasterInstinct >= 2 && JobGauge.PetInstinct >= 1 &&
            ActionReady(Rally) && ActionReady(RallyingCheer) && JobGauge.PlayerTP >= 100 && JobGauge.BeastTP >= 100;
        if (ready && !testingWasReady && testingFinisher.Current == 0)
        {
            testingRecipient = 0;
            testingProgressAt = Environment.TickCount64;
            testingFinisher.Begin([Trick, CounterClockwiseInstinctualAction, Rally, RallyingCheer, Trick], capability, Environment.TickCount64);
        }
        testingWasReady = ready;
        FinisherActions.Clear();
        if (testingFinisher.Current != 0) FinisherActions.Add(testingFinisher.Current);
        FinisherReady = testingFinisher.Current != 0;
        testingExpected = FinisherReady ? OriginalHook(testingFinisher.Current) : 0;
        if (testingExpected != 0 && Environment.TickCount64 - testingProgressAt > 1200 && !ActionReady(testingExpected))
        {
            TestingApi.Observe("BST.SequenceCanceled:capability-or-resource", testingExpected, 0, testingRecipient);
            testingFinisher.Reset(); FinisherReady = false; FinisherActions.Clear();
        }
    }
    internal static void ConfirmTestingFinisher(uint action, ulong recipient)
    {
        if (!ExperimentalSelection.ActiveBeastmaster || testingGeneration != SelectionRuntime.Generation) return;
        if (testingRecipient == 0 && testingFinisher.Current != 0 &&
            (action == testingFinisher.Current || action == testingExpected) &&
            Svc.Objects.SearchById(recipient) is { } target && target.IsHostile()) testingRecipient = recipient;
        if (testingFinisher.Current != 0 && action != testingFinisher.Current && action != testingExpected &&
            Data.ActionWatching.ActionSheet.TryGetValue(action, out var row) && row.ActionCategory.RowId is 2 or 3)
        {
            testingFinisher.Reset(); // An actual manual weaponskill supersedes the retained pet sequence.
            return;
        }
        if (action == testingFinisher.Current || action == testingExpected) testingProgressAt = Environment.TickCount64;
        testingFinisher.Confirm(action, testingExpected, Environment.TickCount64);
    }
}
