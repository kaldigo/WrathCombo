# Rotation review implementation and acceptance

Authorized scope: the complete 27 November 2026 rotation review, plus manual Phoenix Down execution protection. The saved `codex/rotation-review` work is merged into `testing` following the user's authorization to publish for in-game testing. Stable source and feeds remain untouched. Review source pins: Wrath `e7586437`, RSR `a946eb10`. The full source assessment is in the workspace `docs/rotation-review-2026-11-27.md`.

Completion requires implementation, focused behavioral tests, build/client compatibility checks and a final code/requirement audit. In-game outcomes must be identified separately from automated evidence. A partial milestone must not be described as completion of this list.

## Publication status

The user authorized resuming the Wrath rollout on 27 November 2026. The saved implementation is merged into `testing` for candidate **1.0.4.81**, with exact-source build, regression, package and public artifact/feed verification required before completion. The earlier attempt was cancelled before release; its unpublished tag was removed. RSR **7.5.7.23** is already published separately with the manual-item guard. Native combat acceptance remains pending.

## Stage A: readiness and execution

- [x] Phoenix Down/manual item queue and cast ownership in both engines.
- [x] Authoritative active native recast, cast, animation and queue readiness.
- [x] Explicit submission/rejection/interruption/completion lifecycle and timings.
- [x] Preemptible preparation with bounded status confirmation and no healer starvation.
- [x] Successful submission distinguished from a waiting or rejected decision.
- [x] Manual target, LB/channel, Raise, exclusions and engine ownership regressions.

## Stage B: encounter and healing

- [x] Attributed forecast/native/marker event snapshot with freshness and fallback.
- [x] Shared incoming-damage consumers and attributed tankbuster deadlines.
- [x] Per-purpose/recipient prevention fulfillment and coverage.
- [x] Event-timed short defenses and Variant/field self-care.
- [x] Urgent healing arbitration across simple/advanced/buttons/auto.
- [x] Full toolkit coverage and timed pre-hit shields/post-hit heals for WHM/SCH/AST/SGE.
- [x] Actual Eukrasia/preparation timing; pending-HP capability/fallback checks.
- [x] BossMod movement, cancellation and automated dash safety with manual overrides.

## Stage C: job adapters

- [x] PLD
- [x] WAR
- [x] DRK
- [x] GNB
- [x] WHM
- [x] SCH
- [x] AST
- [x] SGE
- [x] DRG
- [x] MNK
- [x] NIN
- [x] SAM
- [x] RPR
- [x] VPR
- [x] BRD
- [x] DNC
- [x] MCH
- [x] BLM
- [x] RDM
- [x] SMN
- [x] PCT
- [x] BLU
- [x] BST
- [x] Base classes/sync, noncombat waiting/resume and all 21 manual PvP regression dispositions.

## Stage D: verification and release

- [x] Deterministic native-state, readiness, event and job decision fixtures.
- [x] Bounded last-duty diagnostics with opportunity/rejection/impact/landing evidence.
- [x] Instrumentation supports logging-on/off comparison; native-state work stays on the game thread. In-game timing comparison remains an acceptance exercise.
- [x] Full build, meaningful tests, client compatibility and testing package validation.
- [x] Final source-versus-plan audit; address discovered omissions and rerun affected checks.
- [x] Testing-only release prepared with honest remaining native acceptance notes; stable untouched. Public artifact/feed verification is recorded after publication.

## Implemented behavior and retained contracts

The changes close the reviewed source gaps without replacing Wrath's job knowledge or user controls. Normal damage weaving, opener permission, manual burst/support/protection/class-mechanic permissions, exclusions, resource holds and native action eligibility remain binding. A legal native manual action is not subjected to automatic forecast vetoes. Automatic cast cancellation owns only a cast actually submitted by automation, including when a different next spell is queued.

The readiness snapshot uses active native recast state, cast remaining and animation lock. Inactive retained recast and queue IDs cannot create a false wait. A successful queue acceptance counts as submission even if the native call returned false; one accepted action consumes the runner pass. An early native cast release invalidates selection immediately but is not mislabeled as a proven interruption: a successful slidecast may still produce its effect. Genuine game-enforced recast remains respected.

Phoenix Down and other deliberate item input bypass rotation interception in both engines, preserve their native recipient, and hold automation through queue, cast, placement and completion lock. Native item casts may expose an action ID rather than the item ID. The guard follows that cast instead of guessing a duration. Invalid/cancelled input expires, automation resumes, and no saved auto preference changes. An owned automatically queued consumable is distinguished from manual input. Existing LB protection uses the same guard.

Healer urgency runs before retained preparation, prevention and nonurgent utility. Emergency abilities can use a legal native window without requiring an ordinary damage weave. Preparation/status and Deployment-shield visibility waits are bounded. Settings and held resources still apply: urgency does not silently enable an excluded heal. Scheduled healing reserves only the immediately preceding damage GCD, revalidates event/coverage/cast legality, and distinguishes pre-impact shields from post-impact recovery. Plenary and Zoe retain the chosen heal, recipient and landing time after confirmation.

BossMod snapshots retain freshness, generation and unsupported-capability fallback. Known hostile casts and marker victims produce one per-frame incoming-damage snapshot; a global buster timer or party bit mask is never guessed to mean the local player. Per-event purposes and recipients permit mitigation, shields, reactive resources and recovery without an indiscriminate cooldown dump. Short defenses and enabled Variant/field self-care use attributed deadlines and observed loss. Generic damage/type/mask/vulnerability fields are observable where supplied; this is not a new all-job vulnerability optimizer. Automatic casts/dashes consume movement/cancel/path hints when available. Unknown hints preserve existing behavior. Manual actions remain available.

## Job-by-job closure

All listed jobs also use shared native readiness, submission ownership and applicable cast/survival rules. Extracted GCD stages reuse the original local priority logic; they do not invoke the full rotation twice or simulate cooldowns. Existing scripted openers retain ownership of their script.

| Job | Concrete implementation / preserved logic |
|---|---|
| PLD | ST/AoE simple/advanced selected GCD feeds FoF; existing magic/proc/Confiteor priorities retained; short defense timed to actual upgraded coverage. |
| WAR | Selected GCD, enabled IR payload duration including learned Rend/Ruination, Nascent Chaos expiry rescue; Infuriate/reserve, stance and pull rules retained. |
| DRK | Blood/core GCD stages separated from ability spending; actual Delirium stacks, Scorn expiry and Darkside replace fixed Scorn waits; attributed TBN reserve retained. |
| GNB | Existing complete GCD planner feeds selection; No Mercy payoff models available chains and enabled payload; cartridge/Continuation/urgent Sonic Break retained. |
| WHM | Recovery-first arbitration; Benison/Aquaveil, Temperance/Caress/Asylum/Bell/Plenary coverage; lily/HoT-aware post-impact area healing and bounded Thin Air continuation. |
| SCH | Recovery-first, Protraction/Excog/Adlo/Recitation/Deployment, fairy/Seraph/Seraphism/Soil/Expedient coverage; last flow reserved for enabled imminent healing; shield/status waits bounded. |
| AST | Recovery-first; mature-Star lead time, Sun Sign/Neutral Sect, Macro/Micro release and multihit coverage; manual Macro follow-through and urgent accumulated recovery; cards remain subordinate to urgency. |
| SGE | Recovery-first including AoE damage buttons; actual Eukrasia/preparation timing, Zoe/Pneuma, Haima/Panhaima, Physis/Philosophia/Holos/Kerachole and shields; configured gall holds retained. |
| DRG | Life Surge binds actual selected weaponskill; expiring Mirage/Starcross rescue outside scripted opener; jump permission/safety and combo logic retained. |
| MNK | Selected GCD and earned Fires/Winds deadline account for a new PB phase; original Nadi/form/Blitz and odd/even burst logic retained. |
| NIN | Publishes actual ninjutsu aim without speculative mutation; accepted signs, preview rollback, TCJ movement/status expiry, target and quest eligibility retained. |
| SAM | Selected GCD coordinates Shoha before meditation overflow; legal movement alternative honors settings; existing Sen/Kaeshi/Meikyo/Kenki/Zanshin and accepted Tsubame retained. |
| RPR | Actual selected GCD plus event-timed Crest and shared Communio cast safety; previous Death's Design, Soul/Enshroud quest fallback and earned-action/burst corrections retained. No new encounter scripts or pooling/HP cutoff changes. |
| VPR | Selected GCD, transformed adjusted recasts and Legacy locks in phase budget; combo-expiry guard and diagnostic agree; accepted continuation retained. |
| BRD | Selected GCD, expiring Encore/Resonant/Blast rescue, bounded observed target-lifetime estimate for automatic DoT value; existing song/repertoire/HP restrictions retained. Unknown lifetime remains permissive. |
| DNC | Full selected GCD precedes Devilment/Flourish decisions; original dance ownership, range holds, anti-overwrite, Esprit and payoff rules retained. Technical remains Burst-owned. |
| MCH | Existing tool plan feeds selection; Wildfire/Barrel whole-payoff downtime guards and legal heat dump; original Queen/tool/Reassemble eligibility retained; Flamethrower channel protected. |
| BLM | Selected-spell Swift/Triple binding and legal shorter phase fallback when initial cast is unsafe; existing phase/MP/hearts/soul/Polyglot decisions retained; scripted opener not substituted. |
| RDM | Selected long-spell Swift preparation and earned Grand Impact/finisher deadlines; urgent Raise supersedes offense; original mana/melee/Acceleration/Dualcast settings retained. |
| SMN | Accepted Raise continuation before offense, selected-spell preparation; native-capable elemental order adapts to known movement without removing the sole legal summon; Ruin IV fallback; demi/pet/dash permissions retained. |
| PCT | Selected motif/damage cast budget and Swift binding; Rainbow/Star/Hammer deadlines; existing canvas/burst/paint-in-downtime logic retained. |
| BLU | Equipped eligible ST/AoE basic fallback, protected Flurry/Gate channels with legal finishers; role, equipped spell, learned capability and explicit self-kill permissions retained. |
| BST | Confirmed chain progress has per-step and total bounds; target/capability/manual conflict invalidation; existing native pet-specific eligibility retained. |

Base-class/sync selection still delegates learned/native capability. Noncombat waiting and saved-auto resume remain separate from combat readiness. All 21 standard PvP job sources are unchanged: manual bars/Guard/LB and PvP auto suspension remain the boundary, not an unrequested PvP rotation rewrite.

Pneuma healing coverage uses its secondary 20-yalm circle around the caster rather than its hostile damage line, as described in the [official Sage job guide](https://na.finalfantasyxiv.com/jobguide/sage/). The native-boundary fixture deliberately supplies zero allies for the damage line to catch that distinction.

## Final audit and automated evidence

- Release builds: Wrath 1.0.4.81 (79 inherited warnings, zero errors), RSR 7.5.7.23 (12 inherited warnings, zero errors).
- Pure production policies: 74 behavioral checks, including active/inactive native recast, queue windows, cast release/completion and same-ID ownership, attributed multi-victim prevention budgets, event grace/replacement, heal landing, timed preparation, resource/deadline/limited-job progress, health trends, target lifetime and summon phase.
- Wrath Python suite: 53 tests passed. Production-method/native-boundary fixtures cover manual items/LB, runner proposals, all four healer landing paths, urgent tank recovery, native readiness, target/raise/pull/stance and packaging/publication policy.
- RSR Python suite: 13 tests passed; settings conversion, manual permissions, built settings API and rollback checks passed.
- Current Accord client: 474,403 assertions; pinned compatibility client: 474,383. Both cover 512 PvE job/variant and 336 PvP combinations. These counts prove contracts/configuration combinations, not combat potency or every native rotation outcome.
- Final review repaired: accepted queue returning false incorrectly rejecting preparation; ownership of a casting spell overwritten by its queued follow-up; manual same-spell cast inheriting automatic cancellation ownership; obsolete early-release-as-interruption classification; unrelated tank victims sharing prevention fulfillment; retained inactive queue IDs; Pneuma damage/heal geometry mismatch; simple SGE timed-recovery fallback.
- Bounded last-duty diagnostics retain decision, rejection, native readiness/submission, effect, cast-release/cancellation and landing evidence. Performance scopes cover forecast, healer selection, target lifetime and execution without worker-thread native reads. Detailed logging remains enabled according to the user's setting; retry cadence and saved preferences were not changed.
- Packaging and public testing-feed checks are the final publication gate below. Stable source and stable projections must remain unchanged.

## Native acceptance after installation

Source implementation and automated release checks do not establish measured in-game DPS, survival, effect latency or frame-time improvements. The first gameplay pass should exercise Phoenix Down during active auto (including cancel), early/late interrupted casts, urgent tank recovery during accepted preparations, pre/post-raidwide landing on all four healers, known/unknown BossMod capabilities, unsafe automated versus deliberate manual movement actions, synced capability and scripted/manual burst ownership. Compare timings with detailed logging on/off and inspect the new readiness/landing events. No native game run was performed during this implementation. Native effect/version capability falls back to observed current HP when unsupported; it must not guess packet layouts.
