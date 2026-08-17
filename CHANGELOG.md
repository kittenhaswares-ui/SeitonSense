# Changelog

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
