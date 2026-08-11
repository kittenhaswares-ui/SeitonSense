# Changelog

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
