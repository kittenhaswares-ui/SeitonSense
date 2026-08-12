# Privacy

Seiton Sense is local-only. It does not create accounts, contact a server,
upload gameplay data, or include telemetry. It does not read character names or
Home Worlds, and it does not persist combat, target, status, or key history.

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
and time. It does not read, display, or store damage amounts.

Recent harmful-action evidence remains only in memory for the configured
0.5-8 second window (3 seconds by default) and is then discarded. Queues are
bounded, dropped-event counts are aggregate diagnostics, and no combat history,
actor name, or event payload is logged or uploaded. Pet/owned action sources can
be resolved to their visible player owner solely for the current pressure cue.

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
immediately following hostile macro action. It also verifies the running macro
line and its explicit `<t>`, `<target>`, `<me>`, or `<self>` placeholder.

These values validate one token lasting at most 750 ms. On success, Seiton
Sense may replace only the target ID of that one already incoming action. The
`<me>`/`<self>` form is a carrier that permits a redirect attempt without a
selected target. If it is not redirected, Seiton substitutes an invalid target
only for that carrier attempt so a following `<t>` line can remain the macro's
ordinary fallback; this includes the no-candidate case and prevents a
self-targetable hostile skill from consuming the carrier on you. Other failed
checks forward the incoming action without a Seiton-selected alternate target.

The token is consumed before the original game call. The plugin does not
persist ally/enemy identity, visibly change a hard/soft/focus target, initiate
an action, invent a macro press, accept generic queued-action mode, try another
opponent, or retry. Turbo Hotbar can repeat the macro authored by the user, but
Seiton Sense adds no repeat of its own.

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

## Saved settings

Only local configuration is saved through Dalamud. This includes display and
layout options, pressure window/appearance and context toggles, warning opacity,
MCH warning size/sound selection, the Near Assist opt-in/search/preferences,
target-highlight settings, and the Purify opt-in/held-key/per-debuff controls.
Configuration schema 10 does not save observed actors, targets, combat events,
status timers, or key state.

The integrated focus preset does not read, import, modify, or delete standalone
Super Focus Glow configuration. Likewise, Seiton Sense does not modify the
standalone HOWMANY, CCImmunityWatch, or NearAssist configuration.

Like all third-party FFXIV modifications, use is at your own risk. Seiton Sense
is distributed through a custom repository, not Dalamud's official plugin
repository.
