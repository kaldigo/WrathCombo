# Stage 0 rotation observations

Branch `codex/rotation-stage0` adds session-only diagnostics, consumed by the matching Accord branch's Rotation testing page. It does not change rotation policies, plugin identity, settings schema, release workflows or action execution ownership.

`WrathCombo.Testing.Trace.v1` is an additive string-to-string IPC endpoint. Commands: `start`, `stop`, `read`. Call on the framework thread. JSON returns `Success` or an `Error`, schema 1, active status, stop reason, baseline, saved settings at read, repeated-selection and overflow counts, and immutable observation rows. Start requires a logged-in player. No settings writes or automatic actions are available through this endpoint.

Each row has sequence, elapsed milliseconds, kind, input/output action IDs, and an optional recipient ID. Selection rows observe the existing uncached ActionReplacer call once. Repeated unchanged results are collapsed per input. Send rows observe the outgoing native hook and include action type; they do not prove successful server execution. Do not infer an action-to-target binding from a preceding Selection row.

Sessions stop after 60 seconds or player/job/level/territory/PvP changes. Storage is bounded to the latest 2,048 rows with dropped-row counts. Reading copies the observation buffer and saved settings, never invoking a resolver, consuming a retarget registration or progressing an opener. The original selection path remains stateful. Baselines are saved settings, not effective external lease snapshots; report readers must account for that distinction.

The pure `DecisionTrace` recorder is compiled into the paired Accord test suite. Build this branch using `./tools/build.ps1 -ClientSourceRoot <Accord-testing-worktree>` to include its new scenario tests, or build from Accord with its `-EngineSourceRoot` override. The existing pinned compatibility client still exercises unchanged engine contracts.

Source baseline: Wrath `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`, Accord `8eef51a29cee077edb52135125e8e54666e4debb`. See the paired Accord branch's `docs/rotation-redesign/implementation-status.md` for the mutation audit and live acceptance gate. No live-game validation or gameplay improvement is claimed yet.
