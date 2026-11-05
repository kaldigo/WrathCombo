using System;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using WrathCombo.Engine.Selection;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class PvpAllySelection
{
    internal static bool Active => InPvP() && ExperimentalSelection.PvpTargeting;
    internal static IBattleChara? Resolve(uint action, Func<IBattleChara, bool>? eligible = null, ulong? retained = null)
    {
        if (!Active || !ActionRoutes.Allowed(action)) return null;
        var choice = SelectionRuntime.SelectAlly(action, retained, eligible);
        return choice.Recipient is { } id ? Svc.Objects.SearchById(id) as IBattleChara : null;
    }
    internal static uint Publish(uint input, uint action, IBattleChara recipient, string reason) =>
        SelectionRuntime.Publish(input, new SelectionDecision(action, action, recipient.GameObjectId, 0,
            reason, Environment.TickCount64, SelectionRuntime.Generation));
}
