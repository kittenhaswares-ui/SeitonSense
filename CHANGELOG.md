# Changelog

## 0.42.0.1

- Fixed instant leave after the first successful match. Both reported match
  results reached the exact result validator, but a fast loading transition
  could skip every framework frame that previously cleared the spent one-shot
  latch. A different nonzero `TerritoryChanged` event now closes that old context, and
  the next exact public-CC `DutyStarted` event is a same-map backstop. Zero,
  non-PvP, non-public, and identity-unknown lifecycle signals remain inert.
- Extended the exact native leave-ready polling window from 10 to 30 seconds.
  The result, public territory, local identity, area-transition, option, and
  native-boundary checks still run on every frame. Each result reserves at most
  one normal `LeaveCurrentContent(false)` request; the void call is never
  retried and no match is queued.
- Added concise lifecycle diagnostics for arm, duplicate, request, cancellation,
  exit, and reset events. This distinguishes a future native-readiness timeout
  from a stale match latch without continuously scanning game files.
- Smart Action, Near Assist, and Near Help no longer invisibly redirect actions
  with a proven adjusted or base cast time. A hidden macro carrier is consumed
  and suppressed so the following visible `<t>` fallback remains vanilla; a
  direct cast already authored on the exact current hard target passes through
  unchanged. Instant actions retain the existing smart redirect behavior. This
  avoids delayed native auto-facing toward a hidden target after a manual target
  switch without adding a rotation or FaceTarget hook.
- Configuration schema remains `48`. Source build, all `570` Core tests, safety,
  package parity, and release verification are automated. Consecutive live
  match exit and cast-facing behavior remain current-client validation points.

## 0.42.0.0

- Added a separate default-off **instant leave after public Crystalline
  Conflict** option. It shares the existing post-match hook and arms only after
  the complete result passes the same public-territory, result, duration,
  ten-unique-player, 5v5-team, known-job, and exact local-Content-ID proof used
  by the local map W/L feature. Wolves' Den, custom CC, Frontline, Rival Wings,
  spectators, incomplete payloads, and identity/context drift fail closed.
- The result hook still calls the game's original function first and drains on
  the framework thread. If local W/L capture is enabled, persistence is
  attempted before instant leave is signalled; instant leave remains available
  independently when W/L recording is disabled or its local storage is
  unavailable.
- A confirmed result creates one in-memory intent lasting at most ten seconds.
  It continuously requires the same public territory and local identity, no
  area transition, and FFXIV's native `CanLeaveCurrentContent()` result. At the
  first ready boundary it reserves the intent, calls
  `LeaveCurrentContent(false)` exactly once, and never retries that void
  request. It does not press UI callbacks, use a chat command, force departure,
  or automatically queue another match.
- Configuration schema is `48`. Source build, all `570` Core tests, safety,
  package parity, and release verification are automated. The current-client
  result-to-loading transition and practical queue-time improvement remain live
  in-game validation boundaries.

## 0.41.0.0

- Automatic Purify and Recuperate now retain their exact frozen episode through
  temporary native unavailability instead of retiring after one unlucky frame.
  Their bounded retry/wait path rechecks the original status, HP/MP, context,
  readiness, queue, cast, and safety gates before every request; Purify keeps
  first priority and Recuperate follows without starving the remaining helpers.
- The default-off high-pressure Stun Auto-Guard path is now keyless. It waits
  for the exact Purify/Resilience transition, freezes one two-second lease, and
  allows one readiness-proven retry inside that original lease. A client return
  is provisional: the card, sound, action suppression, and two-second Guard-
  reuse protection arm only after exact live Guard `3054`/`3673` confirmation.
  Clean rejection retracts the provisional generation, so it cannot create a
  phantom Guard, block manual Guard, or suppress higher-priority recovery.
- Added exact enemy danger warnings for SAM Chiten status `1240` and SMN
  Bahamut/Phoenix LB action/icon/status pairs. Chiten receives a large
  nameplate emblem/countdown and `DO NOT HIT THE SAMURAI` card; both warning
  families reuse the bounded danger lane and one-shot built-in sound.
- Added experimental opponent LB-ready bars above the pressure display. This
  read-only option is **off by default** pending current-client layout testing;
  when enabled it publishes only a complete stable `S1`-`S5` native GaugeBar
  set that matches the same-frame local gauge scale, and hides all bars on any
  identity, hierarchy, stability, freshness, or scale uncertainty.
- Configuration schema is `47`. Source build, all `562` Core tests, safety,
  package parity, and release verification are automated. Current-client action
  acceptance, exact timing, warning placement/sound, and the experimental
  opponent-gauge layout remain live in-game validation boundaries.

## 0.40.0.2

- Fixed the intermittent ranged **Smart Action** no-op where `/smartaction`
  refused to arm unless the live `S1` actor was already usable. Arming is now
  target-independent: it freezes only local identity, territory, and the
  existing 750-ms lifetime. Exact `S1`-`S5` selection happens only when the
  next eligible harmful action actually reaches the hook, so neither a live
  `S1` carrier nor a selected hard target is a plugin-side prerequisite.
- Direct single-target actions no longer require the unrelated hostile object
  table to match every current S-slot before they can select a target. They
  still require one unique live canonical candidate, exact protection status,
  native range/line of sight, and the unchanged frozen-target recheck directly
  before the sole original action call.
- Target-centered circles, unsupported AoEs, and unknown attack shapes retain
  the complete hostile snapshot comparison. Chiten, Guard, Covered, Paladin LB,
  and Dark Knight LB protection remain fail-closed, including the exact
  action-specific Guard-bypass rule. There is still no visible target change,
  generated action, rerank, alternate, or retry.
- Configuration schema remains `46`. Source build, all `553` Core tests,
  safety, package parity, and release verification are automated. Whether the
  current client emits the authored `<e1>` action call when `S1` is unusable—
  especially with no selected target—and final action acceptance remain live
  in-game validation boundaries.

## 0.40.0.1

- Added one separate default-off **automatic basic-shot cast cancellation**
  permission shared only by Automatic Purify and Automatic Recuperate. Existing
  automatic helper opt-ins are preserved across the upgrade, but schema 46
  initializes this new side effect off for every upgraded, fresh, and Reset
  Defaults configuration. The generic held-helper cast-cancel toggle remains
  independent and cannot enable or widen either automatic path.
- The automatic permission admits only the exact current **BRD job 23 / Powerful
  Shot `29391`** or **MCH job 31 / Blast Charge `29402`** pair. Startup English
  PvP metadata must verify the action row, and the live local job, active cast
  ID, and adjusted raw-action identity must still match that same row. Missing,
  drifted, transformed, cross-job, or otherwise uncertain evidence waits for the
  cast to end; it never falls back to another action. Instant MCH Blazing Shot
  `41468` and the legacy Heat Blast row `29403` are explicitly excluded.
- A permitted cancellation remains once per observed cast epoch and owns its
  framework frame. Purify or Recuperate is never requested in that frame; only
  a later clear-cast frame may repeat the complete automatic helper preflight.
  The void native call reports only `requested`, not confirmed.
- Configuration schema is `46`. Source build, all `551` Core tests, safety,
  package parity, and release verification are automated; current-client BRD/MCH
  cast cancellation and final Purify/Recuperate acceptance remain live-validation
  boundaries.

## 0.40.0.0

- Added a separate default-off **Automatic Purify** mode. An individually
  enabled, actually present Stun, Heavy, Bind, Silence, Deep Freeze, or Miracle
  of Nature can now freeze one exact self/status episode and request Purify
  `29056` without a fresh or held gameplay key. Ready Purify keeps absolute
  scheduler priority; cooldown/resource shortage does not starve lower helpers.
  If a cast is the sole remaining block, Auto Purify may request one native cast
  cancellation for that cast epoch independently of the generic held-helper
  cancel toggle, then revalidates and sends Purify only on a later clear frame.
- Added a separate default-off **Automatic Recuperate** mode alongside the
  unchanged held-key helper. At the existing inclusive 16,000-missing-HP and
  2,000-MP boundary it freezes the same exact self/context intent without a key.
  It deliberately does not cancel casts: it waits, rechecks HP/MP and every
  safety gate, and shares the existing accepted-cooldown latch so automatic and
  held modes cannot duplicate one Recuperate `29711` episode.
- Both automatic paths remain blocked by own Guard and its propagation latch,
  text input, invalid metadata/context/identity, and NIN Shukuchi Hidden. The NIN
  automatic gate fails closed if job or Hidden metadata is uncertain. Legacy
  physical-key Purify and Smart Recuperate remain available as independent
  opt-ins. When both consent modes are enabled for one opportunity, automatic
  consent wins deterministically and never retires the held-key generation.
- Configuration schema is `45`. Source build, all `549` Core tests, safety, package parity,
  and release verification are automated; current-client status timing, native
  cast cancellation, and final in-game action acceptance remain live-validation
  boundaries.

## 0.39.0.2

- Fixed the default-off AST held Near Help helper being permanently disabled by
  its own metadata gate. Harmonischer Orbis / Aspected Benefic `29243` now
  validates as the player action, while the internal adjusted repeat `29247`
  correctly validates as a non-player row. If a raw Double Cast `29245` charge
  was free before an accepted Orbis, the helper waits at least one framework
  frame, proves `29245 -> 29247`, and invokes raw `29245` on the same frozen
  ally. It never dispatches internal row `29247` directly or adopts another
  Double Cast form.
- Fixed non-NIN `/seitonbw` going forward when ReAction's camera-relative dash
  option rewrote character facing during the same native action call. A lazy,
  same-thread local-actor boundary preserves the frozen screen-back heading
  only for that one reviewed dash and never writes camera or target state. The
  existing audited compatibility check admits camera-relative-only ReAction but
  refuses ReAction Auto Target/Action Stacks or MOAction ownership before the
  exact action call.
- Restored the compact Wolves' Den rotation view: one large current-map card is
  visible by default, with a full-width control to expand or hide the next six
  maps. The current card slides automatically at hourly rollover; the expanded
  seven-card deck retains its reorder animation, enlarged text, artwork, and
  per-character W/L display.
- Retained configuration schema `44`. The warning-free Release build, all `541`
  Core tests, safety contract, package parity, and release verification pass.
  Current-client AST acceptance, ReAction coexistence, dash direction, and final
  in-game visual behavior remain separate live-validation boundaries.

## 0.39.0.1

- Expanded the default-off `/seitonbw` macro from NIN Shukuchi to the closed
  current PvP self-dash catalog: AST Epicycle `41506`, DNC En Avant `29430`,
  DRG Elusive Jump `29494`, RPR Hell's Ingress `29550`, and PCT Smudge `39210`.
  NIN retains its exact 19.5-yalm ground-location call. The other jobs align
  only local character facing immediately before one exact self-action so the
  native movement travels toward camera screen-back; the camera and targets are
  never changed. Per-action metadata, unchanged adjusted ID, charge/resources,
  standard camera state, and a clean immediate native boundary are required.
  There is no wait, queue, retry, rerank, target search, transformed follow-up,
  or alternate action. The same-thread Auto-Guard exception is scoped to only
  the exact matching location or standard-action boundary.
- Made the Wolves' Den CC rotation deck materially easier to read at 1.0x: panel
  width increases from 520 to 610 pixels, cards from 66 to 84 pixels high, map
  names to 17 pixels, and countdown/W-L text to roughly 14-15 pixels. Artwork
  and the statistics column were widened to keep the larger text separated.
- Retained configuration schema `44`. Source build, 540 Core tests, safety,
  package parity, and release verification are automated; current-client dash
  direction/acceptance and final in-game visual sizing remain live validation.

## 0.39.0.0

- Added the default-off `/seitonbw` NIN macro command. It reuses Panic
  Shukuchi's immediate fail-closed safety/action boundary but computes one exact
  19.5-yalm terrain point in the current normal gameplay camera's screen-back
  direction. Standard first- and third-person modes are handled explicitly;
  unavailable, non-finite, event/cutscene, spectator, aiming, and lock-on camera
  state refuses the command. It rotates neither camera nor character, changes no
  target, and has no queue, pending state, fallback, or retry.
- Added a default-off RDM held-key fresh-Guard engage. One exact enemy Guard
  absent-to-present edge can authorize Corps-a-corps `29699` only during that
  Guard's first second, while exact Riposte `41488` starter readiness and the
  configurable inclusive own HP/MP thresholds (80% / 50% by default) agree. The
  exact actor, episode, action, context, and physical key stay frozen; only a
  client-accepted Corps-a-corps may hard-target that actor once, and no melee
  follow-up, alternate, or rerank is generated. Wolves' Den testing uses only
  the exact current target.
- Replaced the redundant Current/Next summary and expansion switch in the
  Wolves' Den rotation panel with one larger, always-visible seven-card deck.
  Added fail-closed local per-character W/L beside every map card for future
  exact public CC results. Ambiguous/custom/duplicate results record nothing;
  the atomic local file stores salted HMAC identifiers and bounded hashed
  deduplication entries, never names or raw Content IDs, and no network request
  is made. Configuration schema is `44`; current-patch camera, action,
  result-packet, and visual behavior remain separate live-validation boundaries.

## 0.38.0.0

- Added a shared 1.8-second cadence to both held Auto Shadowbringer paths. A
  continuously safe HP/pressure state can now open exactly one later HP-cost
  generation when that cadence ends, instead of remaining spent until HP or
  pressure happens to change. Dark Arts still wins and ignores the configured
  HP/pressure thresholds, but observes the same cadence.
- Added a default-on **Preserve Blackblood** sub-option. Exact status `3033`
  blocks both paths until it is consumed or expires. A confirmed or ambiguous
  automatic boundary that misses the complete short status lifecycle now uses
  a 1.5-second propagation grace plus one later distinct absent sample instead
  of deadlocking until manual Shadowbringer; ambiguous calls still require the
  frozen physical key to be released. Disabling this sub-option removes only
  the Blackblood wait, not the shared 1.8-second cadence.
- Auto Shadowbringer in exact Crystalline Conflict now uses the existing held
  Smart Action target policy without requiring the `/smartaction` macro toggle.
  It changes no visible target and freezes the selected exact actor; later
  invalidation cancels instead of reranking. Its line-AoE protection remains
  fail-closed under the existing Smart Action policy. Wolves' Den testing stays
  restricted to the exact current `<t>` duel opponent or striking dummy and
  treats unavailable CC team-pressure telemetry as known zero for that test
  context; HP, range, line of sight, cadence, and Blackblood gates remain.
- The expanded Wolves' Den rotation panel now shows the complete seven-map
  current-to-next deck with local FFXIV duty artwork. On a rotation change, the
  cards reorder over `0.65` seconds. The artwork is loaded from the local game
  installation, with no download or network request. Configuration schema is
  `43`; live current-patch action behavior and visual timing remain separate
  in-game validation boundaries.

## 0.37.0.0

- Added a movable, clickable Wolves' Den Pier panel for the Patch 7.5
  Crystalline Conflict arena rotation. It displays the current map, live
  countdown, and next map; clicking the current map expands the complete
  seven-map order and persistent local `<` / `>` phase calibration. The official
  order and interval are paired with a bundled public-community phase reference,
  not mislabeled as an official epoch. It runs offline, only in exact territory
  `250`, and has lock, scale, background, and reset-position controls.
- Added a read-only PvP world-range helper around the local player. The inner
  ring marks nominal 5-yalm melee reach; the outer ring uses the current job's
  furthest reviewed hostile non-LB reach, including hostile gap closers. All 21
  PvP-enabled jobs fail closed through one exact catalog. Labels, colors,
  opacity, line width, and background/foreground placement are configurable.
- Both overlays are visual-only: they do not scan a player list, select a target,
  issue or suppress an action, inspect queue registrations, raycast terrain, or
  make a network request. The renderer performs a fixed 96 world projections per
  frame at most and breaks behind-camera/discontinuous segments. Configuration
  schema is `42`; the warning-free Release build and all `520` Core tests pass.
  Live current-patch visual placement and map-phase confirmation remain separate
  in-game validation boundaries.

## 0.36.0.1

- Changed the default-off Viper held Serpentiner-Geist follow-ups in exact
  Crystalline Conflict from current-target-only dispatch to the existing Smart
  Action target policy. The concrete exposed action now ranks only reachable
  canonical enemies by reach tier, HP, fresh team pressure, unavailable Guard,
  trusted MP, and stable slot order, with the exact current target retained only
  as a last fallback after the identical full protection/range/line-of-sight
  validation. No visible target is changed. Wolves' Den remains exact `<t>`.
- Once Viper selects either a Smart winner or the safe fallback, its action,
  actor, context, key, carrier generation, and native identities remain frozen.
  Later death, ambiguity, protection, range, or line-of-sight drift cancels and
  spends that exact carrier exposure; the same hold cannot rerank or jump to an
  alternate enemy.
- Added an enemy DRG Limit Break danger warning from the exact `Sky High`
  activation `29497`, so the top-center icon/card and one-shot selectable FFXIV
  sound begin at takeoff rather than waiting for `Sky Shatter` impact. Only the
  live exact caster status `3180` may extend the warning with a countdown;
  `3181`, landing damage, gauge estimates, and ambiguous actors do not. The
  existing personal-warning/MCH danger controls are reused, so configuration
  schema remains `41`. The warning-free Release build, source safety contract,
  package checks, and all `517` Core tests pass. Live current-patch behavior
  remains a separate in-game validation boundary.

## 0.36.0.0

- Added a separate default-off Astrologian held-key helper that applies the
  exact `/nearhelp` friendly selection to living self/party players at or below
  60% HP, then requests Harmonischer Orbis / Aspected Benefic `29243` directly
  without changing the visible target. It supports exact Crystalline Conflict
  and the existing opt-in Wolves' Den test context.
- If Double Cast `29245` was already locally available before a client-accepted
  Orbis, the same frozen ally may receive the repeat on a later scheduler frame.
  The helper invokes raw carrier `29245` only while it resolves exactly to
  adjusted Orbis `29247`. The follow-up never reranks after the first
  heal, adopts another Double Cast form, changes targets, or shares an action
  frame with the base heal.
- Purify remains absolute scheduler and cast-cancel priority. The AST sequence
  uses exact metadata, actor/key/context freeze, distinct observed Orbis charge
  epochs, native readiness/range/line-of-sight revalidation, and the common
  bounded same-intent retry policy. Active or still-propagating own Guard is
  checked again at the final action-hook boundary and immediately before any
  optional native cast cancellation, so neither AST action may break Guard.
  Configuration schema is `41`; the warning-
  free Release build, source safety contract, package checks, and all `516` Core
  tests pass. Live current-patch behavior remains a separate in-game validation
  boundary.

## 0.35.0.3

- Fixed `/smartaction` for PvP attacks that explicitly ignore Guard. Seiton now
  resolves the adjusted action first and admits a Guarded target only when that
  exact current English `ActionTransient` description contains the canonical
  Guard-ignore sentence. This covers transformed combo steps without a brittle
  job or action-ID allowlist; missing or drifted metadata remains blocked.
- The exception removes only Guard. Protection state is now a bit mask, so
  Chiten, Covered, Paladin LB Hallowed Ground, Dark Knight LB Undead Redemption,
  and mixed Guard-plus-protection actors remain blocked regardless of status
  order. The same rule applies to direct attacks, target-centered AoE, initial
  selection, frozen-target validation, authored fallback, and buffer replay.
- Configuration schema remains `40`; the warning-free Release build, source
  safety contract, package checks, and all `511` Core tests pass. Live current-
  patch behavior remains a separate in-game validation boundary.

## 0.35.0.2

- Fixed the v0.35.0.1 `/panicshu` regression. Panic Shukuchi and the NIN
  Guard-Shukuchi helper once again use the original exact Action/ActionTransient
  metadata predicate; supplemental Hidden-status discovery can no longer turn
  either feature into a permanent `Metadata mismatch`.
- Kept Ninja stealth protection independent: every exact English `Hidden`
  status row is collected once at startup, and runtime Auto-Purify/Auto-Recup
  checks compare only those language-independent IDs. The catalog is checked at
  scheduling, cast-cancel, and final native boundaries without changing manual
  Purify or Recuperate.
- The v0.35.0.1 Turbo/Latest Input, Viper Wolves' Den, diagnostics, and Scholar
  removal changes remain intact. Configuration schema stays `40`; all `510`
  Core tests and the warning-free Release build pass.

## 0.35.0.1

- Fixed native Hotbar Turbo so a due repeat is consumed by XIV's normal hotbar
  scan. The game can now own the visible slot press and Seiton's Latest Input;
  an unconsumed scan is diagnostic-only and never bypasses the hotbar with a
  hidden direct action call.
- Fixed Viper Serpent's Tail in Wolves' Den duels by resolving the exact live
  hostile current hard target directly. The separately verified striking dummy
  remains supported, and Crystalline Conflict still uses exact e1-e5 actors.
- Automatic Purify and Recuperate now fail closed while Ninja's metadata-
  verified Shukuchi Hidden status is active. The status is checked both before
  scheduler ownership/cast cancellation and again immediately before the sole
  native action call; manual actions and other Ninja helpers are unaffected.
- Removed the nonfunctional Scholar Biolysis/Adloquium/Deployment Tactics held
  workflow completely. Scholar Critical Strategy remains unchanged. Added
  compact buffer/Turbo counters to `/seiton debug` and one unload summary; no
  live file scanning or per-frame log spam was added.
- Configuration schema remains `40`; all `510` Core tests, the warning-free
  Release build, source safety contract, and package checks pass. Current-patch
  live in-game behavior remains a separate validation boundary.

## 0.35.0.0

- Integrated a generic one-shot action buffer directly into Seiton Sense. A
  fresh physical press on a standard keyboard hotbar may retain one exact
  direct instant action for 1,000 ms by default, adjustable from 100-1,500 ms,
  when the client rejects it only for a short local recast or animation lock.
  The post-Smart-Action target, resolved action, slot, local actor, territory,
  and instance remain immutable; at most one later native request is made.
  Casts, ground-targeted or movement actions, macros, mouse clicks, controller/
  cross-hotbar input, resource failures, and ambiguous results are excluded.
- Added a separate default-off native Hotbar Turbo. Holding a certified key
  repeats only its current standard-hotbar slot; the newest physical input owns
  repetition, no catch-up burst is emitted, and native slot/macro semantics stay
  with the game. Disabling or reconfiguring Turbo, including enabling its
  outside-combat test scope, requires a real release and new press. A movable,
  lockable learning panel shows the certified key, slot, action, held state, and
  live buffer countdown.
- Connected both paths to Seiton's Purify-first held scheduler. Critical
  utilities pause final buffered dispatch and Turbo for that framework frame
  without consuming their intent. The optional PvP latency-response budget may
  extend only proven clean-false held retries; it does not fabricate acceptance
  or broaden action, target, range, or context ownership.
- Preserved Smart Action safety across delayed replay: the requested/resolved
  action and target must still match, then Chiten, Guard, Covered, Paladin LB,
  Dark Knight LB, and supported target-circle protection are rebuilt directly
  before the sole native call. Audited ReAction/MOAction conflicts fail closed
  for only that buffer opportunity; ordinary native input and Turbo are not
  blocked. Compatibility is checked in memory without scanning plugin files.
- Configuration schema is now `40`; all `518` Core tests, the warning-free
  Release build, package safety contract, and artifact parity checks pass.
  Current-patch live in-game behavior remains a separate validation boundary.

## 0.34.0.4

- Smart Action now treats protection safety as part of target replacement. A
  Samurai with active Chiten, any enemy in Guard, a Covered target, Paladin LB
  Hallowed Ground, and Dark Knight LB Undead Redemption are excluded before
  the usual reach -> HP -> pressure -> Guard-CD -> MP ranking. If the best
  ranked actor is protected, the next safe Smart Target is selected instead.
- Target-centered circle attacks also reject any candidate whose exact current
  effect radius plus the protected actor's hitbox would hit one of those
  enemies. Other unreviewed AoE shapes are not redirected while any protected
  enemy is present. The frozen target and complete protection snapshot are
  rebuilt immediately before the sole native action call, which receives the
  exact canonical target ID rather than a mutable selected-target carrier.
- Closed the authored `<t>` fallback gap with a post-claim 750-ms lease bound
  to the exact semantic resolved action, its authored raw action identity,
  local actor, and territory. The arm token must still be strictly live when
  that exact action is claimed; an expired arm is not consumed and remains on
  the vanilla path. Equivalent `Action`/
  `PvPAction` carriers for that same resolved skill remain inspected; adjusted-
  action drift and an unresolved exact raw fallback are blocked.
  Native rejection keeps the same action under protection inspection. Unrelated
  resolved actions pass unchanged; if resolution is unavailable and aliases
  therefore cannot be disproved, supported macro calls stay blocked only for
  that fresh at-most-750-ms safety lease. An accepted safe replacement/fallback
  releases the lease. Chiten metadata drift conservatively excludes every
  Samurai or unknown-job actor. The Guard/Covered/LB proof is status-only, so
  unrelated NIN or Guard action-balance changes do not disable Smart Action.
- Configuration schema remains `38`; all `461` Core tests pass.

## 0.34.0.3

- Fixed **Smart Tab** admitting enemies behind walls. Every geometrically
  eligible enemy now also needs FFXIV's native range/line-of-sight result from a
  metadata-verified hostile spatial probe. The frozen target is probed again
  immediately before the single hard-target write; blocked, out-of-range, or
  unknown results fail closed without an alternate target.
- Fixed repeated forward Tab presses selecting the same highest-ranked enemy.
  Smart Tab now advances from the exact current eligible target through the
  freshly ranked reachable list and wraps after the last actor. If the current
  target is absent or ineligible, selection starts at rank one. The cycle stores
  no cursor, so manual target changes re-anchor it automatically.
- Reverse targeting, unsupported jobs and contexts, and metadata-unverified
  clients remain on FFXIV's native path. Smart Tab still performs no combat
  action and keeps its one-setter, exact-readback, no-retry/no-alternate
  boundary. Configuration schema remains `38`; all `454` Core tests pass.

## 0.34.0.2

- Fixed the live **Scholar Smart Spread** shield blocker: Galvanize can be
  consumed by incoming damage before Catalyze ends. The helper now treats that
  half-status state as known after the complete locally owned setup pair has
  first been observed, then can deploy whichever exact plugin-owned effect
  remains. A completely clean seed is still required before setup, and a first
  staggered status alone cannot authorize Deployment.
- Replaced the all-or-nothing five-player Scholar snapshot with a conservative
  stable exact slice of two to five unique actors. Missing or respawning actors
  are omitted, every retained actor is still double-resolved by native slot and
  exact identity, and a spread still needs at least two exact recipients.
- Scholar now separates Biolysis's own recast from the shared PvP GCD while
  planning, so a charged Adloquium cannot outrank a ready DoT during that GCD.
  Final native readiness still gates the actual request. Scholar also soft-waits
  through transient charged-action readiness. Ordinary
  target/status drift, manual conflicts, and a final preflight miss replan on
  the continuing hold instead of disabling the helper until every WASD key is
  released. Ambiguous ownership, timeout, exhausted rejection, and completed
  chains remain release-terminal. Every plugin-issued native Scholar attempt is
  now recorded in the local Dalamud log for unambiguous live diagnosis.
- Changed held **Monk** sequencing so a confirmed Wind's Reply always prefers
  Thunderclap before Phantom Rush, even when the client still reports the
  pre-knockback melee position. Stable cooldown/charge evidence now reserves
  Wind's Reply, Thunderclap, and Rising Phoenix across transient GCD/readiness
  frames; a genuinely unavailable charge still falls through without deadlock.
  Wind remains a melee and ranged setup before Phantom. Configuration schema
  remains `38`; all `454` Core tests pass.

## 0.34.0.1

- Fixed held **Monk Wind's Reply** being skipped at the one-frame Pouncing
  Coeurl -> Phantom Rush transition. The frozen combo now retains the knockback
  reservation across a transient native-ready sample, and Wind's Reply remains
  ahead of an already active Fire Resonance.
- Fixed **Scholar Smart Spread** setup attribution by capturing the exact first
  effect recipient for single-target Biolysis/Adloquium while retaining the
  animation target for area Deployment. An already client-accepted exact setup
  may also advance from the frozen target's matching local-source status pair;
  unusable or delayed matching packet metadata waits for that exact proof
  instead of cancelling Deployment, and evidence after the 2.5-second ownership
  deadline can no longer revive an expired workflow.
- Made Scholar recast observation dynamic and optional. Candidate targeting no
  longer mixes structural target validity with transient recast/cast state;
  unknown timing blocks only the one-charge shield reservation, while DoT and
  two-charge shield plans retain their final native readiness checks.
- Enabled known pressure sampling for default-off held **DRK Shadowbringer** in
  the Wolves' Den, so its safe fallback can be tested against the exact reviewed
  dummy. Configuration schema remains `38`; all `454` Core tests and the zero-
  warning release build pass in this source snapshot. Current-patch in-game
  behavior remains a separate live-validation boundary.

## 0.34.0.0

- Fixed the held **Monk combo** against the live Dragon Kick stall by resolving
  FFXIV's native PvP `ActionComboRoute` function and invoking route `55` in
  Combo mode for every normal stage and Phantom Rush. A missing or drifted
  native resolver now disables the combo helper fail closed.
- Extended **Viper Serpentiner Geist** Wolves' Den testing to the exact current
  hard target when it is either the native hostile duel opponent or the
  reviewed combat striking dummy with NameId `541`. The exact actor remains
  frozen and revalidated as slot `0`; no target switch or synthetic enemy slot
  is introduced.
- Reworked **Samurai Soten -> Mineuchi** around direct targeted requests, one
  exact protection episode, the complete verified blocker family, and measured
  current-session Soten/Mineuchi impact timing. A client-accepted Soten retains
  its frozen Mineuchi completion; without enough safe samples, the helper waits
  for authoritative protection absence.
- Added bounded, action-specific impact calibration for protection-end
  counter-CC. Prediction requires five valid current-or-nearer samples,
  including one from the current runtime session, and every early request keeps
  the exact action, actor, and protection episode through the final native check.
- A true main-GCD counter that is still busy at its learned ideal request frame
  now reserves only that frozen intent for the first ready frame strictly before
  `1000 ms`; the deadline never slides and the wait neither claims input nor
  cancels a cast. Existing oGCD paths do not inherit this late window.
- Added default-off protection-end options for **RDM Vice of Thorns** from its
  exact Forte proc and **BLM Frost Star** from its exact Soul Resonance proc,
  alongside the existing WHM, BRD, NIN, PLD, RDM Resolution, and SAM paths.
- Bumped the plugin to `0.34.0.0` and configuration schema to `38`. The new
  hostile options remain off for upgrades, fresh installs, and Reset Defaults;
  all `454` Core tests and the zero-warning release build pass in this source
  snapshot. Current-patch in-game behavior remains a separate live-validation
  boundary.

## 0.33.0.1

- Fixed the held **Monk combo** stopping after Dragon Kick. Every normal combo
  stage and Phantom Rush now uses FFXIV's exact PvP combo mode and route `55`;
  standalone helpers such as Fire's Reply and Rising Phoenix keep their normal
  invocation mode. One frozen held-key episode can therefore advance all six
  normal stages across their real GCD waits.
- Fixed held **Dark Knight Shadowbringer** failing metadata validation. The
  hotbar carrier `29091` is correctly required to be a player action while its
  internal Dark Arts replacement `29738` is correctly required not to be one.
- Removed the unrelated striking-dummy metadata requirement from the DRK
  Wolves' Den held-input gate. A native duel opponent remains identity-frozen
  and revalidated independently; the strict NameId-`541` dummy path still
  requires its own verified metadata. Dark Arts remains pressure-independent,
  while the HP-cost fallback still fails closed without known pressure.
- Bumped the plugin to `0.33.0.1`; configuration schema remains `37`. All `441`
  Core tests and the full zero-warning release build pass in this source
  snapshot; current-patch in-game behavior remains a separate live validation
  boundary.

## 0.33.0.0

- Replaced the retired `/seitonbringer` macro pair with a default-off **held
  Shadowbringer** helper for PvP Dark Knight. Exact Dark Arts from a broken
  Blackest Night gets the first DRK slot; otherwise the configurable base-action
  fallback requires HP strictly above 85% and fresh pressure strictly below two
  by default. It freezes one reachable lowest-HP enemy without changing the
  visible target. Dark Arts runs before Hiebsprung; the HP-cost fallback runs
  after it.
- Added a default-off **Gunbreaker Continuation** held helper for the exact
  transformed carrier and own proc status. It supports Hypervelocity, Jugular
  Rip, Abdomen Tear, Eye Gouge, and Fated Brand, spends one exposure once, and
  uses only the frozen exact reachable enemy. It neither cancels casts nor
  changes the selected target.
- Added a default-off **Monk held combo** helper with native melee/ranged routing
  and the reviewed knockback, Thunderclap, Rising Phoenix, and Phantom Rush
  sequence. It preserves the required attack-buff resource, freezes every
  action/target/key intent, and remains behind the earlier job-specific lanes.
- Added optional **Samurai Soten -> Mineuchi** follow-ups after exact enemy
  Purify/Guard evidence and optional held **Zantetsuken** for an enemy carrying
  the Samurai's own Kuzushi with exactly zero shield. A client-accepted Soten
  reserves the helper slot for its 1.5-second Mineuchi stage. Wolves' Den testing
  uses only the exact current reviewed target.
- Extended protection-end counter-CC with optional PLD Intervene and RDM
  Resolution profiles. WHM Miracle, BRD Silent Nocturne, and NIN Raiju now retain
  exact source-sequence/protection evidence through the bounded three-second
  release lease; positive team pressure improves ranking but is never required.
- Accepted **Auto-Guard** can now show a local activation card and play a small
  configurable sound. Its owned input shield also blocks an accidental second
  Guard press for the first two seconds; after that, Guard reuse is again the
  deliberate release path. The explicit `/panicshu` override remains unchanged.
- Guardian communication may re-offer the same frozen localized Quick Chat only
  while FFXIV's text-command shell is explicitly busy and only before the
  original 1.5-second deadline. Once the native shell is invoked there is no
  retry. German clients use the localized `/schnellchat <n> Ziel decken` form.
- Hardened `/panicshu` only at its immediate native boundary: exact adjusted
  Shukuchi, recast group, cooldown, and resource readiness must all be positively
  ready before the single location call. This prevents the predicted animation
  start and cooldown rollback while preserving its command-only, own-Guard,
  no-wait, no-retry behavior.
- Bumped the plugin to `0.33.0.0` and configuration schema to `37`. Every new
  hostile action path remains off for upgrades, fresh installs, and Reset
  Defaults. All `440` Core tests and the full zero-warning release build pass in
  this source snapshot; current-patch in-game behavior remains a separate live
  validation boundary.

## 0.32.0.1

- Fixed **Scholar Smart Spread** running during Crystalline Conflict
  preparation. It now remains inactive until the current territory receives
  Duty Start / recommence evidence, remembers that start through transient
  `IsDutyStarted` gaps, and closes again on duty completion or context reset.
- Replaced the fragile requirement that one ActionEffect packet contain both
  expected setup statuses. The helper now attributes its exact accepted
  Biolysis/Adloquium through local caster, action, frozen animation target,
  generation, and source/global sequence evidence, then observes the exact
  local-source status pair on that frozen actor for up to 2.5 seconds.
  Deployment Tactics is eligible immediately when the pair appears and runs at
  the first safe native animation/cast/queue boundary; 2.5 seconds is only the
  fail-closed timeout, not an added delay.
- Accepted calls whose native source sequence is not synchronously visible may
  now bind the first exact nonzero server ActionEffect instead of cancelling a
  valid chain. Manual Scholar actions still cannot start or hijack a workflow,
  and target/status ambiguity still retires it without fallback.
- Full-health allies away from the tactical crystal no longer qualify as
  Adloquium seeds. A completed setup -> Deployment chain is terminal until all
  physical gameplay keys are released, preventing a continuous WASD hold from
  becoming an idle Scholar rotation.
- Bumped the plugin to `0.32.0.1`; configuration schema remains `35`, preserving
  every setting. All `423` Core tests and the full zero-warning release build
  pass in this source snapshot. Current-patch in-game confirmation remains
  separate from these source/build checks.

## 0.32.0.0

- Added a default-off **Emergency Teleport** held helper for PvP MNK, BLM, SGE,
  and VPR. When the exact local player is strictly below the configurable HP and
  MP limits and has fresh direct enemy focus, it ranks exact party destinations
  by fewest nearby enemies, farthest safe travel, clearance, and stable identity.
  The default requires at least one focusing enemy, at least 10 yalms of travel,
  and no enemy within 10 yalms of the destination.
- Emergency Teleport runs directly after Smart Recuperate and before generic
  Guard/Sprint. It freezes one exact ally and action, uses no visible target
  change, and spends the danger episode before at most one native call. Rejection,
  ambiguous acceptance, exception, target drift, or missing safety data never
  retries or falls back; a new episode requires a clearly observed safe interval.
- Its final boundary keeps the held-key snapshot readable until after exact
  commit, checks native target-specific action usability as well as range/line of
  sight, rejects duplicate party identities fail-closed, and retires any failed
  final preflight instead of silently examining the plan again next frame.
- Added default-off **Scholar Smart Spread** in exact Crystalline Conflict. Its
  independent raw-held-key lane does not consume the shared Purify/Recuperate/job
  scheduler. It selects the reachable enemy seed with maximum new 15-yalm
  Biolysis coverage before considering an Adloquium shield spread. A one-charge
  shield spend is allowed only when Deployment Tactics will return by the next
  Biolysis opportunity; two charges are immediately safe.
- Scholar setup and Deployment remain bound to one frozen actor. The existing
  shared ActionEffect hook confirms only the exact locally generated action,
  target, nonzero source sequence, and expected status pair. Manual Adloquium,
  Biolysis, or Deployment Tactics never starts or gets adopted by the automatic
  workflow. Scholar never changes targets, consumes the shared held frame, or
  cancels a cast; it waits for the real native animation/cast/queue boundary.
- Scholar planning and final revalidation now require the complete exact five-
  enemy or five-member party roster. Every terminal frozen-plan failure stays
  retired until the held key is released, and Scholar packet parsing has its own
  error epoch so an unrelated consumer of the shared hook cannot cancel or skip
  this workflow.
- Bumped the plugin to `0.32.0.0` and configuration schema to `35`. Both new
  helpers are off for upgrades, fresh installs, and Reset Defaults. All `423`
  Core tests pass in this source snapshot.

## 0.31.0.1

- Reworked the default-off Viper Serpentiner-Geist held helper around the native
  transformed carrier `39183`. It is now observed directly on every active VPR
  framework frame; no preceding-action hook, synchronous acceptance proof,
  native queue-drain provenance, accepted-action epoch, or five-second trigger
  window is required.
- Any eligible currently held gameplay key, including WASD, can authorize the
  exact exposed `39174`-`39182` follow-up as soon as its current hard target,
  native readiness, range, line of sight, and action boundary are valid. A proc
  may appear before the hold. Each exact carrier exposure is spent once, one
  false carrier sample cannot rearm it, and a genuinely distinct follow-up such
  as `39177` to `39178` remains available under the same continuous hold.
- Purify remains ahead of Viper in the shared scheduler. Own Guard still
  suppresses the helper; CC remains exact canonical `S1`-`S5`, Wolves' Den
  remains exact-current-hard-target striking-dummy-only, and Viper still never
  changes the selected target or cancels a cast. Only a clean native `false`
  keeps the exact bounded retry; ambiguous acceptance is terminal.
- Bumped the plugin to `0.31.0.1`. Configuration schema remains `34`, preserving
  the user's existing Viper and Wolves' Den test toggles. All `404` Core tests
  pass in this source snapshot.

## 0.31.0.0

- Extended the default-off Smart Tab replacement to the reviewed ranged DPS jobs
  in exact Crystalline Conflict. BRD, BLM, SMN, MCH, RDM, and PCT use one
  25-yalm geometric hitbox-edge tier; DNC uses one 15-yalm tier. Ranged jobs do
  not receive the melee-first preference. The existing HP, positive fresh team-
  pressure, verified Guard-cooldown, trusted-MP, and stable-slot ranking remains
  unchanged, as do the one-setter/readback and no-retry/no-alternate boundaries.
- Extended the separate default-off held Smart Recuperate helper to Wolves' Den
  only while the existing testing option is enabled. The exact supported context
  is frozen with the self intent, so a CC/Den/context transition cancels instead
  of carrying an attempt across content. Frontline and Rival Wings remain
  excluded.
- Added a separate default-off PvP Viper held-key helper for Serpent's Tail /
  Serpentiner Geist. A client-accepted qualifying Viper action may arm one exact
  five-second opportunity; the carrier `39183` must adjust to the expected exact
  follow-up `39174`-`39182`. Direct execution requires a clean native queue and a
  synchronous action-sequence advance. A normally early-queued input is accepted
  only when FFXIV later exposes the exact queued action type, adjusted action,
  canonical target, extra parameter, and combo route before a successful native
  queue drain with a sequence advance. The initial queueing call, arbitrary Queue
  calls, and any uncertain queue transition never arm or replace a trigger. Every
  newer proven qualifying-action epoch invalidates an older buffered opportunity,
  even when its adjusted follow-up and target are unchanged. The accepted action
  stores no input key. Any eligible
  currently held gameplay key, including WASD, may supply consent when the
  follow-up intent forms; only then is that exact key frozen with the same actor,
  context, and territory. `39177`/`39178` use their native 20-yalm reach; the other
  reviewed follow-ups use 5 yalms. Native zero/default-target carriers are accepted
  only when the original path was not redirected or suppressed and the same exact
  hard target is proven before and after client acceptance. Own Guard, an earlier
  scheduler claim, readiness, target status, range, line of sight, or identity
  uncertainty fails closed or waits without choosing another action or target.
  Action/target waits yield lower helpers; only an otherwise-ready native-boundary
  or retry-throttle wait retains Viper's frame. The five-second deadline remains
  exact after intent formation. Only a clean client rejection may use the shared
  bounded same-intent retry; ambiguity or exhaustion latches the frozen key until
  release.
- In Crystalline Conflict the Viper helper accepts only an exact canonical
  `S1`-`S5` target. With the separate Wolves' Den testing option it accepts only
  the exact current hard-target combat striking dummy with NameId `541`; duel
  players, arbitrary NPCs, and synthetic enemy slots are rejected. It never
  changes the visible target, never dispatches carrier `39183`, and deliberately
  does not participate in held-action cast cancellation. Client acceptance is
  not proof of a server-side hit.
- The current request order is **Purify > NIN Seiton / VPR Serpentiner Geist >
  reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH
  Critical Strategy > DRK Hiebsprung > Smart Recuperate > generic Guard >
  pressure Sprint > event Kardia > event Monk**. There are now twelve physical-
  hold helpers; the cast-cancellation experiment remains limited to its existing
  eleven and explicitly excludes Viper.
- Bumped the plugin to `0.31.0.0` and configuration schema to `34`. The new Viper
  helper remains off for upgrades, fresh installs, and Reset Defaults. All `404`
  Core tests pass in this source snapshot.

## 0.30.0.1

- Changed Smart Tab into a default-off replacement for FFXIV's native logical
  forward-target command in exact Crystalline Conflict on the six reviewed
  melee-DPS jobs. The Targets checkbox or `/smarttab` (`/sstarget`)
  `[on|off|toggle]` controls it; OFF is fully vanilla. One hook retains FFXIV's
  targeting-handler scope and the second intercepts only its nested forward
  world-target cycle after FFXIV's own input/UI gates. Reverse targeting,
  direct cycle callers outside that handler, UI/chat input, other target commands,
  unsupported jobs, and other content remain unchanged.
- Smart Tab first admits enemies at no more than 5 yalms of hitbox-edge melee
  distance, then the reviewed job gap cap: 20 yalms for MNK, DRG, NIN, SAM, and
  VPR, or 15 yalms for RPR. The first non-empty reach tier ranks lowest exact HP
  ratio, highest fresh positive team pressure, verified Guard cooldown
  unavailable, lowest trusted MP ratio, then stable native S-slot. Live Guard is
  excluded.
- One owned forward-target request freezes and revalidates one exact canonical `S1`-`S5` actor, writes
  the visible hard target through FFXIV's native setter once, and verifies exact
  readback. Missing or ambiguous identity, invalid geometry, or context/reach
  drift before that setter consumes only the owned cycle and leaves the current
  target unchanged. Setter rejection or readback mismatch is terminal and never
  retries, reranks, restores, or selects an alternate; no combat action is sent.
- Moved the previous optional harmful-action redirect to `/smartaction`
  (`/ssaction`) behind its own default-off `EnableSmartActionMacro` setting.
  `/nearassist`, `/nearhelp`, and `/farhelp` retain their existing shared option.
  Schema 33 preserves an older explicit macro-helper opt-in for Smart Action but
  leaves the new target-writing Smart Tab off for every upgrade, fresh install,
  and Reset Defaults.
- Bumped the plugin to `0.30.0.1` and configuration schema to `33`. All `398`
  Core tests pass.

## 0.30.0.0

- Replaced Smart-Seiton policy experiments with one simple tactical switch.
  `/autoseiton`, its clickable action-bar-style NIN tile, and distinct ON/OFF
  icons control whether held-key Auto-Seiton is available. A ready sparkle shows
  the resolved action state. Ongoing physical held-key consent remains required;
  Purify stays first and enabled NIN Seiton now gets the next scheduler slot.
- Added `/smarttab` (`/sstarget`) for one exact incoming harmful PvP macro action.
  It requires no selected target, uses the actual action's native range/line of
  sight, excludes live Guard, and ranks reach tier, lowest HP%, fresh team
  pressure, observed Guard cooldown unavailability, trusted MP%, and stable
  S-slot. The authored `<t>` line is the only fallback; no target selection,
  rerank, alternate, retry, or generated action is added.
- Made WHM Miracle, BRD Silent Nocturne, and NIN Raiju protection-end follow-ups
  freeze the exact actor/key at authoritative Purify/Guard release and wait for
  transient range, LoS, cast, queue, and animation gates inside a non-extending
  3-second held lease. Positive pressure improves ranking but is never required.
- Made the default-off PLD Guardian helper act earlier without guessing: the
  original `<=20%` rescue remains unconditional; `21-35%` is eligible only from
  a fresh exact `3+` incoming hard/cast-target count. Critical candidates always
  precede proactive ones and the frozen target never reranks.
- A client-accepted automatic Guard now owns a cancellation shield before any
  macro redirect is consumed. It bridges the exact 1.5-second status propagation
  window, then follows the live Guard status; ordinary Action/PvPAction presses
  cannot end it accidentally. Manual Guard is never owned, Guard reuse remains
  the deliberate release path, `/panicshu` remains an explicit emergency
  override, and a six-second hard cap fails open. Auto-Guard does not dispatch
  if its protection hook is unavailable.
- Removed the unusable fixed Combat Frames, their targeting/mouseover path, and
  calibrated gauge runtime. Enemy LB activation icons now sit above exact native
  nameplates with verified duration or a brief instant flash. A separate safe
  top-center self `LB ACTIVATED!` banner and left-side ally `player -> target`
  damage feed retain the useful notifications without covering HP/MP bars.
- Added local-player MP sounds at downward crossings of `4,000` and `2,000`, with
  independent hysteresis and the critical cue winning a direct double crossing.
  Added a compact one-time What's New window after each plugin version update;
  closing it records that version without chat spam.
- Bumped the plugin to `0.30.0.0` and configuration schema to `32`. The custom-
  repository listing is visible again. All `388` Core tests pass.

## 0.29.0.1

- Hotfixed the default-off exact-CC NIN Auto-Seiton helper to exclude an enemy
  carrying Guardian's target-side `Covered` / `Gedeckt` status, the Paladin
  caster's Phalanx `Hallowed Ground` self-invulnerability, or Dark Knight
  Eventide's `Undead Redemption` HP-floor status. Guard / Wehr itself remains a
  valid Seiton target; the covering Paladin and Phalanx's 33% party mitigation
  are deliberately not blockers.
- Applied the same exact numeric-status check before initial ranking, every
  frozen retry, optional held-cast cancellation, and the latest safe native
  request boundary. Protection appearing after commitment retires only that
  exact action/actor epoch without `UseAction`, reranking, an alternate target,
  or reopening the same Unsealed follow-up while the key stays held.
- Seiton execute and preparation cues now clear while the exact enemy is
  protected. Current English status metadata is validated fail-closed, and
  `/seiton debug` reports the blocking status plus protection-cancel counters.
  Added two pure Core tests; all `367` Core tests pass.
- Bumped the plugin version to `0.29.0.1`. Configuration schema remains `31`;
  this hotfix adds no setting or migration, and manual Seiton plus `/panicshu`
  are unchanged.

## 0.29.0.0

- Added a separate default-off NIN held helper for exact Crystalline Conflict.
  It selects only an independently resolved canonical S1-S5 enemy that is alive,
  targetable, strictly below `20%` HP, currently has live Guard / Wehr status
  `3054` or `3673`, and is within Shukuchi's native three-dimensional `20`-yalm
  range. Exactly `20%`, stale identity, missing Guard, Three Mudra's adjusted
  Doton `29514`, and non-finite positions fail closed.
- The helper freezes one exact actor and calls ground-targeted Shukuchi `29513`
  at that actor's latest revalidated position. Positive fresh team pressure is
  an optional ranking bonus; zero, unknown, stale, or unavailable pressure is
  neutral and never blocks. Missing unrelated enemy slots do not disable an
  otherwise exact target. A frozen intent never reranks or substitutes another
  enemy; only proven client-false feedback may use the common bounded same-actor
  retry.
- After a client-accepted Shukuchi, the helper re-resolves and hard-targets that
  exact same living enemy once. Rejection, unknown acceptance, identity drift,
  death, or readback mismatch never changes the target. The automatic helper is
  suppressed by the local player's own Guard and propagation latch; explicit
  `/panicshu` remains the sole own-Guard-breaking exception.
- Inserted Guard-Shukuchi after PLD Guardian and before NIN Seiton in the shared
  scheduler and optional cast-cancel order. A continuing hold can open another
  Guard-Shukuchi only after a real cooldown-unavailable to ready epoch; frame
  jitter cannot replay it. Added live diagnostics and nine pure Core tests.
- Bumped the plugin to `0.29.0.0` and configuration schema to `31`. The new
  target-mutating action helper is off for new, upgraded, and reset configs.

## 0.28.0.1

- Simplified the explicit manual NIN `/panicshu` macro into one immediate action
  path. Every invocation computes the exact 19.5-yalm forward terrain point and
  calls native Shukuchi at most once in the command callback; the 500-ms lease,
  pending state, framework wait, expiry, and second-command preservation were
  removed.
- Intentionally allows the manual command from the local player's own Guard so
  Shukuchi may break it. Panic Shukuchi no longer consults the held-action
  scheduler, Self-Purify priority, crowd-control state, cast, native queue,
  animation lock, cooldown, or resource readiness. FFXIV immediately accepts or
  rejects that one request; a later macro press is a new explicit command, never
  an automatic retry.
- Kept the exact PvP NIN/context/metadata checks, opt-in Wolves' Den test path,
  exact adjusted Shukuchi `29513` Doton block, 19.5-yalm terrain proof, one native
  location-action boundary, and no target/cursor mutation, shorter fallback,
  alternate action, or replay. Routine command results are now chat-silent and
  remain available in `/seiton debug`.
- Bumped the plugin version to `0.28.0.1`. Configuration schema remains `30`;
  this hotfix adds no setting or migration.

## 0.28.0.0

- Added the explicit manual NIN `/panicshu` macro command. It runs only from the
  authored command invocation, never from an automatic, pressure, enemy, status,
  or held-key trigger, and is independent of the held-action scheduler and its
  cast-cancellation option.
- Each invocation projects the exact terrain point 19.5 yalms along the local
  character's current facing and freezes that destination for at most 500 ms.
  Only an active Self-Purify priority claim, cast, occupied native action queue,
  or animation lock may wait inside that lease. The lease is spent before at most one native Shukuchi
  location-action call, with no retry, destination recomputation, path search,
  alternate action, or shorter/inward fallback.
- Restricted the command to exact PvP Ninja in Crystalline Conflict and the
  Wolves' Den only when the existing test option is enabled. Exact identity,
  territory/context, metadata, action/readiness, own Guard, crowd-control, and
  terrain evidence fail closed. Three Mudra's adjusted Doton route blocks the
  command, crowd control leaves Purify priority, and the helper never reads or
  changes the mouse/ground cursor or any hard, soft, Focus, or mouseover target.
- Added core/safety coverage for the one frozen command intent and documented the
  remaining current-client boundary. Four-direction flat-ground, slope, wall,
  invalid-endpoint, and observed movement tests in the Wolves' Den are still
  required; a local client-accepted return is not proof of server movement or CC
  behavior. Bumped the plugin version to `0.28.0.0`; configuration schema remains
  `30`, with no new setting or migration.

## 0.27.1.0

- Fixed the v0.27 reactive held-key regression without widening any event
  deadline. An urgent startup now remembers its exact actor, action, and event
  first, then may attach the first currently eligible held/fresh key generation
  inside the original bounded threat lease. Post-Purify and post-Guard remember
  the exact enemy episode while protection is live and bind the current eligible
  generation only when authoritative Resilience/Guard absence opens the original
  500-ms release opportunity. Once attached, the key is strict: release, text
  input, identity drift, or ambiguity cannot substitute another generation,
  actor, or action. Expired/disabled leases retire and every active startup
  revalidates its frozen job, action, and actor before new packets are drained;
  an exact later urgent startup may preempt only an unattempted lower-priority
  reactive lease.
- Made post-Purify capture tolerate the exact self-target Purify action packet
  when it omits an individual recovered-status tuple. Live Resilience is still
  mandatory before any follow-up can arm, and real status-list absence remains
  mandatory before dispatch. If the canonical enemy row is transiently absent,
  only that already-deduplicated signal is retained for resolution inside its
  original 750-ms acquisition deadline; no key, target fallback, action, or
  deadline extension is created. Native range/line of sight and blocker state are
  now checked before simultaneous protection-end candidates are ranked, so an
  unreachable high-pressure enemy cannot starve a reachable exact enemy.
- Extended only NIN post-protection reactive intent lifetime to 3,000 ms. Both
  verified Raiju rows have a 2.5-second recast, so this covers one full recast
  plus the existing 500-ms release opportunity; WHM and BRD retain the normal
  1.5-second held-action lease.
- Bound automatic reactive-CC and Ally Rescue landing confirmation to the exact
  `SourceSequence` produced by the plugin's accepted native request. A manual
  Miracle, Silent Nocturne, Raiju, Paean, or Aquaveil can no longer claim the
  pending automatic `AUTO CC LANDED` / `CLEANSED` result.
- Added bounded durable diagnostics for reactive episode memory, key attachment,
  protection-end promotion, native attempt outcome, and exact source-sequence
  confirmation. Bumped the plugin version to `0.27.1.0`; configuration schema
  remains `30`, with no new setting or migration.

## 0.27.0.0

- Added event-owned reservations to reactive counter-CC. An eligible physical
  key, exact canonical actor, local counter action, and event epoch are frozen
  when an urgent startup, enemy Purify, or first enemy Guard presence is
  observed. A later key cannot inherit that episode; release, text-input
  poisoning, identity drift, or ambiguity is terminal with no alternate,
  target switch, fallback, or replay.
- Added bounded protection-end timing hints without speculative dispatch.
  Validated live `RemainingTime` may establish only a non-extending expected
  Resilience/Guard end. Real status-list absence remains mandatory. Post-Purify
  can use the first authoritative absent frame at or after its expected end;
  early or untimed absence keeps the 150-ms anti-flicker proof. Post-Guard
  still releases on its first authoritative absent frame, including an early
  manual cancel. A released Guard reservation stays retired through ambiguous
  samples until exact absence separates a later Guard episode.
- Added PvP Ninja to reactive counter-CC with both metadata-verified variants:
  Forked Raiju `29510` and Fleeting Raiju `29707`, each using native 20-yalm
  reachability and the standard Purify-removable protection matrix. Landing is
  confirmed only by exact Stun `1343` on the frozen enemy and uses the matching
  Raiju icon. Both metadata rows must verify before NIN can arm, and the exact
  executable variant must be exposed through the PvP Spinning Edge/Aeolian Edge
  Combo carrier `29500`. Forked Raiju also waits while the exact local Sealed
  Forked Raiju status `3195` is active, and both variants wait through exact
  local Bind `1345`.
- Ranked all same-drain urgent startup captures before arming one frozen winner:
  MCH/SAM/VPR first, DNC second, protection-end releases third, then stable
  event time and canonical identity. Simultaneous losers are terminal. Reactive
  observation may remain alive while own Guard suppresses every action request.
- Bumped the plugin version to `0.27.0.0`. Configuration schema remains `30`;
  there is no new setting or migration, and all existing opt-ins are preserved.

## 0.26.0.0

- Raised every job-specific physical-hold helper into the second priority tier,
  immediately after Purify. The deterministic order inside that tier is
  **reactive counter-CC > Ally Rescue > PLD Guardian > NIN Seiton > SCH
  Critical Strategy > DRK Hiebsprung**. Reactive WHM Miracle / BRD Silent
  Nocturne wins before ally cleanse because its LB, post-Purify, and post-Guard
  windows are shorter.
- The complete request order is now **Purify > reactive counter-CC > Ally Rescue >
  PLD Guardian > NIN Seiton > SCH Critical Strategy > DRK Hiebsprung > Smart
  Recuperate > generic Guard > pressure Sprint > event Kardia > event Monk**.
  One continuously held key may still authorize later distinct exact episodes,
  while at most one held helper crosses the native action boundary per framework
  frame.
- Made team pressure an optional positive-only ranking bonus for simultaneous
  post-Purify and post-Guard counter-CC releases. A fresh exact count above zero
  ranks ahead; zero, unknown, or stale pressure is neutral and never gates a
  candidate. Remaining order is lowest HP ratio, lowest trusted MP ratio, then
  stable canonical identity. The selected episode remains frozen with no rerank,
  alternate, target change, fallback, or replay.
- Bumped the plugin version to `0.26.0.0`. Configuration schema remains `30`;
  there is no new setting or migration, and existing opt-ins are preserved.

## 0.25.0.0

- Fixed the v0.24 held-key lease regression across all ten physical-hold
  helpers: Purify, Smart Recuperate, Ally Rescue, reactive counter-CC, Guard,
  Guardian, pressure Sprint, NIN Seiton, SCH Critical Strategy, and DRK
  Hiebsprung. An already-held movement key now wins before another stable held
  key, while a fresh movement or other gameplay key is used only as fallback.
  Each frozen intent keeps that exact lease until release, ineligibility, reset,
  or its action-specific terminal outcome, so tapping and releasing a hotbar key
  no longer cancels an otherwise-valid lease still backed by held WASD.
- Kept held input ahead of fresh fallback in every affected helper and retained
  the existing exact action, actor/target, status/episode, context, Guard, range,
  line-of-sight, and key revalidation. The request priority remains **Purify >
  Smart Recuperate > Ally Rescue > reactive counter-CC > Guard > Guardian >
  pressure Sprint > Kardia > NIN > SCH > Monk > DRK Hiebsprung**. Kardia and
  Monk remain separately event-driven rather than physical-hold origins.
- Added a separate default-off **cancel my active cast for an otherwise-ready
  held helper** test toggle for exactly the ten physical-hold helpers above.
  When the highest-priority frozen intent is otherwise ready and only the local
  cast blocks it, Seiton Sense may request FFXIV's native cast cancellation once
  for that observed cast. The void native call proves only that cancellation was
  requested, not that the cast stopped.
- Cast cancellation and the held action can never occur in the same framework
  frame. A later frame must observe the cast cleared and repeat the complete
  ordinary helper preflight before any `UseAction` request. The test synthesizes
  no movement or Escape input, clears no native queue, writes no cast state, and
  never changes a target. Smart Kardia, Monk Earth's Reply, every already-
  incoming manual/Turbo redirect (including Paean), and macro helpers are
  excluded. Current-patch live proof remains
  pending for stationary casts and mobile BRD Powerful Shot / MCH Blast Charge.
- Added bounded Settings and `/seiton debug` diagnostics for the selected cast-
  cancellation helper, exact action/target/key/intent epoch, observed cast, one-
  request latch, native request/fault counts, and last result. The existing
  explicit-client-rejection retry remains at least 50 ms apart with eight calls
  maximum and is independent of the one-request-per-cast cancellation path.
- Bumped the plugin version to `0.25.0.0` and configuration schema to `30`.
  The cast-cancellation test is explicit opt-in and remains off for fresh, reset,
  and migrated configurations; every existing setting is otherwise preserved.

## 0.24.0.0

- Replaced global one-use held-key consumption with continuous physical-key
  consent and a shared per-frame priority scheduler. The same hold may authorize
  later distinct exact held episodes. The overall request order is **Purify >
  Smart Recuperate > Ally Rescue > reactive counter-CC > Guard > Guardian >
  pressure Sprint > Kardia > NIN > SCH > Monk > DRK Hiebsprung**; Kardia and
  Monk keep their separate event-driven origins, and at most one held helper
  crosses the native action boundary in one framework frame.
- Added a common bounded pre-acceptance contract to every held-action helper.
  Known cooldown, resource, cast, queued-action, and animation-lock blocks wait
  without spending the attempt budget. Only an explicit client rejection may
  retry the same frozen intent after 50 ms, with eight native attempts maximum;
  client acceptance and ambiguous/exceptional outcomes are terminal.
- Kept exact actor, action, status/episode, physical key, context, range, line of
  sight, and Guard revalidation for every retry. No helper may rerank after
  freezing, select an alternate, mutate the selected target, or retry an action
  already accepted by the client.
- Changed NIN Seiton from a fresh-edge-only helper to the same held scheduler.
  An explicit prior opt-in migrates to the new held option; base Seiton and its
  verified follow-up remain separate adjusted-action epochs and a rejected base
  call can never substitute the follow-up.
- Preserved accepted Ally Rescue confirmation evidence across later rejected
  calls, and separated soft waits, explicit client rejections, accepted calls,
  and exact server-observed cleanses in diagnostics.
- Changed Limit Break activation in Combat Frames to a pulsing outer border and
  compact icon/name/countdown banner so HP, MP, LB gauge, and status badges stay
  visible. When Combat Frames are enabled while interaction is off, Settings now
  shows a prominent state label and a one-click enable button.
- Bumped the plugin version to `0.24.0.0` and configuration schema to `29` for
  the explicit NIN fresh-edge-to-held migration. Existing action-helper opt-ins
  otherwise remain unchanged; new and reset action helpers remain default-off.

## 0.23.0.0

- Changed held WHM Miracle and BRD Silent Nocturne post-Purify/post-Guard
  handling from one consumed physical-key generation to one attempt for the
  selected exact protection-end episode. A later distinct verified Resilience/
  Guard-end release epoch can trigger on the same continuous hold; the selected
  episode never repeats.
- Removed the hard team-pressure-count requirement from both exact protection-
  end paths. Known fresh exact team pressure ranks before unknown and then
  highest-first, followed by lowest HP ratio; known trusted MP ranks before
  unknown and then lowest-first. Stable canonical identity closes ties. Exactly one winner is
  selected; simultaneous losers are terminal and never become fallback attempts.
- Kept independent bounded post-Purify state for each canonical `S1`-`S5` enemy,
  so simultaneous exact enemies can reach their own verified Resilience end
  without replacing each other before ranking.
- Retained Purify and Smart Recuperate priority, WHM's native 10-yalm and BRD's
  native 20-yalm reachability/line-of-sight checks, direct exact-actor dispatch,
  and no selected-target switch, alternate, replay, or retry.
- Clarified in Ally Rescue settings and diagnostics that confirmation counters
  represent server-observed cleanses rather than every attempted or accepted
  action request.
- Bumped the plugin version to `0.23.0.0`. Configuration schema remains `28`;
  the existing reactive-counter master, held-key, post-Purify, and post-Guard
  choices are preserved with no new persisted option, and the master remains
  default-off.

## 0.22.0.0

- Added an optional **post-Guard reactive counter-CC** trigger for BRD and WHM.
  It binds one exact canonical `S1`-`S5` actor only after Guard `3054` or `3673`
  was observed present and the first verified framework observation finds it
  absent. A fresh exact team-target count of at least two and one bounded fresh/
  held-key opportunity are still required at the job-specific native range and
  line of sight.
- Corrected the post-Purify contract to dispatch directly against its frozen
  exact `S1`-`S5` actor. It no longer requires that actor to be the selected
  target and never mutates the selected target. Target drift, alternates,
  replay, and retry remain fail-closed.
- Updated the reactive-counter UI to use the client-correct bilingual names
  **WHM Wunder der Natur / Miracle of Nature** and **BRD Stumme Nocturne /
  Silent Nocturne**.
- Bumped the plugin version to `0.22.0.0` and configuration schema to `28`.
  The new post-Guard leaf defaults on only for fresh/reset configurations behind
  the existing default-off reactive-counter master. Schema-27 and older users
  migrate that leaf explicitly off, preserving every existing helper choice and
  preventing an already-enabled installation from silently gaining a hostile
  action trigger.

## 0.21.0.0

- Fixed German Paladin Guardian communication to use FFXIV's canonical
  localized `/schnellchat <P#> Ziel decken` form after a live report where the
  Guardian signs appeared but the Quick Chat message did not.
- Added optional **Combat Frame interaction**. A fresh, living, exact canonical
  `S1`-`S5` row can be clicked once to set that actor as the hard target, while
  hover can publish that exact actor through FFXIV's native mouseover slots.
  Self, preview, dead/unknown rows, stale snapshots, and gaps stay click-through;
  every click is revalidated once with no retry, and external mouseover
  ownership wins. The native HUD, soft target, and Focus Target are not changed.
- Added configurable **Combat Frame Limit Break telemetry**. Self uses the exact
  native LimitBreakController gauge. Remote `S1`-`S5` gauges remain `LB ?` until
  the current native HUD instance proves a live calibration against Self; no
  elapsed-time or job charge-time estimate is used. Exact activation evidence
  opens the card, and duration countdowns originate only from a matching live
  `RemainingTime`. One missing sample of at most 150 ms may preserve the last
  exact expiry without extending it. Instant LBs use a fixed 1.8-second card.
- Added an optional **ally LB damage feed** using only direct ActionEffect damage
  attributed to an exact ally caster and reviewed LB action. It does not infer
  damage from HP deltas and stays silent for pet, periodic, or ambiguous damage.
- Added a separate default-off **Dark Knight Hiebsprung** held-key helper for
  exact Crystalline Conflict. It considers canonical enemies at 30% HP or lower,
  enforces a strict 10-yalm center-distance cap plus native range/line of sight,
  blocks either side's Guard, and can spend only one attempt per proven ready
  epoch. A continuous hold may repeat only after an observed not-ready-to-ready
  cooldown transition, never from a guessed reset; target mutation, alternate,
  replay, and retry remain forbidden.
- Expanded BRD Silent Nocturne urgent startup coverage to DNC, MCH, SAM, and VPR
  at the action's native 20-yalm range. The bounded startup evidence and exact
  protection/identity checks remain fail-closed. A client/server race remains:
  an instant LB already accepted before the reactive request may still resolve
  even if Silence subsequently lands.
- The current request priority is **Purify > Smart Recuperate > Guard > Guardian
  > pressure Sprint > Ally Rescue > reactive CC > Kardia > NIN > SCH > Monk >
  Hiebsprung**. Kardia still requires its separate accepted-Eukrasia trigger.
- Bumped the plugin version to `0.21.0.0` and configuration schema to `27`.
  Schema-27 migration preserves an existing schema-26 user's Combat Frames
  master and helper choices, forces only the new Hiebsprung and interaction
  leaves off, and enables both read-only LB detail leaves behind that existing
  master choice. Configurations older than schema 26 still traverse the earlier
  quiet migration first. Fresh/reset action and Combat Frames masters remain off;
  interaction and both LB detail leaves default on behind the disabled frame
  master.

## 0.20.0.1

- Fixed **Smart Recuperate** remaining blocked after opt-in because the current
  action-sheet representation exposes the shared PvP Recuperate action's row-0
  `ClassJob` reference as valid. Metadata validation no longer rejects that
  canonical shared-action representation.
- Runtime behavior and configuration are otherwise unchanged. Smart Recuperate
  remains default-off, exact-Crystalline-Conflict-only, self-only, inclusive at
  16,000 missing HP and 2,000 MP, and limited to one attempt per eligible held
  generation. Configuration schema remains `26`.

## 0.20.0.0

- Added default-off **fixed Combat Frames** for exact Crystalline Conflict: one
  Self frame and stable `S1`-`S5` enemy rows with job icons, HP, trusted MP and
  2,000-MP divisions, relevant Guard/CC/execute states, direct pressure, team
  focus, and read-only current/focus accents. They are non-clickable screen-space
  overlays; no world projection, target mutation, or native FFXIV HUD edit is
  performed. Native parameter/enemy-list elements must be hidden manually if
  the user wants the overlay to visually replace them.
- Added default-off **Smart Recuperate on held gameplay key** for exact
  Crystalline Conflict. At exactly 16,000 or more missing HP and at least 2,000
  observed MP, one eligible held generation may request self-targeted PvP
  Recuperate `29711`. Missing MP or native readiness leaves the generation
  unspent so a real MP/readiness update can make it eligible. The generation is
  consumed before final identity, HP, MP, Guard, context, metadata, and readiness
  revalidation; drift, rejection, or exception never retries.
- Replaced the frame-driven held-key **Smart Kardia** scanner and six-second
  throttle with one short-lived opportunity after an incoming PvP Eukrasia
  `29258` call is forwarded unchanged and returns client-accepted. A real local
  charge decrease or newly observed own-source Eukrasia status must causally
  confirm that call. Kardia waits for animation lock to clear and requires a
  fresh complete pressure publication created after acceptance.
- Smart Kardia still ranks exact living, targetable self/party candidates at
  two or more direct incoming enemies by pressure, exact HP ratio, party slot,
  and actor identity; exact self is the sole initial fallback when nobody meets
  the threshold. The frozen trigger and actor are spent before at most one
  direct-GOID request. Unknown Kardion state, drift, or failure cannot rerank,
  select an alternate, switch target, replay, or retry.
- Removed the speculative low-HP **pre-Guard** rule. The verified reactive path
  remains: after a high-pressure Stun Purify attempt, live Resilience, removal
  of the cleansable CC, and a genuinely new physical generation are required
  before Guard may be requested.
- Moved **Paladin Guardian** to an independent default-off Job Tool. It no longer
  depends on the defensive-utility master and keeps its exact low-HP ally,
  native reachability, direct-GOID, one-attempt, communication, and marker-
  ownership safeguards.
- The shared physical-input priority is now Self-Purify, Smart Recuperate,
  reactive Guard, PLD Guardian, pressure Sprint, Ally Rescue, reactive counter-CC,
  Ninja Seiton, then Scholar Critical Strategy. Accepted-Eukrasia Kardia is a
  separate bounded follow-up; Monk Earth's Reply still yields after an earlier
  helper attempt.
- Bumped the plugin version to `0.20.0.0` and configuration schema to `26`.
  Smart Recuperate and Combat Frames remain off for fresh, upgrading, and reset
  configurations. A prior explicit Smart Kardia opt-in migrates to the new
  Eukrasia-triggered mode, speculative pre-Guard is disabled, and only a
  previously effective Guardian opt-in migrates to the independent Job Tool.
  Source/build validation does not replace current-patch live Crystalline
  Conflict checks for action ordering, telemetry, rendering, or native effects.

## 0.19.0.0

- Added a separate default-off **Smart Kardia on held gameplay key** helper under
  Job Tools > Sage. It runs only on PvP Sage in exact Crystalline Conflict and
  requires a complete, unique, stable exact five-player party view.
- Eligible destinations are self and exact living, targetable party members
  with trusted direct incoming pressure from at least two unique enemies. A
  non-self destination must also pass FFXIV's native 30-yalm Kardia range and
  line-of-sight check. Higher pressure wins, then lower exact HP ratio, party
  slot, network entity ID, and game-object ID.
- If the highest-ranked candidate already has Kardion sourced by the local
  Sage, the helper makes no attempt and never falls through to a lower-ranked
  candidate. The frozen actor, pressure threshold, local-source Kardion state,
  exact Kardia metadata/readiness, and native reachability are revalidated at
  the final boundary.
- Kept the shared physical-generation priority as Self-Purify, defensive
  utilities, pressure Sprint, Ally Rescue, reactive counter-CC, Kardia, Ninja
  Seiton, Scholar Critical Strategy, then Monk Earth's Reply. The Kardia intent
  and generation are spent before at most one native request, with no selected-
  target mutation, alternate, fallback, substituted action, replay, or retry.
  Client acceptance is not proof that Kardia or Kardion applied; current-patch
  held-input, dispatch, status-source, and reachability behavior still require
  a live Crystalline Conflict test.
- Bumped the plugin version to 0.19.0.0 and configuration schema to 25. Smart
  Kardia remains off for fresh configurations, upgrades, and reset defaults;
  all existing settings and defaults migrate unchanged.

## 0.18.0.1

- Extended the separate default-off `/seitonbringer` helper to Wolves' Den
  striking-dummy testing. It requires both the existing DRK Macro Helpers
  opt-in and the existing Wolves' Den test option; Frontline and Rival Wings
  remain blocked.
- The Den path accepts only the unchanged native current hard target when it is
  the exact live, targetable combat striking dummy with current NameId `541`.
  That object identity and hard target are frozen and revalidated. It never
  uses synthetic `S1`, `<e1>`, the duel-opponent resolver, a player, another
  attackable object, an alternate target, or a retry. The canonical `S1`-`S5`
  Crystalline Conflict path is unchanged.
- Retained the proven 2.40-second GCD-cycle token, inclusive 0.60-0.80-second
  window, spend-before-request ownership, Guard, queue, animation-lock,
  readiness, resource, and native dual range/line-of-sight checks. A Den dummy
  trace validates only that test context and is not proof of live CC behavior.
- Corrected the cached current-data combo metadata gate to require the exact
  per-row secondary cost types `0/58/58/147/147/147` for Hard Slash, Syphon
  Strike, Souleater, and the three Delirium forms. The previous all-zero check
  failed closed and could leave `/seitonbringer` unavailable everywhere.
- Deferred the first native GCD observation from synchronous plugin startup to
  the framework update thread. This avoids the observed off-main-thread
  local-player lookup failure while preserving the same fail-closed cycle priming.
- Bumped the plugin version to 0.18.0.1. Configuration schema 24 is unchanged;
  this hotfix adds no setting and preserves all existing defaults and migration
  behavior.

## 0.18.0.0

- Added a separate default-off **Auto Low-MP Focus Target** option for exact
  Crystalline Conflict. It requires one complete unique native `S1`-`S5` view,
  150 ms of trusted MP at or below 2,000, and FFXIV's native 20-yalm range and
  line-of-sight result. The low-MP wave clears only after 150 ms at or above
  2,300 MP.
- The helper can set only an empty local native Focus Target. It never clears,
  replaces, restores, or retries one. An occupied or manually changed Focus
  wins; confirmed external drift after a plugin-set Focus latches manual
  override until the option is toggled off/on or a new exact match lifetime.
  The result feeds FFXIV's Focus Target HUD and `<f>` and is independent of the
  party-visible Attack1 sign. Because the API has no atomic compare-and-set,
  live current-patch setter/readback behavior remains an A/B boundary.
- Added a separate default-off **DRK Shadowbringer macro helper** for the exact
  adjacent lines `/seitonbringer` and `/pvpac "Souleater Combo" <t>`. The quoted
  PvP action name must match the client language. The supported setup uses both
  ReAction Macro Queue and Turbo.
- On exact PvP Dark Knight in Crystalline Conflict, the helper can make at most
  one Shadowbringer attempt per proven 2.40-second Souleater Combo GCD, only in
  the inclusive 0.60-0.80-seconds-remaining window. Missing that window skips
  the attempt, and 0.50 seconds or less never triggers Shadowbringer; a later
  Turbo pulse can queue the authored combo line normally.
- Final DRK gates require the unchanged exact current canonical enemy, clear own
  Guard/propagation and target Guard, native 5-yalm combo and 10-yalm
  Shadowbringer range/line of sight, stable queue/action sequencing, readiness,
  and either more than 12,000 HP or the exact Dark Arts state. The cycle's
  one-attempt token is spent before the final request, with no target mutation,
  alternate, replay, or retry. Client acceptance is not proof of server execution; live queue, mode,
  recast-group, and clipping behavior still require a current-patch CC trace.
- Added `/seitonbringer` to command help and placed both new controls in the
  persistent Macro Helpers and Targets settings pages.
- Bumped the plugin version to 0.18.0.0 and configuration schema to 24. Both new
  features remain off for fresh configurations, upgrades, and reset defaults.

## 0.17.0.0

- Added a large fixed top-center **high-pressure warning** for exact Crystalline
  Conflict when at least three distinct current enemies directly hard-target or
  cast at the local player. It uses the narrow current direct-intent view rather
  than the ordinary counter's longer recent-harmful-action union; unknown data
  hides immediately and cannot rearm a sound episode. Only a continuously known
  below-three separation can close and rearm that episode.
- The red `FOCUSED xN` card pulses only its alpha and border, never its geometry.
  When isolation is also proven, the pressure card stays centered while the
  separate amber isolation card keeps its own top-left position. On a narrow
  work area, actual scaled-card overlap stacks isolation below the pressure card.
- Added a separate optional selectable built-in FFXIV system sound. It is
  consumed once when a new high-pressure episode begins, not on every update,
  and includes a settings test button. No external or Windows audio is used.
- Added a separate default-off **Sprint once from a held movement key** option.
  It listens only to WASD/arrow movement keys, does not swallow the original
  key, and can request only the exact self Sprint action while direct pressure
  is still at least three. It shares the existing one-physical-generation/one-action
  coordinator, and is suppressed by own Guard. The generation is consumed
  before the final request; drift, rejection, or an exception cannot choose an
  alternate, replay, or retry.
- The complete shared action priority is Self-Purify, defensive utilities,
  pressure Sprint, Ally Rescue, reactive counter-CC, Ninja Seiton, Scholar
  Critical Strategy, then Monk Earth's Reply. One claimed physical generation
  cannot reach a later helper.
- Clarified command scope: `/seiton show` and `/seiton hide` control the entire
  plugin, while `/howmany show`/`hide` affect only the pressure counter and
  `/howmany reset` restores only that counter's window position. Standardized
  current feature wording to `PvP` and Smart Paean `manual or Turbo`.
- Reorganized settings into persistent Start, Alerts, HUD & Nameplates, Action
  Helpers, Job Tools, Macro Helpers, Targets, and Diagnostics pages. Existing
  settings, defaults, callbacks, ranges, and preview behavior are unchanged.
- Bumped the plugin version to 0.17.0.0 and configuration schema to 23. The
  visual warning is enabled for fresh and reset configurations but remains off
  for upgrading installations to avoid a surprise overlay. Sound and Sprint
  remain off for fresh, upgraded, and reset settings.

## 0.16.0.0

- Added a separate, persisted, default-off **Smart Paean target for manual or
  Turbo calls** option under Jobs > Bard. It is passive: on PvP Bard in exact
  Crystalline Conflict it may redirect only an already incoming The Warden's
  Paean `29400` ability call from the manual action path or a Turbo pulse, and
  never creates an action or consumes the shared generic input.
- Selection requires a complete, unique, stable exact party view. Eligible
  destinations are exact living, targetable, non-self party members without the
  live Warden's Paean ward `3143`, accepted by native 30-yalm range and line of
  sight, and with a trusted incoming-pressure count of at least three. Unknown
  pressure excludes only that candidate. Higher pressure wins, then lower exact
  HP ratio, party slot, entity ID, and game-object ID.
- With no complete exact party view or no known `3+` candidate, the original
  target and incoming call remain unchanged as vanilla behavior. After a
  redirect is frozen, final identity, job, exact resolved action/metadata,
  life/targetable state, HP, live ward, native reachability, or pressure drift
  suppresses that one call rather than falling back to its original target or
  choosing another ally. This passive transform deliberately has no cooldown
  or readiness gate.
- The helper never changes a selected target, substitutes an action, replays,
  or retries. Each later manual or Turbo call is evaluated independently, and
  client acceptance remains dispatch feedback rather than proof that Paean
  applied or affected crowd control. Existing Ally Rescue and Aquaveil behavior
  remains unchanged and separate.
- Hardened the experimental Ninja Seiton helper at its latest safe dispatch
  boundary. Immediately before its sole native request, Seiton Sense now
  re-resolves only the frozen `S#` actor and re-reads that exact actor's HP;
  healing to exactly 50% or higher cancels the spent attempt with no alternate,
  fallback, or retry. The unavoidable client-read-to-server-execution race
  remains a live boundary.
- Fixed the Guardian communication Bind pair not starting on clients that report
  an unused native marker slot as `0xE0000000` instead of `0`. Both native empty
  representations are now accepted only with otherwise exact marker telemetry;
  occupied, ambiguous, drifted, or foreign signs retain the same fail-closed
  ownership and cleanup rules.
- Bumped the plugin version to 0.16.0.0 and configuration schema to 22. Smart
  Paean remains off for new configurations, upgrades, and reset defaults;
  current-patch routing and effect behavior still require live confirmation.

## 0.15.0.0

- Added a separate, persisted, default-off **Auto Guardian Quick Chat + Bind
  pair** option under Jobs > Defensive utilities. It can run only after this
  module's automatic PLD Guardian `29066` request returns client-accepted in
  exact Crystalline Conflict; manual Guardian, Far Help, and rejected requests
  do not arm communication.
- The communication freezes the same exact party slot and may issue the
  client-localized CC Quick Chat row 35 (`Ziel decken`, displayed as `Ich decke
  ...` on a German client) for that `P#`. It may then place Bind2 on that exact
  party member followed by Bind1 on self.
- The complete marker pair is skipped when either sign is occupied or marker
  state is uncertain. Bind2 must be confirmed on the exact ally before Bind1 is
  attempted on self. If Bind1 then fails, only the proven-owned Bind2 may be
  cleaned. A complete pair expires nine seconds after Guardian was accepted;
  cleanup tries Bind2 and then Bind1, each only while actor, sign, and marker
  timestamp still prove ownership. Drift is relinquished rather than cleared.
- This path does not mutate any selected target, initiate another combat action,
  select an alternate, fall back, replay, or retry. Guardian client acceptance
  does not prove server-applied protection, and an issued command does not prove
  that Quick Chat or the signs appeared.
- Added a separate default-off **Critical Strategy on held gameplay key** option
  under Jobs > Scholar. On PvP SCH in exact CC, one shared held-key generation
  may select only among the complete unique canonical `S1`-`S5` enemies with
  live Guard `3054`/`3673`, exact living/targetable identity, verified Critical
  Strategy `29716` readiness, and native 25-yalm range/line of sight.
- If every eligible Guard candidate has an active, exact, non-negative team-
  pressure count and at least one is positive, highest team pressure wins and
  lowest exact HP ratio follows. If any eligible pressure is unavailable or
  invalid, or every count is zero, the whole selection is HP-first. Stable
  S-slot, entity ID, and game-object ID resolve remaining ties. Pressure is
  selection-only and is not a final dispatch gate.
- The Scholar helper never uses Critical Strategy as its ordinary 10% damage-
  taken debuff. On a Guard target, the current official effect instead halves
  Guard's defensive bonus for 10 seconds. The frozen intent and held generation
  are consumed before one native attempt. Final revalidation covers only exact
  identity, readiness, live Guard, and native range/line of sight, with no
  rerank, target mutation, alternate, fallback action, replay, or retry.
- Bumped the plugin version to 0.15.0.0 and configuration schema to 21. Guardian
  team communication and Scholar Critical Strategy both remain off for new
  configurations, upgrades, and reset defaults. Localized Quick Chat, marker
  placement/cleanup, and Critical Strategy dispatch/effect remain current-patch
  live-confirmation boundaries.

## 0.14.0.0

- Added a separate default-off **Seiton on fresh gameplay key** experiment for
  PvP Ninja in exact Crystalline Conflict. It considers every exact canonical
  `S1`-`S5` enemy that is strictly below 50% HP and natively reachable, then
  selects the lowest exact HP ratio with stable slot/actor tie-breaks.
- A genuinely fresh physical gameplay-key down edge is eligible only while the
  metadata-verified adjusted action is ready as base Seiton Tenchu `29515` or
  Unsealed follow-up `29516`. The exact target must remain living, targetable,
  hostile, strictly below 50% HP, and accepted by FFXIV's native action range
  and line-of-sight check immediately before dispatch.
- Kept Self-Purify, defensive utilities, Ally Rescue, and reactive counter-CC
  ahead of Ninja Seiton in the shared physical-generation chain. Active own
  Guard and the bounded 1.5-second Guard-propagation gate suppress the helper.
- The exact selected intent and input generation are consumed before at most
  one native action request. There is no target mutation, second selection,
  alternate target/action, fallback, replay, or retry after a race, false
  return, or exception; the original gameplay key is not swallowed.
- Client acceptance remains dispatch-only feedback and is not presented as
  proof that Seiton landed, executed the enemy, or caused a kill. Current-patch
  Crystalline Conflict timing and dispatch still require live validation.
- Bumped the plugin version to 0.14.0.0 and configuration schema to 19. The new
  action-initiating option is off for new configurations, upgrades, and reset
  defaults.

## 0.13.1.0

- Upgraded Near Help from strict lowest-party-HP routing to a bounded survival
  preference. Lowest exact HP remains the anchor and always wins at or below
  25% HP. Above that boundary, optional incoming pressure may reorder only
  candidates no more than 10 HP percentage points above the anchor.
- Pressure ordering requires a trusted live view and a non-negative count for
  every eligible candidate inside that window. Highest unique enemy
  pressure wins, then lower exact HP, shorter distance, and stable party/actor
  identity. Missing data inside the window or zero-only pressure falls back
  exactly to the previous lowest-HP behavior; unknown data outside it is ignored.
- Allowed the local player to enter Near Help selection only when the actually
  resolved friendly PvP action explicitly supports self-targeting and the same
  exact action-target plus native range/line-of-sight gates succeed. Actions
  that cannot target self keep excluding it.
- Preserved the existing one-shot boundary: Near Help still redirects only one
  explicitly armed, already incoming action, consumes before the game call,
  never changes the action ID or visible target, and has no alternate candidate,
  action, replay, or retry.
- Added the dedicated **Prefer incoming pressure near the lowest-health target**
  setting. Configuration schema 18 enables it for upgrades, new configurations,
  and reset defaults, while the shared macro-helper master itself remains the
  existing explicit opt-in. Bumped the plugin version to 0.13.1.0.
- Deterministic source tests cover the exact 25% and +10-point boundaries,
  incomplete-pressure fallback, overflow-safe ratios, self-target eligibility,
  and stable ordering. Current-patch live macro/action validation remains
  required before claiming in-game confirmation.

## 0.13.0.1

- Corrected Guardian `29066` targeting in both defensive utility selection and
  Far Help. FFXIV's native, hitbox-aware 20-yalm action-range and line-of-sight
  result is now authoritative, with no custom center-distance cap.
- Clarified that Guardian's 10-yalm condition governs staying close enough to
  the protected party member after the jump; it is not a 10-yalm cast limit.
- Added a 1.5-second `GUARDIAN TRIGGERED` card after the automatic PLD helper's
  native request is accepted. It identifies the selected party slot and says
  `CLIENT ACCEPTED`; it does not claim the server applied Guardian protection.
- Bumped the plugin version to 0.13.0.1. Configuration schema 17 and all saved
  settings remain unchanged.

## 0.13.0.0

- Added an enabled-by-default urgent isolation warning for exact Crystalline
  Conflict. It requires the complete five-person local party and FFXIV's native
  20-yalm range/line-of-sight result for every living ally. Continuous confirmed
  isolation enters after 500 ms, confirmed connection clears after 200 ms, and
  incomplete or unknown data stays silent. The large gently pulsing card is
  local, fixed at the top-left, and never issues an action or target change.
- Deliberately omitted automatic navigation, a tactical position guide,
  Splatoon integration, and map painting. The release reports only the narrow
  isolation fact it can verify rather than presenting rapidly changing tactical
  placement as authoritative.
- Added default-off Crystalline Conflict defensive utilities behind one shared
  physical-input-generation boundary. At known pressure from at least three
  unique enemies, exact Stun can request Purify; Guard requires positive live
  Resilience, removal of the CC, and a new release/repress generation, so Purify
  and Guard cannot fire from the same physical generation.
- Added a risk-based pre-Guard at or below 50% HP with three or more known
  incoming enemies and no existing Purify-removable CC. Added PLD Guardian for
  an exact party ally at or below 20% HP, strict distance below 10 yalms, native
  range/line of sight, and both own Guard and Guardian available. Lowest HP%,
  known higher pressure, distance, and stable identity determine the ally.
- Active own Guard now suppresses every Seiton Sense action-request helper. A
  non-extending 1.5-second propagation gate begins at an exact local Guard
  request and covers the interval before the live status appears, preventing the
  plugin from cancelling Guard. Manual FFXIV actions and other plugins remain
  outside that boundary, and exact live client/server ordering for the new
  defensive rules still requires current-patch validation.
- Expanded the default-off WHM Miracle helper into WHM/BRD reactive counter-CC.
  Both jobs can respond to the exact DNC Contradance `29432` startup; WHM uses
  Miracle of Nature `29228` at native 10-yalm range and BRD uses Silent Nocturne
  `29395` at native 20-yalm range. Existing MCH/SAM/VPR urgent startup paths
  remain WHM-only, with VPR still waiting for live Hardened Scales absence.
- Expanded the enemy-Purify follow-up from Stun to all six exact removable PvP
  statuses: Stun, Heavy, Bind, Silence, Miracle of Nature, and Deep Freeze. It
  requires exact self-Purify, positive live Resilience followed by stable real
  absence, and exact team focus of at least two: the local hard target plus at
  least one exact ally hard-targeting the same enemy.
- Replaced the Miracle-only success card with action-specific blue
  `AUTO CC LANDED` confirmation. It requires exact Miracle status for WHM or
  Silence for BRD on the pending enemy. It proves the counter-CC status landed;
  it does not prove Contradance, another limit break, or damage was interrupted.
- Added a separate default-off, exact-CC party-visible Attack1 focus module. An
  enemy is eligible only when Guard is known unavailable and HP is at or below
  50% and/or trusted low MP is active. The trusted state enters after 150 ms
  below 2,000 MP and clears after 150 ms at or above 2,300 MP. Ranking is both
  low, HP-only, MP-only, then lowest HP%, lowest trusted MP%, highest known
  team-target count, and stable `<e1>`-`<e5>` slot.
- The marker module sends only the hardcoded normal `/mk attack1 <eN>` command,
  never changes the selected target, never overwrites an occupied Attack1, and
  clears only after an empty-to-exact-target transition established ownership
  and the same slot, actor, and marker timestamp still match. Native command and
  shared-marker behavior still require live current-patch CC validation.
- Added focused Jobs settings and diagnostics for defensive utilities, reactive
  counter-CC, and the team-visible sign. Existing Miracle master/post-Purify
  choices migrate into the new reactive controls; DNC activation remains off
  for upgrades. Defensive and marker masters remain default-off; the reactive
  master defaults off for new installs while preserving an existing Miracle opt-in.
- Bumped the plugin version to 0.13.0.0 and configuration schema to 17. Source
  build and deterministic tests validate thresholds, ordering, fail-closed
  identity, debounce, and marker ownership rules; they do not claim fresh live
  confirmation of native line of sight, action timing, interruption, or the
  party-visible marker command.

## 0.12.0.1

- Hardened exact enemy resolution for the default-off CC-immunity brake when
  FFXIV omits the `Hostile` status flag in a public Crystalline Conflict match.
  The fallback is accepted only in a known public CC territory when the local
  party is exactly five members, includes the local player, and every party
  entity is currently visible. Self, party/alliance, life/targetable, native
  identity, and exact canonical `<e1>`-`<e5>` checks remain mandatory.
- Plugin-owned exact-target Miracle requests now bypass only macro target
  redirection. They still pass through the same final CC-immunity brake, so a
  verified blocker appearing between the helper's pre-check and its native
  request can stop that one attempt.
- The brake remains a stateless per-attempt hard stop. This hotfix adds no
  timer, stored input, target mutation, alternate action, replay, or retry.
- Bumped the plugin version to 0.12.0.1. Configuration schema 16 is unchanged.

## 0.12.0.0

- Added an independently default-off **post-Purify Stun** subtype beneath the
  existing default-off WHM Miracle master. It recognizes only an exact enemy
  self-Purify `29056` ActionEffect with one self target, a non-empty event
  sequence, and recovered-status effect `0x10` for Stun `1343`; the source must
  then resolve to exactly one canonical `<e1>`-`<e5>` opponent.
- The follow-up requires Resilience `3248` to be positively observed live within
  750 ms. It waits for 150 ms of stable live absence, abandons the release wait
  3 seconds after positive observation, and then provides one 500-ms release
  opportunity. It never predicts the timer or authorizes from `RemainingTime`
  or an internal status address.
- Existing urgent MCH/SAM/VPR Miracle threats keep priority. The new subtype
  shares the same eligible held-or-fresh physical key generation, exact actor
  and blocker revalidation, and native 10-yalm range/line-of-sight check. State
  and input are consumed before the sole Miracle attempt; there is no alternate
  target, fallback, replay, or retry. A priority wait retains the original
  verified release timestamp and can never restart or extend the 500-ms window.
- Extended the existing bounded single ActionEffect observer and Miracle threat
  queue rather than adding another hook or dispatch boundary. The blue landing
  confirmation distinguishes the post-Purify Stun follow-up, but still proves
  only that Miracle status `3085` landed on the intended enemy, not that an
  interrupt occurred.
- Bumped the plugin version to 0.12.0.0 and configuration schema to 16. Existing
  Miracle settings migrate unchanged, with the new subtype disabled until it is
  explicitly enabled.

## 0.11.0.2

- Fixed direct-hotbar and Turbo calls that represent FFXIV's selected target
  with an unchanged native carrier of either `0` or the default-target sentinel
  `0xE0000000` instead of a concrete actor ID. The brake now reads the local
  player's native selected hard target, requires it to remain stable during
  evaluation, and still resolves it to exactly one live canonical `<e1>`-`<e5>`
  opponent before checking protection.
- Added explicit redirect-suppression provenance so a zero deliberately created
  by Seiton's fail-closed Near Assist, Near Help, or Far Help path can never be
  reinterpreted as the selected target. Explicit actor IDs remain authoritative;
  missing, changed, non-canonical, or ambiguous identity still passes through.
- Added VPR Hardened Scales `4096` to Miracle of Nature's VPR-only blocker
  matrix. A bounded live CC trace captured two Miracle attempts where `4096`
  was the only relevant protection and neither applied Miracle status `3085`.
  Manual/Turbo Miracle attempts are now held by the same verified live status
  that already gates the VPR auto-intercept path.
- Expanded bounded CC-brake diagnostics with configured/current-context state,
  default/exact/failed target-resolution counts, invocation mode, original /
  forwarded / effective target IDs, and the exact last resolution result.
- Miracle intercept now retains an exact armed opportunity through a transient
  same-frame self-Purify or Ally Rescue priority claim. The original 500-ms
  MCH/SAM or 250-ms VPR deadline is never extended, the claimed physical input
  cannot be reused, and only a genuinely fresh eligible input generation inside
  that original window can make the one Miracle attempt.
- Added retained, in-memory Miracle opportunity diagnostics for recognized,
  armed, rejected, waiting, and expired threats plus the last opportunity
  outcome. Active threat/queue state is still cleared on context exit, while
  those aggregate diagnostics remain available afterward through
  `/seiton debug` and the settings panel.
- Kept Miracle's exact native 10-yalm range and line-of-sight gate. Live CC
  evidence reached that existing gate; this hotfix does not widen the range or
  relax identity, protection, deadline, input, one-attempt, or no-retry checks.
- Bumped the plugin version to 0.11.0.2. Configuration schema 15 is unchanged.

## 0.11.0.1

- Changed a confirmed CC-immunity-brake decision into a hard stop: the detour
  now returns `false` immediately without calling the downstream/original
  action function. This removes the former invalid-`targetId = 0` handoff,
  where later game processing could restore or resolve a default target and
  still let the action through.
- Kept the action-attempt boundary unchanged: there is no stored press, delayed
  dispatch, target change, fallback, replay, or retry. Every later physical
  press or Turbo pulse is still a fresh check against the current exact target
  status.
- Documented the unavoidable simultaneous-activation boundary. An action the
  server already accepted roughly 295-355 ms before immunity became locally
  visible cannot be recalled by a client-side pre-dispatch brake. FFXIV may
  still present that action's animation and damage while rejecting its status
  effect on the protected target.
- Miracle landing correlation now preserves the first still-unexpired pending
  helper attempt instead of allowing a later registration to overwrite it.
- Bumped the plugin version to 0.11.0.1. Configuration schema 15 is unchanged.

## 0.11.0.0

- Added a default-off, Crystalline-Conflict-only CC-immunity brake that works
  directly from incoming hotbar action attempts without requiring a macro.
- Added a master switch plus separate per-job and per-action controls for a
  conservative reviewed list: PLD Intervene; WAR Blota; BRD Silent Nocturne and
  Repelling Shot; WHM Miracle of Nature; BLM Lethargy; NIN Forked/Fleeting
  Raiju; MCH Air Anchor; AST Gravity II including its Double Cast form behind
  one setting; and SAM Mineuchi.
- The initial 0.11.0.0 implementation gave one protected attempt an invalid
  target; 0.11.0.1 superseded that handoff with an immediate `false` return
  before the downstream/original call. Standard Purify-removable CC and Miracle
  of Nature have separate blocker sets. Seiton Sense does not switch targets,
  select an alternative action or actor, store or replay the press, dispatch an
  action, or retry. Every later real press or Turbo pulse is checked again, so
  the first repeat after immunity disappears can pass normally; vanilla holding
  alone does not generate repeats.
- Excluded broad cone, ground, self-centered, and ambiguous multi-target CC from
  this first list. Blocking an enabled action also blocks any damage or movement
  attached to it, which is why every action can be disabled independently.
  Downstream target-rewrite plugins can still change the call after Seiton
  Sense and must be tested separately.
- Added a blue 1.5-second `MIRACLE LANDED` news flash for the existing WHM
  Miracle intercept. It appears only when the shared bounded action-effect
  capture reports the exact local caster, Miracle action `29228`, pending threat
  target, status-add effect `0x0E`, and Miracle status `3085` within 1500 ms of
  the one helper attempt. The subtitle identifies MCH LB, SAM LB, or VPR Nest.
  This confirms that Miracle landed; it does not claim that hostile damage was
  conclusively cancelled. A settings preview and live counters were added.
- Fixed live self-hotbar resource auras that could inherit a displaced oversized
  rectangle from a native hotbar container. Each bar now unions only its exact
  currently visible action-slot nodes, with no container fallback; preview and
  live drawing share those same anchors.
- Bumped the plugin version to 0.11.0.0 and configuration schema to 15. Existing
  settings migrate forward with the new master switch off; reviewed job/action
  selections default on behind that explicit opt-in.

## 0.10.0.1

- Fixed the low-resource preview appearing as a detached purple `430 x 58`
  rectangle that could overlap only one action-bar row instead of following the
  native HUD.
- The preview now copies the exact current rectangles of every visible native
  self hotbar, matching the live aura. The ordinary live resource-aura pass is
  suppressed while previewing, preventing duplicate or overlapping outlines.
- Configuration schema 14 is unchanged. This is a presentation-only hotfix and
  does not alter resource thresholds, targeting, actions, or native HUD nodes.

## 0.10.0.0

- Added a visual-only low-resource aura on the native HUD. At configurable
  thresholds (30% HP and 2,000 MP by default), visible self action bars softly
  pulse red for low HP, blue for trusted low MP, or purple for both. Party-list
  rows and exact Crystalline Conflict ally/enemy rows can show a subtler aura.
- Kept the aura read-only: it copies current visible native rectangles and draws
  a separate foreground overlay without writing to action slots or UI nodes.
  Initial/unknown MP does not warn, MP has a 300-point exit margin, and invalid,
  hidden, stale, duplicate, or ambiguous actor/row identity fails closed. Exact
  current-patch CC addon/node anchoring still needs live validation.
- Added a default-off PvP Monk Earth's Reply helper. It requires job `20`, exact
  Earth Resonance `3171`, verified current metadata, and adjusted Riddle of
  Earth `29482` -> Earth's Reply `29483`. It can attempt the reply at or below
  30% HP or with at most 1.25 seconds remaining by default.
- The Monk helper marks a continuous resonance spent before one exact self
  `29483` request. Self-Purify has priority; a false/throwing request is never
  retried, and Riddle of Earth `29482` is never used as a fallback. It supports
  Crystalline Conflict and explicitly enabled Wolves' Den testing. Native
  direct-call acceptance and exact live timing remain an in-game validation
  boundary.
- Reorganized quality-of-life settings under a Jobs tab with All jobs / General,
  Ninja, Monk, Bard / White Mage, and White Mage sections.
- Included the Far Help action-time backline preference from 0.9.0.2: a complete
  exact `<e1>`-`<e5>` snapshot prefers destinations with strictly more than 10
  yalms of horizontal hitbox-edge enemy clearance, while an unavailable or empty
  safe tier still selects the farthest otherwise valid reachable ally. Exact
  distance ties prefer healer, ranged/caster, then other.
- Preserved Far Help's strict three-line `<me>` carrier, no-selected-target
  fallback, 750-ms legacy same-action quarantine, and no-retry behavior. Only no
  valid reachable ally produces no movement.
- Bumped the plugin version to 0.10.0.0 and configuration schema to 14. Existing
  settings migrate forward; the new visual aura is enabled by default and every
  new action-attempt helper remains opt-in.

## 0.9.0.2

- Added a conservative Far Help backline preference evaluated at action time.
  All five native `<e1>`-`<e5>` slots must resolve to exact, unique, valid
  opponent identities. Confirmed dead opponents are ignored for clearance;
  live opponents count even while untargetable.
- A candidate must have strictly more than 10 yalms of horizontal hitbox-edge
  clearance from every live enemy to join the preferred backline group. The
  farthest member of that group wins. If no candidate can be certified, or the
  enemy snapshot is missing, ambiguous, invalid, or has no live enemy, Far Help
  instead chooses the farthest otherwise valid reachable ally. This
  map-agnostic preference cannot guarantee tactical safety.
- Role is now only an exact-distance tie-breaker: healer first, then
  physical/magical ranged or caster, then every other job. Native party order
  and stable actor identity resolve the remaining ties.
- Kept the fail-closed three-line `<me>` macro, native range/line-of-sight
  checks, strict Guardian distance, legacy same-action quarantine, and all
  no-selected-target/no-retry guarantees unchanged. Only having no valid
  reachable ally produces no movement.
- Bumped the plugin version to 0.9.0.2. Configuration schema 13 remains current;
  the backline/comparator hotfix changes no saved setting.

## 0.9.0.1

- Changed Far Help to a deliberately fail-closed three-line macro: `/mlock`,
  `/farhelp`, then exactly one reviewed mobility action using `<me>`. The former
  selected-target fallback is not part of the Far Help macro.
- All five reviewed actions cannot target self, making `<me>` intrinsically
  invalid even when the hook is unavailable or no token is armed. When no exact
  live, targetable, action-valid ally is reachable, no movement occurs. Far Help
  never substitutes the player's selected target, self, or another fallback
  actor.
- Added a migration guard that suppresses matching legacy calls of the same
  movement action for the remainder of the 750-ms window, including the old
  `<t>` fourth line and Turbo duplicates. That line should be removed and is
  not part of the supported macro.
- Kept the 750-ms one-shot token, healer/ranged preference, farthest-in-tier
  ranking, native range/line-of-sight checks, one original native call, and the
  exclusions for direct dispatch, Queue mode, visible target mutation, repeat,
  and retry.
- Bumped the plugin version to 0.9.0.1. Configuration schema 13 remains current;
  the hotfix changes no saved setting.

## 0.9.0.0

- Added default-off, Crystalline-Conflict-only `/farhelp` with collision-safe
  `/ssfar`. It redirects one already incoming reviewed friendly movement action
  to the farthest reachable exact non-self party member while preferring
  healers and physical/magical ranged jobs over all other jobs.
- Limited Far Help to the current reviewed PvP movement actions Guardian
  `29066`, Thunderclap `29484`, Aetherial Manipulation `29660`, Icarus `29261`,
  and Slither `39184`. The action's native range and line of sight remain
  authoritative; Guardian additionally requires a strict under-10-yalm
  distance.
- Added the original `/mlock`, `/farhelp`, action `<2>`, then action `<t>`
  macro pattern. Version 0.9.0.1 replaces this selected-target fallback with
  the intrinsically fail-closed `<me>` carrier.
- Near Assist, Near Help, and Far Help replace one another's pending token and
  share the existing single target-only detour. Unrelated actions do not
  consume Far Help; Queue mode, visible target mutation, direct dispatch,
  alternate actions, repeats, and retries remain excluded.
- Added a prominent live ON/OFF line under the experimental WHM Miracle master
  toggle. This makes it clear when all sub-triggers are selected but threat
  capture itself is still disabled.
- Bumped the plugin version to 0.9.0.0. Configuration schema 13 remains current
  because Far Help reuses the existing shared macro-helper opt-in and adds no
  persisted setting.

## 0.8.0.0

- Added a default-off, Crystalline-Conflict-only WHM Miracle intercept. One
  eligible held or freshly pressed physical key generation can make one exact
  Miracle of Nature attempt against the canonical MCH, SAM, or VPR opponent
  that produced the reviewed early Marksman's Spite, Zantetsuken, or Furious
  Backlash / Nest der Blutschuppen signal.
- Added independent MCH, SAM, and VPR trigger toggles. The exact action IDs are
  `29415`, `29537`, and `39188`; runtime selection additionally verifies the
  expected job, exact enemy identity, life/targetable state, and Miracle's
  native 10-yalm range and line of sight.
- VPR timing is transition-based rather than predicted: the exact Nest signal
  may arm a 250-ms opportunity, but dispatch waits until live Hardened Scales
  `4096` is truly absent. Verified full CC protection blocks deliberate casts
  into immunity.
- MCH and SAM start opportunities expire after 500 ms so a late key cannot
  spend Miracle after the relevant startup window.
- Self-Purify, Ally Rescue, and Miracle now share one physical-input priority
  path in that order. State and input are consumed before the sole Miracle
  request; no selected-target mutation, alternate target, fallback action,
  logical-Turbo repeat, or retry is added.
- Extended the existing single bounded ActionEffect observer for the three
  exact start signatures without adding another action hook. Local acceptance
  is diagnostic only and is not presented as proof that an enemy startup was
  interrupted. Current-patch live CC validation remains required.
- Bumped the configuration schema to 13 and the plugin version to 0.8.0.0.
  Existing users retain all prior settings and must explicitly enable the new
  action-attempt feature.

## 0.7.0.1

- Relaxed two fragile Ally Rescue prefilters: valid statuses no longer require
  an internal status-slot address, and a local cooldown-ready sample is no
  longer required before dispatch. Exact party identity, the four activation
  statuses, live/targetable state, current action, native range/line of sight,
  one consumed physical generation, one normal action request, and no retry
  remain unchanged; FFXIV decides whether the request can queue or execute.
- Fixed BRD metadata validation for the current lowercase leading article in
  `the Warden's Paean` while retaining the exact action ID and all other
  fail-closed metadata checks.
- Added exact Ally Rescue success confirmation. A cleanse is confirmed only by
  a correlated local-caster Paean/Aquaveil ActionEffect `0x10` result on the
  exact attempted ally for Stun, Heavy, Bind, Silence, Deep Freeze, or Miracle
  of Nature. Heavy and Bind are confirmation-only and remain excluded from
  activation.
- Added a 1.5-second blue `CLEANSED` popup plus an Overview breakdown of
  attempts, client-accepted requests, and confirmed removals. Confirmed totals
  are tracked for the current match and plugin session, with per-action and
  per-status details and a reset control; none of these aggregates persist.
- Expanded the existing bounded local ActionEffect observation only for the
  exact Ally Rescue confirmation path. No names, combat history, raw-payload
  persistence, telemetry, external network traffic, or uploads are added; the
  new aggregate counters remain memory-only. Current-patch live validation of
  the action result and popup is still required.

## 0.7.0.0

- Added an opt-in, CC-only Ally Rescue experiment for BRD and WHM. One fresh or
  explicitly eligible held physical key can attempt The Warden's Paean or
  Aquaveil on an exact non-self party member with Stun, Silence, Deep Freeze, or
  Miracle of Nature; Heavy and Bind deliberately do not trigger it.
- Ally Rescue filters through the exact action's live native range and line of
  sight, then ranks candidates by lowest exact HP percentage, highest known
  unique incoming enemy pressure, lowest trusted MP percentage, distance, and
  stable party identity. Current metadata is validated per job and selection is
  independent of the client's display language.
- Self-Purify and Ally Rescue now share one physical-key generation coordinator,
  with self-Purify first. State and input are consumed before the sole action
  attempt, rejection or exceptions never retry, and internal calls bypass the
  Near Assist/Near Help target redirect without consuming their token.
- Added opt-in `/nearhelp` with collision-safe `/sshelp`. It arms one 750 ms
  token for the next supported friendly PvP macro action and selects only an
  exact, live, non-self party member inside that action's native range and line
  of sight.
- Near Help ranks candidates by the lowest exact HP percentage, then shorter
  distance, native party order, and stable actor identity. Dual-purpose
  friendly/hostile actions remain supported when their metadata explicitly
  allows party or ally targets.
- Added a target-safe macro carrier: `/mlock`, `/nearhelp`, the friendly action
  with `<2>`, then the same action with `<t>`. Only the exact native party-slot-2
  carrier can be invalidated when no redirect is possible; a compact `<t>` call
  keeps its actual target unchanged.
- Near Help and Near Assist share the existing single action boundary and
  replace each other's pending token. Neither helper selects a visible target,
  sends an action, loops, or retries; the already incoming call reaches the
  game exactly once.
- Updated the recommended Near Assist macro to begin with `/mlock`, preventing
  ReAction Turbo Hotbar from restarting a held macro before its fallback line.

## 0.6.0.2

- Replaced the tiny distant active-CC icon with one large static crossed-`CC`
  emblem anchored directly above FFXIV's native nameplate job icon. It uses a
  red prohibition stroke, bright side chevrons, and a separate high-contrast
  countdown without competing source text inside the symbol.
- Overlapping Guard and full immunity now resolve to the farthest verified
  expiry in one stable emblem instead of duplicating or swapping shorter
  warnings. Crossed Guard cooldown, MP, Seiton, and pressure indicators retain
  fixed positions.
- Added a dedicated isolated emblem preview and size setting. The display uses static
  contrast instead of pulse, fade, resizing, or world projection; the existing
  status/anchor grace and absolute expiry remain unchanged.
- Added the verified CC-protection metadata count to `/seiton debug` so live
  testing can distinguish detection from presentation failures.

## 0.6.0.1

- Simplified Near Assist after live testing showed that FFXIV and Turbo Hotbar
  can advance or hide the macro-line text before the native action call reaches
  Seiton Sense. The command now arms the bounded one-shot directly; the next
  supported hostile PvP action consumes it without a second fragile macro-line
  proof.
- Changed the recommended targetless macro to an intuitive `<e1>` carrier
  followed by the normal `<t>` line. The carrier is redirected to the selected
  nearby ally's actual S1-S5 target, not necessarily S1. Failed carriers are
  invalidated so the authored `<t>` fallback can run.
- Added exact native hard-target matching through both game-object and network
  entity identity. Only a verified hostile PvP action can consume the token, so
  Guard, Purify, Recuperate, and other defensive calls cannot steal or be
  invalidated by Near Assist. The E1 carrier is recognized through exact E1
  identity rather than treating every changed target as a carrier. Near Assist
  still performs one target-ID substitution at most, calls the original game
  action once, rejects generic Queue mode, never visibly changes target, and
  never retries.

## 0.6.0.0

- Integrated the useful HOWMANY pressure view into Seiton Sense. The movable
  counter combines exact enemy hard-target, cast-target, bounded recent harmful
  action, and early MCH-LB-marker evidence. Its main number now uses an explicit
  pixel-sized game font for a sharper result, with optional job icons, CC slots,
  threat colors, background, locking, and click-through.
- Added enemy-nameplate pressure cues in permanently reserved slots: `P#` is the
  number of valid allies currently hard-targeting that exact enemy, while
  `YOU`, `HIT`, and `LB` distinguish direct intent, recent harmful action, and
  the Marksman's Spite marker aimed at the local player.
- Added an optional Near Assist team-pressure preference. It ranks pressure
  inside the existing nearby-candidate window, then retains the normal
  damage-role/distance preference and original macro fallback. It is off by
  default.
- Added stable, emphasized CC-protection icons and remaining-time labels beside
  native job icons. The exact validated catalog is Guard `3054`/`3673` folded
  into one family, Resilience `3248`, WAR Inner Release `1303`, SAM Meikyo
  Shisui `1320`, VPR Hardened Scales `4096`, and large-scale-only Swift `4477`.
  One-hit, partial, and ambiguous wards are deliberately excluded.
- Hardened native-nameplate joins with exact game-object plus network-entity
  identity, fixed indicator slots, stale-identity rejection, a short missing
  sample grace, and non-drifting absolute status expiry. Active Guard replaces
  the crossed Guard-cooldown presentation instead of duplicating it.
- Enlarged the Marksman's Spite warning by default and added a selectable,
  testable built-in FFXIV sound. A verified threat produces at most one sound
  attempt; the feature remains warning-only and never presses Guard.
- Added personal-warning background opacity independent of icon, text, and
  border opacity, allowing a fully transparent card fill without hiding the
  warning.
- Reorganized settings into Overview, Pressure, Warnings, Seiton, Assist,
  Targets, and Advanced tabs.
- Updated Near Assist to a 750 ms CC-only token, a 25-yalm default search, and
  Turbo-compatible exact macro provenance. The recommended targetless macro can
  use a `<me>`/`<self>` carrier followed by the normal `<t>` line; the compact
  two-line `<t>` form remains supported. A selected target is not required for
  a valid carrier redirect attempt. An unredirected carrier, including the
  no-candidate case, is made invalid so self-targetable hostile skills cannot
  consume it before the authored `<t>` fallback.
- Preserved the one-shot Near Assist boundary: one incoming macro action, one
  possible target-ID replacement, one original game call, no visible target
  switch, alternate action, generic queued-action mode, or retry. Near Assist
  remains unavailable in Wolves' Den, Frontline, and Rival Wings.
- Kept pressure and protection available independently of Near Assist. Wolves'
  Den warning/protection testing uses the strict duel opponent, with a separate
  pressure opt-in; pressure and verified protection also support large-scale
  PvP without CC enemy-slot labels.
- Extended the fixed current-target information card with team and incoming
  pressure context while keeping it separate from native nameplates.
- Bumped the configuration schema to 10. Existing Near Assist distances of 15
  yalms or less migrate to the new 25-yalm default; new pressure, protection,
  warning-opacity, and MCH-sound controls receive bounded defaults.
- Updated privacy documentation for the bounded action-effect pressure observer,
  exact actor/status data, local built-in sound, and transient macro provenance.

## 0.5.0.0

- Added an independently configurable, warning-only Marksman's Spite alert.
  It recognizes the exact early MCH-LB target-marker event aimed at the local
  player and rejects the later damage/miss event, leaving roughly the normal
  visible sniper reaction window. The alert never presses Guard or any action.
- The marker observer is bounded, read-only, enemy/job validated, and active
  only for supported PvP while the warning is enabled. Metadata drift disables
  only this alert; no character name or combat history is stored.

- Integrated a default-off, Crystalline Conflict-only Near Assist macro helper.
  `/nearassist` arms one 500 ms token for only the immediately following hostile
  macro action; the command never sends an action itself.
- Near Assist defaults to a 25-yalm smart search. Among allies no farther from
  you than the nearest candidate plus 8 yalms, it prefers ranged/caster DPS, then melee
  DPS, then support; an option restores strict nearest-distance selection.
  The chosen ally must hard-target an exact opponent from FFXIV's canonical
  `<e1>`-`<e5>` CC list.
- Re-resolves the same enemy slot and checks the exact action's native range and
  line of sight at dispatch time. Success may replace only that action's target
  ID; every failure preserves the original `<t>` target exactly.
- Added a configurable 5-30 yalm ally-search radius with a 25-yalm default. This
  search radius is independent of the actual action-range check.
- Added one-shot and fail-closed boundaries: no visible hard/soft/focus target
  change, direct action, alternate opponent, retry, or Wolves' Den/Frontline/
  Rival Wings fallback.
- Bumped the configuration schema to 9. Near Assist is disabled on update and
  reset so existing users must opt in deliberately; prior HUD, target-highlight,
  warning, and Purify settings remain unchanged. The new warning defaults on
  like Wildfire and Death Warrant and can be disabled separately.
- Retired the standalone NearAssist workflow. Disable or remove the old plugin
  before loading this version because both use the `/nearassist` command.

## 0.4.0.0

- Integrated the Super Focus Glow visual language as a separate, optional
  focus-target module: projected hitbox ring, layered halo, rays, chevrons,
  label, pulse, color, foreground, rainbow, and reduced-motion controls.
- Added a distinct optional current-target highlight with an independent cyan
  default style and a default-on PvP-only safety boundary.
- Added a separate configurable current-target information card at a fixed
  screen position. It never attaches information to native nameplates, job
  icons, health bars, or Seiton Sense's existing nameplate slots.
- Added focus and current-target presets. The focus preset reproduces the
  migrated red Super Focus Glow setup; this first release does not read or
  modify the standalone plugin's configuration file.
- Bumped the configuration schema to 7. All three new display modules are
  disabled by default, and schema-6 HUD, warning, held-key, and per-debuff
  Purify settings remain unchanged on update.

## 0.3.0.3

- Added a separate, default-off option allowing a gameplay key that was already
  physically pressed before an enabled CC to trigger the one-attempt Purify
  helper when that status first appears. This intentionally includes WASD.
- Added per-key physical hold generations. ReAction Turbo's logical repeats do
  not create new input, and the generation is consumed as soon as Purify arms
  or dispatches. A continuous hold cannot trigger again after timeout, status
  replacement, or status reapplication; the key must be released first.
- Keys already down during initial observation, option activation, reset, or
  text input remain ineligible until released. Fresh-key behavior and all six
  individual debuff toggles remain unchanged.
- Added dependency-free coverage for held-generation priming, consumption,
  release, text input, reset, status-entry authorization, and one-shot behavior.

## 0.3.0.2

- Fixed the opt-in Purify helper discarding the first important key edge by
  keeping a read-only input baseline throughout supported PvP and accepting a
  fresh edge on the first observed debuff frame.
- Removed fragile animation-lock, action-status, adjusted-ID, cooldown, MP, and
  targetability prefilters. A selected debuff plus one fresh key now produces
  exactly one normal native Purify request; FFXIV validates/queues it and no
  failed or rejected request is retried.
- Ordinary ImGui keyboard capture no longer masquerades as text entry, so the
  helper can be tested while its settings window is visible. Real chat/text
  input still blocks it without consuming the debuff window.
- Completed the current Purify suite: Stun, Heavy, Bind, Silence, Deep Freeze,
  and Miracle of Nature. Each type now has an independent auto-Purify toggle;
  the warning display can remain enabled for types whose automation is off.
- Personal warnings and self-Purify in Wolves' Den no longer depend on resolving
  an enemy HUD actor. Enemy nameplate/Seiton data keeps the strict duel-opponent
  requirement.

## 0.3.0.1

- Added an enabled-by-default Wolves' Den duel test mode for the complete HUD:
  Seiton cues, Guard/MP nameplate indicators, personal debuff warnings, and the
  separately opt-in Purify experiment.
- A duel is accepted only when FFXIV exposes one valid, targetable native duel
  opponent carrying the hostile flag. That opponent receives a synthetic
  visual `S1`; missing or invalid identity fails closed. Party-member duels are
  supported because the native duel identity remains authoritative.
- Crystalline Conflict still uses FFXIV's exact native `<e1>`-`<e5>` order.
  Wolves' Den `S1` does not claim that the `<e1>` macro placeholder exists in a
  duel. Frontline and Rival Wings remain excluded.
- Duel state is cleared when the strict opponent disappears or changes so a
  Guard estimate or Seiton cue cannot leak into a later duel.

## 0.3.0.0

- Replaced the short-only Seiton prompt with a persistent, center-adjacent
  official job-icon card showing the configurable key label and exact S1-S5
  slot; the default presentation reads `SHIFT + 1-5`.
- Added an optional `PREP` cue for verified Seiton candidates from 50% to below
  60% HP, while retaining a one-time pulse when the real execute window begins.
- Added stable local warnings and countdowns for Wildfire, Death Warrant, Stun,
  and Miracle of Nature, with independent Patch 7.5 metadata validation.
- Added an experimental Purify-on-next-fresh-key buffer, disabled by default.
  The original key remains untouched and one key permits at most one native
  Purify attempt within the configured bounded window.
- Purify attempts are consumed before dispatch: there is no retry after client
  or server rejection, no alternative action or target change by Seiton Sense,
  and no packet or network-reply manipulation. Rules in other plugins that
  rewrite Purify remain outside this guarantee.
- Added fail-closed cancellation for timeout, status removal/replacement, death,
  text input, configuration disable, and leaving Crystalline Conflict.
- The plugin remains local-only with no accounts, telemetry, gameplay upload,
  character-name collection, or stored input history.

## 0.2.0.0

- Replaced 20 Hz world-projected overhead labels with per-frame, read-only
  anchors copied from FFXIV's native PvP nameplate job icons.
- Expanded the HUD to every job in Crystalline Conflict.
- Added a crossed Guard icon and countdown after an enemy Guard is actually
  observed; unknown cooldowns are never guessed and KO/revive resets it.
- Added a crossed blue Standard-issue Elixir below the current 2,000 MP cost
  of Recuperate, with trusted-sample gating, debounce, and recovery hysteresis.
- Rebuilt Seiton readiness so transient facing, animation-lock, casting, and
  current-target state cannot make the alert flicker or prevent the popup.
- Added a configurable one-shot Seiton popup containing the enemy's official
  job icon and exact S1-S5 slot, plus a stable nameplate Seiton indicator.
- Added fixed indicator slots, a 200 ms range/LoS false-grace, and a separate
  popup rearm condition so range jitter cannot spam alerts.
- The plugin remains display-only: it never changes native nameplate nodes,
  targets, inputs, actions, or networking.

## 0.1.0.1

- Keep valid execute labels visible when the only native result is that the
  player is not currently facing the enemy.
- Continue to suppress alerts for line-of-sight failures, out-of-range targets,
  and unknown native results.

## 0.1.0.0

- Initial Ninja-only Crystalline Conflict execute overlay.
- Exact S1-S5 mapping from FFXIV's native `<e1>`-`<e5>` enemy slots.
- Strict below-50% HP, native 20-yalm range/line-of-sight, and current
  Seiton-usability gate, including Unsealed Seiton Tenchu.
- One-shot flash with two-sample confirmation and stable 52% rearm threshold.
- Strict startup validation for both Seiton action variants and Unsealed
  Seiton Tenchu; changed game data fails closed.
- Configurable label size, height, tracking distance, flash, and previews.
- Display-only safety contract, dependency-free core tests, and verified
  custom-repository package.
