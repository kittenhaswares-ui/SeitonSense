# Privacy

Seiton Sense has no account, independent server, telemetry, or external gameplay
upload. Optional gameplay helpers can submit ordinary action, target-sign, or
Quick Chat commands to FFXIV, and the separate default-off Auto Low-MP Focus
helper can set only an empty local native Focus Target. In particular, the separate default-off Guardian
communication option can send one standardized Crystalline Conflict Quick Chat
and party-visible marker commands through the normal FFXIV service after an
automatic Guardian request is client-accepted. It embeds no character name or
free text. The plugin does not otherwise persist or transmit character names,
Home Worlds, combat, target, status, or key history. Ally Rescue attempt,
client-accepted, and confirmed-cleanse counters exist only in memory for the
current match/plugin session and are never uploaded.

## Transient local data

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
- when its optional visual module is enabled, your current hard/Focus Target and
  locally available job, HP, distance, CC slot, and pressure state;
- when Auto Low-MP Focus is enabled, the native Focus Target's empty/occupied
  state, the complete exact canonical `S1`-`S5` set, trusted HP/MP samples and
  low-MP latches, local identity/text-input state, and FFXIV's native 20-yalm
  range/line-of-sight result for the frozen candidate;
- for the isolation/defensive helpers, exact local party membership and FFXIV's
  native action range/line-of-sight result for the relevant ally;
- for the optional high-pressure warning/sound/Sprint features, the distinct
  exact current enemies whose hard or cast target is the local player, the
  bounded warning-episode token, held gameplay-key generation, own Guard/Sprint
  state, and exact Sprint action metadata/readiness;
- when the team-visible focus-sign module is enabled, the current native Attack1
  marker target and marker timestamp needed to avoid overwriting or clearing a
  sign the plugin cannot prove it owns;
- when Guardian communication is enabled, the client language and localized
  Quick Chat row-35 metadata, the bounded client-accepted Guardian episode and
  exact party slot, and current Bind2/Bind1 targets and marker timestamps needed
  to skip occupied/uncertain signs and prove cleanup ownership;
- when the Scholar held-key helper is enabled, the exact local job and held-key
  generation, Critical Strategy `29716` readiness/metadata, the complete unique
  canonical `S1`-`S5` actor set, live Guard `3054`/`3673`, exact HP and trusted
  team-pressure observations, and FFXIV's native 25-yalm range/line-of-sight
  result for the frozen candidate.
- when the DRK Shadowbringer macro is enabled, the exact macro line/cycle token,
  local DRK and current canonical CC target identity or exact native Wolves'
  Den striking-dummy hard-target identity, native combo/Shadowbringer recast and
  queue state, action sequence, animation lock/cast state, HP/Dark Arts and
  Guard states, and both actions' native range/line-of-sight/readiness results.

Actor observations are joined using exact game-object and network entity
identity. Ambiguous or stale identity is discarded. Nameplate rectangles and
protection timers are held only in bounded in-memory state needed to smooth
short client/UI sampling gaps.

## Isolation warning

The local isolation warning is enabled only in exact Crystalline Conflict. It
requires one complete five-player party containing the local player and four
unique allies. For each living ally, the plugin transiently reads exact identity,
life state, and FFXIV's native 20-yalm range/line-of-sight result. Dead allies do
not count as connected. The warning uses only a 500-ms entry debounce and 200-ms
clear debounce in memory; incomplete party data, an unsupported native result,
or any other unknown state suppresses it.

The warning draws one local top-left overlay. It does not issue an action,
target change, marker, navigation instruction, map paint, or network message.
No Splatoon integration or position guide is included.

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

When optional Ally Rescue, Near Help pressure preference, or defensive utilities
are enabled, the same current-frame enemy hard/cast identities are also reduced
to a unique incoming-pressure count for the local player and/or exact party
members. This drives only the documented pressure thresholds and candidate
ordering. The data is bounded to the live snapshot and is not retained as
combat history.

## High-pressure warning, sound, and held Sprint

The high-pressure feature uses only a narrower live subset of the existing
pressure snapshot: distinct exact enemies whose current hard target or cast
target is the local player. At least three are required. Recent damage/action
history cannot start or sustain the warning, sound, or Sprint eligibility, and
unknown or inactive pressure data fails closed.

The visual option draws one fixed local top-center warning. The separate
isolation warning keeps its own top-left position when both are visible, except
that a narrow work area stacks it below the pressure card if their actual scaled
rectangles would overlap. The optional alert calls one
selected built-in FFXIV UI/system sound once when a new high-pressure episode
begins. No audio is downloaded, recorded, persisted, transmitted, or uploaded.
The in-memory episode token is cleared with context/player/feature lifetime and
exists only to prevent the same episode from sounding every frame.
Unknown/stale pressure hides the visual state immediately but does not rearm a
second sound; rearming requires a continuously known below-threshold separation.

The separate default-off held-key option can submit at most one exact ordinary
self Sprint action attempt for one physical WASD/arrow movement-key generation
while the same direct-enemy count remains at least three. The original movement
key is not swallowed. It reads current local identity,
life state, own Guard/Sprint status, exact Sprint metadata/readiness, and the
shared input-generation claim. The generation is consumed before the final
native request. It never changes a selected target, chooses another action,
stores or replays the key, or retries after drift, rejection, or exception.
The native request result is diagnostic only and does not prove that Sprint was
accepted or applied by the server.

Across action-request helpers, one physical generation is offered in this exact
order: Self-Purify, defensive utilities, pressure Sprint, Ally Rescue, reactive
counter-CC, Ninja Seiton, Scholar Critical Strategy, then Monk Earth's Reply.
Once an earlier helper claims it, no later helper can reuse that generation.

## Optional team-visible Attack1 focus sign

This module is disabled by default and runs only in exact Crystalline Conflict.
It transiently reads canonical `<e1>`-`<e5>` identity, life/targetable state,
current/maximum HP and MP, the locally observed Guard-unavailable state, exact
team-target count, and the native Attack1 marker target/time. MP is eligible only
after the existing trusted low-MP signal has observed 150 ms continuously below
2,000 MP; it clears after 150 ms continuously at or above 2,300 MP. Unknown MP
never qualifies. An enemy must also be at or below 50% HP and/or have that trusted
low-MP state while Guard is known unavailable.

Eligible enemies rank as both resources low, then HP-only, then MP-only; ties use
lowest exact HP ratio, lowest trusted MP ratio, highest known team-target count, and
stable enemy slot. The module can issue only the hardcoded normal
`/mk attack1 <eN>` or matching owned-clear command. It does not read character
names for the command, write marker memory directly, or change a hard, soft, or
focus target.

An already occupied Attack1 is never overwritten. Ownership is accepted only
after the plugin observed an empty marker, sent its one command, then observed
the exact intended actor with a changed marker timestamp. Clear is allowed only
while the same canonical slot, game-object/network identity, target, and marker
timestamp still match. Uncertain state relinquishes ownership without clearing.
The command attempt and bounded ownership state are not persisted or uploaded;
current-patch party-visible command behavior remains a live-validation boundary.

## Optional Auto Low-MP Focus Target

This separate local setter is disabled by default and runs only in exact
Crystalline Conflict. It requires one complete, unique canonical `S1`-`S5` set.
For every enemy it transiently samples exact game-object/network identity,
life/targetable state, HP/MP, and native reachability. MP must remain trusted at
2,000 or lower for 150 ms to enter a low-MP wave; the wave clears only after
150 ms continuously at 2,300 MP or higher. Unknown MP never qualifies. A
candidate must also pass FFXIV's native 20-yalm action range and line-of-sight
probe. Lowest exact MP ratio wins, then lowest HP ratio, stable S-slot, entity
ID, and game-object ID.

The helper reads the native Focus Target state and may invoke exactly one
reviewed setter only after Focus was observed stably empty and the frozen
candidate passed a final exact preflight. It never clears, replaces, restores,
or retries a Focus Target. An already occupied Focus spends that low-MP wave
without mutation. After an exact plugin-set readback, any confirmed manual or
external change or clear latches manual ownership until the option is toggled
off/on or a new exact match lifetime begins. The local native Focus feeds
FFXIV's Focus Target HUD and `<f>`; it is not a team-visible Attack1 sign and
does not change the hard or soft target.

Dalamud exposes no atomic Focus Target compare-and-set. The sole same-thread
setter therefore has an immediately adjacent final empty read followed by an
exact readback, but a live client race remains possible. Setter invocation and
readback counts, bounded low-MP state, exact actor identities, and last decision
remain in memory only and are cleared with feature/context/player lifetime.
Nothing is persisted or uploaded. The current-patch setter, HUD/`<f>` result,
and native range probe remain live A/B boundaries.

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
contains `3248`, `1320`, VPR-only `4096`, `3143`, `3052`, and `3162`. Its reviewed action
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

For an unchanged native selected-target carrier of either `0` or
`0xE0000000`, the brake can transiently read the local player's native selected
hard-target ID. It does so only when the original and final forwarded carriers
are identical and the redirect path did not mark the target as deliberately
suppressed. It requires the native selection to stay stable across the check
and then requires one exact canonical `<e1>`-`<e5>` identity. It does not
rewrite the forwarded call. A zero deliberately produced by Seiton's
fail-closed redirect path carries explicit suppression provenance and is never
reinterpreted as the selected target. Explicit actor IDs remain authoritative.

If FFXIV omits an opponent's `Hostile` flag, the brake permits a narrower
public-match fallback only in a known public Crystalline Conflict territory.
It transiently verifies that the local party contains exactly five valid
entities, includes the local player, and is wholly present in the visible player
object table. The candidate must still be one exact native `<e1>`-`<e5>` actor,
must not be self or any party/alliance member, and must retain valid native
identity, life, HP, and targetable state. Incomplete or ambiguous evidence does
not authorize a block.

Plugin-owned exact-target Miracle calls bypass only Seiton's macro target
redirection. They still pass through this final brake before the one downstream
native request. If a verified blocker appears after the helper's pre-check, the
brake can reject that single incoming request without storing it, changing its
target, scheduling delayed work, replaying it, or retrying it.

This is a client-side pre-dispatch check, not a server rollback. Near a
simultaneous activation, an action already accepted by the server roughly
295-355 ms before immunity became locally visible cannot be recalled. FFXIV
may still present its animation and damage while the server rejects the status
effect on the protected target. No additional history is retained to try to
undo or compensate for that result.

Unsupported actions, jobs, contexts, missing or ambiguous actor identity, and
unverified protection pass through unchanged. Broad cone, ground-targeted,
self-centered, and ambiguous multi-target actions are excluded. On a
pass-through call, another plugin downstream can still alter it; a confirmed
block does not invoke the downstream/original function. Aggregate resolution
counts and the latest original, forwarded, and effective target IDs, invocation
mode, and resolution result are retained only in memory for diagnostics.
Incoming identities and protection state are not persisted or transmitted.

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
reads exact party-slot identity, current HP, position, current incoming-pressure
counts, and the next supported friendly PvP action. It filters candidates
through that action's native target, range, and line-of-sight result. The local
player is eligible only when that exact resolved action explicitly supports
self-targeting and passes the same action-specific validation.

Lowest exact HP is the anchor. At or below 25% HP it always wins. Above that
boundary, the enabled pressure preference requires a trusted live pressure view
and complete non-negative counts for every eligible candidate no more than
10 HP percentage points above the anchor. Only that bounded group can be ranked
by highest unique incoming enemy count, then lower exact HP, distance, and
stable party/actor identity. Missing data inside that bounded group or zero-only
pressure falls back to the original exact lowest-HP ordering; unknown data
outside it is irrelevant. Observed counts and the selection decision remain
transient and are not persisted.

The recommended `<2>` line is only a concrete friendly carrier. If no eligible
party member is found, Seiton Sense substitutes an invalid target for that
exact carrier attempt so the following authored `<t>` line can run normally.
A compact `<t>` form otherwise preserves its incoming target. The token is
consumed before the one original game call. Near Help does not initiate an
action, visibly change a target, try a second candidate, or retry.

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
identity resolve any remaining tie. Guardian uses FFXIV's native 20-yalm
action-range and line-of-sight result without a custom center-distance cap. Its
10-yalm condition governs staying close enough for protection after the jump.
This is a preference and does not guarantee tactical safety.
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

## Experimental DRK Shadowbringer macro helper

This helper is disabled by default and runs only for exact PvP Dark Knight in
Crystalline Conflict or explicitly enabled Wolves' Den testing. The Den path
requires both the existing DRK helper and Wolves' Den test options; no new
setting was added. `/seitonbringer` may arm only the immediately following
authored Souleater Combo `<t>` macro line for at most 750 ms. The macro name,
line cursor, exact local identity/context, proven GCD-cycle token, and incoming
action/route/mode are kept only long enough to pair those two adjacent lines.
The recommended ReAction setup uses both Macro Queue and Turbo; Seiton Sense
does not create a macro pulse.

In Crystalline Conflict, the target must remain one exact current canonical
`S1`-`S5` actor. In Wolves' Den, the plugin instead reads the local player's
native hard-target ID and the matching object-table battle character. It accepts
only the live, targetable combat striking dummy with NameId `541`, then freezes
and revalidates its game-object ID, entity ID, address, object/sub-kind, NameId,
and hard-target ownership. It does not query the synthetic `S1`/`<e1>` or native
duel-opponent paths for this macro and cannot accept a player, another object,
or an alternate target. Frontline and Rival Wings remain excluded.

A cycle is proven only from the exact 2.40-second combo recast group restarting
with a changed native action sequence. At most one Shadowbringer attempt may be
claimed for that cycle, and only in the inclusive 0.60-0.80-seconds-remaining
window. A missed window is skipped and 0.50 seconds or less never triggers
Shadowbringer. The paired outer Souleater Combo call continues unchanged so a
later authored Turbo pulse can enter FFXIV's normal queue window.

Before and after spending the cycle's one-attempt token, the helper revalidates
exact context/local/target identity, the Souleater Combo route, unchanged GCD
token and action sequence, an empty stable native queue, clear cast and
animation lock, clear own Guard/propagation and target Guard, native 5-yalm
combo and 10-yalm Shadowbringer range/line of sight, and exact action readiness
and resources. Base Shadowbringer requires strictly more than 12,000 HP; its
adjusted Dark Arts action requires the exact Dark Arts status/action state.

The plugin may submit one normal exact-target Shadowbringer request before the
unchanged outer combo call. It never changes a hard, soft, or Focus Target,
chooses another target/action, replays the macro, or retries after drift,
rejection, or exception. A local client-accepted return is bounded diagnostic
feedback only and does not prove server execution or a clip-free weave. Macro
Queue/Turbo mode, native queue and recast-group timing, action effect, and
clipping remain current-patch live-trace boundaries. All paired identities,
cycle/queue samples, counters, and last-result diagnostics remain memory-only
and are neither persisted nor uploaded. A Wolves' Den dummy result proves only
that test path and is not proof of current-patch CC execution or timing.

Current English game-data validation independently pins the striking-dummy
NameId and the exact per-row combo secondary cost types
`0/58/58/147/147/147`. A dummy metadata mismatch disables only the Den path;
other DRK metadata mismatch fails the whole helper closed. Native GCD sampling
starts on the framework update thread rather than performing a local-player
lookup during synchronous plugin startup.

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

Self-Purify, defensive utilities, and pressure Sprint receive the shared
physical input generation before Ally Rescue. Ally Rescue consumes its state
and that generation before at most one exact native action attempt. A false return,
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

## Smart Bard Paean pressure redirect

This separate option is disabled by default and exact-Crystalline-Conflict-only.
On PvP Bard it transiently examines an already incoming The Warden's Paean
ability call `29400`, its original target ID, exact local job/actor/context, and
the current exact party view. For party candidates it reads exact native party-
slot identity, life/targetable state, HP, position/native action reachability,
and the current unique-enemy incoming-pressure count. It does not read a generic
physical gameplay-key generation and never creates an action call by itself.

Selection requires a complete, unique, stable party view. Only an exact living,
targetable, non-self party member without the live Warden's Paean ward `3143`,
in native 30-yalm range and line of sight, and with a trusted count of at least
three unique live enemies currently hard-targeting or casting at that ally is
eligible. Unknown pressure excludes only that actor. Known candidates rank by
higher pressure, lower exact HP ratio, party slot, entity ID, and game-object
ID. With no complete exact view or no known `3+` candidate, the original target
and incoming call are forwarded unchanged.

After one destination is frozen, the exact party slot and actor are revalidated.
Final identity, local job, exact resolved action/metadata, life/targetable state,
HP, live ward, native reachability, or pressure drift suppresses that one call
instead of forwarding the original target, choosing another ally, replaying, or
retrying. There is deliberately no cooldown/readiness gate on this passive
transform. The option never changes any selected target or substitutes an
action. Each later manual or Turbo call is a separate incoming call
and is evaluated independently. A client-accepted return is not stored or
presented as proof that Paean applied, removed, or nullified crowd control.

The transient party, pressure, action, target, selection, and result state is not
persisted, transmitted, or uploaded. Any current diagnostic counters or last-
decision text remain in memory and appear only when the user explicitly requests
the existing local debug output. Existing Ally Rescue behavior and its in-memory
confirmation path, including Aquaveil, remain unchanged.

## Experimental defensive utilities

This module is disabled by default and exact-Crystalline-Conflict-only. It
transiently reads the local player's exact identity, job, HP, Stun and Resilience
status membership, own Guard state/cooldown, current unique incoming-enemy count,
and current physical gameplay-key generation. PLD Guardian selection additionally
reads exact party identity, life/targetable state, HP, position, incoming pressure,
and native Guardian range/line-of-sight result.

At known pressure from at least three unique enemies, an exact Stun can permit
one normal Purify `29056` request. The same physical generation can never also
request Guard `29054`. A later Guard request requires a genuinely new release/
repress generation, positive live Resilience `3248`, no remaining
Purify-removable CC, and the bounded post-Purify opportunity. The optional
pre-Guard rule requires HP at or below 50%, the same known pressure threshold,
no removable CC, and Guard available.

The PLD-only rule requires an exact living, targetable, non-self party member at
or below 20% HP, FFXIV's native 20-yalm Guardian range/line-of-sight acceptance,
and both own Guard and Guardian `29066` available. No custom center-distance cap
is applied; the 10-yalm condition governs staying close enough for protection
after the jump. Candidate ranking uses exact HP ratio, known incoming pressure,
distance, and stable party identity. The helper
consumes its selected state and physical generation before at most one normal
native action request. It does not change the visible target, select an alternate
action after failure, replay input, or retry.

An accepted automatic Guardian request creates a local in-memory notification
containing only the selected party slot and its start/end timestamps. The
1.5-second `GUARDIAN TRIGGERED` card says `CLIENT ACCEPTED`; it is cleared on
reset, configuration/context loss, or expiry and is not stored or transmitted.
It does not claim server-confirmed protection or damage interception.

The separate Guardian communication setting is persisted but disabled by
default. Only a new client-accepted automatic Guardian episode in exact
Crystalline Conflict can make it consume a bounded communication opportunity.
The helper revalidates the same frozen exact party slot before it may issue the
client-localized CC Quick Chat row 35 (`Ziel decken`, displayed as `Ich decke
...` on a German client) for that party placeholder. This standardized message
is sent through FFXIV's normal Quick Chat channel and is visible to the party;
there is no plugin service or additional upload.

The same opportunity may place Bind2 on the same exact party member followed by
Bind1 on the exact local Paladin. If either sign is occupied or marker state is
unknown, the marker sequence is not started. Bind2 must be observed on the exact
ally with a new marker timestamp before Bind1 is attempted. If Bind1 then fails,
only the proven-owned Bind2 may be cleaned. A complete pair expires nine seconds
after Guardian acceptance; cleanup tries Bind2 and then Bind1, each only while
the same actor, sign, and marker timestamp remain exact. Drift is relinquished
rather than cleared, and cleanup success cannot be guaranteed. The native
unused-marker values `0` and `0xE0000000` are recognized only while the marker
slot, availability, and timestamp telemetry are otherwise exact; these values
are transient and are neither persisted nor transmitted. Communication does
not change a selected target, issue another combat action, select an
alternate, fall back, queue, replay, or retry. A command attempt and its bounded
ownership state are not persisted as history. Client-accepted Guardian does not
prove server-applied protection; an issued Quick Chat or marker command does not
prove delivery or display. Localized row-35 syntax, party display, Bind pairing,
and cleanup remain current-patch live-validation boundaries.

## Experimental Scholar Critical Strategy held-key helper

This separate persisted option is disabled by default and can run only for PvP
Scholar in exact Crystalline Conflict. It transiently reads the exact local job
and held physical gameplay-key generation, verified Critical Strategy `29716`
metadata/readiness, the complete unique canonical `S1`-`S5` actor set, enemy
life/targetable state, exact HP, live Guard `3054` or `3673`, exact team-pressure
observations, and FFXIV's native 25-yalm range/line-of-sight result.

Only a living, targetable exact canonical enemy with one of those live Guard
statuses is eligible. The helper never spends Critical Strategy as its ordinary
10% damage-taken debuff; against Guard, the current official action instead
halves Guard's defensive bonus for 10 seconds. If every eligible guarded
candidate has active, exact, non-negative team pressure and at least one count
is positive, candidates rank by team target count descending and then exact HP
ratio ascending. If any eligible candidate has inactive, unavailable, or
negative pressure, or if all counts are zero, the whole set ranks by exact HP
ratio ascending. Stable `S#`, network entity ID, and game-object ID resolve the
remaining ties. Pressure is used only for that frozen selection and is not a
final dispatch requirement.

One shared held-key generation can create at most one frozen intent. The intent
and generation are consumed before one normal native action request. The frozen
enemy is then revalidated only for exact identity, action readiness, live Guard,
and native range/line of sight. Pressure drift neither reranks, switches, nor
invalidates that frozen target. No drift can cause another selection, alternate
target/action, fallback, queue, replay, or retry, and the helper never changes a
hard, soft, focus, or mouseover target or swallows the original key. Bounded
state and diagnostics remain local in memory and are not saved as combat,
target, or key history or uploaded. A client-accepted request does not prove
that Critical Strategy landed or changed Guard; exact dispatch and effect
behavior remain current-patch live-validation boundaries.

When own Guard is active, every Seiton Sense action-request helper is suppressed.
The same in-memory suppression begins immediately after an exact local Guard
request and expires after 1.5 seconds unless the real Guard status takes over; the
same observed request cannot extend or rearm it. This prevents the plugin from
cancelling Guard; it does not intercept or prevent manual FFXIV actions or action
requests from another plugin. All observations, pending state, and aggregate
attempt diagnostics are bounded in memory and are not stored as combat/key
history or uploaded. Exact client/server action ordering remains a live-validation
boundary.

## Experimental WHM/BRD reactive counter-CC

If explicitly enabled in exact Crystalline Conflict, the plugin extends its
bounded local action-effect observer to recognize the reviewed early event
shapes for DNC Contradance `29432`, Marksman's Spite `29415`, Zantetsuken
`29537`, and VPR Furious Backlash / Nest der Blutschuppen `39188`. It reads
source/target network identity, action identity, bounded event sequence/time,
and the small fixed effect-slot shape needed to reject later hit packets. DNC is
available to WHM and BRD; MCH/SAM/VPR remain WHM-only. The bounded queues exist
only in memory.

The optional post-Purify path recognizes only exact enemy self-Purify `29056`
with one self target, a non-empty event sequence, and recovered-status effect
`0x10` for Stun `1343`, Heavy `1344`, Bind `1345`, Silence `1347`, Miracle of
Nature `3085`, or Deep Freeze `3219`. The source must resolve to one exact live
canonical `<e1>`-`<e5>` enemy. The plugin then requires positive live Resilience
`3248`, waits for 150 ms of stable real absence, and never predicts its timer.
Promotion additionally requires the enemy to be the local player's exact hard
target and at least one exact ally's hard target, for team focus of two or more.

At dispatch, the enemy identity, expected triggering job, life/targetable state,
and action-specific verified protection are revalidated. WHM uses only Miracle
of Nature `29228` with native 10-yalm range/line of sight. BRD uses only Silent
Nocturne `29395` with native 20-yalm range/line of sight. VPR requires live
Hardened Scales `4096` to be actually absent. The DNC opportunity expires after
750 ms, existing MCH/SAM opportunities after 500 ms, VPR after 250 ms, and the
post-Purify release opportunity after 500 ms; waiting never restarts a deadline.

The helper shares the physical-generation observer after Self-Purify, defensive
utilities, pressure Sprint, and Ally Rescue. A claimed generation cannot be
reused. The helper
consumes its state and generation before at most one normal native exact-target
request, without changing the visible target, choosing an alternate enemy/action,
replaying input, or retrying. Its internal redirect bypass excludes only macro
target rewriting; the final action-specific CC-immunity brake still runs at the
native dispatch boundary.

The action-effect hook also places exact local counter-status observations into
a separate bounded in-memory queue. A 1.5-second `AUTO CC LANDED` visual is
created only when local caster, expected action, pending enemy, effect type
`0x0E`, non-empty sequence, and action-specific status match within 1500 ms:
Miracle `3085` for WHM or Silence `1347` for BRD. This proves only that the
counter-CC status landed on the intended enemy, not that Contradance, another
limit break, or damage was interrupted. A client-accepted action request alone
is never presented as confirmation.

No observed threat, actor identity, key state, status, action result, or team
focus is written to disk, uploaded, or retained as combat history. Aggregate
bounded diagnostics remain memory-only across context exit; active threats and
queues are cleared. Current-patch startup, release, dispatch, and interruption
behavior remains a live-validation boundary.

## Experimental Ninja Seiton fresh-key helper

This helper is disabled by default and runs only for PvP Ninja in exact
Crystalline Conflict. When explicitly enabled, it transiently reads the local
player's exact identity and job, fresh physical gameplay-key down edges, own
Guard state, the current adjusted Seiton action and readiness state, and the
exact canonical `<e1>`-`<e5>` enemy actors. Eligible candidates must remain
living, targetable, hostile, strictly below 50% HP, and accepted by FFXIV's
native action range and line-of-sight check. Selection uses the lowest exact HP
ratio, then stable enemy slot and actor identity.

The only allowed actions are the metadata-verified base Seiton Tenchu `29515`
and its Unsealed follow-up `29516`. Self-Purify, defensive utilities, pressure
Sprint, Ally Rescue, and reactive counter-CC retain priority over the shared
physical input generation. Active own Guard and the bounded post-request Guard-propagation
state suppress the helper. The already-selected target is never changed, and
the helper never changes the visible hard, soft, or focus target.

After every gate passes, the intent and input generation are consumed before at
most one exact native action request. A changed identity, readiness, health, or
reachability result; a false return; or an exception is not followed by an
additional selection, alternate target, fallback action, replay, or retry. The
frozen S-slot and actor identity are resolved once more immediately before that
request and the same actor's current HP is re-read. A value at exactly 50% or
higher cancels the already-consumed attempt; this last client-side sample does
not prove what HP the server observes when processing the request. The original
gameplay key is neither swallowed nor replayed. The local request
return may be kept as a bounded aggregate `client-accepted` diagnostic, but it
is not proof that Seiton
landed, executed the target, or caused a kill. No target, key, attempt, or result
history is persisted, transmitted, or uploaded.

## Experimental Monk Earth's Reply helper

If explicitly enabled on PvP Monk, the helper transiently reads the local job,
exact local actor identity, HP, one exact Earth Resonance status `3171` and its
remaining time, and the adjusted result of Riddle of Earth action `29482`.
Current English action/status/proc metadata must independently validate before
the helper can act. It runs in Crystalline Conflict and in explicitly enabled
Wolves' Den test mode; other PvP contexts fail closed.

At the configured low-HP or expiry threshold, and only after Self-Purify,
defensive utilities, pressure Sprint, Ally Rescue, reactive counter-CC, Ninja
Seiton, and Scholar Critical Strategy decline the shared generation, the
continuous resonance state is marked spent before at most one normal
self-targeted Earth's Reply `29483`
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
MCH warning size/sound selection, the high-pressure warning/native-sound/Sprint
opt-ins and sound selection, the shared Near Assist/Near Help/Far Help opt-in,
Near Assist search/preferences, the Near Help incoming-pressure preference,
target-highlight settings, the separate Auto Low-MP Focus Target opt-in, the
Purify opt-in/held-key/per-debuff controls, the Ally Rescue master/held-key
opt-ins, the separate Bard Paean pressure-redirect
opt-in, isolation warning/scale, defensive master/held-key/per-rule opt-ins,
WHM/BRD reactive counter-CC master/held-key/per-trigger opt-ins, the team-visible Attack1
marker opt-in, the separate Guardian Quick Chat/Bind-pair opt-in, resource-aura
surfaces/thresholds/appearance, the Monk Earth's Reply master/triggers/
thresholds, the Ninja Seiton fresh-key opt-in, the Scholar Critical Strategy
held-key opt-in, the DRK Shadowbringer macro opt-in, and the CC-immunity-brake
master plus exact per-job/per-action selections. Configuration schema 24 is
current in v0.18.0.1. The hotfix adds no setting; both v0.18 opt-ins remain off
for fresh, upgraded, and reset configurations. Configuration does not save observed actors, targets, combat events, status timers, key
state, marker ownership, pending helper state, ActionEffect confirmation state,
or in-memory counters.

The integrated focus preset does not read, import, modify, or delete standalone
Super Focus Glow configuration. Likewise, Seiton Sense does not modify the
standalone HOWMANY, CCImmunityWatch, or NearAssist configuration.

Like all third-party FFXIV modifications, use is at your own risk. Seiton Sense
is distributed through a custom repository, not Dalamud's official plugin
repository.
