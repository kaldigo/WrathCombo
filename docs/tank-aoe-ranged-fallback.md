# Shared tank AoE ranged fallback

`WrathCombo.RotationSettings.v1` exposes the persistent boolean `TankAoeRangedFallback`, defaulting to true. The engine owns this ordinary rotation preference; it has no dependency on Accord variants or UI.

The shared preference gates PLD/WAR's existing simple/advanced AoE ranged-uptime branches and enables matching fallback for DRK/GNB. DRK/GNB require a battle target outside the AoE's range, a ready ranged attack within its range, and no current weave opportunity. No new target is acquired by these branches. Disabling the preference leaves their native AoE paths intact. ST ranged options, the dedicated Pull selector, automatic role mechanics and PLD's normal magic sequence keep their existing controls.

The client presents one tank-wide switch. Runtime persistence tests cover the default, saved opt-out, rollback and reload. Compiled production snippets exercise PLD/WAR mode gates and DRK/GNB readiness, target, range and weave boundaries. Build/tests do not establish in-game acceptance.
