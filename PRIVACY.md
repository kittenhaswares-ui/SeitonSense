# Privacy

Seiton Sense is local-only. It does not create accounts, contact a server,
upload gameplay data, or include telemetry. It does not read character names or
Home Worlds, and it does not persist combat, target, status, or key history.
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
- when its optional module is enabled, your manually selected current/focus
  target and locally available job, HP, distance, CC slot, and pressure state.

Actor observations are joined using exact game-object and network entity
identity. Ambiguous or stale identity is discarded. Nameplate rectangles and
protection timers are held only in bounded in-memory state needed to smooth
short client/UI sampling gaps.

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

No observed threat, actor identity, key state, status, or action result is
written to disk, uploaded, or retained as combat history. Aggregate bounded
diagnostic counters, if displayed, remain memory-only.

## Saved settings

Only local configuration is saved through Dalamud. This includes display and
layout options, pressure window/appearance and context toggles, warning opacity,
MCH warning size/sound selection, the shared Near Assist/Near Help opt-in,
Near Assist search/preferences, target-highlight settings, the Purify
opt-in/held-key/per-debuff controls, the Ally Rescue master/held-key opt-ins,
and the WHM Miracle master/per-trigger opt-ins. Configuration schema 13 does
not save observed actors, targets, combat events,
status timers, key state, Ally Rescue confirmation state, or its counters.

The integrated focus preset does not read, import, modify, or delete standalone
Super Focus Glow configuration. Likewise, Seiton Sense does not modify the
standalone HOWMANY, CCImmunityWatch, or NearAssist configuration.

Like all third-party FFXIV modifications, use is at your own risk. Seiton Sense
is distributed through a custom repository, not Dalamud's official plugin
repository.
