using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using ECommons.ExcelServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using WrathCombo.Combos.PvE;
using WrathCombo.Data;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using WrathCombo.Window.Functions;
using WrathFork.Contract;
using WrathCombo.Services;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static class OccultEventTargets
{
    private static DateTime frame;
    private static ushort fate;
    private static ushort encounter;
    private static uint duty;

    private static unsafe bool Active()
    {
        if (Svc.Framework.LastUpdateUTC != frame)
        {
            frame = Svc.Framework.LastUpdateUTC;
            fate = encounter = 0;
            duty = 0;
            if (Player.TerritoryIntendedUseEnum == TerritoryIntendedUseEnum.Occult_Crescent)
            {
                var manager = FateManager.Instance();
                var current = manager == null ? null : manager->CurrentFate;
                if (current != null && current->State == FateState.Running && Player.Level <= current->MaxLevel)
                    fate = current->FateId;
                var events = DynamicEventContainer.GetInstance();
                var ce = events == null ? null : events->GetCurrentEvent();
                if (ce != null && ce->State == DynamicEventState.Battle)
                {
                    encounter = ce->DynamicEventId;
                    duty = FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance()->CurrentContentFinderConditionId;
                }
            }
        }
        return fate != 0 || encounter != 0;
    }

    internal static bool MatchesEvent(ushort fateId, ushort enemyFate, ushort ceId, uint dutyId, uint actorId,
        Vector3 player, Vector3 enemy) => fateId != 0 && fateId == enemyFate ||
        ceId != 0 && InEncounterArena(dutyId, actorId, player, enemy);

    internal static unsafe bool Matches(IBattleChara enemy) => Active() && enemy is IBattleNpc &&
        MatchesEvent(fate, enemy.Struct()->FateId, encounter, duty, enemy.BaseId, Player.Position, enemy.Position);

    // Encounter-specific battle NPCs and arena bounds from awgil/ffxiv_bossmod,
    // BossMod.Modules/Dawntrail/Foray/CriticalEngagement at c50e67f. Generic helpers are excluded.
    internal static bool InEncounterArena(uint dutyId, uint actorId, Vector3 player, Vector3 enemy)
    {
        var arena = (dutyId, actorId) switch
        {
            (1093, 0x4BE1 or 0x4BE2 or 0x4BE3 or 0x4BE4) => (-150f, -860f, 24f, false), // Abductor
            (1093, 0x4BBE or 0x4EBD or 0x4BBF or 0x4BC0) => (-519f, -641f, 24.5f, false), // AlabasterBlade
            (1093, 0x4C4B or 0x4D87 or 0x4C4C or 0x4C4D) => (765f, 0f, 24f, false), // Algol
            (1093, 0x4BD3 or 0x4BD4 or 0x4BD5 or 0x4BD6 or 0x4BD7 or 0x4BD8) => (659f, 659f, 24.5f, false), // Arbatel
            (1093, 0x4C4F or 0x4C50 or 0x4D88) => (238f, 352f, 20f, true), // AtlasCarbuncle
            (1018, 0x46A5 or 0x46A6 or 0x46A7 or 0x46A8 or 0x46A9) => (450f, 357f, 20f, true), // BlackChocobo
            (1093, 0x4D25 or 0x4C46 or 0x4C47 or 0x4C48) => (-688f, 150f, 20f, true), // ClaretDragon
            (1018, 0x46C9 or 0x47F0 or 0x46CB) => (-340f, 800f, 29.5f, false), // CloisterDemon
            (1018, 0x46E1 or 0x4739 or 0x46E2) => (-352f, -608f, 19.5f, false), // CommandUrn
            (1093, 0x4BB8 or 0x4BB9 or 0x4BBA or 0x4BBB or 0x4BBC) => (-215f, -65f, 22f, false), // ConjuredCalofisteri
            (1093, 0x4DFA or 0x4DFB) => (170f, -136f, 20f, false), // CrescentArachne
            (1018, 0x4703) => (620f, 800f, 24.5f, false), // CrescentBerserker
            (1018, 0x4715 or 0x4716 or 0x4717) => (-414f, 75f, 24.5f, false), // CrystalDragon
            (1018, 0x46C4 or 0x46C5 or 0x46C6 or 0x46C7 or 0x46C8) => (681f, 534f, 21f, true), // DeathClaw
            (1093, 0x4BD9 or 0x4BDA) => (-390f, 700f, 29.5f, false), // ElmGigas
            (1018, 0x46D1 or 0x46D2 or 0x46D3) => (-570f, -160f, 20f, false), // Hinkypunk
            (1018, 0x46DC or 0x45D1 or 0x45D2 or 0x46DD or 0x46DE or 0x46DF or 0x46E0) => (636f, -54f, 26f, false), // LionRampant
            (1093, 0x4C77 or 0x4DFD) => (500f, -310f, 25f, false), // Metamorph
            (1018, 0x46B5 or 0x46B6 or 0x46B7) => (300f, 730f, 30f, false), // MysteriousMindflayer
            (1018, 0x469B or 0x469C) => (-800f, 245f, 24.5f, false), // MythicIdol
            (1018, 0x469E or 0x469F) => (461f, -363f, 23f, false), // NeoGarula
            (1018, 0x4704 or 0x47BC or 0x4705 or 0x4706 or 0x4707) => (-117f, -850f, 20f, false), // NymianPetalodus
            (1018, 0x471E or 0x471F or 0x4720 or 0x4721) => (680f, -280f, 20f, false), // OccultKnight
            (1093, 0x4D8F or 0x4D90 or 0x4D91) => (807f, -562f, 20f, false), // Pallmagia
            (1093, 0x4BCA or 0x4BCC or 0x4BCD) => (-870f, -560f, 25f, false), // Pelekys
            (1093, 0x4BC5 or 0x4BC7 or 0x4BC9 or 0x4BC8 or 0x4BC6) => (-82f, 485f, 19.5f, false), // PhantomHydra
            (1093, 0x4BC1 or 0x4C75 or 0x4BC2 or 0x4BC3) => (224f, -860f, 20f, true), // PhantomNecromancer
            (1018, 0x46CC or 0x45CF or 0x46CD) => (870f, 180f, 25f, false), // RepairedLion
            (1093, 0x4C6D or 0x4D55 or 0x4C6E or 0x4C6F or 0x4C70 or 0x4C71 or 0x4C72 or 0x4C73 or 0x4C74) => (152f, 716f, 20f, false), // TinyMage
            (1018, 0x46E7 or 0x46E8 or 0x46E9 or 0x46EA) => (72f, -545f, 25f, false), // TradeTortoise
            _ => default,
        };
        var (x, z, radius, square) = arena;
        bool Inside(Vector3 position)
        {
            float dx = position.X - x, dz = position.Z - z;
            return square ? Math.Max(Math.Abs(dx), Math.Abs(dz)) <= radius : dx * dx + dz * dz <= radius * radius;
        }
        return radius > 0 && Inside(player) && Inside(enemy);
    }

    internal static void AcquireVisible(float range)
    {
        if (!Active() || !ExperimentalSelection.ActiveTargets || !ExperimentalSelection.AcquireVisibleEnemy ||
            !(ExperimentalSelection.AcquireEnemy || ExperimentalSelection.AutomaticEnemies)) return;
        var selected = Svc.Targets.Target;
        if (selected != null && selected.GameObjectId != Player.Object!.GameObjectId &&
            !(selected is IBattleNpc && (selected.IsDead || !selected.IsTargetable))) return;

        var next = Svc.Objects.GetBattleCharas()
            .Where(enemy => !enemy.IsDead && enemy.IsTargetable && !enemy.IsInvincible && Matches(enemy) &&
                enemy.IsHostile() && !Service.Configuration.IgnoredNPCs.ContainsKey(enemy.BaseId) &&
                IsInRange(enemy, range) && IsInLineOfSight(enemy))
            .MinBy(enemy => GetTargetDistance(enemy));
        if (next == null) return;
        Svc.Targets.Target = next;
        AccordDiagnostics.DiagnosticLog.Event("Runner.VisibleEventEnemy", () => new { Fate = fate, Encounter = encounter, Enemy = next.GameObjectId, next.BaseId });
    }
    // CE participants carry Hoofing It. Check both players against the same known arena
    // as well: the status alone does not identify which encounter a player joined.
    private const uint HoofingIt = 1778;
    private static DateTime healFrame;
    private static uint arenaActor;
    private static DateTime patientFrame;
    private static uint patientSpell;
    private static IBattleChara? cachedPatient;
    internal static bool CanHelpEncounter()
    {
        if (!ExperimentalSelection.HealEncounterPlayers || !ExperimentalSelection.AutomaticAllies ||
            !Active() || encounter == 0 || !Player.Object.HasStatus(HoofingIt, true)) return false;
        if (healFrame != frame)
        {
            healFrame = frame;
            arenaActor = Svc.Objects.GetBattleCharas().FirstOrDefault(actor => actor is IBattleNpc &&
                InEncounterArena(duty, actor.BaseId, Player.Position, actor.Position))?.BaseId ?? 0;
        }
        return arenaActor != 0 && !GetPartyMembers().Any(member => member.BattleChara is { IsDead: false } ally &&
            GetTargetHPPercent(ally, forceUsePending: true) <= 70);
    }

    private static unsafe bool EligiblePatient(uint spell, IBattleChara actor) =>
        actor.IsAPlayer() && !actor.IsDead && actor.IsTargetable && actor.CurrentHp > 0 &&
        GetTargetHPPercent(actor, forceUsePending: true) < 50 && !actor.IsInParty() &&
        actor.GameObjectId != Player.Object!.GameObjectId && actor.HasStatus(HoofingIt, true) &&
        InEncounterArena(duty, arenaActor, Player.Position, actor.Position) && !actor.IsHostile() &&
        InActionRange(spell, actor) && IsInLineOfSight(actor) && ActionManager.CanUseActionOnTarget(spell, actor.GameObject());

    internal static bool PatientStillEligible(uint spell, IBattleChara patient) =>
        CanHelpEncounter() && EligiblePatient(spell, patient);

    internal static SelectionDecision? HealPatient(uint input)
    {
        if (SelectionRuntime.Commitment != null || !CanHelpEncounter()) return null;
        uint spell = Player.Job switch
        {
            Job.WHM => ActionLearned(WHM.Cure2) ? WHM.Cure2 : WHM.Cure,
            Job.SCH => SCH.Physick,
            Job.AST => ActionLearned(AST.Benefic2) ? AST.Benefic2 : AST.Benefic,
            Job.SGE => SGE.Diagnosis,
            _ => 0,
        };
        if (spell == 0 || !ActionRoutes.Allowed(spell) || OriginalHook(spell) != spell) return null;
        if (patientFrame != frame || patientSpell != spell)
        {
            patientFrame = frame;
            patientSpell = spell;
            cachedPatient = Svc.Objects.GetBattleCharas().Where(actor => EligiblePatient(spell, actor))
                .MinBy(actor => GetTargetHPPercent(actor, forceUsePending: true));
        }
        var patient = cachedPatient;
        if (patient == null || !ActionReadyForTarget(spell, patient.GameObjectId)) return null;
        var decision = new SelectionDecision(spell, spell, patient.GameObjectId, 0, "Direct heal for Critical Encounter participant",
            Environment.TickCount64, SelectionRuntime.Generation, EncounterHeal: true);
        SelectionRuntime.Publish(input, decision);
        return decision;
    }

    internal static unsafe void EngageSelected(float range)
    {
        if (!Active() || !ExperimentalSelection.ActiveTargets || InCombat() || PartyInCombat() ||
            Player.Object is not { IsCasting: false } || SelectionRuntime.Commitment != null ||
            Svc.Targets.Target is not IBattleNpc { IsTargetable: true, IsDead: false, IsInvincible: false } enemy ||
            !Matches(enemy) || !enemy.IsHostile() || Service.Configuration.IgnoredNPCs.ContainsKey(enemy.BaseId) ||
            !IsInRange(enemy, range) || !IsInLineOfSight(enemy) || PlayerHasActionPenalty(true) ||
            !EzThrottler.Throttle("OccultEventEngage", 1000)) return;

        var preset = Presets.GetJobAutorots.Keys.Select(key => key.Attributes()).FirstOrDefault(attr =>
            attr.AutoAction is { IsHeal: false, IsAoE: false } && attr.ReplaceSkill?.ActionIDs.Count > 0);
        if (preset?.ReplaceSkill == null || AutoRotation.AutoRotationController.WaitForPartyTank()) return;

        // Set the native state explicitly. A toggle would switch it off on the next retry.
        var ui = UIState.Instance();
        if (ui != null && !ui->WeaponState.AutoAttackState.Get())
        {
            bool enabled = ui->WeaponState.AutoAttackState.Set(true);
            AccordDiagnostics.DiagnosticLog.Event("Runner.EventAutoAttack", () => new { Enemy = enemy.GameObjectId, enabled });
        }

        // A caster's weapon swing cannot start combat at spell range. Use one learned,
        // single-target GCD; leave AoE, movement actions and the opener to the normal runner.
        uint root = preset.ReplaceSkill.ActionIDs.First();
        if (!InMeleeRange(enemy))
            root = Player.Job switch
            {
                Job.PLD or Job.GLA => PLD.ShieldLob, Job.WAR or Job.MRD => WAR.Tomahawk,
                Job.GNB => GNB.LightningShot, Job.DRK => DRK.Unmend, Job.DRG or Job.LNC => DRG.PiercingTalon,
                Job.NIN or Job.ROG => NIN.ThrowingDaggers, Job.SAM => SAM.Enpi,
                Job.VPR => VPR.WrithingSnap, Job.RPR => RPR.Harpe, _ => root,
            };
        using var route = new ActionRouteContext(ActionRoutes.For(All.SingleTargetDPS));
        uint action = OriginalHook(root);
        if (!ActionWatching.ActionSheet.TryGetValue(action, out var row) || !row.CanTargetHostile ||
            row.CastType != 1 || row.ActionCategory.RowId is not (2 or 3) || !ActionLearned(action) ||
            !InActionRange(action, enemy) || !ActionReadyForTarget(action, enemy.GameObjectId, true, true) ||
            IsMoving() && ActionManager.GetAdjustedCastTime(ActionType.Action, action) > 0) return;

        SelectionRuntime.BeginPreview(All.SingleTargetDPS);
        var decision = new SelectionDecision(action, action, enemy.GameObjectId, 0, "Start combat on selected event enemy",
            Environment.TickCount64, SelectionRuntime.Generation);
        SelectionRuntime.Publish(All.SingleTargetDPS, decision);
        bool submitted = SelectionRuntime.ExecuteAutomatic(decision);
        AccordDiagnostics.DiagnosticLog.Event("Runner.EventPull", () => new { Action = action, Enemy = enemy.GameObjectId, submitted });
    }

}
