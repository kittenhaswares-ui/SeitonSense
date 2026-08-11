# Changelog

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
