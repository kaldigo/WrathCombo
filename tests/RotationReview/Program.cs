using WrathCombo.Engine.Selection;

static class Checks
{
    private static int checks;
    private static void That(bool condition, string scenario)
    {
        checks++;
        if (!condition) throw new Exception(scenario);
    }

    static void Main()
    {
        // Cancellation clears native activity, not necessarily the last group's
        // fields. The inactive values must never impose a synthetic GCD wait.
        That(ActionReadiness.Remaining(false, 2.5f, .4f) == 0, "stale inactive interrupted recast");
        That(ActionReadiness.Remaining(true, 2.5f, .4f) > 2, "real recast retained after interruption");
        That(ActionReadiness.Remaining(true, 2.5f, 3) == 0, "elapsed recast clamp");
        That(ActionReadiness.Remaining(true, float.NaN, 0) == 0, "invalid native sample");
        var ready = new ActionReadiness(true, true, 2.5f, 2, 0, 0, 0, false, 0);
        That(ready.CanSubmit(false, .5f) && !ready.CanSubmit(true, .5f), "urgent ability versus unavailable GCD");
        That(!(ready with { Cast = 1, CastRemaining = 1 }).CanSubmit(false, .5f), "do not interrupt a real cast to weave");
        That(!(ready with { AnimationLock = .1f }).CanSubmit(false, .5f), "native animation lock respected");
        That((ready with { GcdRemaining = .4f }).CanSubmit(true, .5f), "native queue window");

        var casts = new CastLifecycle();
        casts.Observe(10, 2.5f, true, 1000);
        casts.Observe(10, .4f, true, 3100);
        That(casts.Observe(0, 0, false, 3200).RecastReleasedEarly, "late cancellation resumes when native recast is released");
        casts.Observe(10, 2.5f, true, 4000);
        casts.Complete(10, 6450);
        That(!casts.Observe(0, 0, false, 6470).RecastReleasedEarly, "confirmed completion is not an interruption");
        casts.Observe(10, 2.5f, true, 6500);
        That(casts.Observe(0, 0, false, 6800).RecastReleasedEarly, "previous same-spell completion cannot hide next interruption");
        casts.Observe(20, .4f, true, 7000);
        That(!casts.Observe(0, 0, true, 7100).RecastReleasedEarly, "retained native recast is not guessed away");

        var trend = new HealthTrend();
        trend.Observe(100, 1000); trend.Observe(70, 2000);
        That(trend.Project(70, 2, 2000) == 50, "recent observed loss can advance enabled self-care");
        trend.Observe(70, 6000);
        That(trend.Project(70, 2, 6000) == 70, "external stall is not extrapolated as continuous incoming damage");
        trend.Reset();
        That(trend.Project(100, 2, 7000) == 100, "sync/context reset clears prior damage trend");

        That(SummonPhase.Choose([new(1,true,true),new(2,true,false)],false)==2,
            "known movement uses an enabled instant elemental phase before hard casts");
        That(SummonPhase.Choose([new(1,true,true),new(2,true,false)],true)==1,
            "safe elemental choice preserves configured order");
        That(SummonPhase.Choose([new(1,true,true),new(2,false,false)],false)==1,
            "no alternative phase must not suppress a legal summon");
        That(SummonPhase.Choose([new(1,false,false)],true)==0,"locked or excluded summon cannot strand rotation fallback");

        var lifetime = new LifetimeEstimate();
        lifetime.Observe(900,1000,1000); lifetime.Observe(800,1000,1500); lifetime.Observe(700,1000,2000);
        That(lifetime.Remaining(2000) is null,"insufficient target history remains unknown");
        lifetime.Observe(600,1000,2500); lifetime.Observe(500,1000,3000);
        That(lifetime.Remaining(3000) is > 2.4f and < 2.6f,"per-target lifetime uses observed rate rather than HP percentage alone");
        lifetime.Observe(900,1000,3500);
        That(lifetime.Remaining(3500) is null,"target recovery/phase reset invalidates damage history");
        That(lifetime.Remaining(7000) is null,"stale history cannot suppress a legal DoT");

        That(!DarkKnightPhase.UseScorn(25,3,15,true,0,2.5f),"real Delirium chain precedes a safe Scorn continuation");
        That(DarkKnightPhase.UseScorn(25,0,0,true,0,2.5f),"completed chain releases Scorn without fixed cooldown-age waits");
        That(DarkKnightPhase.UseScorn(6,3,15,true,0,2.5f),"earlier Scorn expiry is not buried under a fresh chain");
        That(!DarkKnightPhase.UseScorn(10,0,0,false,0,2.5f),"prepare Darkside before an unbuffed nonurgent continuation");

        var sameCast = new CastLifecycle();
        sameCast.Observe(10,.1f,true,1000);
        That(sameCast.Observe(10,2.5f,true,1100).Changed,"back-to-back same-spell casts have separate generations");
        var owner = new CastOwnership();
        owner.Submit(10,10,false,1,1000);
        owner.Submit(20,10,true,1,1200);
        That(owner.Observe(10,1,1250),"queuing another spell must not abandon the currently owned automatic cast");
        That(owner.Observe(20,2,1500),"queued spell becomes owned only when its native cast starts");
        owner.ManualInput();
        owner.Submit(20,20,true,2,1600);
        That(!owner.Observe(20,2,1700),"same-ID queued spell cannot turn the player's current cast into an automatic cast");
        That(owner.Observe(20,3,1800),"next actual cast generation accepts queued ownership");
        owner.ManualInput();
        That(!owner.Observe(20,3,1900),"manual input prevents forecast cancellation of the player's own cast");

        owner.Submit(10,10,false,4,2000); owner.Observe(10,4,2050);
        owner.ManualInput(queued:true);
        That(owner.Observe(10,4,2100),"manual queued follow-up does not hide the currently casting automatic spell");
        That(!owner.Observe(10,5,2200),"same-ID manual follow-up is not inherited as automatic ownership");

        var post = HealLanding.Plan(new(4, 2, 0, 2.5f, .1f, false));
        That(post.Fits && post.ReserveGcd && post.StartIn > 2 && post.LandingIn > 4, "full HP post-hit Medica cast plan");
        var shield = HealLanding.Plan(new(4, 2, 0, 2.5f, .1f, true));
        That(shield.Fits && shield.LandingIn < 4, "shield before hit, not after");
        That(!HealLanding.Plan(new(1, 2, 0, 2.5f, .1f, true)).Fits, "shield cannot finish before hit");
        That(!HealLanding.Plan(new(1, 2, 2, 2.5f, .1f, false)).Fits, "GCD-delayed heal misses landing window");
        That(!HealLanding.Plan(new(12, 2, 0, 2.5f, .1f, false)).ReserveGcd, "long horizon must not idle healer");
        That(!HealLanding.Plan(new(-3, 2, 0, 2.5f, .1f, false)).Fits, "stale event cannot schedule recovery");

        float viper = ResourceWindow.Duration(.2f, [2.2f, 2, 2, 2, 2]);
        That(viper > 11 && viper < 12, "Reawaken uses transformed intervals, not six ordinary GCDs");
        That(ResourceWindow.Covers(viper, 12, 15), "phase fits known buff/downtime window");
        That(!ResourceWindow.Covers(viper, 10, 15), "phase payoff cannot finish before downtime");
        That(!ResourceWindow.Covers(viper, 12, 10), "phase would lose required buff");
        That(!ResourceWindow.Covers(ResourceWindow.Duration(0, [0, 2]), 12), "unknown phase recast is not invented");

        That(EarnedActionDeadline.Choose([new(10, 1, true), new(20, .5f, false)], 0, 2.5f) == 10, "excluded proc cannot replace enabled deadline");
        That(EarnedActionDeadline.Choose([new(10, 4, true), new(20, 1, true)], 0, 2.5f) == 20, "earliest earned action before another GCD");
        That(EarnedActionDeadline.Choose([new(10, 20, true)], 0, 2.5f) == 0, "no unconditional proc dumping");
        That(EarnedActionDeadline.Choose([new(10, 6, true, 3)], 0, 2.5f) == 10, "multi-step payoff must start before resource expires");

        var budget = new PreventionBudget();
        budget.Observe("raidwide", true, 1000, 1, 0, 4, "boss:cast:1");
        var hit = budget.Available("raidwide")!;
        budget.Confirm(hit.Id, PreventionPurpose.Reduction);
        That(budget.Available("raidwide", PreventionPurpose.Reduction) == null, "no duplicate mitigation");
        That(budget.Available("raidwide", PreventionPurpose.Shield) != null, "mitigation must not consume shield plan");
        That(budget.Available("raidwide", PreventionPurpose.Recovery) != null, "mitigation must not consume post-hit recovery");
        budget.Confirm(hit.Id, PreventionPurpose.Shield, 42);
        That(budget.Available("raidwide", PreventionPurpose.Shield, 42) == null &&
             budget.Available("raidwide", PreventionPurpose.Shield, 43) != null, "recipient-specific fulfillment");
        budget.Observe("raidwide", false, 5100, 1, 0, null, recoveryGraceMs: 2000);
        That(budget.Contains(hit.Id), "retain event briefly after impact for landing/recovery");
        budget.Observe("raidwide", false, 7100, 1, 0, null, recoveryGraceMs: 2000);
        That(!budget.Contains(hit.Id), "expired event removed");
        budget.Observe("raidwide", true, 8000, 1, 0, 4, "boss:cast:2");
        That(budget.Available("raidwide", PreventionPurpose.Reduction) != null, "new event has independent budget");

        var rapid = new PreventionBudget();
        rapid.Observe("raidwide", true, 1000, 1, 0, 1, "same-cast");
        long prior = rapid.Available("raidwide")!.Id;
        rapid.Confirm(prior, PreventionPurpose.Recovery);
        rapid.Observe("raidwide", true, 2050, 1, 0, 1, "same-cast");
        That(rapid.Available("raidwide", PreventionPurpose.Recovery) is { } next && next.Id != prior,
            "successive same-ID hit inside recovery grace has a new budget");

        var busters = new PreventionBudget();
        busters.ObserveMany("tankbuster", [new(42,3,"boss:a"),new(43,3,"boss:b")], 1000, 1);
        var victims = System.Linq.Enumerable.ToArray(busters.Active("tankbuster"));
        That(victims.Length == 2,"simultaneous known busters retain independent recipients");
        busters.Confirm(victims[0].Id,PreventionPurpose.Shield);
        That(busters.Fulfilled(victims[0].Id,PreventionPurpose.Shield) && !busters.Fulfilled(victims[1].Id,PreventionPurpose.Shield),
            "shielding one tank cannot consume another tank's event budget");
        busters.ObserveMany("tankbuster", [new(43,2,"boss:b")], 2000, 1);
        That(System.Linq.Enumerable.Count(busters.Active("tankbuster")) == 1,"cancelled native buster removes only its own preparation event");

        var earlyEnd = new PreventionBudget();
        earlyEnd.Observe("raidwide",true,1000,1,0,3,"native-cast");
        long earlyId=earlyEnd.Available("raidwide")!.Id;
        earlyEnd.Observe("raidwide",false,3900,1,0,null,recoveryGraceMs:2000);
        That(earlyEnd.Contains(earlyId),"native cast disappearance just before impact must retain scheduled recovery");

        var sequence = new PreparationSequence();
        var prep = new SelectionDecision(10, 20, 42, 10, "prepare", 1000, 1);
        sequence.Press(prep);
        That(sequence.Current(1000, 1) == null, "preview/press does not accept preparation");
        sequence.Confirm(10, 1100, 1, false);
        That(sequence.Current(1200, 1)?.Recipient == 42, "accepted preparation retains patient");
        sequence.CancelPress();
        That(sequence.Current(1200, 1) != null, "interrupted follow-up can retain existing preparation buff");
        sequence.CancelAccepted();
        That(sequence.Current(1200, 1) == null, "invalid/consumed preparation releases selection");
        var landingPrep = new PreparationSequence();
        var timedPrep = new SelectionDecision(10, 20, 42, 10, "timed enhancement", 1000, 1,
            Purpose: PreventionPurpose.Enhancement, FollowupAfter: 2300, FollowupPurpose: PreventionPurpose.Recovery);
        landingPrep.Press(timedPrep); landingPrep.Confirm(10, 1100, 1, false);
        That(landingPrep.Current(1200, 1) is { ExecuteAfter: 2300, Purpose: PreventionPurpose.Recovery, Recipient: 42 },
            "accepted Plenary/Zoe retains selected heal, recipient and landing time");

        var limited = new LimitedSequence();
        That(limited.Begin([1, 2, 3, 4, 5], 42, 1000), "start capability-bound sequence");
        foreach (var step in new uint[] { 1, 2, 3, 4 })
        {
            long at = 1000 + step * 2500;
            That(limited.Validate(42, true, at), "five GCD sequence must not expire ten seconds after preview");
            limited.Confirm(step, step, at);
        }
        That(limited.Validate(42, true, 13500) && limited.Current == 5, "accepted progress retains final step");
        That(!limited.Validate(42, true, 18000), "no-progress sequence releases instead of waiting indefinitely");

        var earlyRecovery = SelfHealPlan.Evaluate(56.5f, 50, true, .34f, 0, 1.5f, 1.47f, 53);
        That(earlyRecovery.Needed && earlyRecovery.Landing < 1.47f && earlyRecovery.DeferredLanding > 1.47f,
            "instant Cure must take the current GCD opportunity before a known hit, not wait until one second out");
        That(SelfHealPlan.Evaluate(60, 50, true, 0, 0, 2.5f, null, 42).Needed,
            "ongoing observed damage can require recovery without a BossMod timer");
        That(!SelfHealPlan.Evaluate(60, 50, true, 0, 0, 2.5f, null, 59).Needed,
            "healthy stable player does not receive preventive Cure");
        That(!SelfHealPlan.Evaluate(90, 50, true, 0, 0, 2.5f, 1, 40).Needed,
            "preventive recovery retains the configured bounded health ceiling");
        That(!SelfHealPlan.Evaluate(40, 0, true, 0, 0, 2.5f, 1, 0).Needed,
            "disabled self-heal threshold remains disabled");
        That(!SelfHealPlan.Evaluate(60, 50, false, 0, 0, 2.5f, 1, 0).Needed &&
            SelfHealPlan.Evaluate(40, 50, false, 0, 0, 2.5f, null, 40).Needed,
            "forecast option off preserves ordinary threshold recovery");
        That(!SelfHealPlan.Evaluate(60, 50, true, 0, 0, 2.5f, 10, 60).Needed,
            "distant event must not consume a recovery GCD now");
        That(SelfHealPlan.Evaluate(60, 50, true, 1, 2, 2.5f, 4, 60).Needed,
            "cast-time and current lock contribute to the last recovery opportunity");
        Console.WriteLine($"Rotation review foundation: {checks} behavioral checks passed.");
    }
}
