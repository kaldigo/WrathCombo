# Combat evaluation costs

The 1.0.4.83 capture showed recurring targeting and diagnostic work after the repeated custom-button lookup was fixed. These changes reduce repeated work without slowing retries or reusing executable decisions across inputs.

## Target evaluation

Managed ST and AoE automation ask the managed resolver first. Legacy ranking runs only when that resolver is unavailable or the root is unsupported. A supported resolver returning no target remains authoritative. Healer legacy routing, manual fallback policies, party/alliance rules and the subsequent rotation/provider checks remain intact.

Actor snapshots can be reused within a single provider evaluation, including its repeated fresh requests. The party roster is read once while constructing that snapshot. The existing frame cache remains available to previews. A new evaluation and final press/execution validation retain their existing freshness rules; neither action eligibility nor an executable action/recipient pair is cached by this optimization.

Action-specific eligibility rejects candidates outside the same direct range used by the selector before querying native eligibility. All actors remain in the affected set, including those outside direct range, so unsafe AoE splash is still detected. A self-centered action's native self eligibility is queried at most once per selection, and is checked again on the next selection.

## Diagnostics and timings

Feature availability is state. Capture emits its first observation, every active/job/PvP transition and a periodic one-second refresh. Exact active/inactive call counts remain in coverage and summaries. This applies within native presses too; it removes repeated identical feature-state serialization, not action attempts. Readiness results, decisions, recipients, rejections, submissions and completion records retain their existing capture behavior.

The implementation adds independent timing counters for `LegacyTargetRanking`, `EnemySnapshot`, `ActionEligibility`, `DiagnosticCapture` (including gate/count/payload work) and `NativePress`. Timers are inclusive and can overlap; do not add these counters to framework/runner totals as exclusive costs.

Production-method regressions cover ST/AoE ownership and fallback, authoritative empty decisions, healer routing, snapshot lifetime, fresh native rejection, party snapshot count, direct-range pruning and unsafe affected actors. A real transport test checks repeated features inside a native press, state/job/PvP changes, periodic refresh, exact counts, complete readiness/submission records and a new diagnostic session. In-game FPS and frame-time improvement require the next capture.
