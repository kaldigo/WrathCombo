using System;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using WrathCombo.Combos.PvE;
using WrathCombo.Core;
using WrathCombo.Services;

namespace WrathCombo.Engine;

internal sealed class EmergencyRotation : IDisposable
{
    [ThreadStatic] internal static bool Resolving;
    private readonly ICallGateProvider<bool, string> press = Svc.PluginInterface.GetIpcProvider<bool, string>("WrathCombo.EmergencyDamage.v1");
    internal EmergencyRotation() => press.RegisterFunc(Press);
    private static unsafe string Press(bool area)
    {
        if (!Svc.Framework.IsInFrameworkUpdateThread || !Player.Available || Player.IsDead || Svc.ClientState.IsPvP)
            return "Backup damage controls require a living PvE character.";
        if (!AutomationOwnership.Managed || AutomationOwnership.Allowed)
            return "Waiting for Accord to stop automation.";
        uint input = area ? All.AoEDPS : All.SingleTargetDPS;
        uint action = 0;
        try
        {
            Resolving = ExperimentalSelection.LegacyDamage = true;
            if (ActionReplacer.FilteredCombos == null) Service.ActionReplacer.UpdateFilteredCombos();
            foreach (var combo in ActionReplacer.FilteredCombos!)
                if (combo.TryInvoke(input, out action)) break;
        }
        finally { Resolving = ExperimentalSelection.LegacyDamage = false; }
        if (action == 0 || action >= All.SingleTargetDPS) return "Wrath has no usable damage action for this press.";
        ulong target = Svc.Targets.Target?.GameObjectId ?? 0xE0000000;
        using var dispatch = AutomationOwnership.QueuedExternalScope();
        bool accepted = ActionManager.Instance()->UseAction(ActionType.Action, action, target);
        AccordDiagnostics.DiagnosticLog.Event("Emergency.Press", () => new { Area = area, Action = action, Target = target, Accepted = accepted });
        return accepted ? "Wrath accepted the backup press." : "The game did not accept the backup action.";
    }
    public void Dispose() => press.UnregisterFunc();
}
