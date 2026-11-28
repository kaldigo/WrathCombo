using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Combos.PvE;
using WrathCombo.Extensions;

namespace WrathCombo.Engine;

internal static class IncomingRaise
{
    private const uint RaiseLogos = 12996, SacrificeLogos = 14481;
    internal static bool IsRaise(uint action) => action is WHM.Raise or SCH.Resurrection or AST.Ascend
        or SGE.Egeiro or RDM.Verraise or BLU.AngelWhisper or Variant.Raise or Variant.Raise2
        or Bozja.LostArise or Bozja.LostSacrifice or RaiseLogos or SacrificeLogos || action == OccultCrescent.OccultRaise || action == OccultCrescent.Revive;

    private static readonly HashSet<ulong> recipients = new();
    private static DateTime frame;
    internal static bool Available(IGameObject? target, bool fresh = false)
    {
        if (SelectionRuntime.CurrentFriendlyActor(target) is not { IsDead: true, IsTargetable: true } patient ||
            patient.HasRaiseStatus || patient.HasStatus(All.Buffs.Raised, true)) return false;

        if (fresh || frame != Svc.Framework.LastUpdateUTC)
        {
            recipients.Clear();
            var self = Player.Object?.GameObjectId;
            foreach (var obj in Svc.Objects)
                if (obj is IPlayerCharacter caster && SelectionRuntime.CurrentFriendlyActor(caster) != null &&
                    caster.IsCasting && IsRaise(caster.CastActionId) && caster.GameObjectId != self)
                    recipients.Add(caster.CastTargetObjectId);
            frame = Svc.Framework.LastUpdateUTC;
        }
        return !recipients.Contains(patient.GameObjectId);
    }

    // Swiftcast targets self at the native boundary. Keep the intended patient
    // through that call so a queued preparation can also be checked on replay.
    [ThreadStatic] internal static ulong DispatchPatient;
    internal static unsafe bool Submit(uint action, ulong patient, ulong nativeTarget)
    {
        if (!Available(Svc.Objects.SearchById(patient), fresh: true))
            return AccordDiagnostics.DiagnosticLog.Reject("Automatic raise patient unavailable or has incoming raise");
        ulong previous = DispatchPatient;
        DispatchPatient = patient;
        try { return ActionManager.Instance()->UseAction(ActionType.Action, action, nativeTarget); }
        finally { DispatchPatient = previous; }
    }
}
