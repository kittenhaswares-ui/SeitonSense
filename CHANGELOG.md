# Changelog

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
- Added the target-safe `/mlock`, `/farhelp`, action `<2>`, action `<t>` macro
  pattern. The exact `<2>` carrier is invalidated only when redirect validation
  fails so the authored vanilla target fallback can run. Compact `<t>` keeps
  its original target.
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
