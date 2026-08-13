# Privacy

Seiton Sense is local-only. It does not create accounts, contact a server,
upload gameplay data, or include telemetry. It does not persist or transmit
character names, Home Worlds, combat, target, status, or key history.
Ally Rescue attempt, client-accepted, and confirmed-cleanse counters exist only
in memory for the current match/plugin session and are never uploaded.

## Transient display data

While an enabled feature is active, the plugin can transiently read the
following data already available in the local FFXIV client:

- your job, PvP context, life state, limit gauge, HP/MP, and relevant statuses;
- hostile and party/alliance actor game-object IDs and network entity IDs,
  jobs, life/targetable state, positions, hard targets, cast targets, and
  relevant status IDs with their remaining time;
- FFXIV's native Crystalline Conflict `<e1>`-`<e5>` identities or the single
  native Wolves' Den duel-opponent identity;
- the copied screen rectangle of a visible native nameplate job icon;
- the copied rectangles of visible native self action bars, party-list rows,
  and Crystalline Conflict ally/enemy rows; a visible CC row name is compared
  transiently for equality with the resolved actor and is not retained;
- when its optional module is enabled, your manually selected current/focus
  target and locally available job, HP, distance, CC slot, and pressure state.

Actor observations are joined using exact game-object and network entity
identity. Ambiguous or stale identity is discarded. Nameplate rectangles and
protection timers are held only in bounded in-memory state needed to smooth
short client/UI sampling gaps.

## Native-HUD resource aura

When enabled in PvP, the visual-only resource aura reads current HP/maximum HP
and current/maximum MP for the exact actor associated with each enabled surface.
Low HP is evaluated against the configured percentage. Low MP is displayed only
after a plausible MP sample has established trust and uses an in-memory latch
with a 300-MP exit margin. The latch is keyed by both game-object and network
entity identity and is discarded when that actor leaves the current observation
set or the feature/context becomes inactive.

For the local player, the plugin unions only the visible native action-slot
bounds of each standard, cross, and double-cross bar. It does not use the
broader hotbar-container rectangle or retain slot pointers. Party-list rows
require the native party agent's row index, entity ID, and object pointer to
agree with the resolved actor. Crystalline Conflict ally/enemy rows require exact party or
`<e1>`-`<e5>` resolution, unique actor identity, the reviewed addon/row node, and
equality with the currently visible row name. Invalid, hidden, stale, duplicate,
or ambiguous observations return no aura.

All colors, fills, and pulses are drawn in a separate foreground overlay. The
plugin never writes to, recolors, pulses, or otherwise mutates a native action
slot or UI node. Actor identities, row names, bounds, and resource samples are
not logged, persisted, transmitted, or uploaded. Exact current-patch native row
placement remains a live-validation boundary.

## Pressure tracking

The pressure counter transiently observes enemy hard targets and cast targets,
plus party/alliance hard targets used to compute the team `P#` count. A
read-only action-effect observer examines bounded records directed at your
local entity. For pressure it uses only source/target identity, action identity,
effect-type categories needed to recognize a harmful event, and event sequence
and time. While an Ally Rescue confirmation is pending, the same local observer
can also examine the exact local-caster action result directed at the attempted
party member. It does not read, display, or store damage amounts.

Recent harmful-action evidence remains only in memory for the configured
0.5-8 second window (3 seconds by default) and is then discarded. Queues are
bounded, dropped-event counts are aggregate diagnostics, and no combat history,
actor name, or event payload is logged or uploaded. Pet/owned action sources can
be resolved to their visible player owner solely for the current pressure cue.

When the optional Ally Rescue experiment is enabled, the same current-frame
enemy hard/cast identities are also reduced to a unique incoming-pressure count
for exact party members. This tie-break data is bounded to the live snapshot and
is not retained as combat history.

## Marksman's Spite warning

When the warning is enabled, the same local observer verifies action ID `29415`,
the exact early target-marker shape, the hostile MCH caster identity, and that
the sole target is your local player. The later damage/miss event is rejected.
The warning is deduplicated and held only for its short in-memory lifetime.

The optional alert sound calls one selectable built-in FFXIV UI sound after a
verified warning. No audio file is downloaded, recorded, or transmitted. The
sound attempt is one-shot and never presses Guard or another action.

## CC-protection cues

The nameplate protection feature reads only exact locally visible status IDs,
remaining time, actor job, and PvP context. Its catalog is limited to Guard
`3054`/`3673`, Resilience `3248`, WAR Inner Release `1303`, SAM Meikyo Shisui
`1320`, VPR Hardened Scales `4096`, and large-scale-only Swift `4477` after
metadata validation. One-hit, partial, or ambiguous wards are not classified as
full immunity. The short status grace and absolute expiry are in-memory only.

## Optional CC-immunity brake

The brake is disabled by default and runs only in Crystalline Conflict. When
enabled, it transiently examines the local player's exact job, one already
incoming action ID and target ID, the exact canonical opponent identity, and
that opponent's action-specific verified CC-protection snapshot. Standard
Purify-removable CC and Miracle of Nature use separate blocker matrices, which
include only verified relevant status IDs. The standard matrix contains
`3054`, `3673`, `3248`, `1303`, `1320`, `4096`, and `3143`; Miracle's matrix
contains `3248`, `1320`, `3143`, `3052`, and `3162`. Its reviewed action
list is limited to Intervene `29065`, Blota `29081`, Silent Nocturne `29395`,
Repelling Shot `29399`, Miracle of Nature `29228`, Lethargy `41510`, Forked
Raiju `29510`, Fleeting Raiju `29707`, Air Anchor `29407`, Gravity II `29244`,
its Double Cast form `29248`, and Mineuchi `29535`.

If the master, exact job, and exact action are enabled and the exact hostile
target has verified protection against that action's CC, the plugin returns
`false` for that one incoming call without invoking the downstream/original
action function. This replaces the former invalid-`targetId = 0` handoff and
prevents later game processing from restoring or resolving a default target.
It does not store, log, synthesize, or replay input; initiate or queue an
action; choose another action or target; change the visible hard, soft, or
focus target; or retry later. Every later physical press or third-party Turbo
pulse is an independent incoming call and is checked from current state again.
Vanilla key holding does not create a repeat through this feature.

This is a client-side pre-dispatch check, not a server rollback. Near a
simultaneous activation, an action already accepted by the server roughly
295-355 ms before immunity became locally visible cannot be recalled. FFXIV
may still present its animation and damage while the server rejects the status
effect on the protected target. No additional history is retained to try to
undo or compensate for that result.

Unsupported actions, jobs, contexts, missing or ambiguous actor identity, and
unverified protection pass through unchanged. Broad cone, ground-targeted,
self-centered, and ambiguous multi-target actions are excluded. Another plugin
downstream can still alter the call after Seiton Sense; no claim is made about
such rewritten output. Incoming identities and protection state are not
persisted or transmitted.

## One-shot Near Assist

Near Assist is disabled by default and runs only in Crystalline Conflict. When
explicitly enabled and armed, it transiently reads nearby party/alliance
membership, ally positions, jobs and hard targets, exact native enemy slots,
current team-pressure counts when that preference is enabled, and the
next supported hostile PvP action inside the bounded token window. It does not
read or retain the authored macro text.

These values validate one token lasting at most 750 ms. On success, Seiton
Sense may replace only the target ID of that one already incoming action. The
recommended `<e1>` line is a carrier that permits a redirect attempt without a
selected target; it does not choose S1. Carrier identity requires the concrete
incoming target to match the exact canonical E1 identity while differing from
your current hard target; it does not inspect macro text. If the action is not
redirected, Seiton substitutes an invalid
target only for that carrier attempt so a following `<t>` line remains the
ordinary fallback. The compact `<t>` form otherwise preserves its incoming
target.

The token is consumed before the original game call. The plugin does not
persist ally/enemy identity, visibly change a hard/soft/focus target, initiate
an action, invent a macro press, accept generic queued-action mode, try another
opponent, or retry. Turbo Hotbar can repeat the macro authored by the user, but
Seiton Sense adds no repeat of its own.

## One-shot Near Help

Near Help is disabled by default and runs only in Crystalline Conflict. It
shares Near Assist's bounded action boundary but uses a separate, mutually
exclusive token. When `/nearhelp` or `/sshelp` is armed, the plugin transiently
reads exact party-slot identity, current HP, position, and the next supported
friendly PvP action. It filters party members through that action's native
range and line-of-sight result, then selects the lowest HP percentage with
distance and stable party/actor identity as deterministic tie-breakers.

The recommended `<2>` line is only a concrete friendly carrier. If no eligible
party member is found, Seiton Sense substitutes an invalid target for that
exact carrier attempt so the following authored `<t>` line can run normally.
A compact `<t>` form otherwise preserves its incoming target. The token is
consumed before the one original game call. Near Help does not initiate an
action, visibly change a target, try a second party member, or retry.

## One-shot Far Help

Far Help is disabled by default and runs only in Crystalline Conflict. It
shares the same bounded, mutually exclusive action boundary as Near Assist and
Near Help. When `/farhelp` or `/ssfar` is armed, the plugin transiently reads
exact party-slot identity, job, position, and the next supported friendly PvP
movement action. Only Guardian `29066`, Thunderclap `29484`, Aetherial
Manipulation `29660`, Icarus `29261`, and Slither `39184` are accepted.

Candidates must be live, targetable, non-self exact party members and pass the
actual action's native range and line-of-sight result. At action time, all five
native `<e1>`-`<e5>` slots must resolve to exact, unique, valid opponent
identities. Confirmed dead opponents are ignored for clearance; every live
opponent counts even while temporarily untargetable. Each candidate must have
strictly more than 10 yalms of horizontal hitbox-edge clearance from every live
opponent to enter the preferred backline group. Missing, ambiguous, invalid, or
no-live-enemy observations make that preference unavailable instead of being
treated as proof that a destination is clear.

If any candidates pass this conservative, map-agnostic backline heuristic, the
farthest of those actors from the local player wins. If none pass or the enemy
snapshot cannot certify them, the farthest otherwise valid reachable ally wins
instead. Only at exactly equal measured distance does role break the tie, in
healer, ranged/caster, then other-job order. Native party order and stable actor
identity resolve any remaining tie. Guardian also uses a strict under-10-yalm
local-player limit. This is a preference and does not guarantee tactical safety.
Only having no valid reachable ally produces no movement.

The recommended macro has exactly three lines: `/mlock`, `/farhelp`, and one
supported mobility action using `<me>`. There is deliberately no selected-target
fallback line. All five reviewed actions cannot target self, so `<me>` remains
intrinsically invalid when the hook is unavailable, no token was armed, or no
candidate is valid. In each case no movement occurs. Far Help never substitutes
your current target, self, or another fallback actor. The token is consumed
before the one original game call.

For migration from the former macro shape, the bounded action observer
suppresses matching calls of the same movement action for the remainder of its
750-ms quarantine, including the former `<t>` line and Turbo duplicates. That
legacy line is not part of the recommended macro and should be removed. Far
Help does not initiate, repeat, queue, or retry an action; change
its ID; or visibly change a hard, soft, or focus target. No observed
party/action data is persisted or uploaded.

## Experimental Purify helper

If the experimental helper is explicitly enabled, the plugin reads current
local key-down states in a supported PvP context. This baseline distinguishes
physical press/hold generations when an individually enabled Stun, Heavy, Bind,
Silence, Deep Freeze, or Miracle of Nature status appears. The separate
held-key option is off by default.

The plugin does not log or persist key text/history, swallow or replay the
original key, change targets, or transmit input. One physical generation can
request at most one normal native Purify attempt; it is consumed before
dispatch and is not retried after rejection. ReAction Turbo's logical repeats
do not create new physical generations. Other plugins can still alter the
downstream call if configured to rewrite Purify or its target.

## Experimental Ally Rescue helper

If explicitly enabled in Crystalline Conflict, the plugin reads exact native
party-slot identity, life/targetable state, HP/MP, position, and the four exact
trigger statuses Stun `1343`, Silence `1347`, Deep Freeze `3219`, and Miracle of
Nature `3085`. Heavy and Bind are not triggers. Initial zero MP remains unknown
until a plausible nonzero sample made that actor's MP trustworthy.

On BRD, only The Warden's Paean action `29400` is allowed; on WHM, only
Aquaveil action `29227` is allowed. Current English action metadata is validated
independently for each job, while runtime selection uses numeric identities and
therefore does not depend on the client's display language. The exact ally must
still pass the action's native range and line-of-sight check immediately before
the attempt.

The candidate and dispatch checks do not depend on an internal status-slot
address or an early local cooldown-ready sample. The exact party identity,
live/targetable state, one of the four trigger statuses, current action identity,
and native range/line-of-sight result are still revalidated. FFXIV receives one
normal native request and remains the authority on whether it queues or
executes.

Self-Purify and Ally Rescue share one physical input-generation observer, with
self-Purify receiving first claim. Ally Rescue consumes its state and that
generation before at most one exact native action attempt. A false return,
exception, vanished status, or changed target is not retried. The original key
is still neither swallowed nor replayed, and no observed ally/status/input data
is logged, persisted, or transmitted.

The local return from the action request is counted separately as
`client-accepted`; it is not proof of a cleanse. A confirmed removal requires a
matching ActionEffect from the local caster, the exact Paean/Aquaveil action,
the exact attempted party target, effect type `0x10`
(`RecoveredFromStatusEffect`), and one of Stun `1343`, Heavy `1344`, Bind `1345`,
Silence `1347`, Miracle of Nature `3085`, or Deep Freeze `3219` within the
bounded correlation window. Heavy and Bind can confirm what the action actually
removed but remain excluded from activation.

An exact confirmation can show a short blue `CLEANSED` popup and increment
aggregate current-match and plugin-session counts, including per-action and
per-status totals. Attempts and client-accepted requests remain visibly
separate. These counters, the bounded pending correlation, and duplicate-event
keys exist only in memory; the settings reset can clear the displayed
statistics. No ActionEffect payload, actor identity, counter, or popup history
is written to disk, logged as combat history, sent over the network, or
uploaded.

## Experimental WHM Miracle intercept

If explicitly enabled in Crystalline Conflict, the plugin extends its existing
bounded local action-effect observer to recognize only the reviewed early event
shapes for Marksman's Spite `29415`, Zantetsuken `29537`, and VPR Furious
Backlash / Nest der Blutschuppen `39188`. It reads the source and target network
identity, action identity, bounded event sequence/time, and the small fixed
effect-slot shape needed to reject later hit packets. The queue is bounded and
exists only in memory. MCH and SAM opportunities expire after 500 ms; the VPR
opportunity expires after 250 ms.

On the framework thread, the source must resolve to the exact canonical CC
opponent with the expected job. The helper transiently reads that actor's
life/targetable state and verified full-CC-protection statuses, including VPR
Hardened Scales `4096`. For the VPR trigger it waits for `4096` to be actually
absent rather than predicting status expiry. The exact target must also pass
Miracle of Nature's native 10-yalm range and line-of-sight result.

The helper shares the existing physical key-generation observer and receives
priority only after self-Purify and Ally Rescue. If all gates remain valid, it
consumes its state and that input generation before at most one normal native
Miracle of Nature `29228` request to the exact enemy. It never changes the
visible target, chooses an alternate enemy, falls back to another action, or
retries a rejected/failed request. A client-accepted request is not recorded as
proof that the enemy startup was interrupted.

The existing action-effect hook also places exact local Miracle status-add
observations into a separate bounded in-memory queue. A 1.5-second visual
confirmation is created only when local caster, action `29228`, pending threat
target, effect type `0x0E`, status `3085`, and a non-empty event sequence match
within 1500 ms of the one helper attempt. This is labelled `MIRACLE LANDED` and
is not stored as proof that the hostile action's damage was cancelled. Bounded
capture/drop and confirmed-landing counters remain memory-only diagnostics.
The first still-unexpired pending helper attempt is preserved; a later attempt
registration does not replace that correlation before it expires.

No observed threat, actor identity, key state, status, or action result is
written to disk, uploaded, or retained as combat history. Aggregate bounded
diagnostic counters, if displayed, remain memory-only.

## Experimental Monk Earth's Reply helper

If explicitly enabled on PvP Monk, the helper transiently reads the local job,
exact local actor identity, HP, one exact Earth Resonance status `3171` and its
remaining time, and the adjusted result of Riddle of Earth action `29482`.
Current English action/status/proc metadata must independently validate before
the helper can act. It runs in Crystalline Conflict and in explicitly enabled
Wolves' Den test mode; other PvP contexts fail closed.

At the configured low-HP or expiry threshold, and only after a same-frame
self-Purify opportunity declines priority, the continuous resonance state is
marked spent before at most one normal self-targeted Earth's Reply `29483`
request. The helper never activates Riddle of Earth `29482`, substitutes an
alternate action or target, changes a visible target, queues a custom retry, or
tries again after a false return or exception. The local request return is only
diagnostic and does not prove that the server executed or accepted the effect.

The resonance state, attempt/accepted counters, local actor identity, HP/timer,
and action result remain in memory only. Nothing from this helper is logged as
combat history, written to disk, transmitted, or uploaded.

## Saved settings

Only local configuration is saved through Dalamud. This includes display and
layout options, pressure window/appearance and context toggles, warning opacity,
MCH warning size/sound selection, the shared Near Assist/Near Help/Far Help opt-in,
Near Assist search/preferences, target-highlight settings, the Purify
opt-in/held-key/per-debuff controls, the Ally Rescue master/held-key opt-ins,
the WHM Miracle master/per-trigger opt-ins, resource-aura surfaces/thresholds/
appearance, the Monk Earth's Reply master/triggers/thresholds, and the
CC-immunity-brake master plus exact per-job/per-action selections. Configuration
schema 15 remains unchanged in v0.11.0.1 and does
not save observed actors, targets, combat events,
status timers, key state, Ally Rescue confirmation state, or its counters.

The integrated focus preset does not read, import, modify, or delete standalone
Super Focus Glow configuration. Likewise, Seiton Sense does not modify the
standalone HOWMANY, CCImmunityWatch, or NearAssist configuration.

Like all third-party FFXIV modifications, use is at your own risk. Seiton Sense
is distributed through a custom repository, not Dalamud's official plugin
repository.
