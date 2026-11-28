# Manual preview and automatic decision ownership

Automatic rotation resolves fresh actions under its existing runner cadence. It neither reads nor writes the manual hotbar preview cache or its throttle, and does not overwrite remembered idle icons. The direct auto combo path also records its result separately. The existing LastActionInvokeFor store retains manual preview semantics.

Retarget validation reads the last action for the current context. Resolver records, action results, settings/job invalidation and expiry cleanup are separated for manual and automatic contexts, including the case where both have the same retarget ID. Manual presses retain their existing forced refresh and queued/native target binding. Checked native execution, external-engine execution, loading guards and backup buttons retain their bypasses.

This prevents a moving automatic heal rejection (Wait) from contaminating a manual Medica preview, and prevents a manually displayed spell or patient from becoming a cached automatic decision. Automatic movement/cast checks remain enabled; no global native cooldown or cast restriction is removed. Runner retry cadence and logging preferences are unchanged.

Tests exercise the production hook, context-bound action records and production retarget add/remove/reset methods, alongside existing interruption, targeting, raising and manual input regressions. Native icon stability and frame-time improvement require the next in-game capture.
