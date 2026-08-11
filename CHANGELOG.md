# Changelog

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
