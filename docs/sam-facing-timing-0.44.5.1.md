# SAM facing timing — 0.44.5.1

## Evidence

- The [official Samurai job guide](https://na.finalfantasyxiv.com/jobguide/samurai/)
  lists **1.3 seconds** for both PvP Ogi Namikiri and Tendo Setsugekka. Meikyo
  Shisui grants access to Tendo; the Kaeshi follow-ups are instant.
- [BossMod's action-manager implementation](https://github.com/awgil/ffxiv_bossmod/blob/master/BossMod/Framework/ActionManagerEx.cs#L360-L364)
  explicitly describes an empirically observed facing requirement before the
  slidecast boundary and uses 0.5 seconds remaining to distinguish this phase.
  This is relevant direct implementation evidence for earlier facing,
  not an official guarantee or an Ogi/Tendo-specific PvP measurement.
- The author of [Black Mage in the Shell (Endwalker)](https://miyehn.me/ffxiv-blm-rotation-endwalker/)
  reports checking logs and finding a small cast-time dependence in slidecast
  duration, approximated as **0.5 seconds** in that simulator. This is firsthand
  PvE research, not a measured current PvP SAM facing or line-of-sight cutoff.
- [FFXIVClientStructs ActionManager](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/ActionManager.cs)
  documents that IsActionOffCooldown accounts for slidecastability, but does
  not provide an exact window or prove when the server commits facing/LoS.
- The user reports interruptions at **0.33, 0.27 and 0.25 seconds remaining**.
  These are user observations, not synchronized server timing measurements.

Slidecasting describes movement being possible before the visible cast bar
ends. It must not be treated as proof that range, facing, line of sight, buffs,
and damage all commit at the same instant. No reliable Ogi-specific or
Tendo-specific server boundary was established by this review.

## Change and rationale

The previous helper waited until **0.15 seconds remained**. That is after all
reported interruption points and also later than the approximate 0.5-second
slidecast reference. Its native exact-cast check cannot turn after that cast
has already disappeared or been cancelled.

The new default is **0.60 seconds remaining**: the 0.5-second reference plus
0.1 seconds of experimental headroom. This margin is not measured network
latency, and it does not promise that a rotation reaches the server in time.
For an unmodified 1.3-second cast, this corresponds to roughly 0.7 seconds
after casting begins. Runtime scheduling uses the current cast's observed
remaining time rather than assuming a fixed duration from the key press.

The configurable range expands to 0.05-1.00 seconds. The old exact 0.15
default migrates once to 0.60; customized values and the on/off choice stay
saved. An explicitly chosen old 0.15 is indistinguishable from the old default.
The feature remains off for fresh/reset settings.

## Unchanged boundaries and limitations

- One native automatic-facing call for the same frozen target; no fresh target
  search, target switch, camera turn, raw rotation write, or repeated tracking.
- The game automatic-facing option remains necessary.
- Exact cast/action/actor identity, native cast presence, and existing target
  protection still apply. An already cancelled cast is not revived or retried.
- Den testing uses the current exact visible duel/dummy target. Changing that
  hard target suppresses the facing attempt; CC retains its frozen actor.
- A target can move behind again after the one turn. True range/LoS loss,
  crowd control, or network delay can still interrupt the cast.
- Added offline scenarios cover 1.3-second Ogi and Tendo casts crossing the
  0.60-second threshold, the reported later cast-loss points, one-shot behavior,
  identities, timing bounds, migration and preserved opt-in. They do not verify
  a successful in-game turn or hit.
