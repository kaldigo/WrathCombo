using System;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using WrathCombo.Combos.PvE;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Engine;

namespace WrathCombo.CustomComboNS;

internal sealed class FateStanceRestore
{
    private ulong playerId;
    private uint stance;
    private bool wasSynced;
    private bool wasOn;
    private uint pendingFate;
    private long observeLossUntil;
    private bool observedLoss;
    private long nextCheck;

    internal void Reset()
    {
        playerId = stance = pendingFate = 0;
        wasOn = wasSynced = false;
    }

    internal unsafe void Tick()
    {
        var player = Player.Object;
        var (action, release) = Player.Job switch
        {
            Job.PLD => (PLD.IronWill, 32065u),
            Job.WAR => (WAR.Defiance, WAR.ReleaseDefiance),
            Job.DRK => (DRK.Grit, DRK.ReleaseGrit),
            Job.GNB => (GNB.RoyalGuard, GNB.ReleaseRoyalGuard),
            _ => (0u, 0u),
        };
        if (player == null || action == 0 || player.IsDead || CustomComboFunctions.InDuty())
        {
            Reset();
            return;
        }

        bool synced = Player.IsLevelSynced;
        bool sameTank = playerId == player.GameObjectId && stance == action;
        if (!sameTank)
            Reset();

        var manager = FateManager.Instance();
        var fate = manager == null ? null : manager->CurrentFate;
        uint fateId = fate == null ? 0u : fate->FateId;
        long now = Environment.TickCount64;
        if (sameTank && synced && !wasSynced && wasOn && fateId != 0)
        {
            pendingFate = fateId;
            // The sync flag and status list can update on successive frames.
            observeLossUntil = now + 1000;
            observedLoss = false;
            nextCheck = now;
        }

        playerId = player.GameObjectId;
        stance = action;
        wasSynced = synced;
        if (!synced)
        {
            wasOn = CustomComboFunctions.PlayerHasTankStance();
            pendingFate = 0;
            return;
        }

        if (pendingFate == 0) return;
        if (fateId != pendingFate || CustomComboFunctions.JustUsed(release))
        {
            pendingFate = 0;
            return;
        }
        if (CustomComboFunctions.PlayerHasTankStance())
        {
            if (observedLoss || now >= observeLossUntil)
                pendingFate = 0;
            return;
        }
        observedLoss = true;
        if (now < nextCheck) return;
        nextCheck = now + 250;
        if (Player.SyncedLevel < 10 || !AutomationOwnership.Allowed || AutomationOwnership.Busy)
            return;
        var am = ActionManager.Instance();
        if (am != null && am->GetAdjustedActionId(action) == action &&
            am->GetActionStatus(ActionType.Action, action, player.GameObjectId) == 0 &&
            am->UseAction(ActionType.Action, action, player.GameObjectId))
        {
            pendingFate = 0;
            Svc.Log.Information($"Restored tank stance {action} after level sync for FATE {fateId}.");
        }
    }
}
