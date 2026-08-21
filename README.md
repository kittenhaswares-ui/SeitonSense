# Seiton Sense

Seiton Sense is a local PvP awareness HUD that combines pressure tracking,
stable native-nameplate cues, personal warnings, job tools, one-shot macro
assistance, and target highlights. Version 0.28.0.0 adds the explicit manual NIN
`/panicshu` macro command. Each invocation freezes one exact terrain point 19.5
yalms along the character's facing and may make at most one native Shukuchi
attempt inside a 500-ms lease. It never originates from an automatic, pressure,
enemy, status, or held-key trigger; changes no mouse cursor or target; and has no
retry, shorter-distance fallback, or destination recomputation. It retains
v0.27.1's bounded reactive held-key attachment, exact post-Purify/post-Guard
episode memory, native pre-rank reachability, three-second NIN protection-end
lease, and source-sequence-bound automatic landing confirmation; v0.27's
metadata-verified Forked and Fleeting Raiju variants; plus v0.26's
Purify-first scheduler and six job-specific second-tier order, optional positive
pressure ranking, v0.25's stable held-key leases across all ten physical-hold
helpers, separate default-off native cast-cancellation test, and 50-ms/eight-call
explicit-rejection retry; v0.23's exact ranked post-Purify/post-Guard dispatch;
v0.21's optional Combat
Frame interaction and evidence-only Limit Break telemetry, default-off Dark
Knight Hiebsprung helper, and BRD coverage for reviewed DNC/MCH/SAM/VPR startup
signals, plus v0.20's Smart Recuperate fix and accepted-Eukrasia
Smart Kardia, fixed Combat Frames, removed speculative pre-Guard, and independent
PLD Guardian Job Tool, plus v0.18.0.1's corrected DRK macro and narrow,
explicitly enabled Wolves' Den striking-dummy test path.
The suite combines the useful parts of HOWMANY, CCImmunityWatch, NearAssist,
and Super Focus Glow into one configurable custom-repository plugin.

## Highlights

- **Sharp pressure counter:** an integrated HOWMANY-style counter shows how many
  enemies are currently pressuring you. It combines verified hard targets, cast
  targets, a bounded recent-harmful-action window, and the early MCH limit-break
  marker. The main number uses an explicit pixel-sized game font instead of
  scaling the whole window, with optional attacker job icons, CC enemy slots,
  threat colors, background, locking, and click-through.
- **High-pressure alarm:** three or more exact current hard/cast targets produce
  a large fixed red `FOCUSED xN` card at the top center. The separate isolation
  warning keeps its own top-left position; only on a narrow work area where the
  two scaled cards would overlap is isolation stacked vertically below it. Both
  pulse only their border/alpha, never their screen position or size. The
  optional sound is a selectable built-in FFXIV system sound and fires once on entry rather than every frame.
- **Optional pressure Sprint:** a separate default-off option may use continuous
  held WASD/arrow movement-key consent for an exact self Sprint episode while
  the same direct-enemy count is at least three. The movement key still reaches
  FFXIV. Known unavailability waits, explicit client rejection permits only the
  bounded same-intent retry, and any later native PvP action ends Sprint.
- **Stable held-action leases:** Purify, reactive counter-CC, Ally Rescue,
  Guardian, NIN, SCH, DRK, Smart Recuperate, Guard, and pressure Sprint prefer an
  already-held movement key, then any other stable held key, before fresh
  movement/other fallbacks. Every helper keeps its exact key lease once bound
  rather than letting a later action tap replace and prematurely cancel it.
  Reactive urgent-startup events may bind the first eligible current generation
  inside the original short threat lease. Purify/Guard retain their exact enemy
  episode while protection is live and bind only at authoritative protection end
  inside the original 500-ms release window; a text-poisoned generation is never
  eligible, and no different key can inherit the intent after binding.
- **Experimental held-action cast cancellation:** a separate default-off test
  may request one native cancel for the current cast when the highest-priority
  exact held intent is otherwise ready. It never requests the helper in that
  same frame, synthesizes movement or Escape, clears the queue, or changes a
  target; the later helper frame repeats full validation. The void cancel call
  reports only `requested`, not confirmed, and current-patch BRD/MCH live proof
  remains pending.
- **Pressure on enemy nameplates:** `P#` shows how many valid allies currently
  hard-target that enemy. A separate fixed slot shows `YOU`, `HIT`, or `LB` when
  the enemy is directly targeting/casting at you, recently hit you, or placed
  the verified MCH limit-break marker on you. These are read-only cues and never
  change your target.
- **Visible CC protection:** one large, static crossed-`CC` emblem and countdown
  are anchored above each enemy's native job icon. Guard and full immunity share
  the same unmistakable symbol without competing with the small utility slots.
- **Optional CC-immunity brake:** selected targeted CC actions can be held back
  while their exact enemy target has verified protection against that CC. It
  works from the normal hotbar without a macro and checks every real press or
  Turbo pulse independently, including unchanged native selected-target
  carriers `0` and `0xE0000000`. A confirmed block returns immediately without
  invoking the downstream/original action call; Seiton Sense never stores, replays,
  redirects, or retries it. A strict complete-party fallback can certify an
  exact public-CC `<e1>`-`<e5>` enemy when FFXIV omits its `Hostile` flag.
- **Personal warnings:** Wildfire, Death Warrant, supported Purify-removable CC,
  and Marksman's Spite receive stable warnings. The MCH LB card is larger by
  default and can play one selectable built-in FFXIV sound per verified threat.
  Warning-card background opacity is independent from its icon, text, and
  border, so the fill can be fully transparent without hiding the warning.
- **Urgent isolation warning:** in exact Crystalline Conflict, a large gently
  pulsing top-left warning appears only after a complete five-person local party
  proves that no living ally is within 20 yalms and native line of sight. Missing,
  ambiguous, or unknown native reachability stays silent.
- **Native-HUD resource aura:** low HP softly pulses red around your visible
  native action bars, trusted low MP pulses blue, and both together pulse
  purple. The same state can be drawn more subtly around exact party-list and
  Crystalline Conflict ally/enemy rows. This is a separate foreground overlay;
  native HUD nodes, action slots, animations, and input are never changed.
- **Fixed Combat Frames:** a separate default-off, Gladius-style screen-space
  overlay shows one Self frame plus stable canonical `S1`-`S5` enemy rows. It can
  show job, HP, trusted MP with 2,000-MP divisions, relevant statuses, pressure,
  current/focus-target accents, exact Self LB, calibrated remote LB, activation
  countdowns, and direct ally LB damage. Rows never move with actors or disappear
  behind obstacles. An optional interaction leaf provides one revalidated hard-
  target click and native `<mo>` hover for fresh exact living enemy rows; the
  native HUD itself is never edited or hidden.
- **Ninja Seiton decisions:** persistent job-icon cards, `S1`-`S5`, preparation
  cues, and entry pulses use FFXIV's native CC enemy order and verified
  range/line-of-sight checks.
- **Experimental Ninja Seiton helper:** a separate default-off option can use
  continuous held-key consent for exact adjusted-action Seiton epochs. It selects
  the lowest exact HP ratio among canonical `S1`-`S5` enemies that are strictly
  below 50% and natively reachable. Exact CC context, Ninja job, adjusted action
  readiness, own-Guard safety, and the shared higher-priority helper boundary
  all fail closed. Base Seiton and the verified Unsealed follow-up are distinct
  epochs; a rejected base request can never substitute the follow-up.
- **Experimental Scholar Critical Strategy helper:** a separate default-off
  held-key option selects only among the complete canonical `S1`-`S5` enemies
  with live Guard. Fully trusted positive team pressure ranks first, otherwise
  exact HP does; every target still requires native 25-yalm range/line of sight.
- **Experimental Sage Smart Kardia helper:** a separate default-off option arms
  only after the existing Eukrasia call is forwarded unchanged and accepted by
  the client. Inside that two-second opportunity it requires causal Eukrasia
  charge/status evidence, an animation-lock-clear Kardia boundary, and a fresh,
  complete exact five-player pressure view. Trusted direct pressure of at least
  two ranks first, with exact self as the sole no-pressure fallback. It makes at
  most one direct-target Kardia attempt without switching the selected target.
- **Experimental Smart Recuperate helper:** a separate default-off held-key
  option can use exact self Recuperate `29711` when at least 16,000
  HP is missing and at least 2,000 MP is available. The thresholds are inclusive;
  cooldown, MP, cast, queue, or animation-lock shortage waits without consuming
  the held consent. An explicit client rejection may retry only the same exact
  self epoch; acceptance is terminal and is never redirected or replayed.
- **Experimental Dark Knight Hiebsprung helper:** a separate default-off held-
  key option considers only exact canonical `S1`-`S5` enemies at 30% HP or lower
  inside a strict 10-yalm center-distance cap and native range/line of sight. A
  continuous hold can authorize one frozen intent per proven ready epoch; an
  accepted later request requires an observed cooldown not-ready-to-ready
  transition, while clean rejection uses only the shared bounded retry.
- **One-shot Near Assist:** an opt-in, CC-only macro helper can redirect one
  already incoming PvP macro action to the exact `<e1>`-`<e5>` hard target of a
  nearby ally. It does not visibly switch your selected target.
- **One-shot Near Help:** `/nearhelp` redirects one already incoming friendly
  PvP macro action to an exact reachable party target, including self only when
  the resolved action explicitly supports it. Lowest HP is the anchor; above
  the critical 25% boundary, optional incoming pressure can win only within a
  10-percentage-point health window. Unknown pressure falls back to exact HP.
- **One-shot Far Help:** `/farhelp` redirects one already incoming, reviewed
  friendly movement action to a reachable non-self party member. It first
  prefers destinations with strictly more than 10 yalms of horizontal
  hitbox-edge clearance from every live enemy, then chooses the farthest one.
  If none can be certified, it still chooses the farthest valid reachable ally.
  Only an exact distance tie prefers healer, then ranged/caster, then another
  job. It supports Guardian, Thunderclap, Aetherial Manipulation, Icarus, and
  Slither. Only no valid reachable ally means no movement; it never falls back
  to your target.
- **Manual NIN Panic Shukuchi macro:** `/panicshu` makes one fail-closed
  Shukuchi attempt at a frozen terrain point 19.5 yalms along the character's
  facing. It is command-only, never automatic or held-triggered, changes no
  cursor or target, and never retries or searches for a shorter fallback point.
- **DRK Shadowbringer macro:** the separate default-off `/seitonbringer` helper
  pairs only with the immediately following authored Souleater Combo `<t>` line
  on exact PvP Dark Knight. It supports canonical `S1`-`S5` enemies in
  Crystalline Conflict and, when the existing test option is enabled, only the
  exact current hard-target Wolves' Den striking dummy. With ReAction Macro
  Queue and Turbo, it may attempt Shadowbringer at most once per proven
  2.40-second GCD, and only at 0.60-0.80 seconds remaining. It never changes a
  target, substitutes an action, or retries.
- **Experimental Ally Rescue:** on BRD or WHM, one fresh or explicitly eligible
  held gameplay key can keep consent active for Paean or Aquaveil on an exact party
  member suffering Stun, Silence, Deep Freeze, or Miracle of Nature. Selection
  uses HP, incoming pressure, trusted MP, and distance in that order. A matching
  explicit client rejection retains only that frozen status/actor intent for a
  bounded retry; acceptance is terminal. A matching successful status-removal
  effect produces a blue `CLEANSED` popup and feeds resettable, in-memory match/
  session counters only when it carries the exact source sequence from the
  plugin's accepted request; a manual Paean or Aquaveil cannot claim it.
- **Smart Bard Paean target:** a separate default-off exact-CC option examines
  only an already incoming manual or Turbo Warden's Paean call. It
  may redirect that call to an exact reachable non-self party ally with trusted
  incoming pressure from at least three unique enemies. No initial candidate
  preserves vanilla; drift after one exact redirect is frozen suppresses only
  that call. It never initiates an action or selects an alternate.
- **Experimental reactive defensive utilities:** the default-off CC helper can
  use continuous held consent for a high-pressure Stun Purify and a distinct
  later Guard episode after positive Resilience. It does not
  pre-Guard from HP/pressure prediction. Active own Guard and the bounded
  1.5-second status-propagation window after an exact Guard request block all
  plugin action helpers.
- **Experimental Paladin Guardian job tool:** an independent default-off held-key
  option can attempt Guardian on one exact critically low reachable ally. A
  separate default-off communication option can follow only a client-accepted
  automatic Guardian with localized CC Quick Chat row 35 and an ownership-safe
  Bind2-ally/Bind1-self pair.
- **Experimental reactive counter-CC:** the default-off helper uses WHM Wunder
  der Natur / Miracle of Nature, BRD Stumme Nocturne / Silent Nocturne, or both
  metadata-verified NIN Forked/Fleeting Raiju variants on exact DNC, MCH, SAM,
  or VPR urgent startup evidence. It can also follow any of
  the six exact Purify-removable enemy statuses after real Resilience ends, or
  an exact Guard on its first verified absent framework observation. Both
  follow-ups bind the exact canonical `S1`-`S5` actor directly and have no minimum
  team-pressure-count gate. For simultaneous releases, only fresh exact team
  pressure above zero earns a highest-first ranking bonus; zero, unknown, or
  stale pressure is neutral. Lowest HP ratio follows, then lowest trusted MP and
  stable identity. Exactly one simultaneous
  winner binds one exact intent; losers are terminal, with no fallback. Native
  range/line of sight and the exact blocker state are checked before ranking. A
  clean rejection may retry only that intent, and a later distinct release epoch
  can trigger on the same continuous held key without requiring or mutating the
  selected target. WHM uses native 10-yalm range; BRD and both NIN Raiju variants
  use native 20-yalm range. NIN protection-end intent uses a 3-second lease to
  cover the verified 2.5-second Raiju recast plus the existing release window;
  WHM and BRD keep the normal 1.5-second lease. NIN confirms only exact Stun on
  the frozen enemy and every automatic landing requires the exact native action
  source sequence, so a manual cast cannot claim it. An
  already-accepted instant LB may still win the client/server race.
- **Optional team focus sign:** a separate default-off module can place the real,
  party-visible Attack1 sign on an exact enemy whose Guard is known unavailable
  and whose HP and/or trusted MP is low. It never overwrites an occupied Attack1,
  clears only a sign it can still prove it owns, and never changes your target.
- **Optional local low-MP Focus Target:** a separate default-off exact-CC helper
  may fill only an empty native Focus Target with one exact reachable `S1`-`S5`
  enemy after the trusted 2,000-MP latch. It never clears, replaces, restores,
  or retries a Focus; manual/external changes win and latch. This feeds the
  local Focus Target HUD and `<f>` and is independent of the team Attack1 sign.
- **Experimental Monk Earth's Reply:** while exact Earth Resonance is active on
  PvP Monk, a separate default-off helper can make one exact Earth's Reply
  attempt at or below 30% HP or at 1.25 seconds remaining by default. It never
  starts Riddle of Earth, never falls back to it, and never retries the same
  continuous resonance.
- **Target clarity:** the integrated focus glow, independent current-target
  highlight, and fixed target-information card remain optional. The Focus Glow
  renders the current native Focus Target, whether selected manually or set by
  the separate low-MP opt-in. The information card can also show team pressure
  and whether the current hard target is pressuring you.
- **Cleaner settings:** a persistent sidebar separates Start, Alerts, HUD &
  Nameplates, Combat Frames, Action Helpers, Job Tools, Macro Helpers, Targets,
  and Diagnostics.
  Shared-input actions document their real priority order, while visual,
  macro, and job-specific controls stay in their own pages. Configuration schema
  30 is unchanged and preserves every existing Combat Frames and helper choice;
  the held-action cast-cancellation test remains explicitly
  off for fresh, reset, and migrated configurations. Combat Frames, Smart
  Recuperate, accepted-Eukrasia Smart Kardia, PLD
  Guardian, Auto Low-MP Focus, the DRK macro, pressure Sprint and its native
  system sound, the Bard Paean pressure redirect, Guardian team communication,
  and Scholar Critical Strategy remain separate opt-ins. Every action-attempt,
  target-redirect, and party-visible communication feature remains opt-in.

## Pressure and team focus

The pressure counter counts distinct, exactly identified hostile players that
currently hard-target you, cast at you, recently produced a harmful action
effect on you, or placed the verified early Marksman's Spite marker on you.
Recent action evidence is held only for the configured 0.5-8 second window
(3 seconds by default). It does not read or display damage amounts.

Team pressure is a separate view: Seiton Sense counts valid party/alliance
members whose current hard target is each exact enemy. That count powers the
enemy `P#` nameplate badge, the current-target information card, and the
optional **Prefer allies attacking the highest team-pressure target** setting
for Near Assist. That preference is off by default and cannot pull selection
outside Near Assist's normal nearby-candidate window.

The urgent high-pressure alarm deliberately uses the narrower direct-intent
view: distinct exact enemies currently hard-targeting or casting at the local
player. Recent harmful-action evidence can keep the ordinary pressure counter
useful, but it cannot start or sustain this warning, its sound, or pressure
Sprint. Unknown/inactive pressure data stays silent. The visual warning is on
for fresh/reset settings and off after migration so an update cannot add a
large surprise overlay; sound and Sprint are always explicit opt-ins.
Unknown/stale pressure hides the card immediately but cannot manufacture a new
sound episode; only a continuously known below-three separation can rearm it.

## Stable nameplates and CC protection

Seiton Sense copies the visible rectangle of FFXIV's native nameplate job icon
after a nameplate update. Small utility indicators keep fixed reserved positions
beside it for observed Guard cooldown, low MP, Seiton readiness, team pressure
(`P#`), and incoming pressure (`YOU`, `HIT`, or `LB`). Active protection no
longer competes for one of those tiny slots: v0.6.0.2 draws one large, static
crossed-`CC` emblem directly above the native job icon. A red prohibition
stroke, bright side chevrons, and a separate countdown make the no-CC window
readable at a glance without internal label/icon clutter. If Guard and another
immunity overlap, the emblem shows the farthest verified expiry instead of
duplicating or swapping shorter warnings. It has no pulse, fade, scale
animation, or world-position projection, and its size is configurable.

Auxiliary slots stay reserved even when empty, so neighboring indicators do
not jump.
The plugin never replaces the job icon or mutates native UI nodes. Anchors and
actors are joined by both game-object and network entity identity; stale or
ambiguous identity fails closed. A short missing-anchor/status grace absorbs a
single transient sample without allowing timers to drift forward indefinitely.

The low-MP slot shows a crossed blue Standard-issue Elixir below 2,000 MP, the
current Recuperate cost. Initial zero samples remain untrusted, and state
changes are debounced to avoid flicker.

## Native-HUD low-resource aura

The optional resource aura uses the UI surfaces players already watch. At the
default thresholds, HP at or below 30% produces a soft red pulse around every
currently visible native self action bar; trusted MP below 2,000 produces
blue; both conditions produce purple. MP must first become a plausible trusted
sample and uses a 300-MP exit margin, so an unknown initial zero or a threshold
edge does not create a misleading flicker.

Party-list rows and the five native Crystalline Conflict ally and enemy rows can
receive a subtler version of the same aura. Each surface is independently
switchable, and pulse speed and intensity are configurable. The plugin copies
only current visible rectangles and draws a foreground ImGui outline/fill. It
does not recolor, pulse, write to, or otherwise mutate a native action slot or UI
node.

Each self-hotbar rectangle is now built only from that bar's currently visible
native action-slot nodes. Seiton Sense no longer trusts the broader hotbar
container, whose hidden layout area could extend far beyond the buttons and
produce a displaced aura. The settings preview uses the same slot-union anchors,
does not place a separate fixed-size sample rectangle over the screen, and
suppresses the ordinary live aura while previewing so the two paths cannot
overlap.

Party rows require exact agent row index, entity identity, and native object
pointer agreement. CC rows additionally require exact native party/enemy-slot
resolution and equality with the visible row name. Hidden, invalid, stale,
duplicated, or ambiguous actors/rows produce no aura. The current CC addon names,
row node mapping, and exact placement still require live current-patch validation;
source tests cannot prove a native HUD layout.

## Fixed Combat Frames

The separate **Combat Frames** page controls a default-off, fixed screen-space
Crystalline Conflict view. It draws one fixed Self frame and exactly five enemy
rows in FFXIV's canonical `S1`-`S5` order. Screen-space placement means walls,
camera movement, actor movement, and nameplate projection cannot move or occlude
the frames. A dead or temporarily unknown enemy keeps its reserved row so the
list does not jump; unknown identity, HP, MP, pressure, or status data is shown as
unknown rather than inferred.

Each frame can show the exact job, HP bar and values, trusted MP bar and values,
2,000-MP divisions, relevant Guard/CC/execute statuses, direct incoming evidence,
team pressure, current- or focus-target accents, and Limit Break evidence. Self's
LB gauge comes directly from the exact native LimitBreakController. Remote
`S1`-`S5` gauges remain `LB ?` until the current native HUD instance has proved
zero, full, and two separated partial samples against that exact Self gauge.
Elapsed time and catalog charge times are never used to guess remote charge.

An exact reviewed activation opens an LB card. A duration countdown originates
only from a matching live status's real `RemainingTime`. One missing sample of
at most 150 ms may preserve the last exact expiry for anti-flicker but can never
extend it; without initial live duration evidence the activation remains a
flash. Instant LBs use a fixed 1.8-second card. The optional ally damage feed
shows only direct ActionEffect damage whose exact caster and reviewed LB action
attribute it to an ally. It does not infer
damage from HP deltas, and pet, periodic, or ambiguous attribution stays silent.

Character-name display is optional. While Combat Frames are enabled, current
names may be read into the transient frame snapshot; the display toggle controls
only whether they are drawn. Names are never persisted or uploaded. Layout,
scale, opacity, visible details, and a preview are configurable. The optional
clean Seiton preset disables only older Seiton overlays that duplicate this
information.

The separate interaction leaf can make a fresh, living, exact canonical enemy
row clickable and publish its exact actor to FFXIV's native `<mo>` target slots
while hovered. Self, preview, dead or unknown rows, stale snapshots, and gaps
stay click-through. A click freezes and revalidates that one actor before one
hard-target write with no retry; external mouseover replacement wins and is
never overwritten back. No soft or Focus Target is changed. Combat Frames never
edit or hide FFXIV's parameter or enemy-list HUD, so players who want the frames
to visually replace those native elements must hide them manually in HUD Layout.

The exact, metadata-validated protection catalog in v0.6 is:

- Guard `3054` and Guard `3673`, folded into one Guard family;
- Resilience `3248`;
- WAR Inner Release `1303`;
- SAM Meikyo Shisui `1320`;
- VPR Hardened Scales `4096`;
- Swift `4477`, only in large-scale PvP.

One-hit, partial, and ambiguous wards are intentionally excluded rather than
being presented as full CC immunity. If a status name, icon, description, job,
or context no longer matches the verified metadata, that cue fails closed.

Active Guard replaces the crossed Guard-cooldown icon instead of creating a
duplicate. Guard cooldown remains an estimate based only on a Guard status
observed by this client; unknown cooldowns are never guessed. Guard does not
block the Seiton cue because Seiton Tenchu ignores Guard.

## Isolation warning and positioning scope

The isolation warning is a local, action-free Crystalline Conflict cue. It
requires the exact complete five-person party, including the local player and
four unique allies. Each living ally is checked with FFXIV's native 20-yalm
range/line-of-sight result. The large top-left card enters only after 500 ms of
continuous confirmed isolation and clears after 200 ms of confirmed connection;
dead allies do not count as a reachable teammate. Any incomplete identity,
unsupported native result, or other ambiguity suppresses the warning rather
than guessing.

The separate high-pressure card remains fixed at the top center. Isolation
keeps its normal top-left position when both are visible and moves below the
pressure card only when the two actual scaled rectangles would overlap on a
narrow work area.

This release deliberately does not include a position guide, automatic
navigation, Splatoon integration, or map painting. A useful position changes
too quickly with map geometry, teams, objectives, and the current fight for a
static overlay to remain trustworthy. The isolation cue reports only the narrow
fact it can prove: no living ally is currently within 20 yalms and native line
of sight.

## Optional CC-immunity brake

The default-off Crystalline Conflict brake uses an exact, action-specific
verified protection matrix. Standard Purify-removable CC and Wunder der Natur /
Miracle of Nature have separate blocker sets, including exact relevant ward
statuses rather than
assuming every visible protection blocks every action. It works directly on
incoming hotbar action attempts; no macro is required. When the current job,
action, and exact hostile target are all enabled and that target has verified
protection against that action's CC, Seiton Sense returns `false` for only that
one incoming attempt without calling the downstream/original action function.
This hard stop replaces the former invalid-`targetId = 0` handoff, which later
game processing could resolve back to a default target. It does not change the
visible selected target, choose another enemy or action, store the press,
dispatch an action, replay it later, or add a retry.

Some direct-hotbar and Turbo calls represent FFXIV's selected target with an
unchanged native carrier of either `0` or the default-target sentinel
`0xE0000000`, rather than a concrete actor ID. Seiton treats either native shape
as the selected hard target only when the original and final forwarded carriers
are identical and the redirect path did not deliberately suppress the target.
It reads the native hard target, resolves it to exactly one live canonical
`<e1>`-`<e5>` enemy, and confirms that the selection did not change during the
protection check. The call itself is not rewritten.

FFXIV can omit the `Hostile` actor flag on a valid public-match opponent. In a
known public Crystalline Conflict territory only, the brake may fall back to a
complete visible party proof: the local party must contain exactly five valid
members, include the local player, and every party entity must be visible. The
candidate must still come from exactly one native `<e1>`-`<e5>` slot, differ
from self and every party/alliance member, retain exact native identity, and be
alive and targetable. Missing or incomplete proof still passes unchanged.

A zero deliberately produced by Seiton's fail-closed Near Assist, Near Help,
Far Help, or legacy-fallback suppression carries explicit provenance and can
never be restored to the selected target. Explicit actor IDs remain
authoritative, and any missing, changed, non-canonical, duplicated, or
otherwise ambiguous target still passes through unchanged.

Plugin-owned exact-target Miracle requests bypass the macro-redirection
branches only. They still reach this same final brake before the one downstream
native call, closing the narrow race where verified protection appears after
the helper's earlier pre-check. This adds no timer, target mutation, replay, or
retry.

For the standard CC family, the verified blockers are Guard `3054`/`3673`,
Resilience `3248`, Inner Release `1303`, Meikyo Shisui `1320`, Hardened Scales
`4096`, and the Warden's Paean ward `3143`. Miracle's separate matrix uses
Resilience `3248`, Meikyo Shisui `1320`, VPR Hardened Scales `4096`, the
Warden's Paean `3143`, Relentless Rush `3052`, and Honing Dance `3162`.
Job-owned protections are accepted only on their exact job. Unsupported or
unverified statuses do not become blockers from their display text alone.

The Action Helpers page provides one master switch plus separate job and action switches.
The conservative current list is:

- PLD: Intervene `29065`;
- WAR: Blota `29081`;
- BRD: Stumme Nocturne / Silent Nocturne `29395` and Repelling Shot `29399`;
- WHM: Wunder der Natur / Miracle of Nature `29228`;
- BLM: Lethargy `41510`;
- NIN: Forked Raiju `29510` and Fleeting Raiju `29707`;
- MCH: Air Anchor `29407`;
- AST: Gravity II `29244`, including its Double Cast form `29248` behind the
  same visible setting;
- SAM: Mineuchi `29535`.

This list is intentionally limited to reviewed single- or primary-target CC.
Broad cone, ground-targeted, self-centered, and ambiguous multi-target actions
are excluded because one protected actor does not prove that every affected
enemy is protected. The brake suppresses the entire selected action attempt,
including any damage or movement attached to it; individual actions can be
disabled to taste.

Every later physical press or Turbo Hotbar pulse is a new incoming attempt and
is evaluated again, so the first real repeat after protection disappears can
pass normally. Vanilla FFXIV key holding does not create those repeats by
itself. The brake is prevention at the client dispatch boundary, not rollback:
at a near-simultaneous activation edge, an action the server already accepted
roughly 295-355 ms before immunity became locally visible cannot be recalled.
FFXIV may still show that action's animation and damage while the server blocks
its status effect on the protected target. Unknown, missing, stale,
unsupported, or ambiguous job/action/target/protection state passes through
unchanged rather than inventing a decision.

## Ninja Seiton cues

When your Seiton resource is ready, an enemy is strictly below 50% HP, and the
native range/line-of-sight check passes, a persistent card shows the enemy's
official job icon and the exact `SHIFT + 1` through `SHIFT + 5` decision.
`SHIFT` is only the configurable display label; Seiton Sense does not change
your keybinds. An optional `PREP` card covers 50% to below 60% HP, and entering
the real execute window can produce one short pulse.

In Crystalline Conflict, `S1`-`S5` follows FFXIV's native `<e1>`-`<e5>` order.
Wolves' Den testing accepts only one strict native hostile duel opponent and
uses synthetic visual `S1`; it does not claim that `<e1>` exists in a duel.

The separate **Seiton on held gameplay key** experiment is disabled by default
and runs only in exact Crystalline Conflict on PvP Ninja. Continuous physical-
key consent considers the exact canonical `S1`-`S5` enemy actors whenever a new
eligible adjusted-action epoch is available.
Every candidate must remain living, targetable, hostile, strictly below 50% HP,
and accepted by FFXIV's native action range and line-of-sight result. The lowest
exact HP ratio wins, followed by stable S-slot and actor-identity tie-breaks.
The current adjusted action must be the ready base Seiton Tenchu `29515` or its
verified Unsealed follow-up `29516`.

Ninja is fifth in the complete request order, after Purify, reactive counter-CC,
Ally Rescue, and PLD Guardian. It precedes SCH Critical Strategy, DRK Hiebsprung,
Smart Recuperate, generic Guard, pressure Sprint, event Kardia, and event Monk.
Active own
Guard and the bounded post-request Guard-propagation gate suppress the Ninja
helper. One exact adjusted-action epoch freezes one target. Known unavailable
states wait without consuming the common retry budget. Only an explicit client
rejection may call that same intent again after 50 ms, with eight native calls
maximum; acceptance or ambiguity is terminal. A genuine accepted base-to-
Unsealed action transition can create a later distinct epoch on the same hold,
but rejected base Seiton can never substitute the follow-up. The same frozen
S-slot and actor identity are resolved before every possible request, and that
exact actor's HP is read again. Exactly 50% or higher cancels the intent. This minimizes wasted
LBs when healing races the selection, but cannot eliminate the unavoidable
interval between the final client read, request, and server execution. The
helper never mutates a hard, soft, or focus target and never swallows the
original gameplay key. A client-accepted return is dispatch feedback only; it
does not prove that Seiton
landed, executed the enemy, or caused a kill. Current-patch timing and dispatch
remain live-validation boundaries.

## Scholar Critical Strategy held-key helper

The separate **Critical Strategy on held gameplay key** experiment is disabled
by default and runs only on PvP Scholar in exact Crystalline Conflict. It can
use Critical Strategy `29716` only while its metadata and readiness are verified
and one living, targetable enemy from the complete unique canonical `S1`-`S5`
set has live Guard `3054` or `3673`. The same frozen enemy must pass FFXIV's
native 25-yalm action range and line-of-sight check immediately before dispatch.

The helper deliberately never spends Critical Strategy as its ordinary 10%
damage-taken debuff. The current official action instead halves Guard's
defensive bonus when the chosen enemy has Guard; the effect lasts 10 seconds.
If every eligible guarded candidate has an active exact non-negative team-
pressure count and at least one count is positive, highest team pressure wins,
then lowest exact HP ratio. If any eligible candidate has unavailable, inactive,
or negative pressure, or every count is zero, the whole candidate set ranks by
lowest exact HP ratio. Stable S-slot, entity ID, and game-object ID resolve
remaining ties. Pressure is used only for this one selection and is not a final
dispatch requirement.

The current request order before Scholar is Purify, reactive counter-CC, Ally
Rescue, PLD Guardian, then NIN Seiton. DRK Hiebsprung follows SCH before Smart
Recuperate and the generic helpers. Continuous held consent
can produce a frozen Critical Strategy intent for a distinct eligible episode.
The frozen enemy is revalidated for exact identity, action readiness, live Guard,
and native range/line of sight before every possible bounded call. Pressure drift
neither reranks nor switches or invalidates the frozen target. The helper never
mutates a hard, soft, focus, or mouseover target, makes a second selection,
chooses an alternate target/action, falls back, or replays. Only an explicit
client rejection may retry the same frozen intent under the shared 50-ms/eight-
call policy, and it does
not swallow the original key. A client-accepted return is dispatch feedback only;
it does not prove that Critical Strategy landed or changed Guard. Exact current-
patch held-input timing, dispatch, and effect behavior require a live CC test.

## Dark Knight Hiebsprung held-key helper

The separate **Hiebsprung on held gameplay key** experiment is disabled by
default and runs only on PvP Dark Knight in exact Crystalline Conflict. It
considers living, targetable canonical `S1`-`S5` enemies at exactly 30% HP or
lower. Your own Bind blocks the helper. Every candidate must be free of Guard,
fit inside the helper's strict 10-yalm center-distance cap, and pass FFXIV's
native action range and line-of-sight result. Lowest exact HP ratio wins,
followed by stable S-slot and actor identity. Your own Guard, a candidate's
Guard, the bounded Guard-propagation latch, animation lock, typing, metadata
uncertainty, incomplete identity, or readiness uncertainty fails closed.

The first eligible epoch freezes one target before final revalidation. After a client-accepted request, that same
physical key may remain held, but a later attempt requires the cooldown to have
been observed not ready and then ready again. A KO reset or natural 12-second
recast can therefore create another proven ready epoch; a reset wholly missed
between framework samples is not guessed. Each epoch can use only the common
bounded explicit-false retry for its frozen direct Hiebsprung / Plunge `29092`
target, with no visible target change, alternate, rerank, or replay. The current
order is **Purify > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Seiton >
SCH Critical Strategy > DRK Hiebsprung > Smart Recuperate > generic Guard >
pressure Sprint > event Kardia > event Monk**.

## Sage Smart Kardia after accepted Eukrasia

The separate **Smart Kardia after accepted Eukrasia** experiment is disabled by
default and runs only on PvP Sage in exact Crystalline Conflict. It does not scan
held keys or poll party pressure while idle. The existing native action hook
first forwards one exact incoming Eukrasia `29258` call unchanged. Only a
client-accepted return creates a token tied to the exact local Sage, territory,
pre-call charge/status evidence, and acceptance time; that opportunity expires
after two seconds.

Before Kardia can be considered, the accepted Eukrasia must become causally
visible through either a lower exact native charge count or a newly present
local-source Eukrasia status. Kardia must resolve exactly to `29264`, be locally
ready, and reach an animation-lock-clear boundary. The pressure publication must
be newer than the accepted Eukrasia and provide one complete, unique, stable
five-player party view. Transiently incomplete causal, pressure, readiness, or
animation-lock evidence may wait only inside the same bounded token.

Exact living, targetable self/party candidates with a trusted current count of at
least two unique live enemies directly hard-targeting or casting at them are
considered first. A non-self candidate must also pass FFXIV's native 30-yalm
Kardia range and line-of-sight check. Eligible candidates rank by higher incoming
pressure, then lower exact HP ratio, party slot, network entity ID, and game-
object ID. If nobody reaches the pressure threshold, exact self is the sole
initial fallback; unknown pressure or an incomplete party view cannot manufacture
that fallback.

The highest-ranked actor is authoritative. If its local-source Kardion state is
unknown or already present, the trigger ends without falling through to another
actor. Once a complete view reaches selection, the token is consumed before the
terminal identity, Kardion, pressure/self-fallback, Kardia metadata/readiness,
animation-lock, and native-reachability checks and before at most one direct-
target request. Later drift, rejection, or exception cannot rerank, switch to
self or another party member, substitute an action, replay, or retry.

This follow-up has no physical-key generation and requires its own accepted-
Eukrasia trigger. In the current request order it follows pressure Sprint and
precedes only event Monk. Active own Guard suppresses it; an actual Kardia
attempt suppresses lower Monk work in that frame. It never changes a hard, soft,
focus, or mouseover target. Client
acceptance is dispatch feedback only and does not prove that Kardia or Kardion
applied; current-patch hook ordering, charge/status evidence, animation lock,
native reachability, dispatch, and server behavior require a live CC test.

## Personal warnings and job quality-of-life helpers

Wildfire and Death Warrant receive danger warnings. Marksman's Spite uses its
exact early target-marker event to show the larger `MCH LIMIT BREAK ON YOU`
card before the later damage event. The optional sound uses FFXIV's built-in
sound effects, plays at most once for a verified threat, and has a test button.
It never presses Guard or another action.

Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature receive urgent
Purify warnings. The experimental **Self-Purify physical-key helper** is disabled
by default. Each debuff type has its own automation toggle, and a separate
default-off option allows an already-held gameplay key such as WASD to remain
continuous consent when the enabled debuff appears.

The original key is never swallowed, delayed, or replayed. Purify has absolute
priority while the exact enabled CC is active. Known cooldown, resource, cast,
queue, or animation-lock blocks wait without spending an attempt. Only an
explicit client rejection may retry the same frozen self intent after 50 ms,
with eight native calls maximum; acceptance or ambiguity ends that CC episode.
ReAction Turbo repeat pulses do not create physical consent.

The separate **Smart Recuperate on held gameplay key** experiment is disabled by
default and runs only in exact Crystalline Conflict. It freezes only PvP
Recuperate `29711` on the exact local player. The local player must be alive and
targetable, the action and metadata must be exact and locally ready, at least
16,000 HP must be missing, and at least the exact 2,000-MP cost must be available.
Both boundaries are inclusive: exactly 16,000 missing HP and exactly 2,000 MP are
eligible.

Held consent may wait while Recuperate is not ready or MP is below 2,000, so the
same hold can become eligible after the cooldown or an MP tick without starving
a currently usable lower-priority helper. Once all gates pass, the exact self
intent is revalidated before every possible call. Only an explicit client
rejection may retry that epoch under the common bound. Temporary readiness/MP,
higher-priority, and Guard states wait without spending a call; dropping below
the HP threshold cancels the current intent. Acceptance ends that epoch, and a
later one requires an observed cooldown unavailable-to-ready transition. Retry
exhaustion or an ambiguous/invalid exact outcome latches only this helper until
the frozen key is released. The helper never changes a target, buffers MP,
substitutes another action or actor, or replays input, and client acceptance is
not a healing-effect claim.

The separate **Ally Rescue on gameplay-key consent** experiment is also disabled
by default and runs only in Crystalline Conflict. It is available on BRD with
The Warden's Paean and on WHM with Aquaveil, using current action IDs rather
than localized names. Only Stun, Silence, Deep Freeze, and Miracle of Nature on
an exact non-self party member are triggers; Heavy and Bind are intentionally
excluded.

At action time, candidates must be alive, targetable, and inside the chosen
action's native range and line of sight. The selector orders them by lowest
exact HP percentage, then most unique enemies currently hard-targeting or
casting at that ally, then lowest trusted MP percentage, distance, and stable
party identity. Hotfix 0.7.0.1 no longer rejects a valid status because an
internal status-slot address is unavailable and no longer requires an early
local cooldown-ready sample. The helper freezes that exact ally, status, action,
and physical key. Every permitted native call revalidates the same frozen intent,
range, and line of sight; it never selects another actor or status.

Purify and reactive counter-CC receive the scheduler frame before Ally Rescue.
Action-specific cooldown/resource or reachability waits do not block a usable
lower helper. The exact ally/status intent may use only the common bounded
explicit-false retry; acceptance or ambiguity is terminal. The BRD metadata
check also accepts the current lowercase leading article in `the Warden's
Paean`; numeric action identity still drives runtime behavior.

A client-accepted action request is not presented as a successful cleanse.
For up to 2.5 seconds after a client-accepted call, Seiton Sense instead correlates an
exact local-caster, action, and ally-target ActionEffect result of type `0x10`
(`RecoveredFromStatusEffect`). Only the six known Purify-removable PvP statuses
can confirm it: Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature.
Heavy and Bind remain confirmation-only here and still never activate Ally
Rescue. A later rejected call neither creates nor erases the pending accepted
correlation. One exact confirmation shows a blue `CLEANSED` popup for 1.5 seconds.

The Action Helpers page separates attempts, client-accepted requests, and exact
confirmed removals, with confirmed totals for the current match and plugin
session plus per-action/per-status details. These aggregates live only in
memory. The provided reset clears the displayed statistics and does not create
another action or confirmation.

The separate **Smart Bard Paean pressure redirect** is disabled by default and
runs only for PvP Bard in exact Crystalline Conflict. It does not observe the
shared generic gameplay-key generation and never casts an action by itself. It
examines only an already incoming The Warden's Paean `29400` ability call from
the normal manual action path or a Turbo pulse.

At that incoming call, selection first requires a complete, unique, stable
exact Crystalline Conflict party view. An eligible destination must be an exact
living, targetable, non-self party member, accepted by FFXIV's native 30-yalm
range and line-of-sight check, without the live Warden's Paean ward `3143`, and
with a trusted current count of at least three unique live enemies whose exact
hard target or cast target is that ally. An unknown pressure count excludes only
that candidate. Among eligible allies, higher incoming pressure wins, then lower
exact HP ratio, party slot, entity ID, and game-object ID. An incomplete or
ambiguous party view, or no exact known `3+` candidate, forwards the original
target and ability call unchanged as vanilla behavior.

The redirect may substitute only the target ID of that one incoming Paean call.
Once that exact party slot and actor identity are frozen, final identity, local
job, exact resolved action ID and metadata, life/targetable state, HP, live ward,
native range/line of sight, or pressure drift suppresses that one call. There is
deliberately no cooldown/readiness gate on this passive transform. It does not
fall back to the original target, select another ally, or retry after this final
gate. It never changes a hard, soft, focus, or mouseover target, initiates
another action, substitutes an action, or stores a call for replay. A later
Turbo pulse is an independent downstream call and is evaluated once on its own.
A client-accepted return is dispatch feedback only, not proof that Paean applied
or later removed or nullified crowd control.
The existing held-key Ally Rescue behavior, including Aquaveil, remains
unchanged and separate from this passive Bard-only option.

The **Reactive defensive utilities** module is a separate, default-off
Crystalline Conflict helper. Its self-only rules require fresh or continuous
eligible held physical gameplay-key consent and exact local metadata. At
three or more unique enemies currently pressuring you, an exact Stun can enable
one Purify episode even if the ordinary Purify helper is off. Guard remains a
distinct later episode: the helper must positively observe live Resilience
`3248` and see the removable CC gone inside its bounded follow-up window. The
same continuous hold may authorize it. It does not
pre-Guard merely because HP is low or pressure is high.

The independent **Paladin Guardian job tool** is separately default-off under
Job Tools and does not depend on the reactive defensive-utilities master.
Continuous held consent can create one frozen Guardian `29066` intent for an
exact living, targetable, non-self party member at or below 20% HP when FFXIV's
native 20-yalm action-range and line-of-sight check accepts that target. There is
no custom center-distance cap: native reachability remains authoritative and
hitbox-aware. After the jump, Guardian's protection requires the Paladin to
remain within 10 yalms of the protected member. Both your own Guard and Guardian
must be available. Candidates rank by lowest exact HP percentage, then known
higher incoming pressure, distance, and stable party identity. When the automatic
Guardian request is accepted locally, a blue 1.5-second **GUARDIAN TRIGGERED**
card shows the selected party slot and explicitly labels the result **CLIENT
ACCEPTED**. This is dispatch feedback, not proof that the server applied Guardian
or intercepted damage.

The separate **After accepted Auto Guardian: Quick Chat + Bind pair** option is
also disabled by default. It can run only after this automatic Guardian request
returns client-accepted in exact Crystalline Conflict; manually used Guardian,
Far Help, and rejected requests do not arm it. It freezes the same exact party
slot and uses the client-localized Crystalline Conflict Quick Chat row 35
(`Ziel decken`, displayed as `Ich decke ...` on a German client) for that `P#`.
It can then place Bind2 on that exact party slot followed by Bind1 on the local
Paladin.
If either sign is already occupied or current marker state cannot be proven,
the marker sequence is not started. Bind2 must be observed on the exact ally
with a new marker timestamp before Bind1 is attempted on self. If Bind1 then
fails, only the proven-owned Bind2 is eligible for cleanup. A complete pair
expires nine seconds after Guardian was accepted; cleanup tries Bind2 and then
Bind1, each only while the same actor/sign/timestamp ownership remains exact.
Drift is relinquished rather than cleared.

FFXIV can represent an unused native marker slot as either `0` or
`0xE0000000`. Seiton Sense accepts both only when the slot index, availability,
and timestamp telemetry are otherwise exact; this prevents an empty slot from
silently skipping the Bind sequence without weakening foreign-marker safety.

This communication path never changes a hard, soft, focus, or mouseover target,
initiates another combat action, selects another ally, falls back, or retries.
A locally issued command is not proof that the party received Quick Chat or saw
both markers. The exact localized row-35 syntax, party display, marker pairing,
and cleanup behavior remain current-patch live-confirmation boundaries.

While your own Guard is active, Seiton Sense blocks all of its action requests,
including Purify, reactive counter-CC, Ally Rescue, Guardian, NIN, SCH,
Hiebsprung, Smart Recuperate, Guard, pressure Sprint, accepted-Eukrasia Kardia,
and Monk. The bounded reactive observer may retain an already eligible enemy
startup/Purify/Guard reservation, but it cannot dispatch it through own Guard.
The same action suppression
starts immediately for a bounded 1.5 seconds after an exact local Guard request,
covering the short interval before
the live Guard status becomes visible without extending the deadline. This
prevents the plugin from cancelling Guard. It cannot prevent a manual game action
or another plugin from cancelling Guard, and the exact live client/server ordering
of these new rules still needs in-game validation.

The **Reactive counter-CC** module is also default-off and CC-only. On WHM it
uses Wunder der Natur / Miracle of Nature `29228` at native 10-yalm range; on
BRD it uses Stumme Nocturne / Silent Nocturne `29395` at native 20-yalm range;
on NIN it resolves the PvP Spinning Edge/Aeolian Edge Combo carrier `29500` to
either Forked Raiju `29510` or Fleeting Raiju `29707` at native 20-yalm range.
Both Raiju metadata rows must verify before NIN can arm, and the carrier must
expose the exact variant before an action can be requested. Forked Raiju remains
blocked while the exact local Sealed Forked Raiju status `3195` is present; both
variants remain blocked through exact local Bind `1345`.
All three jobs can respond to the exact
early DNC Contradance `29432`, MCH Marksman's Spite `29415`, SAM Zantetsuken
`29537`, and VPR Furious Backlash / Nest der Blutschuppen `39188` startup
signals. VPR waits for live Hardened Scales `4096` to be genuinely absent, and
every path revalidates exact canonical
enemy identity, life/targetability, action-specific CC protection, native range,
and line of sight. Expired or disabled leases retire, and every active startup
must still resolve the same local job, counter action, and exact enemy before a
new packet can compete. A later exact urgent startup may preempt only an
unattempted lower-priority reactive lease; equal/lower events and every lease
with a native attempt remain frozen.

The post-Purify subtype accepts all six exact recovered statuses: Stun
`1343`, Heavy `1344`, Bind `1345`, Silence `1347`, Miracle of Nature `3085`, and
Deep Freeze `3219`. It accepts an exact enemy self-Purify `29056` action packet
even when that packet omits an individual recovered-status tuple, but positive
live Resilience `3248` remains mandatory. The Purify observation remembers the
exact actor, action, episode, and a validated bounded `RemainingTime` hint; it
does not freeze whichever key happened to be down on that packet frame.
If the exact canonical enemy row is transiently unavailable on that frame, the
already-deduplicated signal may retry only that identity resolution inside its
original 750-ms acquisition deadline. It carries no key or action, cannot select
another actor, and cannot extend or replay the signal. Live
Resilience membership remains authoritative: the first real absent
frame at or after the non-extending expected end is eligible immediately, while
an early or untimed absence still needs 150 ms of continuous proof. The signal
may bind the current eligible held/fresh generation at that authoritative end or
during the same original 500-ms release opportunity, then dispatches directly
to that actor. It neither requires nor changes the selected target. There is no
minimum team-pressure count. Each exact protection-end
episode retains its original bounded release edge and is never extended while
another helper has priority. Post-Purify state is tracked independently for each
canonical `S1`-`S5` slot, so two exact enemies can reach their own verified
Resilience end without either signal replacing the other.

The separate post-Guard subtype requires exact Guard `3054` or `3673` to be
observed present on one canonical `S1`-`S5` actor. Its first exact presence
remembers the actor, action, episode, and bounded non-extending duration hint,
without freezing an event-edge key. The first verified framework observation
that finds Guard absent exposes one bounded exact protection-end episode,
including an early manual Guard cancel, with no minimum team-pressure count. It
may bind the current eligible held/fresh generation on that observation or
inside the same original 500-ms release opportunity. Once bound, releasing that
key retires the intent; the uninterrupted Guard episode stays retired through
unknown or ambiguous samples until real absence separates a later episode.
Dispatch uses the frozen actor
directly at the job-specific native range and line of sight, without requiring
or switching the selected target, choosing an alternate action/actor, or
replaying. Only an explicit client rejection may retry that same frozen intent
under the common bound.

If multiple exact post-Purify or post-Guard releases are simultaneously eligible,
each candidate must first pass its exact action-specific blocker state, native
range, and line of sight, so an unreachable high-pressure actor cannot starve a
reachable one. Among the remaining candidates,
only a fresh exact team-pressure count above zero earns a ranking bonus, with
higher positive counts first. Known zero, unknown, or stale pressure is neutral
and never gates a candidate. Lowest HP ratio follows, then lowest trusted MP
ratio and stable `S1`-`S5` identity. It selects exactly one winner.
Every simultaneous loser is terminal and cannot become a fallback attempt. A
continuously held eligible gameplay key keeps consent for the selected frozen
episode and may also authorize a later distinct episode. Before binding, the
current eligible generation may attach only inside that episode's original
bounded opportunity. After binding, release or text input retires that exact
generation without substitution. Only an explicit
client rejection may retry the same intent under the common bound; acceptance
or ambiguity is terminal.

The current request order is **Purify > reactive counter-CC > Ally Rescue > PLD
Guardian > NIN Seiton > SCH Critical Strategy > DRK Hiebsprung > Smart
Recuperate > generic Guard > pressure Sprint > event Kardia > event Monk**.
The six job-specific physical-hold helpers share the second tier; reactive
counter-CC wins its deterministic urgency order before ally cleanse because the
LB and protection-end windows are shorter. Kardia and Monk retain their separate
event-driven origins. At the reactive counter-CC stage, the chosen opportunity
freezes one exact-target intent;
there is no visible selected-target change,
alternate action/target, fallback, or replay. Plugin-owned Miracle, Silent
Nocturne, and Raiju requests still pass through the final action-specific
CC-immunity brake immediately before the native call.

After a client-accepted request, a blue `AUTO CC LANDED` popup appears only if the bounded
ActionEffect observer captures the matching status on that exact pending enemy:
Miracle `3085` for WHM, Silence `1347` for BRD, or Stun `1343` for either NIN
Raiju variant, with the exact `SourceSequence` created by the plugin request.
A manual use of the same action cannot claim the pending automatic result. A
local client-accepted request
does not count. Even an exact landed popup proves only that the counter-CC status
landed; it does not conclusively prove that Contradance, another limit break, or
its damage was interrupted. In particular, an instant LB already accepted by
the server can resolve before the reactive request arrives. All startup timing,
Purify/Resilience release
ordering, WHM/BRD/NIN dispatch, and claimed interruption outcomes remain explicit
current-patch live-validation boundaries.

Bounded transition diagnostics record reactive episode memory, current-key
attachment, protection-end promotion, native attempt outcome, and exact source
sequence in the durable plugin log. This lets a later live match be diagnosed
without treating a manual press as an automatic success.

The separate **team-visible enemy focus sign** module is default-off and exact
CC-only. It considers an enemy only when this client knows Guard is unavailable
and the exact enemy is at or below 50% HP and/or has entered the trusted low-MP
state. That state enters after 150 ms continuously below 2,000 MP and clears
after 150 ms continuously at or above 2,300 MP, preventing threshold flicker.
Priority is both low, then HP-only, then MP-only; ties use lowest exact
HP percentage, lowest trusted MP percentage, highest known exact team-target count,
and stable `<e1>`-`<e5>` slot.

The module sends only the hardcoded normal `/mk attack1 <eN>` command and never
changes the selected target. It never overwrites an occupied Attack1 sign. It
claims ownership only after observing an empty-to-exact-target transition with a
changed native marker timestamp, and clears only while the same slot, actor, and
timestamp still match. Uncertain ownership deliberately leaves the sign alone.
The command path and party-visible ownership/clear behavior are covered at
source level but still require live current-patch Crystalline Conflict validation.

The Monk section contains a separate default-off Earth's Reply helper for PvP
job 20. It requires one exact Earth Resonance `3171`, current metadata
validation, and the adjusted Riddle of Earth `29482` slot to resolve to Earth's
Reply `29483`. The enabled trigger fires at or below the configured HP threshold
(30% by default) or inside the configured expiry window (1.25 seconds by
default).

The continuous resonance is marked spent before one self-targeted normal
`29483` request. A rejected or throwing request is not retried, and `29482` is
never used as an alternate action. Event Monk is last after Purify, the complete
job-specific second tier, Smart Recuperate, generic Guard, pressure Sprint, and
event Kardia. The helper
runs in Crystalline Conflict and in explicitly enabled Wolves' Den test mode;
the native direct-call result and exact timer behavior still need a live test.

## One-shot Near Assist macro

Near Assist is disabled by default and intentionally supports Crystalline
Conflict only. The recommended macro first offers one enemy-slot carrier to
Seiton Sense and keeps normal `<t>` behavior as the final fallback:

```text
/mlock
/nearassist
/pvpac "Ability" <e1>
/pvpac "Ability" <t>
```

If you already keep a selected target, the compact form remains supported:

```text
/nearassist
/pvpac "Ability" <t>
```

`/nearassist` creates one token lasting at most 750 ms. It considers valid
party/alliance allies within the configurable 5-30 yalm search radius (25 by
default). Smart mode first limits preference to allies no farther than the
nearest candidate plus 8 yalms, then favors ranged/caster DPS, melee DPS, and
support. Strict nearest mode is available. The independent team-pressure
preference can first favor allies attacking the enemy with the highest current
ally target count inside that same nearby window.

The ally must hard-target one exact opponent from FFXIV's native CC
`<e1>`-`<e5>` list. Seiton snapshots that ally target when `/nearassist` runs.
The next supported hostile PvP action inside the 750 ms window is checked
against that exact enemy identity plus the action's native range and line of
sight. It no longer depends on FFXIV still exposing the same macro-line text
inside the later action call.

The authored `<e1>` line is only a reliable concrete carrier; it does not force
the assist destination to S1. On a valid redirect, Seiton replaces that one
incoming target ID with the selected ally's actual S1-S5 target. If it cannot
redirect, the carrier is deliberately made invalid and the following `<t>` line
remains the ordinary fallback. This works even when no own target was selected.
The compact two-line form forwards its current `<t>` unchanged on failure.

Turbo Hotbar may repeat the authored macro, but Seiton Sense creates no repeat,
alternate action, or retry; generic queued-action mode is rejected. The token is
consumed before the one original game call. Near Assist never changes your hard,
soft, or focus target and never sends an action by itself.

`/mlock` is recommended as the first line when Turbo Hotbar is enabled. It
prevents a held macro slot from restarting the short macro before the authored
fallback line is reached.

Wolves' Den, Frontline, and Rival Wings are excluded from Near Assist because
they do not provide the same canonical CC enemy-slot contract.

## One-shot Near Help macro

Near Help shares the same default-off macro-helper switch and is intentionally
Crystalline Conflict only. Use one concrete friendly carrier followed by your
ordinary target fallback:

```text
/mlock
/nearhelp
/pvpac "Ability" <2>
/pvpac "Ability" <t>
```

`/nearhelp` arms one token for at most 750 ms. When the next supported friendly
PvP macro action arrives, Seiton resolves the current native party list and
checks every exact, live, targetable party member against that action's native
target, range, and line-of-sight rules. Self enters the same candidate list only
when the resolved action explicitly supports self-targeting and the native
target check succeeds.

Lowest exact HP is always the anchor. At 25% HP or lower, that critical target
always wins. Otherwise, when **Prefer incoming pressure near the lowest-health
target** is enabled and the live pressure view is trusted, only candidates
within 10 HP percentage points of the anchor may compete; the highest unique
incoming enemy count wins, followed by lower exact HP, shorter distance, native
party order, and stable actor identity. Missing pressure on any candidate inside
that window, or zero-only pressure, falls back exactly to lowest HP; unknown
pressure outside the window is irrelevant.

The `<2>` line is only a reliable concrete friendly carrier; it does not make
party member 2 the preferred destination. When a valid candidate exists,
Seiton replaces that one incoming target ID with the selected ally. If no
candidate or validation is available, only an exact authored `<2>` carrier is
made invalid so the following `<t>` line remains vanilla. If your actual
selected target already is party member 2, exact identity handling preserves
the compact `<t>` form instead of mistaking it for a carrier.

Dual-purpose skills are supported when current game metadata explicitly allows
party, ally, or self targets. Near Help and Near Assist replace each other's
pending token. Near Help never visibly switches a target, sends an action by
itself, changes the action ID, accepts generic Queue mode, tries an alternate
candidate, or retries.

## One-shot Far Help mobility macro

Far Help shares the same default-off macro-helper switch and remains
Crystalline Conflict only. It supports exactly five reviewed friendly-target
PvP movement actions: Guardian `29066`, Thunderclap `29484`, Aetherial
Manipulation `29660`, Icarus `29261`, and Slither `39184`.

```text
/mlock
/farhelp
/pvpac "Mobility Ability" <me>
```

`/farhelp` arms one token for at most 750 ms. The immediately following
supported movement action resolves exact, live, targetable non-self party
members and checks that action's native range and line of sight. At that action,
all five native `<e1>`-`<e5>` slots must resolve to exact, unique, valid opponent
identities. Confirmed dead opponents are ignored for clearance, while every
live opponent counts even when temporarily untargetable. Each candidate must
have strictly more than 10 yalms of horizontal hitbox-edge clearance from every
live opponent to enter the preferred backline group. Missing, ambiguous,
invalid, or no-live-enemy observations make that preference unavailable; they
do not cancel an otherwise valid movement destination.

If one or more candidates pass that conservative backline heuristic, the
farthest of those candidates from you wins. If none pass or the snapshot cannot
certify them, Far Help falls back to the farthest otherwise valid reachable
ally. Only at exactly equal measured distance does role break the tie: healer,
then physical/magical ranged or caster, then every other job. Native party order
and stable actor identity break any remaining tie. Guardian uses FFXIV's native
20-yalm action-range and line-of-sight result with no custom center-distance
cap; its 10-yalm condition applies to staying close enough for protection after
the jump. The enemy-clearance test is a map-agnostic preference, not a guarantee
that a destination is tactically safe.

Use exactly those three lines; Far Help deliberately has no selected-target
fallback. `<me>` is an intrinsically invalid carrier because none of the five
reviewed actions can target self. This remains harmless even when the hook is
unavailable or no token was armed. On a valid redirect, Seiton changes only
that already incoming action's target ID. If no eligible ally exists, the
self-target attempt stays invalid and no movement occurs. It is never forwarded
to your current target, self, or a different fallback actor.
Unrelated actions do not consume the token. Near Assist, Near Help, and Far
Help replace one another's pending token and share one action detour with one
original game call. Far Help never switches a visible target, initiates or
repeats an action, changes an action ID, accepts generic Queue mode, or retries.
Use `/mlock` so Turbo Hotbar cannot restart the held macro.

For migration only, Seiton suppresses matching legacy calls of the same
movement action for the rest of the bounded 750-ms window, including a former
fourth `<t>` line and Turbo duplicates. Remove that old line; it is not part of
the supported Far Help macro.

## Manual NIN Panic Shukuchi macro

Panic Shukuchi is an explicit one-line macro command, not an automatic feature
and not part of the held-action scheduler:

```text
/panicshu
```

It runs only on exact PvP Ninja in Crystalline Conflict, or in the Wolves' Den
when the existing **Enable Wolves' Den testing** option is enabled. Frontline and
Rival Wings remain excluded. Each command invocation reads the character's
current position and facing, projects only the point 19.5 yalms straight ahead
onto terrain, and freezes that exact destination. Turning or moving during the
short lease does not recompute it.

The lease lasts at most 500 ms and may remain pending only while Self-Purify owns
the current scheduler frame, the local character is casting, FFXIV's native
action queue is occupied, or animation lock remains active. Shukuchi must
already have known, ready cooldown/resources when the
command arms. Before the sole native location-action call, Seiton revalidates the
exact local identity, job, territory/context, metadata, adjusted action, own
Guard, crowd-control state, cooldown/resources, structural availability, queue,
and animation lock. Three Mudra changes Shukuchi into Doton, so any adjusted
action other than exact Shukuchi `29513` cancels without an attempt. Crowd
control also cancels so Purify keeps priority.

The frozen lease is spent before the native call. Client rejection, ambiguity,
or exception is terminal: there is no retry, alternate action, shorter/inward
point, path search, or destination fallback. The command never moves the mouse
or ground-target cursor and never reads, changes, or substitutes a hard, soft,
Focus, or mouseover target. A wall, missing exact terrain hit, excessive vertical
offset, or native line-of-sight refusal therefore fails closed instead of making
a different jump.

## DRK Shadowbringer two-line macro

This separate helper is disabled by default and supports exact PvP Dark Knight
in Crystalline Conflict. It also supports the Wolves' Den only when the existing
**Enable Wolves' Den testing** option on Start and this DRK helper are both
enabled. Author exactly two adjacent macro lines:

```text
/seitonbringer
/pvpac "Souleater Combo" <t>
```

On a non-English client, replace only the quoted `Souleater Combo` text with
the exact localized PvP action name. Keep `/seitonbringer`, the line order, and
`<t>` unchanged. In ReAction, enable both **Macro Queue** and **Turbo** for this
macro.

The command may arm only the immediately following exact Souleater Combo route
against your unchanged current `<t>`. In Crystalline Conflict, that target must
resolve to one exact canonical `S1`-`S5` enemy, exactly as before. In Wolves'
Den, it must instead remain the exact native current hard target and resolve to
the live, targetable combat striking dummy with current NameId `541`. The Den
path freezes and revalidates that dummy's game-object ID, entity ID, address,
object/sub-kind, NameId, and the native hard-target ID. It never queries a
synthetic `S1`, `<e1>`, or the duel-opponent resolver and never accepts a player,
another attackable object, or an alternate target. Frontline and Rival Wings
remain blocked.

The helper recognizes a new GCD cycle only from a proven exact 2.40-second combo
recast restart plus action-sequence change. It may claim at most one
Shadowbringer attempt for that cycle, and only while the inclusive
remaining-time window is 0.60-0.80 seconds. A missed window is skipped; 0.50 seconds or
less never triggers Shadowbringer. The paired combo call still reaches vanilla
unchanged, and a later Turbo pulse can queue the authored combo line inside
FFXIV's normal queue window.

Both the preliminary and final checks require the same context, DRK identity,
GCD token, exact target and combo route; an empty stable native queue; unchanged
action sequence; no cast or animation lock; clear own Guard/propagation and
target Guard; native 5-yalm combo and 10-yalm Shadowbringer range and line of
sight; and exact action/readiness/resource metadata. Base Shadowbringer requires
strictly more than 12,000 HP. Its adjusted Dark Arts form requires the exact
Dark Arts status/action state instead.

The cycle's one-attempt token is spent before the final native Shadowbringer
request. Drift, rejection, or an exception cannot choose another target or
action, replay the macro, or retry. Seiton Sense never changes the visible hard,
soft, or Focus Target. `CLIENT ACCEPTED` is local dispatch feedback, not proof
that the server executed Shadowbringer or that the weave did not clip. ReAction
mode, native queue ownership, recast-group timing, action effect, and clipping
remain current-patch live-trace boundaries. A successful striking-dummy trace
checks only the Wolves' Den path and does not prove live CC timing or execution.

The current metadata gate pins the exact combo-row secondary cost types
`0/58/58/147/147/147` rather than accepting them loosely. The first native GCD
sample is taken from the framework update thread instead of synchronously during
plugin startup; this avoids the observed off-main-thread local-player lookup
failure while preserving fail-closed cycle priming. If the separately checked
striking-dummy NameId metadata does not match, only the Den test path is disabled
and canonical CC support remains available.

## Optional Auto Low-MP Focus Target

This separate local setter is disabled by default and supports only exact
Crystalline Conflict. It requires a complete, unique native `S1`-`S5` set and
trusted enemy MP that remains at 2,000 or lower for 150 ms. That low-MP wave
clears only after 150 ms continuously at 2,300 MP or higher. The selected enemy
must remain alive, targetable, exact, and accepted by FFXIV's native 20-yalm
range and line-of-sight result. Lowest exact MP ratio wins, then lowest HP ratio,
stable S-slot, and exact actor identity.

The helper can fill only a native Focus Target observed stably empty. It never
clears, replaces, restores, or retries one. An already occupied Focus spends
that low-MP wave without mutation. If the plugin confirmed its own set and the
Focus is then changed or cleared manually or externally, manual ownership wins
and latches until the option is toggled off/on or a new exact match lifetime
begins. Missing identity, MP trust, text-input state, metadata, range, or Focus
state fails closed.

This local native Focus Target feeds FFXIV's Focus Target HUD and `<f>` and can
be rendered by the optional Focus Glow. It is independent of the party-visible
Attack1 sign and never changes the hard or soft target. Dalamud exposes no
atomic compare-and-set operation for Focus Target, so Seiton performs a final
same-thread empty read immediately beside its sole setter and then an exact
readback. The setter, HUD/`<f>` result, native range probe, and unavoidable
read-to-write race remain current-patch live A/B boundaries.

## Focus and current-target modules

The focus glow contains the former Super Focus Glow visual controls: projected
hitbox ring, halo, rays, chevrons, label, pulse, color, foreground, rainbow, and
reduced-motion options. The current-target highlight has an independent style.
The visual focus module only renders FFXIV's current native Focus Target; the
separate default-off low-MP helper above is the only integrated focus feature
that may set one. The current-target highlight remains read-only and never
chooses, retains, assists, or changes your hard target.

The target-information HUD is a separate fixed screen-space card. It does not
attach to the native nameplate, job icon, health bar, or Seiton indicator slots.
Disable the standalone Super Focus Glow renderer before enabling the integrated
focus module to avoid drawing both over the same actor.

## Supported contexts

| Feature | Crystalline Conflict | Wolves' Den test mode | Frontline / Rival Wings |
| --- | --- | --- | --- |
| Pressure counter and pressure badges | Yes | Separate opt-in | Yes, without CC slot labels |
| Verified CC-protection icons | Yes | Yes, for the strict duel opponent | Yes, including large-scale-only Swift |
| Optional per-action CC-immunity brake | Yes | No | No |
| Personal warnings and optional self-Purify | Yes | Yes | No |
| Urgent isolation warning | Yes | No | No |
| Native-HUD low-resource aura | Yes | Yes | Yes, without CC team rows |
| Fixed Combat Frames, optional interaction, and LB telemetry | Yes | No | No |
| Optional BRD/WHM Ally Rescue | Yes | No | No |
| Optional held Smart Recuperate | Yes | No | No |
| Optional reactive defensive utilities | Yes | No | No |
| Optional PLD Guardian job tool | Yes | No | No |
| Optional WHM/BRD/NIN reactive counter-CC | Yes | No | No |
| Optional team-visible Attack1 focus sign | Yes | No | No |
| Optional local Auto Low-MP Focus Target | Yes | No | No |
| Optional MNK Earth's Reply | Yes | Yes, when test mode is enabled | No |
| Optional DRK Hiebsprung held-key helper | Yes | No | No |
| Seiton `S1`-`S5` decision cues | Yes | Synthetic visual `S1` | No |
| Optional NIN Seiton held-key helper | Yes | No | No |
| Optional SGE Smart Kardia after accepted Eukrasia | Yes | No | No |
| Manual NIN Panic Shukuchi macro | Yes | Yes, when test mode is enabled | No |
| Optional DRK Shadowbringer two-line macro | Yes | Yes, for the exact current hard-target striking dummy when test mode is enabled | No |
| Near Assist | Yes | No | No |
| Near Help | Yes | No | No |
| Far Help | Yes | No | No |

Wolves' Den support is explicitly a test option. Panic Shukuchi chooses no enemy
and uses no target; its destination comes from the local NIN position, facing,
and exact forward terrain point. Enemy visuals require one
strict native hostile duel opponent; missing or ambiguous identity shows
nothing. The DRK macro does not use that duel-opponent path: with both existing
options enabled, it accepts only the exact current hard-target striking dummy.
Pressure has an additional Wolves' Den opt-in so testing does not create an
always-on pressure display by surprise.

## Settings and schema migration

The sidebar order is Start, Alerts, HUD & Nameplates, Combat Frames, Action
Helpers, Job Tools, Macro Helpers, Targets, and Diagnostics. Combat Frames are
visual controls; reactive defensive utilities and Smart Recuperate remain under
Action Helpers; independent PLD Guardian and accepted-Eukrasia Smart Kardia are
under Job Tools. Reset Defaults clears previews and restores every action,
target-write, party-visible communication, and Combat Frames master to off.

Configuration schema 30 remains current in v0.28.0.0; this release adds no
setting or migration. `/panicshu` is command-only and uses the existing global
plugin enable plus the existing Wolves' Den testing option. The held-action
cast-cancellation test is explicitly off
for fresh, reset, and migrated
configurations. An older explicitly enabled NIN fresh-edge helper still traverses
schema 29 and migrates to the replacement held-key option; the obsolete
compatibility field is then cleared. Every other existing master and helper
choice is preserved. Older configurations still traverse the earlier migrations
first, including schema 28's default-off post-Guard migration. Fresh and reset
configurations keep the Combat Frames master and every action-helper master off;
post-Guard defaults on only behind the disabled reactive-counter master, while
interaction and LB detail leaves likewise default on only behind the disabled
Combat Frames master.

## Install

Add this custom repository in Dalamud:

```text
https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/main/repo.json
```

Then search for **Seiton Sense** in the Plugin Installer. Existing installs
update through the same repository.

## Commands

- `/seiton` or `/ssense` - open settings
- `/nearassist` - arm one CC-only 750 ms target choice for the immediately
  following supported PvP macro action
- `/ssassist` - collision-free alias for `/nearassist`; `/seiton assist` is an
  additional fallback
- `/nearhelp` - arm one CC-only survival-target redirect for the next supported
  friendly PvP macro action; exact self is allowed only for self-targetable actions
- `/sshelp` - collision-free alias for `/nearhelp`
- `/farhelp` - arm one CC-only, backline-preferred farthest mobility redirect for
  the next reviewed friendly movement action; no valid reachable ally means no movement
- `/ssfar` - collision-free alias for `/farhelp`
- `/panicshu` - on exact PvP NIN, freeze the terrain point 19.5 yalms straight
  ahead and make at most one Shukuchi attempt in CC or enabled Wolves' Den testing
- `/seitonbringer` - arm the immediately following exact DRK Souleater Combo
  macro line for the bounded CC or explicitly enabled Wolves' Den striking-
  dummy weave check
- `/seiton show` / `/seiton hide` - enable or disable the entire plugin
- `/seiton preview` - preview nameplate indicators
- `/seiton flash` - preview the Seiton popup
- `/seiton debug` - print bounded diagnostics, including recent Near Assist,
  selected-target CC-brake resolution, isolation/reactive-defense state, Smart
  Recuperate, accepted-Eukrasia Smart Kardia, Combat Frames interaction/LB
  telemetry, Auto Low-MP Focus, DRK Hiebsprung/Shadowbringer, Panic Shukuchi,
  retained reactive counter-CC opportunity results, and the held-action cast-
  cancel request/epoch state
- `/seiton reset` - restore defaults
- `/howmany show` / `/howmany hide` - show or hide only the integrated pressure
  counter; these do not disable pressure-dependent helpers
- `/howmany lock` / `/howmany unlock` - lock or unlock only the counter window
- `/howmany reset` - restore only the counter-window position

## Standalone plugin retirement

Disable or remove standalone HOWMANY, CCImmunityWatch, NearAssist, and Super
Focus Glow before enabling their integrated equivalents. In particular, the old
NearAssist plugin owns `/nearassist`; Seiton Sense cannot register that command
while the old plugin is loaded. `/ssassist` remains available as a collision-free
alias during migration. Seiton Sense does not import, modify, or delete the
standalone plugins' saved configuration.

## Privacy and safety

Seiton Sense has no account, independent server, telemetry, or gameplay upload.
It does not read Home Worlds and does not persist combat, target, character-name,
or key history. While Combat Frames are enabled, current character names may be
read into the transient in-memory frame snapshot; the name-display toggle controls
only drawing. Names are never persisted or uploaded. The separate default-off
Guardian communication uses ordinary FFXIV
Quick Chat and marker commands, so enabling it creates the described party-
visible in-game side effect through FFXIV. Transient observations and the exact
one-shot action boundary are documented in [PRIVACY.md](PRIVACY.md).

Display-only features such as the resource aura never target, press actions,
accept clicks, or mutate native UI. Combat Frames do not automatically hide or
edit FFXIV's parameter/enemy-list HUD; their separate interaction leaf can only
set one revalidated exact living enemy row as hard target or expose it through
native `<mo>` while hovered. Auto Low-MP Focus is a separate explicit setter, and
the DRK macro is a separate explicit action helper; both are default-off and
bounded as described above. Panic Shukuchi instead has no automatic or held-key
trigger and no dedicated saved toggle: only the user-authored `/panicshu` command
can create its one bounded forward-location attempt. For one already incoming,
enabled CC action attempt
against an exact protected enemy, the optional brake can return `false` without
calling the downstream/original action function. The exact native selected target may
be read only to resolve an unchanged native target carrier of `0` or
`0xE0000000`; a zero marked as deliberately suppressed by Seiton's redirect
path is never restored. A missing hostile flag can be replaced only by the
strict complete visible five-member party proof in a known public CC territory;
self, party/alliance, native identity, and exact `<e1>`-`<e5>` checks remain.
Plugin-owned Wunder der Natur / Miracle of Nature and Stumme Nocturne / Silent
Nocturne attempts receive the same final
action-specific brake after redirect bypass. The brake never stores or replays
input and never chooses another target or action. Near Assist, Near Help, and Far
Help can each replace only the target ID of one explicitly armed, already incoming
macro action. Near Help may choose the local player only when the exact resolved action
supports self and passes native target/range/line-of-sight validation. Optional
action helpers use this current request priority: **Purify > reactive counter-CC >
Ally Rescue > PLD Guardian > NIN Seiton > SCH Critical Strategy > DRK
Hiebsprung > Smart Recuperate > generic Guard > pressure Sprint > event Kardia >
event Monk**. The six job-specific physical-hold helpers share the second tier
and use that deterministic urgency order; reactive counter-CC leads ally cleanse
because its LB and protection-end windows are shorter. Kardia requires its
separate accepted-Eukrasia trigger and does not originate from the physical key;
Monk is an automatic follow-up.
The same continuous hold can authorize later distinct exact held episodes; a
post-Purify reactive Guard no longer requires release/repress. There is no HP/
pressure pre-Guard. While your own Guard is
active, all Seiton action-request helpers are blocked; the same gate applies for
the bounded 1.5-second status-propagation interval after an exact local Guard
request. Manual game actions remain outside that protection. Ally Rescue labels a
removal `CLEANSED` only after the exact successful status-removal ActionEffect is
observed; attempts and client-accepted requests alone are not success claims.

For the ten physical-hold helpers, key choice prefers stable movement, then any
other stable held gameplay key, then fresh movement and fresh other gameplay
keys as fallbacks. Each helper evaluates its held lease before fresh input and
retains the exact frozen key until its normal release, ineligibility, reset, or
terminal action-specific boundary. This prevents a later short action-key tap
from displacing a valid long-held WASD lease.

The separate **Cancel my active cast for an otherwise-ready held helper** test
is disabled by default. It applies only to exact frozen physical-hold intents
for Purify, reactive counter-CC, Ally Rescue, Guardian, NIN Seiton, SCH Critical
Strategy, DRK Hiebsprung, Smart Recuperate, Guard, and pressure Sprint. Smart
Kardia and Monk Earth's Reply are excluded because they do not originate from
held input; every already-incoming manual/Turbo redirect, including Paean, and
all macro helpers are excluded as well.

When the highest-priority frozen intent passes its ordinary action, actor/
target, status/episode, key, context, Guard, resource, cooldown, range, line-of-
sight, empty-queue, and animation-lock gates and only the local cast remains in
the way, the plugin may call FFXIV's native cast-cancel boundary once for that
observed cast epoch. That native call returns no acceptance result: diagnostics
therefore say only that cancellation was **requested**, never confirmed. A cast-
signal mismatch, action-ID drift without a fully observed clear state, or any
other ambiguity fails closed without another request for the same cast.

Cancellation claims its framework frame, so it can never share a frame with a
helper `UseAction` request. Only a later frame that observes both cast signals
clear may run the normal complete helper preflight again. The option does not
synthesize movement or Escape, clear the native action queue, write cast state,
or mutate a selected target. It can sacrifice the current cast, and FFXIV may
refuse to cancel some actions. Stationary casts and mobile BRD Powerful Shot /
MCH Blast Charge still require current-patch live validation. The ordinary
clean-`false` action retry remains independent: calls stay at least 50 ms apart
with eight attempts maximum, and acceptance or ambiguity remains terminal.

The separate default-off Smart Recuperate helper may freeze an exact self
Recuperate `29711` epoch when missing HP is at least 16,000 and MP is at least
2,000. Readiness or insufficient MP waits without starving a currently usable
lower helper. Only an explicit client rejection may use the common bounded
same-intent retry; acceptance or ambiguity is terminal with no target change,
alternate, or replay.

The separate default-off Ninja helper sits between Guardian and Scholar in the
job-specific second tier. A
continuous held gameplay key can authorize one frozen target for each exact
adjusted-action epoch of Seiton `29515`/`29516`, selected by lowest exact HP ratio
among canonical, reachable `S1`-`S5` enemies below 50%. A clean explicit client
rejection may retry only that frozen action/target after at least 50 ms, with
eight native calls maximum. Only an accepted base `29515` can open one distinct
later adjusted `29516` epoch on the same hold. It never mutates a selected target,
selects twice, substitutes an alternate, falls back, reranks, replays input, or
claims that client acceptance proves a landed action or kill.

The separate default-off Monk helper may initiate at most one exact Earth's
Reply attempt per continuously observed Earth Resonance after every earlier
helper in the listed priority declines; it has no alternate action or retry.

The separate default-off Hiebsprung helper may initiate at most one exact DRK
`29092` request per proven ready epoch against a frozen canonical enemy at 30%
HP or lower within its strict 10-yalm cap and native reachability. It closes the
job-specific second tier before Smart Recuperate and the generic helpers. A
continuous hold repeats only after an observed
cooldown not-ready-to-ready transition, never from a guessed reset, and it has
no selected-target mutation, alternate, rerank, or replay. A clean explicit
client rejection may use only the shared bounded retry for that same frozen
ready epoch.

The separate DRK macro does not use that physical-input priority chain. It may
add only one already-spent Shadowbringer attempt to the exact authored
Souleater Combo carrier inside one proven GCD window; the carrier and visible
target remain unchanged, with no alternate or retry.

The isolation warning is local and display-only. The optional Attack1 focus
module is not display-only: it issues one normal, hardcoded party-visible marker
command when its exact gates pass. It never swaps targets or overwrites an
occupied Attack1 sign and clears only a marker whose ownership it can still
prove from exact identity and marker time.

The separate Auto Low-MP Focus option is local but not display-only: it can use
the one reviewed Focus Target setter only after an empty-to-exact preflight. It
never clears, replaces, or restores a Focus Target. Manual/external ownership
wins and latches, and the local Focus HUD/`<f>` result remains independent of
the party-visible Attack1 sign.

The separate Guardian communication option is likewise not display-only. After
one client-accepted automatic Guardian request it may issue one localized,
standardized CC Quick Chat for the frozen exact `P#` and a Bind2-ally/Bind1-self
pair. It sends no free text or character name and does not start the marker
sequence while either sign is occupied or uncertain. Bind2 is confirmed before
Bind1; a later Bind1 failure can clean only the proven-owned Bind2, and every
cleanup remains exact per sign. It does not issue another combat action or
retry a failed command.

The Scholar helper is not display-only either. When explicitly enabled, a
continuous hold may authorize one Critical Strategy intent for each distinct
exact guarded-target episode. The frozen target can use only the shared bounded
explicit-false retry; retry exhaustion or an ambiguous/invalid terminal outcome
latches only the Scholar helper until that exact key is released. It never sends
chat or markers, changes the selected target, reranks after drift, chooses an
alternate enemy, or replays input.

The Sage helper is also action-initiating and default-off, but it is not a held-
key scanner. One client-accepted Eukrasia forwarded unchanged creates one two-
second token. Only causal Eukrasia charge/status evidence, a fresh complete
pressure publication, exact Kardia readiness, and a clear animation lock can
advance it to at most one direct-target request for the frozen pressure-ranked
self/party candidate or exact self fallback. Once selection is evaluated the
token is spent. It never changes the selected target, chooses a lower candidate
when the best already has local-source Kardion, substitutes another action,
replays, or retries.

Like all third-party FFXIV modifications, use is at your own risk. Seiton Sense
is distributed through a custom repository, not Dalamud's official plugin
repository.

## Build and validation

The project targets Dalamud API 15 / .NET 10. The release workflow performs a
locked restore, warning-free build, dependency-free core tests, bounded-input
safety checks, source fingerprinting, and ZIP/manifest verification. Hosted CI
rebuilds the dependency-free core and verifies the committed package because
the Dalamud plugin SDK depends on assemblies from a local XIVLauncher install.

Those checks validate source, contracts, and packaging; they are not a claim of
fresh live in-game confirmation. Exact nameplate placement, native action-bar /
party-row / current CC-row aura anchoring, fixed Combat Frame rendering and
snapshot contents, pressure evidence, MCH marker/sound timing, optional action
helpers, and the macro helpers with both normal macros and Turbo Hotbar should be
rechecked in the relevant live PvP context after FFXIV, Dalamud, macro, network-
event, or input-handling changes.

For v0.28.0.0, source checks pin `/panicshu` as an explicit NIN-only command with
one exact 19.5-yalm forward terrain projection, one frozen destination, an at-
most-500-ms lease, and at most one native location-action call. Only an active
Self-Purify claim, cast, native queue, and animation lock may wait; action/readiness, identity, context, Guard,
crowd-control, destination, or metadata ambiguity fails closed. The checks also
pin exact Shukuchi `29513`, the Three Mudra/Doton block, CC plus existing Wolves'
Den test-option context, and the absence of automatic/held triggers, mouse/cursor
or target mutation, destination recomputation, alternate, retry, or shorter
fallback. They cannot prove current-client terrain collision, native line of
sight, client/server movement, or map behavior. Live Wolves' Den validation in
four facings on flat ground, slopes, and wall/invalid-endpoint cases remains
required; a Den result does not by itself prove Crystalline Conflict behavior.

For v0.27.1.0, source checks pin one shared held-action policy: the physical hold
remains consent across later distinct episodes, a per-frame claim allows at most
one held native boundary, known cooldown/resource/cast/queue/full-animation-lock
states spend no attempt, and only a clean explicit client rejection can retry
the same frozen intent after 50 ms with eight calls maximum. Client acceptance,
exceptions, uncertain queue/sequence transitions, key release, context/job/
identity drift, and other ambiguity are terminal. Tests cover exact action,
actor, status/episode memory, bounded current-key attachment followed by strict
key freezing, no rerank/alternate/target mutation, and
the priority **Purify > reactive counter-CC > Ally Rescue > PLD Guardian > NIN
Seiton > SCH Critical Strategy > DRK Hiebsprung > Smart Recuperate > generic
Guard > pressure Sprint > event Kardia > event Monk**. The six job-specific
physical-hold helpers occupy the second tier in that deterministic urgency order;
Kardia and Monk retain their separate event-driven origins.

The same checks pin stable-movement, stable-other, fresh-movement, then fresh-
other key selection and held-lease-before-fresh behavior for all ten physical-
hold helpers. Cast-cancel checks cover the separate schema-30 default-off toggle,
exact inclusion/exclusion list, one native cancel request per observed cast,
void/requested-only diagnostics, no same-frame helper action, fully revalidated
later dispatch, and the absence of movement/Escape synthesis, queue clearing,
cast-state writes, or target mutation. They cannot prove that FFXIV canceled a
live cast; current-patch stationary and mobile BRD/MCH tests remain required.

The same release checks bounded current-key attachment inside an urgent startup's
original threat lease; exact Purify/Guard actor/action/episode memory without an
event-edge key; current eligible key binding only at authoritative protection end
or inside the original 500-ms release opportunity; and strict generation
retirement on release or text input after binding. It also checks exact
self-target Purify sentinel capture with mandatory live Resilience, bounded
non-extending duration hints with mandatory live status absence, immediate
expected-end Resilience release, early Guard cancellation, pre-rank native
reachability/blocker filtering, the NIN-only 3-second protection-end lease, exact
source-sequence confirmation, and a terminal Guard tombstone through ambiguous
observations. It also checks Purify's absolute active-CC priority, Smart Recuperate's
inclusive 16,000-missing-HP/2,000-MP gates, Ally Rescue confirmation preservation,
all reviewed WHM/BRD/NIN reactive trigger families, both fully verified Raiju
variants with exact Stun confirmation, accepted-only Guard propagation,
Guardian, pressure Sprint, SCH, DRK cooldown epochs, and NIN base/follow-up as
separate adjusted-action epochs. It also retains Smart Kardia's accepted-
Eukrasia trigger, causal charge/status evidence, exact pressure selection and
direct target without a held-key scanner.

Combat Frame checks cover one fixed Self row plus canonical stable `S1`-`S5`,
dead/unknown row preservation, exact HP, trusted MP and 2,000-MP divisions,
bounded relevant statuses, pressure/current/focus display, transient optional
names, freshness, and fixed screen-space drawing. Interaction checks require a
fresh exact living canonical enemy row, one frozen/revalidated click write,
bounded native mouseover ownership, external-owner precedence, and no preview/
self/dead/unknown/stale hit region or native-HUD mutation. LB checks pin exact
Self gauge trust, remote `LB ?` before complete native-HUD calibration, no charge-
time estimate, live-RemainingTime-origin duration with at most 150 ms of non-
extending last-expiry preservation, the 1.8-second instant card, and direct
ally ActionEffect damage without HP-delta inference. Configuration checks pin
schema 30 migration, fresh/reset defaults, and default-off action/communication/
Combat Frame masters. Hiebsprung checks cover exact DRK/CC context, inclusive
30% HP, strict 10-yalm plus native reachability, local Bind, Guard/readiness gates,
one accepted action per proven cooldown epoch, and no target mutation. Reactive
CC checks include DNC/MCH/SAM/VPR BRD coverage, direct exact-actor post-Purify
dispatch without selected-target dependence, exact Guard `3054`/`3673` presence
followed by its first verified absent framework observation, no minimum team-
pressure gate, deterministic simultaneous ranking with fresh positive pressure
as an optional bonus before HP/trusted MP/stable identity while zero, unknown,
and stale pressure stay neutral, independent per-`S1`-`S5` Purify
tracking, one terminal winner, bounded retry for only the selected exact episode,
a later distinct release epoch without key release, the full priority chain, and
job-specific native reachability. Configuration checks pin unchanged schema 30,
the existing cast-cancellation explicit-off migration, and the prior NIN opt-in
migration to held consent.
They cannot prove live Eukrasia hook ordering, MP-tick and held-input timing,
native action acceptance/effects, current client range/line of sight, Combat
Frame appearance/calibration, native status/resource telemetry, reactive
RemainingTime hints, LB packet timing,
or server interruption behavior; current-patch Crystalline Conflict A/B tests
remain required.

For retained v0.18.0.1, the v0.18.0.0 source checks cover Auto Low-MP Focus's
complete canonical set, trusted MP hysteresis, deterministic selection,
stable-empty and manual-override ownership, frozen final preflight, sole set-only
write, exact readback, and no clear/replace/restore/retry contract. DRK checks cover exact
macro adjacency and carrier shape, proven 2.40-second GCD-cycle ownership, the
inclusive 0.60-0.80 remaining-time window, the never-at-or-below-0.50 boundary,
one token spent before one request, exact target/Guard/range/line-of-sight/
queue/resource gates, unchanged outer combo call, and no alternate, mutation,
replay, or retry. Those checks cannot prove the live Focus setter/HUD/`<f>`
result, ReAction Macro Queue/Turbo mode, native queue and recast-group timing,
server execution, or clipping. The hotfix additionally checks the existing
Wolves' Den opt-in, exact hard-target striking-dummy identity and NameId `541`,
the absence of synthetic enemy-slot or duel-opponent fallback, exact current
combo secondary-cost metadata, and main-thread framework-update cycle priming.
It does not turn a successful Den dummy trace into proof of live CC behavior;
both contexts retain their own current-patch A/B boundary.

The retained v0.17.0.0 source checks cover the exact direct hard/cast
pressure threshold, warning-entry episode ownership, native-sound one-shot
behavior, held-generation ownership, exact Sprint metadata/readiness, own-Guard
suppression, final pressure revalidation, and one consumed self action attempt
without alternate, replay, or retry. Those checks cannot prove live enemy-target
telemetry, the chosen FFXIV system sound, or Sprint acceptance/effect in the
current client and therefore still require a live CC A/B test.
Their historical one-consumed-attempt assertion is superseded by v0.24's shared
continuous-hold scheduler and bounded same-intent pre-acceptance retry.

The retained v0.16.0.0 source checks cover the Bard-only Paean action and
metadata boundary, the exact `3+` incoming-pressure threshold, exact non-self
party identity and native reachability, deterministic pressure/HP/identity
ranking, unchanged vanilla fallthrough before selection, frozen-intent
suppression after final drift, one target substitution on one already incoming
call, and no plugin-created action, alternate, replay, or retry. Those checks do
not prove that a manual or Turbo call was
accepted by the live client or that Paean applied, removed, or nullified CC.
They also retain the then-current Ninja helper's fresh-edge ownership, complete canonical
`S1`-`S5` auto-selection and adjusted-action gates, strict below-50% boundary,
latest-safe frozen-actor HP re-read, exact-50% cancellation,
Guard/priority suppression, and one-attempt/no-retry contract. The Ninja helper
has no hard-target dependency. Those historical NIN assertions are superseded
by v0.24's held-consent, adjusted-epoch, and bounded retry contract. Guardian communication checks cover accepted-
episode consumption, chat-only occupied-marker behavior, Bind2-before-Bind1
confirmation, both native empty-marker representations, exact per-sign
ownership, partial cleanup, and no post-invocation retry. Scholar checks cover
Guard-only complete `S1`-`S5` selection, trusted-
positive-pressure/HP ranking, held-generation ownership, native reachability,
frozen-intent revalidation, and one attempt without target mutation or alternate.
That historical Scholar ownership assertion is likewise superseded by v0.24's
exact episode lease and shared bounded retry.
Localized Quick Chat, marker placement/cleanup, Critical Strategy dispatch/
effect, and the passive Paean redirect still require current-patch live
confirmation.
Existing tests cover Near Help's exact self-target gate, critical-health
override, bounded pressure window, complete-view fallback, and deterministic
pressure/HP/distance ordering, plus the isolation debounce and
fail-closed unknown state, reactive defensive generation ownership, independent
Guardian selection, reactive event/status/team-focus rules, Attack1 selection/
ownership rules, and Guardian's delegation to native reachability without a
custom center-distance cap. They do not prove the current client's native 20-
yalm line-of-sight result, Purify-to-Resilience-to-new-generation Guard or
Guardian dispatch, Contradance startup timing, BRD/WHM counter dispatch, or the
native party-visible marker command and clear path. An `AUTO CC LANDED` confirmation
proves the matching status was observed on the intended enemy, not that a limit
break or damage was stopped. Those outcomes all require a current-patch live CC
A/B test. The deliberately omitted position/Splatoon guide has no runtime or
validation claim.
