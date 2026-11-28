using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using WrathCombo.Combos.PvE;
using WrathCombo.Engine.Selection;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Engine;

internal static partial class HealerSelection
{
    [System.ThreadStatic] private static int emergencyDepth;
    internal static bool EmergencyAbilityWindow => emergencyDepth > 0 && NativeExecution.Capture().CanSubmit(false, 0);
    private readonly struct EmergencyScope : System.IDisposable
    {
        public EmergencyScope() => emergencyDepth++;
        public void Dispose() => emergencyDepth--;
    }
    // Urgency is evaluated before a retained preparation, ordinary cleanse or
    // prevention. This does not change the user's thresholds or enable a route.
    internal static SelectionDecision? UrgentRecovery(uint input, bool gcds = true)
    {
        if (!ExperimentalSelection.ActiveHealer || RotationBehaviour.Support == false) return null;
        using var emergency = new EmergencyScope();
        if (ExperimentalSelection.ActiveSage) return SGE.UrgentRecovery(input, gcds);
        var buster = EncounterDamage.Tankbuster();
        ulong? priority = buster.Recipient != 0 && buster.Seconds is >= 0 and <= 4 ? buster.Recipient : null;
        var selected = SelectionRuntime.SelectAlly(BasicHeal);
        if (priority != null && (selected.Recipient is not { } urgentRecipient ||
            Svc.Objects.SearchById(urgentRecipient) is not { } urgentActor || GetTargetHPPercent(urgentActor, forceUsePending: true) > 35))
            selected = SelectionRuntime.SelectAlly(BasicHeal, priority);
        if (selected.Recipient is not { } recipient || Svc.Objects.SearchById(recipient) is not { } target ||
            target.IsDead() || !UrgentHealth(GetTargetHPPercent(target, forceUsePending: true), recipient)) return null;
        if (Job == 33 && target.HasStatus(AST.Buffs.Macrocosmos, true) && LocalPlayer.HasStatus(AST.Buffs.Macrocosmos) &&
            OriginalHook(AST.Macrocosmos) == AST.MicroCosmos &&
            Ready(input, AST.MicroCosmos, Self, "Release accumulated Macrocosmos recovery for a critical ally") is { } macro) return macro;
        bool simple = Job switch
        {
            24 => IsEnabled(Preset.WHM_SimpleSTHeals),
            28 => IsEnabled(Preset.SCH_Simple_ST_Heal),
            33 => IsEnabled(Preset.AST_Simple_ST_Heals),
            _ => false,
        };
        if (simple && target is IBattleChara actor)
            foreach (var candidate in SimpleCandidates(false, actor))
                if (candidate.Followup == 0 && ImmediateRecovery(candidate.Action) &&
                    (gcds || !IsGcd(candidate.Action)) &&
                    Ready(input, candidate.Action, candidate.Recipient, "Urgent simple-mode recovery") is { } ready) return ready;
        return RecoveryFor(input, recipient, false, gcds, true, urgent: true);
    }

    internal static bool UrgentHealth(float health, ulong recipient)
    {
        var buster = EncounterDamage.Tankbuster(recipient);
        return health <= 35 || health < 80 && buster.Recipient == recipient && buster.Seconds is >= 0 and <= 4;
    }

    private static bool ImmediateRecovery(uint action) => action is
        WHM.Benediction or WHM.Tetragrammaton or WHM.AfflatusSolace or WHM.Cure or WHM.Cure2 or
        SCH.Lustrate or SCH.Excogitation or SCH.Physick or SCH.Adloquium or
        AST.EssentialDignity or AST.CelestialIntersection or AST.Benefic or AST.Benefic2;
}
