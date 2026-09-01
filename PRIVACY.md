# Privacy

Seiton Sense has no account, independent server, telemetry, or external gameplay
upload. Optional gameplay helpers can submit ordinary action, target-sign, or
Quick Chat commands to FFXIV, and the separate default-off Auto Low-MP Focus
helper can set only an empty local native Focus Target. The retired Combat
Frames runtime has no target-click or native-mouseover path. Default-off Smart
Tab may replace one forward world-target cycle nested inside FFXIV's native
targeting handler with one exact visible CC reviewed-DPS hard target. Its paired
hooks
leave OFF, reverse, and calls outside that handler on the native paths. The
separate default-off Smart Action macro helper can replace
only the target ID on one already incoming exact harmful PvP action after an
explicit `/smartaction`, `/ssaction`, or `/seitonfar` token; it does not change a visible hard,
soft, Focus, mouseover, camera, or facing target. For an action with a proven
adjusted or base cast time, an exact Smart Action-owned harmful PvP cast uses the
same reachable Smart Target ranking as an instant action and freezes one actor
for the native request. Near Assist and Near Help retain their separate authored-
target policy: a hidden or missing carrier is suppressed so the visible `<t>`
fallback remains vanilla, while an authored target that already equals the exact
current hard target passes through unchanged. The plugin does not call a face-
target or rotation function for this policy. For metadata-verified SAM Ogi and
instant Kaeshi: Namikiri, protection is candidate-local: the selected
actor must be directly safe and any Chiten actor intersecting the reviewed
8-yalm, 90-degree cone vetoes that candidate, while unrelated Guard, Cover, or
LB invulnerability elsewhere does not. Tendo remains direct-target. The separate default-off NIN Guard-
Shukuchi helper may set only the exact jumped-to enemy as the hard target, and
only after its one ground-targeted Shukuchi location request returns client-
accepted. In particular, the separate default-off Guardian
communication option can send one standardized Crystalline Conflict Quick Chat
and party-visible marker commands through the normal FFXIV service after an
automatic Guardian request is client-accepted. It embeds no character name or
free text. The optional ally LB notification cards may display current character
names read from the live client, but the plugin does not persist or transmit
those names, Home Worlds,
combat, target, status, or key history. Ally Rescue attempt,
client-accepted, and confirmed-cleanse counters exist only in memory for the
current match/plugin session and are never uploaded. To diagnose the explicitly
enabled reactive counter-CC and Ally Rescue helpers, bounded transition,
attempt, and confirmation records are written to the ordinary local Dalamud
plugin log. They contain numeric action/status/entity/sequence identifiers and
outcomes, never character names, chat text, damage values, or a continuous key
history, and are not uploaded by Seiton Sense.

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
- when Smart Tab is enabled and its scoped native forward world-target cycle is
  pressed, the unique canonical `S1`-`S5` candidates, current HP, Guard, fresh
  optional team pressure, trusted MP ratio, hitbox geometry, native
  range/line-of-sight results, the current hard-target anchor, one frozen actor,
  and the native setter readback;
- when Smart Action is enabled and one local macro token is live, the exact
  incoming non-ground-target PvP action, unique canonical `S1`-`S5` candidates,
  Chiten/Guard/Covered/Hallowed Ground/Undead Redemption, current HP, fresh
  optional team pressure, trusted MP ratio, actor position/hitbox geometry,
  native action range/line-of-sight result, and one frozen action/actor intent;
- when enemy LB nameplates are enabled, fresh exact canonical `S1`-`S5`
  identities, visible native nameplate anchors, reviewed LB activation evidence,
  and matching live status duration needed for a bounded icon/countdown or flash;
- when self or ally LB notifications are enabled, exact local/party identities,
  reviewed activation evidence, and optional current ally names needed for the
  local banner or at most three transient ally cards;
- when the ally LB damage feed is enabled, bounded ActionEffect caster, target,
  reviewed LB action, sequence, effect type, and directly decoded damage amount
  needed to attribute one event without inferring an HP delta;
- when local MP sounds are enabled, exact local identity, a trusted current and
  maximum MP sample, and two in-memory crossing/hysteresis latches for 4,000 and
  2,000 MP; only built-in local FFXIV sound IDs are invoked;
- when the Wolves' Den rotation panel is enabled, the Patch 7.5 post-match hook
  transiently copies only the public CC result byte, duration, progress values,
  and ten numeric participant rows. On the framework thread, exact public-CC
  context and territory, a nonzero local Content ID, ten unique nonzero Content
  IDs, known jobs, and exactly five members on each valid team must all agree.
  Character names and name fallback are never read by this path;
- when either public-CC medicine-kit cue is enabled, exact public PvP context
  and territory plus the native content time remaining; the beacon path also
  checks only Dalamud's bounded event-object and reaction-event-object slices
  at 10 Hz for localized object name, BaseId, ready-to-draw state, and finite
  position. A localized match may teach one territory/BaseId hint in memory for
  the current plugin session; no object identity or timer history is persisted;
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
- when the RDM fresh-Guard engage is enabled, the exact local RDM identity,
  current/maximum HP and MP, held-key generation, own Guard state, verified
  Corps-a-corps `29699` and Riposte `41488` readiness, exact fresh enemy Guard
  episode/remaining time, other reviewed protection, canonical `S1`-`S5`
  identity or exact Wolves' Den current target, native range/line-of-sight
  result, and the frozen actor plus one post-acceptance hard-target readback;
- when the Scholar Critical Strategy held-key helper is enabled, the exact local job and held-key
  generation, Critical Strategy `29716` readiness/metadata, the complete unique
  canonical `S1`-`S5` actor set, live Guard `3054`/`3673`, exact HP and trusted
  team-pressure observations, and FFXIV's native 25-yalm range/line-of-sight
  result for the frozen candidate;
- when the NIN Guard-Shukuchi held-key helper is enabled, the exact local NIN
  identity, continuous held-key generation, own Guard/propagation state,
  adjusted Shukuchi `29513` metadata/readiness and observed cooldown epoch,
  independently resolved canonical `S1`-`S5` enemy identities, life/targetable
  state, exact HP, finite positive Guard `3054`/`3673`, current positions, fresh
  optional team pressure, native 20-yalm reachability, the frozen actor, latest
  revalidated location, client action result, and exact hard-target readback;
- when Smart Recuperate is enabled, the exact local identity, life/targetable
  state, current/maximum HP and MP, optional legacy held-key generation, trigger
  mode, own Guard state,
  current exact CC or explicitly enabled Wolves' Den context, and exact PvP
  Recuperate `29711` metadata/readiness needed for one frozen self/context intent
  and its bounded native calls;
- when Emergency Teleport is enabled, the exact local job/identity, HP/MP, fresh
  direct-enemy focus count, held-key generation, supported PvP context, reviewed
  friendly movement action/readiness, exact non-self party identities and
  positions, complete current enemy positions, native target action status,
  range/line-of-sight result, destination safety counts/clearance, and one-shot
  danger-episode state;
- when the Viper Serpentiner-Geist helper is enabled, the directly observed
  adjusted carrier/follow-up action, its in-memory exposure generation/spent
  state, exact local identity, canonical `S1`-`S5` candidates, HP ratio, fresh
  optional team pressure, trusted Guard/MP evidence, positions, complete
  protection geometry, chosen/fallback actor, context, territory, own Guard,
  held-key generation, readiness, and native range/line-of-sight result needed
  for the shared Smart Action rank and exact frozen-target revalidation. No
  preceding-action or native-queue history is recorded. Wolves' Den
  additionally reads the exact current hard target only when it is either the
  native hostile duel opponent or the reviewed combat striking dummy with NameId
  `541`;
- when either separate cast-cancellation permission is enabled, the exact
  highest-priority frozen helper/action/target/key-or-keyless intent epoch, both
  current local cast signals and cast action ID, own Guard, queue, animation
  lock, context, identity, readiness, and one-request-per-cast latch needed to
  decide whether to request FFXIV's native cast cancellation. The automatic
  permission additionally reads the exact local job, adjusted raw action, and
  startup metadata-verification result needed to admit only the reviewed
  BRD/MCH basic-shot pair;
- when the DRK Hiebsprung helper is enabled, the exact local DRK identity, held-
  key ownership, action `29092` metadata/readiness and cooldown epoch, animation
  lock, own Bind/Guard state, complete canonical `S1`-`S5` identity/HP/Guard
  evidence, center distance, and native range/line-of-sight result needed for one
  frozen exact-target intent and its bounded native calls;
- when Smart Kardia is enabled, the exact local Sage identity, one accepted PvP
  Eukrasia call and its before/current charge or own-source status evidence, a
  short-lived trigger token, one fresh complete stable five-player party and
  pressure publication, life/targetable state, exact HP, local-source Kardion
  state, Kardia readiness and animation lock, and FFXIV's native 30-yalm
  range/line-of-sight result for a frozen non-self candidate;
- for one explicit `/panicshu` or enabled `/seitonbw` invocation, the exact local
  identity, job, position/facing, PvP territory/context, startup-verified action
  metadata, exact adjusted action, native recast/charge/resource readiness, and
  clean cast/queue/animation boundary; `/panicshu` additionally reads the exact
  19.5-yalm terrain collision point, while `/seitonbw` reads the normal gameplay
  camera's control/zoom mode, event-camera flag, and finite horizontal direction.
  `/seitonbw` may write only local character facing immediately before and during
  one reviewed self-dash boundary, including replacing a later same-thread local
  facing rewrite; it never writes camera or target state;
- for one explicit `/seitonenavant` DNC invocation, three fresh finite
  local-player world positions forming two consecutive, directionally consistent
  movement segments, plus the same exact local identity, job, PvP context, En
  Avant `29430` identity/readiness, and clean one-call boundary. A fresh current
  sample is folded in synchronously, and finite sub-threshold displacement may
  accumulate against the last meaningful anchor without reading an exact-frame
  processed MOVE/autorun flag. It does not read
  or write camera or target state; the samples and derived heading will remain
  bounded transient local state and are never persisted or uploaded;
- when held DRK Shadowbringer is enabled, the exact local DRK identity, held-key
  ownership, HP, Dark Arts, exact Blackblood status-row presence, incoming
  pressure, own Guard/cast/queue/animation state, native Shadowbringer readiness,
  and one frozen reachable enemy identity with HP and native range/line-of-sight
  evidence. In exact CC its own opt-in reuses the complete Smart Action ranking
  and protection snapshot without changing a visible target. It keeps only the
  local last automatic-boundary time needed for the 1.8-second cadence and the
  in-memory Blackblood gate state. Wolves' Den retains only the exact current
  `<t>` duel/dummy target and treats unavailable CC pressure telemetry as zero
  for that local test context;
- when held GNB Continuation is enabled, the exact local GNB identity, held-key
  ownership, current transformed carrier, own proc status, action readiness, and
  one frozen reachable enemy identity with HP and native range/line-of-sight
  evidence;
- when the held Monk combo is enabled, the exact local Monk identity, held-key
  ownership, current combo/action/status resources, cast/queue/animation state,
  and one frozen reachable enemy identity with HP, distance, native range, and
  line-of-sight evidence;
- when the optional Samurai helpers are enabled, exact local Samurai identity,
  held-key ownership for Soten/Mineuchi only, enemy Purify/Guard action-and-
  status evidence, the frozen Soten/Mineuchi stage, and bounded source/global
  sequence data. Automatic Zantetsuken additionally reads its armed state,
  adjusted-action readiness epoch, exact canonical enemy life/targetability,
  HP, positions and hitboxes for the target-centered 5-yalm cluster, blocking
  protection rows, the selected primary's required exact own-source Kuzushi
  attribution, shield amount for ranking but not as an execution gate, native
  range/line of sight, Bind, cast/queue/animation state, frozen actor, and
  bounded request/retry outcome. Before selection it keeps only the exact local
  actor, PvP context, and first own-Kuzushi observation time for a fixed
  1,500-ms collection window; no enemy target is stored or changed during that
  wait. The same Kuzushi requirement applies to the
  exact Wolves' Den duel target or reviewed striking dummy;
- when reactive counter-CC timing is learned, the exact plugin-owned action,
  target and nonzero source sequence are correlated transiently with the matching
  server status. Only the resulting action ID, landing delay, and target-edge
  distance are eligible for the bounded local calibration; no actor identity or
  character name is stored with a sample.

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

## Limit Break nameplates and notifications

The fixed Combat Frames runtime, remote-gauge calibration/estimation, clickable
rows, and native-mouseover publication are retired. No replacement surface draws
player frames, guesses a gauge, or changes a target. One experimental public-CC
strip is off by default pending live layout validation. When enabled, it reads
direct current/min/max values from the five native enemy GaugeBar components
above the pressure display. It publishes only after exact S1-S5 actor/name/row joins,
stable before/after captures, and a same-frame proof against the local native
LimitBreakController. Any missing, duplicate, stale, unstable, or mismatched value
hides all five bars. It never estimates recharge, stores native pointers/names, or
writes a UI node. The optional enemy LB cue
runs only in exact Crystalline Conflict and only for a fresh, unique canonical
`S1`-`S5` enemy with a visible native nameplate anchor. A reviewed activation may
draw an icon above that nameplate. A numeric countdown is shown only while a
matching live status provides a confirmed duration; instant or otherwise
unconfirmed activations receive only a bounded flash. The cue stacks with the CC
emblem without writing to or moving the native nameplate.

Separate local-only notification options can draw a top-center self
`LB ACTIVATED!` banner and up to three left-side ally damage cards. Ally cards
accept only a direct ActionEffect positive-damage record attributed to an exact
party caster and reviewed LB action, never an inferred HP delta. The optional
name leaf reads only the ally's current client name for that transient card.
When the existing LB danger warning is enabled, an exact enemy DRG `Sky High`
activation may also draw a top-center airborne card immediately. Its continuation
uses only the fresh exact enemy episode and live mapped caster-status duration;
loss of actor, episode, context, metadata, or status clears it without estimation.
An exact enemy SMN Bahamut/Phoenix action/icon/status pair may use the same bounded
danger lane. An exact enemy SAM Chiten status episode may draw a large nameplate
emblem/countdown and `DO NOT HIT` danger card. One selectable built-in FFXIV sound
may play once for each exact episode.
Activation evidence, names, actor identities, durations, damage events, card
state, and nameplate bounds remain bounded in memory and are not logged,
persisted, transmitted, or uploaded.

## Local MP warning sounds

This optional local-only feature requires an exact trusted 10,000 maximum-MP
sample. It watches only downward crossings of 4,000 and 2,000 MP with independent
hysteresis latches. A direct crossing of both thresholds plays only the critical
2,000-MP sound. It invokes configured built-in FFXIV sound IDs; it does not
download, record, persist, transmit, or upload audio or MP history. Unknown or
untrusted MP fails closed, and the latches reset with context/player lifetime.

## Pressure tracking

The pressure counter transiently observes enemy hard targets and cast targets,
plus party/alliance hard targets used to compute the team `P#` count. A
read-only action-effect observer examines bounded records directed at your
local entity. For pressure it uses only source/target identity, action identity,
effect-type categories needed to recognize a harmful event, and event sequence
and time. While an Ally Rescue confirmation is pending, the same local observer
can also examine the exact local-caster action result directed at the attempted
party member. Those paths do not read, display, or store damage amounts. The
separate ally LB feed may decode and briefly display only a directly attributable
LB damage amount under the stricter exact-caster/action rules above.

Recent harmful-action evidence remains only in memory for the configured
0.5-8 second window (3 seconds by default) and is then discarded. Queues are
bounded, dropped-event counts are aggregate diagnostics, and no combat history,
actor name, or event payload is logged or uploaded. Pet/owned action sources can
be resolved to their visible player owner solely for the current pressure cue.

When optional Ally Rescue, Near Help pressure preference, reactive Guard,
Paladin Guardian, NIN Guard-Shukuchi, Smart Paean, Smart Tab, or Smart Action are
enabled,
the same current-frame enemy hard/cast identities can also be reduced to unique
incoming-pressure counts for the local player and/or exact party members. Smart Kardia does not
keep this ally scan running while idle: one client-accepted Eukrasia trigger
requests only the fresh publication needed for that bounded opportunity and its
exact self-fallback proof. The data is bounded to the live snapshot and is not
retained as combat history.

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

The separate default-off held-key option can submit an exact ordinary self
Sprint request for a verified high-pressure episode while physical WASD/arrow
consent remains held and the same direct-enemy count remains at least three. The
original movement key is not swallowed. It reads current local identity,
life state, own Guard/Sprint status, exact Sprint metadata/readiness, and the
shared per-frame scheduler claim. Known unavailable states wait without calling
the native boundary. Only an explicit client rejection may retain the same
frozen episode for the common bounded retry; acceptance, ambiguity, or drift is
terminal. It never changes a selected target, chooses another action, or stores
or replays the key.
The native request result is diagnostic only and does not prove that Sprint was
accepted or applied by the server.

The current action-request priority is **Purify > PLD Guardian > Smart Recuperate > automatic
Guard > AST same-target heal chain > RDM fresh-Guard engage > SAM staged
counter-CC / automatic Zantetsuken > automatic NIN Seiton > VPR Serpentiner Geist > GNB Continuation > reactive
counter-CC > Ally Rescue > NIN Guard-Shukuchi > SCH Critical
Strategy > DRK Shadowbringer (Dark Arts) > DRK Hiebsprung > DRK Shadowbringer
(safe fallback) > Monk combo > Emergency Teleport > pressure Sprint > idle Smart Sprint > event
Kardia > event Monk**. The job-specific helpers use that deterministic urgency
order; automatic Zantetsuken and Auto-Seiton need no key, while physical-hold
helpers retain explicit consent. Reactive counter-CC leads ally cleanse because
its LB and protection-end windows are shorter. One framework frame permits at
most one helper native boundary, but a continuously held key remains consent
for later distinct exact episodes. Kardia and Monk retain their separate event-
driven origins.

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

## MCH, DRG, SMN, and Chiten danger warnings

When the warning is enabled, the same local observer verifies action ID `29415`,
the exact early target-marker shape, the hostile MCH caster identity, and that
the sole target is your local player. The later damage/miss event is rejected.
The warning is deduplicated and held only for its short in-memory lifetime.

For DRG, the shared metadata-validated LB capture accepts only enemy job `22`
using exact `Sky High` activation `29497` in the canonical Crystalline Conflict
roster. That episode can warn immediately from the activation; continued
airborne timing requires the exact caster's live `Sky High` status `3180`.
It does not infer an LB from gauge state, movement, disappearance, `Sky Shatter`
status `3181`, or the later landing damage actions `29498`/`29499`.

For SMN, only enemy job `27` with the exact Bahamut action/icon/status pair
`29673`/`9681`/`3228` or Phoenix pair `29678`/`9683`/`3229` is admitted. The
activation can create a brief warning immediately; a duration is shown only from
the paired live status on that exact caster and episode. Cross-pairs and inferred
summons fail closed.

For SAM Chiten, the existing 50-ms exact hostile roster scan accepts only verified
SAM job `34` with live status `1240`. One bounded in-memory episode token prevents
repeat sounds/cards, and missing status, identity, context, or metadata clears the
warning after its short grace without inference.

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
Purify-removable CC and Wunder der Natur / Miracle of Nature use separate
blocker matrices, which
include only verified relevant status IDs. The standard matrix contains
`3054`, `3673`, `3248`, `1303`, `1320`, `4096`, and `3143`; Miracle's matrix
contains `3248`, `1320`, VPR-only `4096`, `3143`, `3052`, and `3162`. Its reviewed action
list is limited to Intervene `29065`, Blota `29081`, Stumme Nocturne / Silent
Nocturne `29395`, Repelling Shot `29399`, Wunder der Natur / Miracle of Nature
`29228`, Lethargy `41510`, Forked
Raiju `29510`, Fleeting Raiju `29707`, Air Anchor `29407`, Gravity II `29244`,
its Double Cast form `29248`, and Mineuchi `29535`.

The standard matrix makes one narrow movement exception: Intervene `29065` and
Repelling Shot `29399` are not blocked by Guard `3054`/`3673`, so their gap-close
or disengage movement remains usable. Forked Raiju `29510` and Fleeting Raiju
`29707` remain blocked by Guard because their stun timing must not be consumed
into it. Resilience, Warden's Paean, Inner Release, Meikyo Shisui, and Hardened
Scales retain their existing action/job-specific blocks. This exception is not
used by the plugin-owned predictive post-Guard counter-CC path.

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

## DPS Smart Tab

Smart Tab is default off and runs only in exact Crystalline Conflict on the
reviewed melee and ranged DPS jobs. When enabled, paired local native hooks scope
FFXIV's
own targeting handler and own only its nested forward world-target cycle after
the game's logical binding and UI/input gates. Only that scoped forward request
may select one exact hard target. Toggle off, reverse targeting, direct helper
calls outside that handler, UI/chat input, other target commands, unsupported
jobs, and unsupported contexts call the native paths unchanged.

For one owned request, the plugin transiently resolves unique, living, hostile,
targetable canonical `S1`-`S5` enemies and excludes live Guard. It first admits
hitbox-edge melee reach, then only the reviewed melee job's gap-closer cap.
Ranged jobs have one geometric tier and no melee preference: BRD,
BLM, SMN, MCH, RDM, and PCT use 25 yalms, while DNC uses 15 yalms. Ranking inside
the first non-empty tier is lowest HP ratio, fresh positive team pressure, known
Guard-cooldown
unavailability, trusted MP ratio, then stable slot. Every geometrically admitted
candidate must also pass FFXIV's native range/line-of-sight query through a
metadata-verified hostile spatial probe; geometry still enforces each job's
narrower cap. If the exact current hard target is eligible, the request advances
to its successor in that ranking and wraps; otherwise it starts at rank one. No
persistent cursor is retained, so manual target changes re-anchor the cycle. One
actor is frozen and revalidated, including a second native spatial query
immediately before the setter. Missing candidates or any identity, geometry, context, or reach
failure before the setter consume only that owned forward cycle without a target
write, so the current hard target remains unchanged. Otherwise the plugin calls
the native hard-target setter once and checks exact readback once. Setter
rejection or readback mismatch is terminal, with no retry, rerank, or alternate
target. The plugin does not claim that the pre-call target was restored after
those post-setter outcomes. The spatial probe does not execute an action. There
is no persisted input or upload.

## One-shot Smart Action

Smart Action has its own default-off macro-helper switch. It runs in exact
Crystalline Conflict and can use the exact-current-target-only test path in
Wolves' Den when that separate test option is enabled. `/smartaction`,
`/ssaction`, or `/seitonfar` creates one local token lasting
at most 750 ms. The authored macro then supplies the harmful action first with
`<e1>` as a carrier and again with `<t>` as its sole vanilla fallback. The plugin
does not read or retain the macro text and does not require a current target.
Arming reads no enemy slot and stores only the current territory, exact local
identity, expiry, and whether combat-priority or farthest-reachable ranking was
requested; a live `S1` is not a plugin-side arm prerequisite.

Only an exact non-ground-target PvP hostile action and unique live canonical
`S1`-`S5` candidates may qualify. Active Chiten, Guard, Covered, Paladin LB
Hallowed Ground, and Dark Knight LB Undead Redemption block the selected
primary actor. Exact Guard-ignoring damage and a closed catalog of 16 ordinary
hostile-target movement actions may still select Guard; this preserves their
movement and does not claim their damage or CC bypasses Guard. Chiten, Cover,
Hallowed Ground, and Undead Redemption remain blockers for those actions.
Forked Raiju `29510` and Fleeting Raiju `29707` are intentionally absent from
that movement catalog so their stun attempts remain blocked by Guard.
Strictly verified PLD Shield Smite `41430` and SCH Chain Stratagem `29716` may
also select a Guarded primary actor because their current English metadata says
they halve Guard's defensive bonus. This permission opens only Guard on that
candidate; it never opens Chiten, Cover, Hallowed Ground, or Undead Redemption.
Unverified Chiten metadata conservatively blocks every Samurai. Candidates that
pass protection safety rank by native reach tier first (melee, gap closer, then
ranged/other), followed by lowest HP, fresh positive team pressure, known
Guard-cooldown unavailability, trusted MP ratio, and stable S-slot/identity. In
Crystalline Conflict, when the normal selection has no currently reachable
candidate and tap-to-land Chase is enabled, the same order may freeze one
protection-safe but presently out-of-range/line-of-sight actor. That frozen
action/actor tuple cannot rerank. Only a proven native range/line-of-sight
failure may arm the bounded wait; every other blocker keeps its existing
action-boundary behavior.
`/seitonfar` uses the same complete candidate/protection snapshot but ranks only
eligible actors by descending finite hitbox-edge distance and stable S-slot. It
does not apply the generic melee/gap-closer prefilter; the exact incoming
action's native range/line-of-sight probe is authoritative. Invalid geometry or
an ambiguous identity set fails to the authored fallback. Distance changes after
one actor is frozen cannot trigger a rerank.
Target-centered circles compare their effect radius with every Chiten actor's
current position and hitbox. An incidental Guard, Cover, Hallowed Ground, or
Undead Redemption actor does not veto an otherwise safe primary target. Other
unreviewed AoE shapes retain the conservative global Chiten veto, but unrelated
non-retaliatory protections no longer stall the action. Their hostile
S-slot/object-table proof fails closed only for an unaccounted Samurai, unknown
job, or actor visibly carrying Chiten; a transient unaccounted non-Samurai with
no Chiten does not globally cancel the macro. A direct single-target action instead
requires exact protection evidence for its selected actor and does not require
unrelated hostile object-table completeness.

The token is consumed before selection only while it is strictly live; an
expired arm remains on the vanilla path. One action and actor freeze and the
shape-appropriate protection proof is rebuilt immediately before forwarding. Drift
suppresses that carrier call without reranking or retry. A fresh post-claim
safety lease of at most 750 ms keeps the same semantic resolved action and its
authored raw identity under the protection check for the authored `<t>` fallback
after native rejection. Adjusted-action drift is blocked rather than treated as
unrelated. Equivalent raw carriers for the same resolved skill remain inspected,
an unresolved exact raw fallback is blocked, and a safe default-target carrier
is frozen to the inspected canonical enemy ID before forwarding. The lease is
bound to the same local identity and territory, ignores unrelated resolved
actions, and ends after a safe accepted call or expiry. If semantic resolution
is unavailable, supported macro calls stay blocked through only that fresh
lease because an `Action`/`PvPAction` alias cannot be disproved. Protection that
appears only after native acceptance or during later cast/projectile travel is
outside this local pre-dispatch boundary. The plugin creates no action, changes
no selected target, stores no input, and persists or uploads none of these
observations. In enabled Wolves' Den testing, Smart Action uses only the exact
current hostile duel opponent or reviewed dummy and never performs CC
`S1`-`S5` ranking. `/seitonfar` remains reachable-only in every context.

An exact Smart Action-owned harmful, non-ground-target PvP cast with a proven
adjusted or exact base cast time now uses the existing canonical ranking,
range/line-of-sight check, frozen actor, and final protection recheck. A direct
`<t>` carrier cannot bypass that ranking just because it matches the visible
hard target. If no candidate wins, the normal two-line macro may still reach its
exact authored `<t>` fallback. If tap-to-land is enabled, only the already frozen
Smart Action cast may transfer into one bounded reservation after a proven range
or line-of-sight rejection; FFXIV Macro/raw-100 and normal carriers are accepted,
while Queue is rejected.

Near Assist and Near Help retain the authored-target cast policy. If their
authored target is the exact current visible hard target, the incoming call
passes through unchanged; otherwise the hidden or missing carrier is suppressed
and its one-shot token is consumed so the following authored `<t>` line remains
the visible-target game path. Missing or drifted action metadata also fails
closed instead of entering Smart Action cast ranking.

The exact local-SAM Smart Action pair Ogi Namikiri `29530 -> 29530` and Tendo
Setsugekka `29536 -> 41454` or `41454 -> 41454` retains additional protection
handling after strict current English action metadata verifies the cast, range,
hostile target, job, and action identity. The selected actor must remain directly
protection-safe. Ogi additionally checks
the candidate-facing reviewed 8-yalm, 90-degree cone and vetoes a candidate only
when a Chiten actor's hitbox intersects it; unrelated Guard, Cover, Hallowed
Ground, Undead Redemption, and out-of-cone Chiten actors do not globally stall
the cast. Instant Kaeshi: Namikiri `29531` uses the same cone policy after its
separate icon `9664`, 8-yalm range/effect range, and cast-type-`3` metadata pin.
Tendo remains direct-target. The instant Kaeshi actions `29531` and `41455` do
not receive that cast-specific handling. Other instant actions retain the
existing one-shot smart redirect. Seiton Sense does not write facing or camera
state for this rule; any initial facing is FFXIV's normal cast behavior toward
the frozen or visible target.

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

## Experimental Astrologian held Near Help

The separate default-off AST helper reuses Near Help's transient exact
party/self observations while an eligible physical gameplay key remains held.
It considers only living, targetable self/party players at an exact 60% HP or
lower and reads the same current HP, party slot, optional incoming-pressure
count, position, native action target, range, and line-of-sight result. With the
existing Wolves' Den testing option enabled, the same friendly/self path is
available there without reading or selecting an enemy or striking dummy.

One selected player, context, local identity, and key generation are frozen for
Harmonischer Orbis / Aspected Benefic `29243`. The plugin also records whether
Double Cast was already locally available before that Orbis. Only a
client-accepted Orbis may reserve the repeat: the plugin invokes raw Double Cast
carrier `29245` only while it resolves exactly to adjusted Orbis `29247`. That
later request revalidates the same actor without applying the 60% boundary
again. It never reranks after the first heal, changes a visible target, uses a
different Double Cast form, or substitutes another player. Exact action,
identity, readiness, range, and retry state exist only in memory for the active
held episode and are cleared on completion, drift, release, reset, or failure.
Your own active or still-propagating Guard suppresses both action requests and
is rechecked at the final action-hook and optional held-cast-cancel boundaries;
this helper cannot remove or break Guard.

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

## Explicit manual Panic Shukuchi and camera-back dash macros

`/panicshu` and the default-off `/seitonbw` command are command-only,
user-authored macro actions. They have no automatic, pressure, enemy, status, or
held-key trigger and are not part of the shared held-action scheduler. They run
only in Crystalline Conflict, or in Wolves' Den when the existing testing option
is enabled. `/panicshu` remains exact PvP NIN; `/seitonbw` accepts only the
closed current PvP self-dash mapping for NIN, AST, DNC, DRG, RPR, and PCT.
Frontline and Rival Wings are excluded.

`/panicshu` computes only the terrain point 19.5 yalms along the local
character's current facing. `/seitonbw` resolves screen-back from the normal
gameplay camera. NIN uses the exact 19.5-yalm terrain point; AST Epicycle `41506`,
DNC En Avant `29430`, DRG Elusive Jump `29494`, RPR Hell's Ingress `29550`, and
PCT Smudge `39210` set only local character facing immediately before their one
native self-action. The camera and targets are never written. The local-facing
hook remains inactive until the first enabled non-NIN invocation. Immediately
before the exact action boundary, the shared audited ReAction/MOAction check
reads the supported ReAction profile's current action selectors and refuses a
wildcard, exact, or adjusted selector that can own the requested self dash.
Unrelated Auto Target/Action Stacks and camera-relative rotation remain allowed;
exact MOAction ownership is refused. Unavailable or
non-finite data and event/cutscene, spectator, aiming, or lock-on camera modes
refuse the command. One invocation makes at most one matching native location
or standard-action call in the same command callback. It stores no pending
intent and has no lease, timer, framework wait, expiry, scheduler/Purify claim,
retry, target search, or alternate action. Exact metadata, unchanged base action,
available charge/resources, and a clean immediate cast/queue/animation boundary
are required. Transformed actions such as Doton, Wyrmwind Thrust, Retrograde, or
Regress are never substituted. The exact command may intentionally give up
plugin-owned Auto-Guard for only its matching same-thread native boundary.

A client rejection, ambiguity, or exception cannot retry. A later macro press is
a new explicit user command. The helper does not
recompute after movement or turning, search a path, move inward, choose an
alternate action, or use a shorter fallback point. It neither reads nor changes
the mouse/ground-target cursor or any hard, soft, Focus, or mouseover target. A
wall, missing exact terrain collision, excessive vertical offset, or native
line-of-sight refusal therefore fails closed.

The complete identity/candidate, including facing or camera observation, exists
only during the immediate command callback. Afterward, only the last command,
origin/destination coordinates, bounded camera diagnostics, native acceptance
outcome, and aggregate command counters may remain
in plugin memory for local `/seiton debug` diagnostics until unload; they are not
persisted or uploaded. Source
checks cannot establish current-client terrain, line-of-sight, actor-facing
application, or actual movement behavior. Four-direction testing for all six
jobs plus NIN slope/wall/invalid-endpoint cases in Wolves' Den remains a live-
validation boundary, and a Den result is not proof of CC behavior.

## Explicit movement-direction En Avant macro

`/seitonenavant` is a command-only, user-authored DNC macro action. It
reuses the existing default-off directional-dash option and runs only in
Crystalline Conflict or explicitly enabled Wolves' Den testing. It will not have
an automatic, enemy, pressure, status, held-key, or scheduler trigger.

For one invocation, the helper will observe three fresh, finite local-player
world positions to form two consecutive movement segments. Both segments must
be current and agree on direction for the same exact local DNC identity and PvP
context. This position-based rule treats cardinal, diagonal, and remapped
movement uniformly without reading a physical key binding. The command folds a
fresh current-position sample into the observation before resolving the
heading; it does not require a processed MOVE or autorun flag on that exact
callback frame. Finite sub-threshold displacement may accumulate against the
last meaningful anchor until it proves movement, while tiny jitter remains
insufficient. Controller and autorun behavior remain live-test pending.
Stationary or insufficient movement, stale or inconsistent
segments, non-finite coordinates, or actor, job, context, or action-identity drift
discard the observation and make no action call.

The proven world direction may align only local character facing for one exact
PvP En Avant `29430` native action boundary. The helper does not read, write, or
derive direction from the gameplay camera, and it will not read, change, or
substitute a hard, soft, Focus, or mouseover target. Each invocation makes at
most one native En Avant call and has no stored intent, queue, timer,
framework wait, lease, retry, replay, alternate action, last-known direction, or
fallback. The samples and derived direction will be transient local state only;
they will not be persisted or uploaded.

## Public-CC medicine-kit countdown and beacons

The two medicine-kit overlays are visual-only and run only in supported public
Crystalline Conflict. The first-spawn countdown reads the native content time
remaining during the opening of the five-minute match and does not treat the
separate 0:30 preparation value or a still-frozen 5:00 content timer as active
match time. Its assumed 30-second first-spawn boundary and exact current-client
timer transition remain subject to live-game confirmation.

When beacons are enabled, the plugin checks only Dalamud's bounded event-object
and reaction-event-object views at 10 Hz. It reads localized object name,
BaseId, ready-to-draw state, and finite position. A narrow localized name match
may teach one BaseId hint for that territory in memory until plugin unload.
Ready matches may receive a green foreground beam, distance label, or off-screen
edge marker; foreground rendering makes the cue visible through terrain but
does not establish reachability, safety, or a route.

The feature never writes an object, target, camera, marker, map, path, action,
movement, or pickup state. It cannot reserve or consume a kit. Timer and object
observations, learned BaseId hints, and beacon positions are not persisted,
logged as continuous history, transmitted, or uploaded. Exact localized runtime
names/BaseIds and ready-to-draw behavior remain current-client live-validation
points. Missing, ambiguous, or changed runtime identification fails closed by
hiding the beacon.

## Shared held-action scheduler

Held-action helpers share one transient physical-key observation and one
per-framework-frame claim. Claiming a frame never consumes the physical hold:
the same exact key may remain consent for a later distinct Purify, AST same-
target healing, RDM fresh-Guard engage, counter-CC, VPR Serpentiner Geist, ally cleanse,
Guardian, NIN Guard-Shukuchi,
SCH Critical Strategy, DRK, Recuperate, Emergency Teleport,
Guard, or pressure Sprint episode. Enabling an option while a key is already
down still requires a release and new press. Release, text input, context/job/identity loss, death, metadata
failure, or reset clears the relevant leases. Own Guard suppresses every native
helper boundary without consuming the physical hold; individual frozen episodes
either wait or cancel according to their exact action-specific contract.

The shared physical-hold option trackers prefer an already-held movement key, then another
stable held gameplay key, before fresh movement/other fallback. Each helper
checks its held lease before fresh input and retains the exact frozen key until
release, ineligibility, reset, or its action-specific terminal outcome. A short
later action-key tap therefore cannot replace a valid long-held WASD lease.

Every helper freezes its exact action, actor/target, status or episode, and key.
Guard-Shukuchi freezes the actor rather than a stale destination and revalidates
that same actor's latest finite position immediately before its location call.
The first structurally ready call is immediate. Known cooldown, resource, cast,
and blocking animation-lock states are soft waits and do not advance the native-
attempt budget. An occupied native queue remains a soft wait for ordinary
actions. Only Purify, Recuperate, and automatic Guard may opt into FFXIV's
normal action boundary while a different queue entry exists; Seiton never edits
the queue fields directly. FFXIV may replace that queued action when it accepts
the higher-priority recovery request; disabling the option restores the strict
empty-queue gate. A client-false result is retryable only when the complete
queue/action fingerprint is unchanged around that exact call. Action-
specific cooldown, resource, or
reachability waits can leave the scheduler frame to a usable lower helper;
global cast, ordinary occupied-queue, blocking-animation-lock waits and the short explicit-
false throttle retain that frame. Only an explicit `false` return after final
exact revalidation can retain the same intent for another call. A sampled native
not-ready-to-ready transition may wake it on the first later framework frame;
ordinary timer movement cannot manufacture repeated edges, and 50 ms remains
the quiet-boundary fallback. The default legacy budget is eight calls. The PvP
latency-response option in CC or opted-in Wolves' Den freezes the
selected 100-1500 ms clean-false budget (1000 ms = 21 calls; 1500 ms = 31).
The same option registers the local Dalamud IPC function
`SeitonSense.IsCriticalUtilityClaimed`. It returns only one boolean for a
125-ms lease after the shared held scheduler actually consumes a frame; it does
not expose an action, actor, target, key, position, or combat log. Claim/query
counters are memory-only, and a missing or faulting consumer changes no Seiton
action behavior. Nothing is persisted or uploaded.
The first client-accepted return is terminal; exceptions, uncertain queue/
sequence transitions, identity drift, and every other ambiguous result are also
terminal. Retry exhaustion or an ambiguous/unsafe terminal outcome may latch
only that helper's exact key until physical release when recreating the same
epoch would otherwise be possible. Acceptance or ordinary cancellation does not
revoke held consent for a later distinct episode; Purify, Ally Rescue, and
reactive CC instead spend their exact status/event intent. A retry cannot rerank,
select an alternate actor/action, or outlive its original exact event. Guard-
Shukuchi may change the hard target only after a client-accepted location call,
as described below; no rejection, unknown result, or retry mutates a target. All
leases, retry clocks, outcomes, and aggregate diagnostics are memory-only and
are neither persisted nor uploaded.

## Experimental held-action cast cancellation

This separate test is disabled by default. It is the generic held-helper
permission and applies only to otherwise-ready exact physical-hold intents for
Purify, AST same-target Orbis,
RDM fresh-Guard engage, SAM counter-CC,
reactive counter-CC, Ally Rescue, Guardian, NIN Guard-Shukuchi, SCH Critical
Strategy, DRK Shadowbringer, DRK Hiebsprung, Smart Recuperate, Emergency
Teleport, Guard, and pressure Sprint. Smart Kardia, Monk Earth's Reply,
every already-incoming manual/Turbo redirect (including Paean), and macro helpers are excluded.
Automatic Zantetsuken and Auto-Seiton never use this permission. Viper
Serpentiner Geist, GNB Continuation, and held Monk combo are also excluded
because they poll their current native state and deliberately do not cancel a cast.
Cast cancellation therefore constructs sixteen reviewed request shapes across
seventeen ordered selection slots; held Shadowbringer uses the same exact request
adapter at its separate Dark Arts and safe-fallback positions.

For the highest-priority eligible intent, the plugin rechecks exact local and
target identity, held key, context, own Guard, helper action/readiness/resources,
empty queue, and nonblocking animation lock. Only when both local cast signals
prove an active cast may it request FFXIV's native cast cancellation once for
that observed cast epoch. The native function returns no acceptance value, so a
recorded request does not confirm that FFXIV canceled the cast. Signal mismatch,
cast-action drift without a fully observed clear state, or other ambiguity fails
closed for that epoch.

The cancellation request claims its framework frame and can never be paired
with a helper action request in that frame. A later frame must observe both cast
signals clear and repeat the ordinary complete helper preflight. The plugin does
not synthesize movement or Escape, clear the native queue, write cast state, or
change a target. It may sacrifice the current cast, FFXIV may decline to cancel
some actions, and current-patch stationary plus mobile BRD/MCH behavior still
requires live validation. Bounded Settings and `/seiton debug` values retain
only the current cast decision, the last requested helper/action/target/key/
intent and native request result, plus request/fault counts in memory; none is
persisted or uploaded. The separate
explicit-`false` helper-action retry remains at least 50 ms apart. It uses eight
calls by default or the exact intent's frozen opt-in PvP latency-response budget.

## Experimental automatic basic-shot cast cancellation

Automatic Purify and Automatic Recuperate do not inherit that generic held-
helper permission. Their sole cast-cancel route is a second persisted permission
which is independently disabled by default. It can admit only exact local BRD job
`23` / Powerful Shot `29391` or exact local MCH job `31` / Blast Charge `29402`.
The matching English PvP action row must pass startup metadata verification, and
the active cast plus adjusted raw-action identity must still equal that same row.
Missing or drifted metadata, cross-job identity, changed cast signals, instant
transformations such as MCH Blazing Shot `41468`, the legacy Heat Blast row
`29403`, and every other uncertainty fail closed and wait for the cast to end.
The relevant keyless Purify `29056` or Recuperate `29711` intent must already be
otherwise ready. A cancellation consumes that framework frame; the automatic
helper may act only after its full preflight passes again on a later clear-cast
frame. The native request remains `requested`, never proof that FFXIV canceled
the cast. Automatic observation does not retire a physical held-key generation,
and the generic held-helper toggle remains independent.

## Experimental Purify helper

The automatic and legacy physical-key modes are separate persisted opt-ins and
are disabled by default. In a supported PvP context, both inspect only an
actually present, individually enabled Stun, Heavy, Bind, Silence, Deep Freeze,
or Miracle of Nature status. Automatic mode freezes that exact self/status
instance without reading a gameplay-key generation. Legacy mode reads current
local key-down state and distinguishes fresh/held generations; its held-key
option remains separately disabled by default.

If both modes are enabled for the same debuff, automatic consent owns that
status episode. Any physical generation observed because the user also enabled
legacy input remains untouched; it is neither substituted into nor retired by
the automatic intent.

The plugin does not log or persist key text/history, swallow or replay the
original key, change targets, or transmit input. Automatic observation does not
retire a physical held-key generation. While the exact enabled CC remains active,
a structurally ready Purify has absolute scheduler priority and may retain its
exact status/keyless or legacy held-key lease for bounded pre-acceptance retry;
cooldown/resource shortage yields the frame. Automatic mode may request native
cast cancellation only through the separate default-off permission and exact
verified BRD/MCH basic-shot boundary described above. Every other cast waits.
After a permitted cancellation it waits for a later clear frame before complete
revalidation and Purify. A client-accepted or ambiguous call is
terminal for that CC episode. ReAction Turbo's logical repeats do not create
physical consent. Other plugins can still alter the downstream call if
configured to rewrite Purify or its target.

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
address. The exact party identity, live/targetable state, one of the four trigger
statuses, current action identity/readiness, and native range/line-of-sight
result are revalidated before every possible call. FFXIV remains the authority
on whether a request queues or executes.

Purify and reactive counter-CC receive the scheduler frame before Ally Rescue.
Action-specific cooldown/resource or invalid reachability waits do not starve a
currently usable lower helper; a globally blocked queue/animation boundary and
the brief explicit-false retry retain priority. Only the same frozen ally/status
intent may use the common bounded retry. Acceptance, ambiguity, vanished status,
or changed target is terminal. The original key is still neither swallowed nor
replayed. Continuous ally/status/input observations are not persisted or
transmitted. When this helper reaches a native attempt or exact confirmation,
one local Dalamud diagnostic record contains only action ID, target entity ID,
status ID, outcome, and source sequence; it contains no character name, damage,
chat, or key history and is not uploaded by Seiton Sense.

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
statistics. Full ActionEffect payloads, counters, and popup history are not
persisted. The bounded local attempt/confirmation diagnostics described above
may include the exact numeric target entity ID and source sequence; they are not
sent over the network or uploaded by Seiton Sense.

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

## Experimental reactive Purify to Guard

This separate module is disabled by default and exact-Crystalline-Conflict-only.
It transiently reads the local player's exact identity, job, Stun and Resilience
status membership, own Guard state/readiness, current unique incoming-enemy
count, and physical gameplay-key generation. At known pressure from at least
three unique enemies, an exact Stun can create one exact Purify `29056` episode
under the shared bounded native-call policy.
Purify and Guard remain separate exact action episodes even when one continuous
physical hold supplies consent for both.

There is no speculative low-HP pre-Guard rule. A later Guard request requires
positive live Resilience `3248`, no remaining Purify-removable CC, the bounded
post-Purify opportunity, and repeated exact Guard/context/identity validation.
The same hold may authorize this distinct later episode. Known unavailable
states wait; the common explicit-false retry remains exact and bounded, with no
alternate action or replay.

## Experimental Smart Recuperate helper

The automatic and held-key options are independent persisted opt-ins, disabled
by default, and can run only in exact Crystalline Conflict or in Wolves' Den
while the separate testing option is enabled. They share one intent/cooldown
state machine and read the exact local identity, life/targetable state,
current/maximum HP and MP, trigger mode, optional held gameplay-key generation,
own Guard state, current supported context, and verified PvP Recuperate `29711`
metadata/readiness.
Exactly 16,000 or more missing HP and at least 2,000 observed MP are required.

If MP or native readiness is not yet eligible, or a higher-priority/Guard state
temporarily blocks it, the frozen intent waits without spending a native call.
Automatic mode normally waits for casting to end. Only the separate default-off
automatic permission may cancel the exact metadata-verified BRD Powerful Shot or
MCH Blast Charge boundary described above; all other or uncertain casts wait.
After a permitted cancellation it rechecks HP/MP and every other gate on a later
clear-cast frame before any call.
Dropping below the missing-HP threshold cancels that intent and permits a later
distinct health event on the same hold. Once eligible, identity, context, life,
targetability, Guard, metadata/readiness, HP, MP, and the same frozen supported
context are finally revalidated before each possible self-targeted native call.
A CC/Den/context transition cancels rather than transferring the intent. Only a
clean explicit rejection may use the common bounded retry. Acceptance starts an
exact metadata-verified 1.0-second recast floor. A sampled unavailable edge is
retained when visible, but after that floor current positive readiness may rearm
without requiring the brief negative frame, preventing an indefinite latch.
Retry exhaustion or an
ambiguous/invalid exact outcome latches this helper until the frozen key is
released, or until the automatic HP opportunity clears. No selected target is
read or changed and no other action is substituted.
The transient observations and aggregate diagnostics are not stored or uploaded.

## Experimental Emergency Teleport held-key helper

This persisted option is disabled by default. It supports exact PvP MNK
Thunderclap `29484`, BLM Aetherial Manipulation `29660`, SGE Icarus `29261`, and
VPR Slither `39184` in Crystalline Conflict and explicitly enabled Wolves' Den
testing. It runs after Smart Recuperate and before generic Guard. The default
danger gates are strictly below 50% HP, strictly below 4,000 MP, and at least one
fresh direct enemy hard/cast target; settings also control the minimum travel,
destination safety radius, and maximum nearby-enemy count.

Each exact living, targetable non-self party candidate is checked against the
reviewed job action, native target-specific action status, range/line of sight,
minimum edge-to-edge travel, and one complete current enemy-position snapshot.
Ranking minimizes nearby enemies first, then maximizes travel and clearance, with
stable identity tie-breaks. Duplicate party identities, incomplete enemy geometry,
unknown pressure, or any native uncertainty fails closed.

The chosen action, local player, ally, key, context, settings, and danger episode
are frozen. The final preflight rechecks all of them before one native request;
the shared frame is consumed only after this check so its own held-key evidence
remains readable. The episode is marked spent before the native call. Final
preflight drift, rejection, exception, ambiguity, or any other outcome cannot
retry, rerank, or select a fallback. A later episode requires a stable known-safe
state. No target is changed, no input is generated or replayed, and the bounded
episode/diagnostic state remains memory-only. Client acceptance does not prove
that FFXIV moved the player.

## Experimental Viper Serpentiner Geist held-key helper

This independent persisted option is disabled by default and runs only on PvP
Viper in exact Crystalline Conflict or explicitly enabled Wolves' Den testing.
It polls FFXIV's currently adjusted Serpent's Tail carrier directly on each
active framework frame. No preceding action, invocation mode, sequence advance,
native queue drain, accepted-action epoch, or invented wall-clock trigger is
recorded or required. The carrier's exact exposed follow-up defines one local
in-memory generation. In CC, only when that follow-up and held consent are both
available, the shared Smart Action policy reads the complete canonical enemy
set, current HP, optional fresh team pressure, trusted Guard/MP data, geometry,
protection, and native reachability. It selects a ranked winner first; the exact
current hard target is considered only as the fully validated last fallback.
No macro text is read and no physical input is created.

FFXIV must adjust Serpent's Tail / Serpentiner Geist carrier `39183` to the exact
expected follow-up `39174`-`39182`. The carrier itself is never submitted.
Follow-ups `39177` and `39178` use their native 20-yalm range; the other reviewed
follow-ups use 5 yalms. Any eligible currently held physical gameplay key,
including WASD, may supply consent while the follow-up is exposed, including
when the proc appeared before the hold. Only then are that key and the exact
chosen actor frozen for the episode; the same key, actor, action,
context, territory, local Viper identity, own-Guard state,
metadata, readiness, native target validity, range, and line of sight are checked
again before a possible native call. Purify keeps absolute priority. Temporary
action/resource/target-status or reachability unavailability retains the frozen
opportunity while yielding the current frame to a usable lower-priority helper.
Only an otherwise-ready exact intent waiting on the native boundary or retry
throttle retains Viper's scheduler frame. Each exact carrier exposure can be
spent once. One false carrier sample is treated as flicker and cannot rearm a
spent exposure; stable absence allows a later same-ID proc, while a different
exact follow-up such as `39177` to `39178` becomes a new generation immediately.
Only a clean explicit client rejection may use the shared bounded retry for the
same frozen intent. Acceptance, ambiguity, retry exhaustion, key release, or
drift is terminal without an alternate, rerank, fallback, target mutation, or
replay. Ambiguity or retry exhaustion additionally latches the frozen key until
its physical release.

In Crystalline Conflict the frozen target must remain one exact canonical
`S1`-`S5` enemy and must remain safe under the full Smart Action protection
matrix. Drift ends and spends that carrier exposure; the same held episode
cannot rerank. In Wolves' Den, the separate testing option must remain enabled
and the target must remain the exact current hard-target living, targetable
native hostile duel opponent or reviewed combat striking dummy with NameId
`541`. Arbitrary NPCs, synthetic enemy slots, Frontline, and Rival Wings fail
closed. The helper deliberately cannot
request held-action cast cancellation. Exposure, intent, retry, native-result,
and aggregate diagnostic state remain bounded in memory and are not persisted,
transmitted, or uploaded. Client acceptance does not prove server execution or
damage.

## Experimental Paladin Guardian Job Tool

This independent persisted option is disabled by default and runs only on PvP
Paladin in exact Crystalline Conflict. It transiently reads exact local and party
identity, life/targetable state, HP, position, incoming pressure, own Guard and
Guardian readiness, physical gameplay-key generation, and FFXIV's native
Guardian range/line-of-sight result. It does not depend on the reactive-Guard
module's master setting.

An exact living, targetable, non-self party member at or below 20% HP is a
critical candidate without a pressure requirement. From 21% through 40% HP, a
candidate is proactive-only and requires at least two exact incoming hard/cast
targets from a publication no older than 250 ms; from 41% through 50%, it
requires at least three. A critical candidate always
beats a proactive candidate; proactive ties rank higher pressure first, then
lower exact HP, before deterministic identity tie-breakers. The frozen winner
must pass FFXIV's native 20-yalm Guardian range/line-of-sight check and remain
exact. No custom center-distance cap is applied; the 10-yalm condition governs
staying close enough for protection after the jump. Purify keeps priority.
Guardian is next, before Smart Recuperate, automatic Guard, and every other job
helper. Both generic Guard and Guardian must be metadata-verified, adjusted to
their exact actions, and ready; active own Guard also suppresses the request.
Guardian freezes the selected ally and may use only the common bounded exact-
intent retry before a direct native `29066` request is accepted. It does not
change the visible target, select an alternate action/ally, or replay input.

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
not change a selected target, issue another combat action, select an alternate,
fall back, queue, or replay. A native pre-invocation shell-busy result may keep
only the exact same frozen Quick Chat eligible until its original 1.5-second
deadline. After one native invocation there is no retry. A command attempt and
its bounded ownership state are not persisted as history. Client-accepted Guardian does not
prove server-applied protection; an issued Quick Chat or marker command does not
prove delivery or display. Localized row-35 syntax, party display, Bind pairing,
and cleanup remain current-patch live-validation boundaries.

## Experimental Red Mage fresh-Guard held-key helper

This separately persisted helper is disabled by default and runs only for PvP
Red Mage in exact Crystalline Conflict or explicitly enabled Wolves' Den
testing. It transiently reads the local identity/job, inclusive configurable HP
and trusted 10,000-maximum-MP thresholds, held gameplay-key generation, exact
Corps-a-corps `29699` and unspent Riposte carrier `41488` readiness, own Guard,
Bind, and reviewed enemy protection. In CC it considers only complete unique
canonical `S1`-`S5` actors; Wolves' Den considers only the exact current target.

Only an exact Guard `3054` or `3673` absent-to-present edge opens one episode. A
target first seen already guarded is not treated as fresh. The reported Guard
time must be above 3.0 and at most 4.25 seconds, and the frozen lease cannot
survive beyond that Guard's first second. Exact target identity, life,
targetability, sole Guard row, absence of other reviewed protection, native
25-yalm range/line of sight, action forms, context, episode token, and physical
key are revalidated at the final boundary. A later invalidation ends the exact
episode without reranking or substituting another actor.

Only client-accepted Corps-a-corps may cause one hard-target set/readback on the
same revalidated actor; the helper never executes the melee starter or another
follow-up. A clean rejection may retry only that frozen request inside its
original first-second window, while ambiguity is terminal. The observations,
episode tokens, frozen intent, result, target readback, and aggregate diagnostics
remain bounded in memory and are not persisted or uploaded. Client acceptance
does not prove server movement; current-patch Guard timing, action acceptance,
and target timing remain live-validation boundaries.

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

Continuous held consent can create one frozen intent for each distinct eligible
SCH episode. The frozen enemy is revalidated for exact identity, action
readiness, live Guard, and native range/line of sight before every bounded call.
Pressure drift neither reranks, switches, nor invalidates that frozen target.
Only explicit client rejection may retry the same intent under the shared
policy; no drift can cause another selection, alternate target/action, fallback,
or replay, and the helper never changes a hard, soft, focus, or mouseover target
or swallows the original key. Bounded
state and diagnostics remain local in memory and are not saved as combat,
target, or key history or uploaded. A client-accepted request does not prove
that Critical Strategy landed or changed Guard; exact dispatch and effect
behavior remain current-patch live-validation boundaries.

## Experimental Sage Smart Kardia after accepted Eukrasia

This separate persisted option is disabled by default and can run only for PvP
Sage in exact Crystalline Conflict. It does not scan held keys or continuously
rank the party while idle. When one incoming exact PvP Eukrasia `29258` request
is observed, the plugin records exact local identity, territory, and the Sage's
before-call charge/own-source Eukrasia status evidence, then forwards that
original request exactly once without changing its action or target.

Only a client-accepted Eukrasia request may create a trigger, and the trigger is
valid for at most two seconds. A real local causal transition must then be
observed: either available Eukrasia charges decreased, or the exact local-source
PvP Eukrasia `3107` status appeared when it was previously absent. Unknown,
unchanged, regressing, wrong-player, or wrong-territory evidence fails closed.
The accepted trigger requests one fresh incoming-party-pressure publication
created no earlier than the accepted call; there is no idle Kardia pressure scan
and no separate six-second throttle.

One complete, unique, stable exact five-player view supplies exact party slot
and actor identity, life/targetable state, HP, current local-source Kardion state,
and trusted direct incoming-pressure counts. Candidates under pressure from at
least two unique live enemies rank by pressure descending, exact HP ratio
ascending, party slot, network entity ID, and game-object ID. If nobody reaches
the threshold, exact self is the sole initial fallback. An incomplete/unknown
view cannot manufacture that fallback, and an unknown or already-owned Kardion
state on the chosen actor ends the opportunity without selecting another actor.

Smart Kardia waits for the current animation lock to clear while the trigger
remains valid. It follows pressure Sprint and idle Smart Sprint, and precedes
only event Monk in the current request order.
Before at most one direct-GOID Kardia `29264` request, the trigger and frozen
actor are spent and exact configuration, context, local Sage, causal Eukrasia,
fresh pressure, Kardia readiness, own Guard, frozen identity, Kardion state, and
non-self native 30-yalm range/line-of-sight evidence are revalidated. Drift,
rejection, or exception cannot rerank, choose a lower candidate, switch to self
or another ally, mutate a hard/soft/Focus/mouseover target, replay, or retry.

The trigger token, timestamps, Eukrasia evidence, pressure publication, frozen
actor, result, and aggregate diagnostics remain in memory only and are not saved
as combat, target, status, or key history or uploaded. Client acceptance does
not prove that Kardia or Kardion applied; current-patch Eukrasia charge/status,
animation-lock, dispatch, and reachability behavior remain live-validation
boundaries.

When own Guard is active, every scheduled or automatic Seiton Sense action-
request helper is suppressed. The explicit manual `/panicshu` and enabled
`/seitonbw` commands are the sole helper exceptions and may intentionally break
own Guard with one immediate request. The bounded reactive observer may keep an already eligible enemy
startup, Purify, or Guard reservation in memory, but it cannot dispatch that
reservation through own Guard.

In addition, only a matching exact live Guard status following a hook-observed
automatic Guard request can arm temporary cancellation ownership. Auto-Guard
does not dispatch unless both central `UseAction` and `UseActionLocation` hooks
are enabled. Their exact request observation and a client `true` return remain
provisional and block no action. If the status has not appeared after 1.5 seconds,
one retry may cross the native boundary only while exact readiness is proven and
the original two-second post-Purify lease remains alive; a clean rejection
retracts that generation. Once confirmed, the boundaries block only a metadata-
resolved PvP `Action`/`PvPAction` that can cancel Guard; the location boundary
also covers deferred and ground-location calls. Protection follows the exact
status until it ends. An explicit second Guard press is blocked for the first
two seconds after confirmation. At the exact two-second boundary it is allowed
again and atomically releases ownership whether its incoming or resolved ID is
Guard. Manual Guard is observed but never owned. The dedicated exact command scope releases ownership only for
the matching NIN location action or reviewed directional standard action, even
if the native request rejects it.
Disabled/runtime or context, territory, player,
identity, and availability drift, unknown or non-PvP action resolution, status
end, missing propagation, exceptions, and a hard six-second maximum all release
or fail open. All ownership state and aggregate diagnostics remain bounded in
memory and are not persisted or uploaded; source checks do not prove server-side
Guard timing.

## Experimental WHM/BRD/NIN/PLD/RDM/BLM/SAM reactive counter-CC

If explicitly enabled in exact Crystalline Conflict or in the separate Wolves'
Den test mode, the plugin extends its
bounded local action-effect observer to recognize the reviewed early event
shapes for DNC Contradance `29432`, Marksman's Spite `29415`, Zantetsuken
`29537`, and VPR Furious Backlash / Nest der Blutschuppen `39188`. It reads
source/target network identity, action identity, bounded event sequence/time,
and the small fixed effect-slot shape needed to reject later hit packets. DNC
and the reviewed MCH/SAM/VPR urgent startup paths are available to WHM, BRD, and
NIN. Protection-end-only paths may instead use PLD Intervene `29065`, RDM
Resolution `41492`, exact Forte `41496` to Vice of Thorns `41493`, exact Soul
Resonance `29662` to BLM Frost Star `41481`, or the separate staged SAM
Soten/Mineuchi helper. NIN is enabled only when both metadata-verified Forked Raiju `29510` and
Fleeting Raiju `29707` rows are available. The bounded queues exist only in
memory.

The optional post-Purify path recognizes only exact enemy self-Purify `29056`
with one self target and a non-empty event sequence. It retains the exact
recovered-status effect `0x10` when the packet exposes Stun `1343`, Heavy `1344`,
Bind `1345`, Silence `1347`, Miracle of Nature `3085`, or Deep Freeze `3219`, but
also accepts the exact action-level packet when no individual recovery tuple is
present. The source must resolve to one exact live canonical `S1`-`S5` enemy,
and positive live Resilience `3248` is mandatory before the episode can
progress. The Purify observation remembers the exact actor, local counter
action, and event epoch without binding a key. A finite, positive,
catalog-bounded `RemainingTime` may establish only a non-extending expected end;
live status-list membership remains authoritative. The first real absent frame
at or after that end is eligible immediately, while early or untimed absence
still requires 150 ms of continuous proof. Proven absence opens a strict,
non-extending 500-ms edge in which the current exact key may bind; it does not
invent or carry a key while protection is live. The signal dispatches directly
to the frozen actor without
requiring or changing the selected target. There is no minimum team-pressure
count. Each verified Resilience end creates only a bounded exact protection-end
episode in memory. The runtime keeps at most one active post-Purify state per
canonical `S1`-`S5` slot plus a bounded deduplication set, allowing distinct exact
enemies to progress independently without one replacing the other's signal.
If the exact canonical row is transiently unavailable, at most five already-
deduplicated signals may retain only their original caster/event identity until
the original 750-ms acquisition deadline. They carry no key or action, cannot
fall back to another actor, and are retired on context, local identity/job, or
feature-generation change.

The separate optional post-Guard path observes only exact Guard `3054` or `3673`
present on one live canonical `S1`-`S5` enemy. Its first exact presence remembers
the actor, local action, Guard epoch, and the same kind of bounded non-extending
duration hint without binding a key. The first verified framework observation
that finds Guard absent exposes one strict, non-extending 500-ms key-acquisition
opportunity, including an early manual Guard cancel. Only a current eligible
held/fresh generation acquired inside that original edge can bind the episode.
After binding, key release retires that intent;
the same uninterrupted Guard remains retired through missing or ambiguous
samples until exact absence separates a later episode. It retains only the exact
identity, optional bound key, hint, and bounded lifecycle in memory, with no
minimum team-pressure count. Any identity, context, protection,
native range, or line-of-sight uncertainty prevents dispatch. It never requires or
switches the selected target, chooses an alternate action/actor, or replays
input. Only a clean native rejection may retain the same frozen intent under the
shared bounded retry policy.

When multiple exact post-Purify or post-Guard releases are simultaneously
eligible, selection occurs once an exact key is acquired inside the original
500-ms protection-end edge, before native range and line-of-sight are used as
dispatch gates. Selection reads each candidate's fresh exact team-target count,
current/maximum HP, and trusted current/maximum MP. Only a fresh exact pressure
count above zero earns a ranking bonus, with higher positive counts first. Known
zero, unknown, or stale pressure is neutral and never excludes or delays a
candidate. Lower HP ratio follows, then lower trusted MP ratio and stable
canonical slot/identity. Exactly one winner is selected. Every simultaneous
loser is terminal and cannot become a fallback attempt. Guard retires every
simultaneous loser before a higher-priority wait, so no later rank change or alternate can
replace the winner. The winning actor and key freeze once; cast,
queue, animation-lock, range, and line-of-sight gates may wait only until three
seconds from the original release. These values and the frozen winning actor are
retained only in bounded in-memory helper state. A true main-GCD counter that is
busy at its learned ideal request frame retains only the exact frozen reservation
strictly before `1000 ms`; that deadline never slides, and the wait neither claims
input nor requests cast cancellation.

At dispatch, the enemy identity, expected triggering job, life/targetable state,
exact still-eligible key generation, and action-specific verified protection
are revalidated. WHM uses only Wunder
der Natur / Miracle of Nature `29228` with native 10-yalm range/line of sight.
BRD uses only Stumme Nocturne / Silent Nocturne `29395` with native 20-yalm
range/line of sight. NIN reads only the PvP Spinning Edge/Aeolian Edge Combo
carrier `29500` and can dispatch only when it exposes metadata-verified Forked
Raiju `29510` or Fleeting Raiju `29707`, also with native 20-yalm range/line of
sight and the standard Purify-removable protection matrix. Forked Raiju also
requires the exact local Sealed Forked Raiju status `3195` to be absent, and both
variants require exact local Bind `1345` to be absent. VPR requires live
Hardened Scales `4096` to be actually absent. The DNC opportunity expires after
750 ms, existing MCH/SAM opportunities after 500 ms, and VPR after 250 ms.
Every bound post-Purify/post-Guard action uses the same three-second
protection-end deadline measured from its original release, so one ordinary
2.5-second GCD plus its release allowance can finish; key acquisition, waiting,
and retry never restart or extend that deadline. An unbound opportunity still
expires at the strict 500-ms acquisition boundary.

The helper observes the shared held gameplay-key state in its reactive counter-
CC scheduler slot, after the higher-priority recovery and job-helper lanes. An urgent startup may bind the first current
eligible held/fresh generation only inside that startup's original short event
lease. Expired or disabled leases retire, and its exact local job, action, and
enemy are revalidated before new packets are compared.
A later exact urgent startup may replace only an unattempted lower-priority
reactive lease; equal/lower events and every attempted lease remain frozen.
Purify and Guard remember the exact enemy episode while protection is live.
Authoritative absence opens the original strict 500-ms acquisition edge, and
only a current eligible generation acquired inside it may bind. Once bound, a
later key cannot inherit that episode, and text input poisons the exact
generation until real release. One continuous hold can authorize later distinct
startup or protection-end episodes, but each selected episode remains one frozen intent and
no simultaneous loser can follow it. Known action-specific unavailability keeps
the lease without blocking a usable lower helper; a global queue/animation wait
and the brief explicit-false retry retain the scheduler frame. The helper uses
only the common bounded same-intent retry and never changes the visible target,
chooses an alternate enemy/action, or replays input. Its internal redirect bypass excludes only macro target rewriting;
the final action-specific CC-immunity brake still runs at the native dispatch
boundary.

The action-effect hook also places exact local counter-status observations into
a separate bounded in-memory queue. A 1.5-second `AUTO CC LANDED` visual is
created only when local caster, expected action, pending enemy, effect type
`0x0E`, action-specific status, and the same nonzero source sequence created by
the plugin's accepted native request match within 1500 ms: Miracle `3085`,
Silence `1347`, Stun `1343`, or Deep Freeze `3219` for the exact supported action.
A manual use with a different sequence cannot claim the pending automatic
result. This proves only that the
counter-CC status landed on the intended enemy, not that Contradance, another
limit break, or damage was interrupted. A client-accepted action request alone
is never presented as confirmation. For an instant LB, the server may already
have accepted the activation before the reactive request arrives; even a later
confirmed Silence cannot retroactively prevent that race.

No continuous threat, key, status, team-focus, or combat history is retained or
uploaded. Bounded reactive transition/attempt/confirmation records are written
to the ordinary local Dalamud plugin log with numeric action/status/entity/key/
sequence identifiers and outcomes, but no character names, chat text, damage,
or full event payloads. Active threats and queues are cleared on context exit;
the log is retained according to Dalamud's own local logging policy. Current-
patch startup, release, dispatch, and interruption behavior remains a live-
validation boundary.

## Experimental Ninja Guard-Shukuchi held-key helper

This separately persisted helper is disabled by default and runs only for exact
PvP Ninja in exact Crystalline Conflict. It is not enabled by Wolves' Den test
mode. While explicitly enabled, it transiently reads the exact local identity,
continuous physical gameplay-key consent, own Guard and propagation state,
adjusted Shukuchi `29513` metadata/readiness and cooldown state, and independently
resolved canonical `S1`-`S5` enemies. An enemy is eligible only while it remains
living, targetable, strictly below 20% HP, at a finite current position within
Shukuchi's native three-dimensional 20-yalm range, and has a finite positive live
Guard / Wehr status `3054` or `3673`. Exactly 20%, Doton-adjusted action `29514`,
unknown identity, or missing/expired Guard fails closed.

Fresh positive team pressure may improve ranking but is never an eligibility or
dispatch gate. Zero, unknown, unavailable, or stale pressure is neutral. The
remaining order is lowest exact HP ratio, then stable S-slot and actor identity.
The helper freezes one exact actor and never substitutes an alternate, fallback,
or newly better-ranked enemy. Before each possible request it re-resolves that
same actor and revalidates identity, HP, Guard, action readiness, and the actor's
latest finite position and range.

Each permitted native attempt uses the single reviewed `UseActionLocation` call
site at that latest revalidated actor position. Only a proven client-false result
may use the common bounded same-actor retry. Only after the location request is
classified `ClientAccepted` may Seiton Sense re-resolve and set that exact same
living enemy as the hard target once. Rejection, unknown acceptance, exception,
identity drift, death, or target readback failure never changes a target, reranks,
or selects another actor. Client acceptance does not prove that the server moved
the character or that Shukuchi reached the intended location.

The local player's own Guard and bounded post-request Guard-propagation latch
block this automatic helper. The explicit manual `/panicshu` and enabled
`/seitonbw` commands remain the sole own-Guard exceptions. In the shared
scheduler and optional cast-cancel order, Guard-Shukuchi runs after NIN
Auto-Seiton and Ally Rescue, and before SCH Critical Strategy. The first four
slots are Purify, PLD Guardian, Smart Recuperate, then automatic Guard. After an accepted
request, the same continuously held physical key can authorize another Guard-
Shukuchi only after the cooldown is positively observed unavailable and then
ready again; an unknown state or a missed transition between framework samples
cannot create a new epoch. Frozen identity, key/cooldown epochs, positions,
native results, target readback, and aggregate diagnostics remain bounded in
memory and are not persisted or uploaded.

## Experimental Ninja Auto-Seiton helper

This helper is disabled by default and runs for PvP Ninja in exact Crystalline
Conflict or explicitly enabled Wolves' Den testing. When armed, it transiently
reads the local player's exact identity and job, own Guard state, the current
adjusted Seiton action and readiness epoch, and either the exact canonical
`<e1>`-`<e5>` enemy actors or the exact current duel target/reviewed striking
dummy. Eligible candidates must remain living, targetable, hostile, strictly
below 50% HP, and accepted by FFXIV's native action range and line-of-sight
check. Selection uses the lowest exact HP ratio, then stable enemy slot and actor
identity.

The `/autoseiton [on|off|toggle]` command and the NIN-only action-bar-style
ON/OFF tile change only the same persisted automatic arm. While ON, no held or
freshly pressed gameplay key is read or required; the next eligible readiness
epoch may submit automatically.

Automated Seiton also reads the exact candidate's live status IDs. Guardian's
target-side Covered rows, the Paladin's Phalanx self Hallowed Ground, and Dark
Knight Eventide's Undead Redemption make that actor ineligible. The covering
Paladin, Phalanx's party mitigation, and Guard itself are not blockers. The same
exact status check is repeated for a frozen retry and immediately before the
native action request. Active casts wait; Auto-Seiton never requests cast
cancellation. A metadata mismatch disables this helper; no localized status
text is used at runtime.

The only allowed actions are the metadata-verified base Seiton Tenchu `29515`
and its Unsealed follow-up `29516`; each has a distinct availability epoch.
Purify, PLD Guardian, Smart Recuperate, automatic Guard, AST, RDM, and the SAM
helper tier precede NIN Auto-Seiton in the current request order. NIN Auto-Seiton
precedes VPR Serpentiner Geist, GNB Continuation, reactive counter-CC, Ally
Rescue, Guard-Shukuchi, SCH Critical Strategy, DRK, Monk combo, Emergency
Teleport, pressure Sprint, event Kardia, and event Monk. Active own Guard and
the bounded post-request Guard-propagation state suppress the helper. The
helper never changes the visible hard, soft, or focus target.

After every gate passes, one adjusted-action readiness epoch freezes the exact
target. Known unavailable states wait, and only a clean explicit rejection may
use the shared bounded same-intent retry. If the actor becomes invalid before
any native request, the frozen intent clears without spending the readiness
epoch and a different exact actor may be selected on a later frame. Once a
native request was attempted, the target stays frozen and drift is terminal. A
later genuine adjusted-action transition from base Seiton `29515` to Unsealed
`29516` creates a distinct readiness epoch; a rejected base action can never
substitute the follow-up. The frozen S-slot and actor identity are resolved
before every possible request and the same actor's current HP and protection are
re-read. A value at exactly 50% or higher or an execute-blocking status
invalidates that candidate; this last client-side sample does not prove what HP
the server observes when processing the request. The local request return may be
kept as a bounded aggregate `client-accepted` diagnostic, but it is not proof
that Seiton landed, executed the target, or caused a kill. No target, attempt,
or result history is persisted, transmitted, or uploaded.

## Experimental Monk Earth's Reply helper

If explicitly enabled on PvP Monk, the helper transiently reads the local job,
exact local actor identity, HP, one exact Earth Resonance status `3171` and its
remaining time, and the adjusted result of Riddle of Earth action `29482`.
Current English action/status/proc metadata must independently validate before
the helper can act. It runs in Crystalline Conflict and in explicitly enabled
Wolves' Den test mode; other PvP contexts fail closed.

At the configured low-HP or expiry threshold, and only after Purify, NIN Seiton
or VPR Serpentiner Geist,
reactive counter-CC, Ally Rescue, Guardian, NIN Guard-Shukuchi, SCH Critical Strategy,
Hiebsprung, Smart Recuperate, Emergency Teleport, generic Guard, pressure Sprint, and event Kardia
decline or make no earlier attempt, the
continuous resonance state is marked spent before at most one normal
self-targeted Earth's Reply `29483`
request. The helper never activates Riddle of Earth `29482`, substitutes an
alternate action or target, changes a visible target, queues a custom retry, or
tries again after a false return or exception. The local request return is only
diagnostic and does not prove that the server executed or accepted the effect.

The resonance state, attempt/accepted counters, local actor identity, HP/timer,
and action result remain in memory only. Nothing from this helper is logged as
combat history, written to disk, transmitted, or uploaded.

## Experimental Dark Knight Hiebsprung held-key helper

This independent persisted option is disabled by default and runs only on PvP
Dark Knight in exact Crystalline Conflict. It transiently reads the exact local
identity/job, held gameplay-key ownership, animation lock, own Guard and recent
Guard-propagation state, own Bind status, Hiebsprung / Plunge `29092` metadata/
readiness and its observed cooldown epoch, plus the complete canonical `S1`-`S5`
enemies' exact identity, life/targetable state, HP, Guard status, position, and
native range/line-of-sight result.

Candidates must be at exactly 30% HP or lower, within a strict 10-yalm center-
distance cap, and natively reachable. Lowest exact HP ratio wins, then stable
slot and actor identity. The first usable epoch freezes one target. If accepted,
the same physical hold may remain owned, but a
later request requires an observed cooldown not-ready-to-ready transition. A
reset missed between framework samples is deliberately not inferred. Each ready
epoch uses final revalidation and the common bounded explicit-false retry for
only that frozen direct target, with no selected-target mutation, alternate,
rerank, or replay.

Hiebsprung runs after Purify, AST same-target healing, SAM reactive actions, NIN Seiton, VPR Serpentiner
Geist, GNB Continuation, reactive counter-CC, Ally Rescue, Guardian, NIN Guard-
Shukuchi, SCH, and Dark Arts Shadowbringer. It runs before the safe Shadowbringer
fallback, held Monk combo, Smart Recuperate, Emergency Teleport, generic Guard,
pressure Sprint, event Kardia, and event Monk. Held-
key state, cooldown observations, frozen identity, HP/status/reachability samples, action
result, and aggregate diagnostics remain bounded in memory and are not stored as
combat/key history, transmitted, or uploaded. Client acceptance does not prove
server execution or damage.

## Saved settings

Only local configuration is saved through Dalamud. This includes display and
layout options, pressure window/appearance and context toggles, warning opacity,
MCH/DRG/SMN/Chiten danger-warning size/sound selection, opponent-LB strip visibility,
the high-pressure warning/native-sound/Sprint
opt-ins and sound selection, the separate Smart Tab and Smart Action opt-ins,
the shared Near Assist/Near Help/Far Help opt-in, Near Assist search/preferences,
the Near Help incoming-pressure preference,
target-highlight settings, the separate Auto Low-MP Focus Target opt-in, the
  Purify automatic/legacy-held/per-debuff controls, the Ally Rescue master/held-key
opt-ins, the separate Bard Paean pressure-redirect
opt-in, isolation warning/scale, the reactive Purify-to-Guard master/held-key/
trigger opt-ins, the separate automatic/held Smart Recuperate and Emergency Teleport opt-ins
plus the six local Emergency thresholds, the independent PLD
  Guardian master/held-key and Quick Chat/Bind-pair opt-ins, WHM/BRD/NIN/PLD/RDM/
  BLM/SAM reactive counter-CC master/held-key/post-Purify/post-Guard/per-startup-
  trigger opt-ins,
  optional PLD Intervene, RDM Resolution, RDM Vice of Thorns, BLM Frost Star,
  SAM Soten/Mineuchi and SAM Zantetsuken opt-ins plus their local maximum ranges,
  the impact-calibration revision and at most 24 numeric delay/distance samples
  per supported counter action,
the team-visible Attack1 marker
opt-in, resource-aura surfaces/thresholds/appearance, enemy LB nameplates and
scale, self/ally LB activation messages, optional ally names and ally LB damage,
  local MP sounds and their two built-in sound IDs, the Auto-Guard card/sound
  opt-ins and local sound ID, the local What's New
acknowledgement version, the Monk
Earth's Reply master/triggers/thresholds,
the separate NIN Guard-Shukuchi held-key and automatic NIN Seiton opt-ins, the Scholar
Critical Strategy, Astrologian held Near Help, and RDM fresh-Guard opt-ins, the
RDM own-HP/MP thresholds, and the camera-back job dash command opt-in that the
DNC-only `/seitonenavant` movement-direction macro also reuses,
  the public-CC medicine-kit countdown/beacon toggles and overlay scale,
  the Sage accepted-Eukrasia Smart Kardia opt-in, the Viper Serpentiner-Geist,
  GNB Continuation, and Monk combo held-key opt-ins, the DRK Shadowbringer,
  nested Blackblood-preservation, and separate DRK Hiebsprung held-key options,
  the generic held-helper cast-cancellation test and independent automatic
  basic-shot cast-cancellation opt-ins, and the CC-immunity-brake master plus exact per-job/
per-action selections. Retired Combat Frames properties remain only as legacy
configuration compatibility fields; no current runtime or settings page reads
them to draw frames, change targets, or publish mouseover actors.

The Wolves' Den rotation panel calculates the published arena order from local
UTC time, the current PvP / territory flags, and an optional saved whole-map
phase correction. Its compact current card and optional expanded deck request
the same seven reviewed duty-artwork icons from the local game installation; it
does not download, copy, or upload those images.

When both Seiton Sense and the separate local W/L capture option are enabled,
the plugin may confirm future public Crystalline Conflict outcomes independently
of panel visibility from the local Patch 7.5 post-match result payload. The hook
freezes the exact PvP-excluding-Wolves'-Den flag, territory, and local Content ID
with that payload; the framework drain must observe the same live values or it
records nothing. Confirmation requires result `1` or `2`, duration from 10 through 1,800 seconds, one known public-CC territory, exactly ten unique nonzero Content IDs, known jobs, five players on each valid team, and exactly one match for the local nonzero Content ID. There is no character-name fallback. Wolves' Den,
custom or unsupported territories, spectator/ambiguous identity, incomplete or
duplicate participants, unknown teams/jobs, invalid duration/result, and an
unavailable hook all record nothing.

Confirmed totals are saved locally in `cc-map-stats.json`. The file contains a
random salt, an HMAC-SHA256 per-character key, per-map win/loss totals, and at
most 32 recent HMAC-SHA256 match fingerprints per character for duplicate
suppression. It contains no raw Content ID, character name, world, roster, queue,
rating, or per-player scoreboard values. Saving uses a same-directory temporary
file and atomic replacement. If the existing document is malformed, Seiton
Sense disables reading and writing it rather than guessing or overwriting it.
The settings clear control atomically replaces only this file with a fresh empty
document, also allowing an explicitly requested reset to recover malformed
storage; a failed reset preserves the existing file and reports failure in the
UI. There is no historical backfill, upload, telemetry, account, service query,
or network request; maps remain `NO DATA` until an exact future local match is
confirmed.

If the separate instant-leave option is enabled, the same already-confirmed
public-CC result may arm one transient in-memory leave intent even when local
W/L recording is disabled. W/L persistence is attempted first when it is
enabled. For at most 30 seconds, the intent retains only the exact territory,
local Content ID, monotonic result/expiry times, and one spent/requested state.
It cancels if the plugin or option is disabled, the live PvP/territory/identity
changes, the result expires, or the native leave boundary cannot be queried. A
transient `BetweenAreas` frame alone keeps the exact intent waiting because the
client can raise it on the confirmed result boundary before leave becomes ready.
On the first later stable native-ready frame it reserves the
intent and sends one normal non-forced leave request; the void request is never
retried. No leave history is saved or uploaded, no UI confirmation is clicked,
and the feature does not queue a match. Wolves' Den, custom CC, Frontline, and
Rival Wings cannot arm it.

After that match context is spent, only an authoritative nonzero change to a
different territory or the next exact public-CC duty-start event clears the old
latch. The duty-start path deliberately accepts the same arena so consecutive
matches on one map can rearm even when loading suppresses every framework update.
Zero-valued, non-PvP, non-public, or identity-unknown lifecycle signals remain
inert. These lifecycle events do not call the leave function themselves.

The PvP range helper reads only the local player's current job, position, and
hitbox radius plus the game's world-to-screen projection. It draws two fixed
sampled rings and does not scan other actors, retain movement history, raycast
terrain, change a target, or issue/suppress an action.

Configuration schema 50 is current. It replaces held-only chase behavior with
one release-independent tap-to-land reservation (0-3000 ms, 2200 ms default),
adds default-on exact active-Sprint repeat protection, and adds a separate
default-off 3000-5000 ms idle Smart Sprint option. Smart Sprint retains only a
monotonic action-bar activity token and its local time baseline; rejected
action-bar requests still count, while movement, camera, and target input are
not stored as activity. Schema 49 enabled the local
adaptive response clock, sampled readiness-edge wakeups, unchanged-queue
critical recovery, and the original held chase buffer. The latency-response helper defaults on for fresh/
reset configurations while an existing opt-out is preserved on upgrade. These
features are memory-only; there is no latency profiler, traffic capture, upload,
position prediction, range extension, animation-lock write, or direct native-
queue edit. Schema 48 added the separate default-off instant public-CC leave
setting without changing local W/L capture or any action-helper opt-in. Schema 47 added default-on, read-only SMN/Chiten
danger warnings and separate experimental opponent LB bars that remain off by
default pending live layout validation, while retaining the separate automatic
basic-shot cast-cancel permission as default-off for every upgrade and Reset
Defaults without changing either automatic helper opt-in or the independent
generic held-helper toggle. Schema 45 added separate automatic Purify and
Recuperate settings and initialized both off for every upgrade and Reset Defaults.
Schema 44 keeps the RDM fresh-Guard engage and `/seitonbw` command off for every
upgrade while initializing the RDM 80% HP / 50% MP defaults, and explicitly
initializes local CC map W/L capture to on for fresh, upgraded, and Reset Defaults
configurations. Schema 43 keeps the Auto Shadowbringer master off and adds its
default-on nested Blackblood-preservation option plus local rotation card
presentation. Schema 42 adds the local rotation-panel and range-helper
appearance settings without changing any action or targeting opt-in.
Schema 41 adds the default-off Astrologian held Near Help option without enabling
it for upgrades, fresh installs, or Reset Defaults. Schema 40 adds the local generic one-shot smart action-buffer settings,
movable learning-window settings, and default-off native
standard-keyboard-hotbar Turbo settings. The buffer is available in PvE, PvP,
and Wolves' Den without uploading input, action, target, position, or timing
data. Schema 39 adds the default-off 100-1500 ms PvP latency-response budget
and a legacy read-only external critical-utility claim. The integrated buffer
and Turbo require no companion plugin. Schema 38 adds RDM Vice of Thorns and BLM Frost Star as default-off protection-end options, starts calibration revision
1, and clears unversioned timing samples. GNB Continuation, DRK Shadowbringer,
Monk combo, SAM counter-CC/Zantetsuken, PLD Intervene, RDM Resolution, Vice of
Thorns, and Frost Star remain off for every upgrade, fresh install, and Reset
Defaults. Schema 36
adds local Auto-Guard card/sound defaults without enabling Auto-Guard itself.
The schema-35 migration initializes Emergency Teleport off at 50% HP, 4,000 MP,
one direct focuser, 10-yalm minimum
travel, 10-yalm destination radius, and zero nearby enemies. The historical
schema-34 migration still forces Viper Serpentiner Geist off. The historical
schema-33 migration still leaves Smart Tab
off while preserving an older explicitly enabled shared macro-helper opt-in as
the separate Smart Action option. Smart Tab, Smart Action, Viper, and Emergency
Teleport are all off for fresh and reset configurations;
unrelated existing opt-ins are preserved.

The integrated input path reads only the local standard-keyboard-hotbar binding,
raw press/release state, exact slot identity, and the local action/target/context
snapshot needed to prove one bounded attempt. The generic buffer stores one
immutable in-memory action tuple until it succeeds, is cancelled, or expires;
the tap-to-land lane is limited to the same physically pressed direct action,
the exact lease/generation-backed Smart Action visible-target fallback, or the
target-independent exact Smart Action `S1`-`S5` winner in Crystalline Conflict.
It waits only for the frozen hostile actor's native range/line-of-sight result.
Releasing the key does not cancel it. Expiry, new action input, Guard, identity/
action/context/safety drift, or an unsafe queue/cast boundary cancels it before
a later call. Supported visible-target single-target casts also require the
visible hard target to remain the frozen actor, so a delayed cast cannot turn
toward an actor the user switched away from. A ranked Smart Action Chase target
is deliberately independent of an unrelated visible hard target and never
writes that target. Turbo retains one current held-slot
owner. A newer physical input replaces the old buffer/owner. No input history,
action history, target history, timing data,
or learning-window state is uploaded. The learning window is a read-only view of
the current in-memory key/slot, action, and countdown. Neither path
writes position, range, animation lock, cast state, or a visible target.

For buffer and explicit directional-self-action compatibility, Seiton reads
Dalamud's in-memory installed-plugin list, the audited ReAction action-mutation
settings, its configured action-selector IDs/adjusted-ID flags, and MOAction's
published retargeted-action IPC list. The broad buffer profile is refreshed on
plugin-list changes and at a bounded five-second cadence, then checked when an
eligible buffer arms and immediately before its sole replay. A directional dash
also re-reads the selector signature immediately before its one call so
unrelated stacks do not become a global block. It does not inspect plugin files,
retain historical profiles, or upload compatibility data. Unknown or unreadable
compatibility state disables only the affected buffer/command opportunity;
native input and the separate Turbo path remain unchanged.

Historical v0.30.0.0 baseline: schema 32 forced the NIN Guard-Shukuchi held-key
option off for upgrading configurations and left it off for fresh
and Reset Defaults configurations because it initiates an action and may set the
exact hard target after client acceptance. Forward Panic Shukuchi remains
command-only and saves no dedicated option; it uses the global plugin enable and
existing Wolves' Den testing option. Schema 44 adds the separate default-off
backward-command opt-in. The generic held-action cast-cancellation test and the
schema-46 automatic basic-shot permission remain explicitly off for fresh, reset,
and migrated configurations. An older explicitly enabled fresh-edge NIN Seiton option still
traverses schema 29, migrates to the retained compatibility-named opt-in, and
clears the obsolete field. The retained opt-in now arms fully automatic Seiton;
it does not restore a held-key requirement. Every other existing master/helper choice is
preserved. Older configurations still traverse the earlier migrations first,
including schema 28's default-off post-Guard migration. Schema 32 forces the
retired Combat Frames master off, maps its optional name preference to the ally
LB feed, enables the replacement LB surfaces, and initializes local MP sounds to
built-in IDs 4 and 6. Fresh and reset configurations keep NIN Guard-Shukuchi,
Smart Recuperate, Emergency Teleport, Hiebsprung, Smart Action/other macro
helpers, and all other
action-helper masters off; post-Guard defaults on only behind the disabled
reactive-counter master. The replacement LB and local-MP presentation options
default on but neither submits an action nor changes a target. Apart from the
bounded action-keyed numeric impact-calibration samples described above,
configuration does not save observed actors, targets,
combat events, status timers, key state, marker ownership, pending helper state,
NIN Guard-Shukuchi actor/location/cooldown epochs, Panic Shukuchi ground
destinations/camera observations, RDM Guard episodes/frozen intents, Viper
carrier exposures/frozen action-actor-context-key intents,
Emergency danger/destination episodes, ActionEffect confirmation state, or
in-memory counters.

The integrated focus preset does not read, import, modify, or delete standalone
Super Focus Glow configuration. Likewise, Seiton Sense does not modify the
standalone HOWMANY, CCImmunityWatch, or NearAssist configuration.

Like all third-party FFXIV modifications, use is at your own risk. Seiton Sense
is distributed through a custom repository, not Dalamud's official plugin
repository.
