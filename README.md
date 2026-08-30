# Seiton Sense

Seiton Sense is a local PvP awareness HUD that combines pressure tracking,
stable native-nameplate cues, personal warnings, job tools, one-shot macro
assistance, and target highlights. Version 0.41.0.0 makes automatic Purify and
Recuperate retain their exact episode through temporary native blocks and retry
only inside the original bounded window after every safety recheck. High-
pressure Stun Auto-Guard is now keyless and confirmed-only: one readiness-proven
retry may occur inside its original lease, while the card, sound, action
suppression, and two-second Guard-reuse protection begin only after the exact
live Guard status appears. Rejected requests therefore cannot create phantom
protection or block manual Guard. The release also adds exact SAM Chiten and SMN
Bahamut/Phoenix danger warnings plus an experimental opponent LB-ready strip
that is off by default pending current-client layout validation. Version
0.40.0.2 fixed intermittent ranged Smart Action no-ops by making the 750-ms arm
target-independent and deferring
exact enemy selection until the harmful action arrives. Direct single-target
actions no longer require an unrelated complete hostile object-table snapshot;
exact target protection, native range/line of sight, and frozen-target
revalidation remain mandatory, while target-circle and unknown AoE geometry
stay complete and fail-closed. Version 0.40.0.1 added one separate default-off
automatic cast-cancel permission shared only by Auto Purify and Auto Recuperate.
It admits only metadata-verified BRD job 23 / Powerful Shot `29391` or MCH job 31 /
Blast Charge `29402` when the live job, cast, and adjusted identity all remain
exact; every other or uncertain cast waits. The generic held-helper toggle stays
independent, and a permitted automatic helper still revalidates only on a later
clear-cast frame. Version 0.40.0.0 added the separate default-off automatic
Purify and Recuperate modes while preserving the legacy held helpers, exact
Guard/NIN Hidden safety, and shared one-episode anti-duplicate state. Version
0.39.0.2 repairs AST held Harmonischer Orbis and its
same-target Zweifachzauber follow-up, keeps non-NIN `/seitonbw` screen-back
movement authoritative through ReAction's camera-relative dash rewrite, and
restores a compact one-card CC rotation view with an expandable six-map deck.
Version 0.39.0.1 expanded the default-off `/seitonbw` camera-back
macro from NIN to the reviewed self dashes on AST, DNC, DRG, RPR, and PCT and
enlarged the rotation cards and typography. Version 0.39.0.0 added the default-off RDM held
helper for one exact Corps-a-corps into the first second of a freshly observed
enemy Guard and changed the rotation panel to one always-visible seven-card
current-to-next deck using local FFXIV duty artwork and a 0.65-second reorder
animation. Each card can show exact local, per-character W/L for future public
CC matches or `NO DATA`; no result is inferred, and the panel makes no network
request. Recording has a separate default-on local toggle and a safe clear
control, independent of panel visibility. It retains v0.38.0.0's shared Auto Shadowbringer cadence, default-on
**Preserve Blackblood** gate, frozen exact-CC held Smart Action target policy,
and exact `<t>` Wolves' Den test path, v0.37.0.0's movable rotation panel and
read-only PvP range helper, plus
v0.36.0.1's protection-safe Viper
Smart Action targeting and exact enemy DRG `Sky High` warning, v0.36.0.0's AST held `/nearhelp` heal,
v0.35.0.3's exact Guard-ignoring Smart Action
support, v0.35.0.2's Panic Shukuchi repair and Ninja Hidden protection, plus
v0.35.0.1's native Turbo/Latest Input path, exact Viper Wolves' Den targeting,
and removal of the nonfunctional Scholar spread workflow. The generic
one-shot action buffer remains available directly in Seiton Sense. A fresh physical standard-keyboard-hotbar press may retain
one exact direct instant action for 1,000 ms by default, adjustable from
100-1,500 ms; the movable learning panel shows its key, slot, action, and live
countdown. Turbo repeats only the newest certified current slot, emits no
catch-up bursts, and yields to Purify and every higher-priority held helper. Smart
Action protection is rebuilt directly before a sole delayed replay, while
audited ReAction/MOAction conflicts disable only that buffer opportunity. It
retains v0.34.0.4's Chiten, Covered, Paladin-LB, Dark-Knight-LB, and
target-circle safety, with Guard bypassed only by exact Guard-ignoring actions,
plus v0.34.0.3's Smart Tab line-of-sight and ranked-cycle
fixes. Confirmed Auto-Guard can show a card/sound and protects an accidental
second Guard press for two seconds; provisional or rejected requests do not arm
either effect. `/panicshu` now reaches its one location call
only after exact native Shukuchi recast and resource readiness. It retains
Emergency Teleport plus v0.31's ranged Smart Tab, direct Viper carrier handling,
and explicit Wolves' Den testing additions. `/smarttab`
(`/sstarget`) toggles the native forward-target replacement; paired handler/helper
hooks preserve the game's own binding and UI/input gates. The v0.30 line moved the optional harmful-action
redirect to `/smartaction` (`/ssaction`) behind its own default-off setting and retired the
unusable fixed Combat Frames runtime and its click/mouseover and calibrated-gauge
paths. Useful Limit Break evidence now appears as exact enemy nameplate icons, a
safe self activation banner, and a bounded ally damage feed. It also provides a
visible `/autoseiton` ON/OFF tile that still requires a physical held key, local
4,000/2,000-MP sounds, a version-acknowledged What's New window, a pressure-aware
Guardian policy, and three-second protection-end leases.
The suite combines the useful parts of HOWMANY, CCImmunityWatch, NearAssist,
and Super Focus Glow into one configurable custom-repository plugin.

## Highlights

- **Local CC rotation panel:** while in Wolves' Den Pier, one larger movable and
  lockable current-map card shows the Patch 7.5 map, live countdown, local FFXIV
  duty artwork, saved `<` / `>` phase calibration, and this exact local
  character's W/L. A full-width control expands or hides the next six maps.
  At rollover the compact card slides to the new map; the expanded seven-card
  deck reorders over 0.65 seconds. Every visible card shows confirmed W/L and win rate, or
  `NO DATA`. Only future public CC post-match results with one exact unique local
  Content ID, ten unique participants, valid teams, duration, result, and known
  territory are counted; custom matches, Wolves' Den, ambiguous payloads, and
  corrupt storage count nothing. The local file stores only salted HMAC keys,
  per-map totals, and bounded hashed deduplication records—never names or raw
  Content IDs. The panel downloads no artwork and uses no network endpoint.
- **PvP range helper:** two flat world-space rings follow the local player in PvP
  and Wolves' Den. The inner ring marks nominal 5-yalm melee reach; the outer
  ring marks the current combat job's furthest reviewed hostile non-LB action,
  including hostile gap closers. All 21 PvP-enabled jobs are covered, with
  configurable visibility, labels, colors, opacity, width, and foreground
  placement. The guide is geometric only: it does not claim line of sight,
  cooldown readiness, terrain reach, or target-hitbox overlap, and it never
  changes targeting or action dispatch.
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
- **Stable held-action leases:** Purify, AST held Near Help, SAM, NIN Seiton,
  VPR Serpentiner Geist, GNB Continuation, reactive counter-CC, Ally Rescue,
  Guardian, NIN Guard-Shukuchi, SCH Critical Strategy, DRK, held Monk, Smart
  Recuperate, Emergency Teleport, Guard, and pressure Sprint prefer an
  already-held movement key, then any other stable held key, before fresh
  movement/other fallbacks. Every helper keeps its exact key lease once bound
  rather than letting a later action tap replace and prematurely cancel it.
  Reactive urgent-startup events may bind the first eligible current generation
  inside the original short threat lease. Purify/Guard retain their exact enemy
  episode while protection is live. Authoritative protection end opens a strict,
  non-extending 500-ms key-acquisition edge. When an eligible current key is
  acquired inside that edge, exactly one actor/key intent freezes; transient
  range, line of sight, cast, queue, or animation gates may then wait only until
  three seconds from the original release. Binding never restarts that deadline.
  A text-poisoned generation is never eligible, and no different key can inherit
  the frozen intent.
- **Experimental cast cancellation:** the existing separate default-off held-
  helper test may request one native cancel when its highest-priority exact held
  intent is otherwise ready. A second, independent default-off permission lets
  only Auto Purify or Auto Recuperate sacrifice exact metadata-verified BRD
  Powerful Shot `29391` or MCH Blast Charge `29402`; job, active cast, and
  adjusted identity must all match. Every other or uncertain automatic cast
  waits. Cancellation never shares a frame with the helper, synthesizes movement
  or Escape, clears the queue, or changes a target; a later clear frame repeats
  full validation. The void call reports only `requested`, not confirmed, and
  current-patch BRD/MCH live proof remains pending.
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
  Marksman's Spite, an enemy DRG entering `Sky High`, exact SMN Bahamut/Phoenix
  LB activations, and an enemy SAM's exact Chiten episode receive stable warnings.
  The LB danger card is larger by default and can play one selectable built-in
  FFXIV sound once per verified danger episode.
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
- **Exact Limit Break cues without Combat Frames:** reviewed enemy LB activations
  draw an icon above the exact actor's fresh native nameplate, with a countdown
  only when live status duration is confirmed and a bounded flash otherwise.
  Enemy DRG `Sky High` additionally raises an immediate top-center airborne
  danger card and one-shot sound, exact SMN Bahamut/Phoenix activations use the
  same bounded danger lane, and Chiten adds a large SAM nameplate emblem with a
  confirmed countdown plus `DO NOT HIT` card.
  Your own activation uses a separate top-center `LB ACTIVATED!` lane, while
  direct attributable ally LB damage uses at most three left-side cards. The
  retired frame renderer, calibrated/estimated gauges, row clicks, and native
  mouseover publication no longer have a runtime path. A separate experimental,
  default-off CC-only strip above the pressure display reads direct current/max
  GaugeBar values only after a stable exact S1-S5 join and same-frame local-
  controller proof; its current-client layout still needs live validation.
- **Ninja Seiton decisions:** persistent job-icon cards, `S1`-`S5`, preparation
  cues, and entry pulses use FFXIV's native CC enemy order and verified
  range/line-of-sight checks.
- **Experimental Ninja Seiton helper:** a separate default-off option can use
  continuous held-key consent for exact adjusted-action Seiton epochs. It selects
  the lowest exact HP ratio among canonical `S1`-`S5` enemies that are strictly
  below 50% and natively reachable. Exact CC context, Ninja job, adjusted action
  readiness, own-Guard safety, and the shared higher-priority helper boundary
  all fail closed. `/autoseiton` and the movable action-bar-style tile switch
  availability ON/OFF and show a ready sparkle, but ON never replaces the held-
  key consent requirement. Base Seiton and the verified Unsealed follow-up are
  distinct epochs; a rejected base request can never substitute the follow-up.
- **Experimental Viper Serpentiner Geist helper:** a separate default-off option
  checks FFXIV's currently transformed Serpent's Tail carrier `39183` while any
  eligible gameplay key, including WASD, remains held. If the carrier exposes
  one reviewed follow-up `39174`-`39182`, exact CC uses the existing Smart Action
  rank across reachable canonical `S1`-`S5` enemies. A fully protection-safe
  current hard target is only the last fallback. The exact action, chosen actor,
  and held key freeze for the attempt/retry episode; later drift ends that carrier
  exposure instead of reranking. The helper never visibly changes the selected
  target, substitutes a follow-up, dispatches carrier `39183`, or cancels your cast.
  Explicitly enabled Wolves' Den testing remains restricted to the exact current
  hard target when it is either the native hostile duel opponent or the reviewed
  combat striking dummy with NameId `541`.
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
- **Experimental Smart Recuperate helper:** separate default-off automatic and
  held-key options can use exact self Recuperate `29711` when at least 16,000
  HP is missing and at least 2,000 MP is available. The thresholds are inclusive;
  cooldown, MP, cast, queue, or animation-lock shortage waits without consuming
  held consent. The automatic path does not cancel a cast. An explicit client
  rejection may retry only the same exact
  self epoch; acceptance is terminal and is never redirected or replayed. It
  runs in exact CC and, only with the separate test option, Wolves' Den; the
  supported context is frozen so an attempt cannot drift between them.
- **Experimental Emergency Teleport helper:** a separate default-off held-key
  option for PvP MNK, BLM, SGE, and VPR runs after Smart Recuperate. With strict
  configurable low-HP, low-MP, fresh direct-focus, minimum-travel, and destination-
  safety gates, it freezes the safest distant exact party member and makes at
  most one native jump for that danger episode. It never changes the visible
  target, substitutes another ally, falls back, or retries. Wolves' Den support
  is available only through the existing explicit test option.
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
- **Experimental AST held Near Help:** a separate default-off Astrologian option
  applies the same exact friendly ranking continuously while a gameplay key is
  held, restricted to self/party players at 60% HP or lower. It uses Harmonischer
  Orbis / Aspected Benefic `29243` without changing the visible target. If Double
  Cast was already available before that accepted heal, the helper invokes raw
  carrier `29245` only while it resolves exactly to repeat `29247` for the same
  frozen player; otherwise it ends
  after Orbis. The follow-up never reranks when the first heal raises HP.
  Your own active or still-propagating Guard suppresses the entire sequence and
  is rechecked at the final action/cast-cancel boundaries, so neither the base
  heal, its follow-up, nor optional held-cast cancellation can break it.
- **Experimental RDM fresh-Guard engage:** a separate default-off Red Mage
  held-key option can use Corps-a-corps `29699` during only the first second of
  one exact enemy Guard absent-to-present episode. It requires the unspent exact
  Riposte `41488` starter plus inclusive configurable own HP/MP thresholds
  (80% / 50% by default). The actor, Guard episode, action, context, and physical
  key stay frozen; only a client-accepted Corps-a-corps may hard-target that same
  actor once. It never performs the melee follow-up, substitutes another enemy,
  or reranks. Wolves' Den testing uses only the exact current target.
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
  immediate Shukuchi attempt at the terrain point 19.5 yalms along the
  character's current facing. It is command-only, works from own Guard, is never
  automatic or held-triggered, changes no cursor or target, stores no pending
  attempt, and never retries or searches for a shorter fallback point. The one
  native location call is reached only when Shukuchi's exact recast is positively
  ready, preventing a client-predicted startup that would be rolled back.
- **Optional camera-back job dash macro:** enabling the default-off `/seitonbw`
  command makes one immediate reviewed self-dash on NIN, AST, DNC, DRG, RPR, or
  PCT. NIN keeps the exact 19.5-yalm ground Shukuchi; the other jobs use
  Epicycle, En Avant, Elusive Jump, Hell's Ingress, or Smudge after aligning
  only character facing so the native dash travels toward camera screen-back.
  The camera and targets never change. Unsupported/transformed/non-ready actions
  and unavailable or nonstandard camera state fail closed, with no queue,
  pending state, fallback, or retry.
- **Held DRK Shadowbringer:** the separate default-off Dark Knight helper uses
  ordinary held-key consent. Its default-on **Preserve Blackblood** sub-option
  blocks both exact Dark Arts and the configurable high-HP/low-pressure fallback
  while exact status `3033` exists. Both paths share a fixed 1.8-second cadence;
  continuous safe HP/pressure can open exactly one new fallback generation when
  it ends. Dark Arts remains first and ignores those HP/pressure sliders. Once
  an observed Blackblood disappears stably, whether consumed or expired, the
  next eligible cycle may begin. A confirmed or ambiguous automatic request
  whose complete short Blackblood lifecycle falls between framework samples
  uses a 1.5-second grace plus one later absent sample rather than requiring a
  manual Shadowbringer. Ambiguous calls still latch their physical key until
  release. Disabling preservation removes only the Blackblood wait; the cadence
  remains. In exact CC the helper uses the existing
  held Smart Action policy without requiring the macro toggle, never changes the
  visible target, and freezes the exact actor rather than reranking. Shadowbringer's
  line-AoE protection remains fail-closed. Wolves' Den stays on the exact current
  `<t>` duel opponent or striking dummy, assumes unavailable CC pressure as zero
  for testing, and retains HP, range, line-of-sight, cadence, and Blackblood gates.
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
  keylessly reserve a high-pressure Stun chain through exact Purify and positive
  Resilience, then request Guard after the CC is gone. It does not pre-Guard
  from HP/pressure prediction. One readiness-proven confirmation retry may occur
  only inside the original two-second lease. A native return remains provisional:
  only the matching exact live Guard status arms the card, sound, cancellation
  shield, and two-second Guard-reuse protection. Clean rejection retracts the
  generation, so it cannot block manual Guard or higher-priority recovery.
- **Experimental Paladin Guardian job tool:** an independent default-off held-key
  option can attempt Guardian on one exact reachable ally. The original critical
  boundary remains unconditional at 20% HP; a fresh exact current hard/cast-
  target count of at least three enemies may trigger the same frozen rescue
  earlier, at 35% HP or lower. A
  separate default-off communication option can follow only a client-accepted
  automatic Guardian with localized CC Quick Chat row 35 and an ownership-safe
  Bind2-ally/Bind1-self pair.
- **Experimental reactive counter-CC:** the default-off helper uses WHM Wunder
  der Natur / Miracle of Nature, BRD Stumme Nocturne / Silent Nocturne, or both
  metadata-verified NIN Forked/Fleeting Raiju variants on exact DNC, MCH, SAM,
  or VPR urgent startup evidence. Protection-end-only options additionally cover
  PLD Intervene, RDM Resolution or an exact Forte-to-Vice proc, BLM's exact Soul-
  Resonance-to-Frost-Star proc, and staged SAM Soten/Mineuchi. It can also follow
  any of
  the six exact Purify-removable enemy statuses after real Resilience ends, or
  an exact Guard on its first verified absent framework observation. Both
  follow-ups bind the exact canonical `S1`-`S5` actor directly and have no minimum
  team-pressure-count gate. For simultaneous releases, only fresh exact team
  pressure above zero earns a highest-first ranking bonus; zero, unknown, or
  stale pressure is neutral. Lowest HP ratio follows, then lowest trusted MP and
  stable identity. An exact current key must be acquired inside the original
  strict 500-ms protection-end edge. Exactly one simultaneous winner then binds
  one exact intent; Guard retires every simultaneous loser before a
  higher-priority wait, with no rerank or fallback. The bound actor/key remains
  frozen while native range/line of sight, blocker, cast, queue, and animation
  state are revalidated as dispatcher wait gates. A clean rejection may retry
  only that intent, and a later distinct release epoch can trigger on the same
  continuous held key without requiring or mutating the selected target. WHM
  uses native 10-yalm range; BRD and both NIN Raiju variants use native 20-yalm
  range. Exact server ActionEffect landings build a bounded per-action and edge-
  distance calibration. Prediction needs five current-or-nearer samples and at
  least one eligible sample from the current runtime session; otherwise it waits
  for authoritative protection absence. A true main-GCD counter that is busy at
  its ideal request frame keeps only the frozen action/actor/episode until the
  first ready frame strictly before `1000 ms`, without claiming input or
  cancelling a cast. Every automatic landing requires the exact expected status
  on the frozen enemy and the exact native action source sequence, so a manual
  cast cannot claim it. An
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
  Nameplates, Action Helpers, Job Tools, Macro Helpers, Targets, and Diagnostics.
  Shared-input actions document their real priority order, while visual,
  macro, and job-specific controls stay in their own pages. Configuration schema
  33 separates the new native Smart Tab switch from the previous Smart Action
  macro opt-in while retaining schema 32's Combat Frames retirement and useful
  LB-display migration. Both the generic held-helper cast-cancellation test and
  schema-46 automatic basic-shot permission remain explicitly off for fresh,
  reset, and migrated configurations. Smart Recuperate, accepted-Eukrasia Smart Kardia, PLD
  Guardian, Auto Low-MP Focus, held DRK Shadowbringer, pressure Sprint and its native
  system sound, the Bard Paean pressure redirect, Guardian team communication,
  and Scholar Critical Strategy remain separate opt-ins. Every action-attempt,
  target-redirect, and party-visible communication feature remains opt-in.
- **Local release acknowledgement:** after a version update, one compact What's
  New window summarizes three to five changes. Closing it or pressing **Got it**
  stores only the current plugin version locally, so the same release does not
  reopen or write chat spam.

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

## Local MP sounds and Limit Break notifications

The optional local-player MP warning uses only a trusted exact 10,000-MP sample.
It plays one selectable built-in FFXIV system sound when MP crosses downward
through 4,000 and a separately selectable cue at 2,000. Each threshold has its
own recovery hysteresis. A direct drop through both thresholds consumes both
edges but plays only the more urgent 2,000-MP cue. Context, identity, death, or
untrusted telemetry resets continuity rather than inventing a crossing.

The fixed Combat Frames runtime, its six rows, calibrated/estimated remote gauges,
click-target path, and native mouseover publication are retired. The useful LB
evidence is now split into narrow, non-interactive surfaces:

- a reviewed enemy activation icon is stacked above that exact actor's fresh
  native nameplate and never covers the HP/MP row;
- a duration countdown appears only from confirmed live status timing; instant
  or unconfirmed activations use the catalog-bounded brief flash;
- an exact enemy DRG `Sky High` episode also draws an immediate top-center
  airborne danger card and plays at most one configured built-in sound;
- exact enemy SMN Bahamut/Phoenix activations and exact SAM Chiten episodes use
  the same bounded danger lane; Chiten also gets a large nameplate emblem and
  countdown while its exact status remains live;
- an experimental, default-off CC-only strip above the pressure counter shows direct native
  S1-S5 GaugeBar current/max values. It publishes only a complete stable row/name/
  slot join cross-checked against the same-frame local LimitBreakController;
  confirmed-full bars pulse lightly and any uncertainty hides the whole strip;
  the current-client row layout remains a live validation boundary;
- the local player's activation appears in a separate top-center
  `LB ACTIVATED!` banner; and
- direct attributable ally LB damage appears in at most three left-side
  `player -> target` cards, using optional current display names or stable slots.

Every surface requires a fresh exact runtime snapshot. The damage feed accepts
only a direct ActionEffect event with an exact ally caster, enemy target,
reviewed LB action, nonzero event/episode token, and decoded positive damage; it
never infers damage from HP deltas. Names are resolved only for current display,
never persisted or uploaded. These cues do not accept clicks, set a hard/soft/
Focus target, publish `<mo>`, calibrate or estimate a remote gauge, or edit/hide
native UI.

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

## Ninja Guard-Shukuchi held-key helper

The separate **Shukuchi to a guarded enemy below 20% HP on held key** experiment
is disabled by default and runs only on PvP Ninja in exact Crystalline Conflict.
It independently resolves canonical `S1`-`S5` actors, so a missing unrelated slot
does not disable an otherwise exact candidate. The chosen enemy must be alive,
targetable, strictly below 20% HP, carry a finite positive live Guard / Wehr row
`3054` or `3673`, and have a finite current position inside Shukuchi's native
three-dimensional 20-yalm range. Exactly 20%, unknown identity, no live Guard,
or Three Mudra changing Shukuchi `29513` into Doton `29514` fails closed.

Fresh positive team pressure is an optional ranking bonus; zero, unknown, stale,
or unavailable pressure is neutral and never gates the helper. Remaining order is
lowest exact HP ratio, then S-slot and stable actor identity. Selection freezes
one actor, not a substitute or fallback. Every possible request re-resolves that
same actor and uses its latest revalidated position. Only a proven client-false
result may use the common bounded same-actor retry.

The helper calls the ground-targeted Shukuchi location boundary first. Only after
the client returns accepted does Seiton Sense re-resolve and hard-target that same
living enemy once. Rejection, ambiguous acceptance, identity drift, death, or
readback mismatch never changes the selected target. Own Guard and its bounded
propagation latch block this automatic helper; only the explicit `/panicshu` and
enabled `/seitonbw` commands are own-Guard exceptions. A continuing hold may authorize a later Shukuchi only after
the cooldown was positively observed unavailable and then ready again. It runs
after PLD Guardian and before SCH Critical Strategy; NIN Seiton has already had
its higher-priority opportunity immediately after Purify.

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
The `/autoseiton [on|off|toggle]` command and NIN-only action-bar-style tile
change only this persisted opt-in; neither substitutes for a held physical
gameplay key.
Every candidate must remain living, targetable, hostile, strictly below 50% HP,
and accepted by FFXIV's native action range and line-of-sight result. The lowest
exact HP ratio wins, followed by stable S-slot and actor-identity tie-breaks.
The current adjusted action must be the ready base Seiton Tenchu `29515` or its
verified Unsealed follow-up `29516`.

Before ranking and again at every frozen retry, optional cast-cancel request,
and final native action boundary, Seiton Sense checks the exact actor for the
target-side Guardian `Covered` rows, Phalanx's Paladin-only `Hallowed Ground`,
and Eventide's `Undead Redemption`. Those targets are ineligible until the
status disappears. The covering Paladin, Phalanx's party-wide 33% mitigation,
and Guard / Wehr are not blockers. Execute/PREP cues clear for protected actors,
and a metadata mismatch disables automated Seiton fail-closed; manual Seiton is
never intercepted.

Ninja Seiton follows only Purify. It precedes reactive counter-CC, Ally Rescue,
PLD Guardian, NIN Guard-Shukuchi, SCH Critical Strategy, DRK Hiebsprung, Smart
Recuperate, Emergency Teleport, generic Guard, pressure Sprint, event Kardia,
and event Monk.
Active own
Guard and the bounded post-request Guard-propagation gate suppress the Ninja
helper. One exact adjusted-action epoch freezes one target. Known unavailable
states wait without consuming the common retry budget. Only an explicit client
rejection may call that same intent again after 50 ms. The default legacy budget
is eight native calls; the separate default-off PvP latency-response option
freezes the configured 100-1500 ms clean-false budget for that exact intent
(1000 ms = 21 calls, 1500 ms = 31). Acceptance or ambiguity is terminal. A genuine accepted base-to-
Unsealed action transition can create a later distinct epoch on the same hold,
but rejected base Seiton can never substitute the follow-up. The same frozen
S-slot and actor identity are resolved before every possible request, and that
exact actor's HP and protection are read again. Exactly 50% or higher or newly
observed Covered/LB invulnerability cancels and retires that exact intent. This minimizes wasted
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

The current request order before Scholar Critical Strategy is Purify, AST same-
target healing, SAM reactive actions, NIN Seiton, VPR Serpentiner Geist, GNB
Continuation, reactive
counter-CC, Ally Rescue, PLD Guardian, then NIN Guard-Shukuchi. DRK Dark Arts
Shadowbringer, Hiebsprung, the safe Shadowbringer fallback, and held Monk combo
follow before Smart Recuperate, Emergency Teleport, and the generic helpers. Continuous held consent
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
  order is **Purify > Smart Recuperate > automatic Guard > AST same-target heal
  chain > RDM fresh-Guard engage > SAM staged counter-CC / Zantetsuken > NIN Seiton > VPR
Serpentiner Geist > GNB Continuation > reactive counter-CC > Ally Rescue > PLD
  Guardian > NIN Guard-Shukuchi > SCH Critical Strategy > DRK Shadowbringer (Dark
  Arts) > DRK Hiebsprung > DRK Shadowbringer (safe fallback) > Monk combo >
  Emergency Teleport > pressure Sprint > event Kardia
> event Monk**.

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
card before the later damage event. An exact enemy DRG `Sky High` activation
starts a matching airborne LB warning immediately; a countdown continues only
from its live mapped caster status and clears with that exact episode. The
same lane admits only the exact enemy SMN Bahamut/Phoenix action/icon/status pair.
An exact enemy SAM Chiten status episode draws `DO NOT HIT`, a large nameplate
emblem, and its live countdown. The optional sound uses FFXIV's built-in effects
and is consumed once per verified episode. These warnings never press Guard or
another action.

Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature receive urgent
Purify warnings. The experimental **automatic Self-Purify** and legacy physical-
key modes are separate, disabled-by-default options. Each debuff type has its own
automation toggle. Automatic mode reacts to the actually present exact status
without a gameplay key; the legacy mode accepts a fresh key and can optionally
allow an already-held gameplay key such as WASD at status entry. If both modes
are enabled for the same debuff, automatic consent deterministically owns that
status episode; it neither consumes nor retires the observed physical generation.

The original key is never swallowed, delayed, or replayed, and automatic mode
does not retire its generation. Ready Purify has absolute priority while the
exact enabled CC is active. Cooldown/resource shortage does not starve lower
helpers; cast, queue, or animation-lock blocks wait without spending an attempt.
Automatic Purify does not inherit the generic held-helper cast-cancel toggle.
Only the separate default-off automatic permission may request one cancellation,
and only for exact metadata-verified BRD job 23 / Powerful Shot `29391` or MCH
job 31 / Blast Charge `29402` when the live job, cast, and adjusted raw-action
identity agree. Every other, transformed, or uncertain cast waits. Purify may
dispatch only after its complete preflight succeeds again on a later clear frame.
Only an explicit client rejection may retry the same frozen self intent after 50 ms.
The default remains eight native calls; the separate default-off PvP latency-
response option can freeze a 100-1500 ms clean-false budget for that exact CC
episode. Acceptance or ambiguity ends it.
ReAction Turbo repeat pulses do not create physical consent.

The separate **automatic Smart Recuperate** and **Smart Recuperate on held
gameplay key** experiments are disabled by default and run only in exact
Crystalline Conflict or in Wolves' Den while the separate testing option is
enabled. They share one state machine and freeze PvP Recuperate `29711`, the exact
local player, and the current supported context. A CC/Den/context transition
cancels that intent instead of carrying it into different content; Frontline and
Rival Wings remain excluded. The local player must be alive and targetable, the
action and metadata must be exact and locally ready, at least 16,000 HP must be
missing, and at least the exact 2,000-MP cost must be available. Both boundaries
are inclusive: exactly 16,000 missing HP and exactly 2,000 MP are eligible.

Held consent or an automatic opportunity may wait while Recuperate is not ready
or MP is below 2,000, without starving a currently usable lower-priority helper.
Automatic Recuperate normally waits for casting to end. Only the same separate
default-off automatic permission described above may cancel an exact verified
BRD Powerful Shot or MCH Blast Charge; it never inherits the generic held-helper
toggle. Any other or uncertain cast waits, and healing need, HP/MP, identity,
context, Guard, NIN Hidden, metadata, and readiness are all rechecked on a later
clear-cast frame. Once all gates pass, the exact self intent is revalidated before
every possible call. Only an explicit client
rejection may retry that epoch under the common bound. Temporary readiness/MP,
higher-priority, and Guard states wait without spending a call; dropping below
the HP threshold cancels the current intent. Acceptance ends that epoch, and a
later one requires an observed cooldown unavailable-to-ready transition. Retry
exhaustion or an ambiguous/invalid exact outcome latches only this helper until
the held key is released, or for automatic mode until the HP opportunity clears.
The helper never changes a target, buffers MP,
substitutes another action or actor, or replays input, and client acceptance is
not a healing-effect claim.

## Emergency Teleport held-key helper

The separate **Emergency Teleport on held gameplay key** experiment is disabled
by default. It supports PvP Monk Thunderclap `29484`, Black Mage Aetherial
Manipulation `29660`, Sage Icarus `29261`, and Viper Slither `39184` in exact
Crystalline Conflict and, only with the existing test switch, Wolves' Den. It
runs directly after Smart Recuperate and before generic Guard.

An episode opens only while the local player is alive and targetable, HP is
strictly below the configured percentage (50% by default), MP is strictly below
the configured threshold (4,000 by default), and a fresh direct-pressure sample
proves at least the configured enemy count (one by default). The destination
must be an exact living, targetable party member in native range and line of
sight, at least the configured edge-to-edge travel distance away (10 yalms), and
inside a complete enemy-position snapshot. Candidates first minimize enemies
within the configurable safety radius (default: none inside 10 yalms), then
maximize travel distance and minimum enemy clearance, with stable party identity
as the final tie-break.

The winning local actor, action, ally, key, context, and danger episode freeze
before the final native boundary. The shared frame remains readable through that
final commit and is consumed only afterwards, so reservation cannot invalidate
its own held-key evidence. At most one native request is committed for the danger
episode before the call itself; rejection, exception, ambiguity, target drift,
or any later failure cannot choose another ally or retry. A later episode requires
known-safe HP, MP, or direct-focus evidence for a stable rearm interval. The helper
never visibly switches the selected target, and client acceptance does not prove
that the movement occurred.

## Viper Serpentiner Geist held-key helper

The separate **Serpentiner-Geist-Folgeaktion on held gameplay key** experiment
is disabled by default and runs only on PvP Viper. While any eligible gameplay
key, including WASD, remains held, Seiton Sense polls FFXIV's currently transformed
Serpent's Tail / Serpentiner Geist carrier `39183` every active framework frame.
If that carrier exposes one reviewed follow-up `39174`-`39182`, the exposed action
itself is the complete opportunity signal. The helper does not hook, record,
require, or attempt to prove the preceding Viper action, its invocation mode, or
its native queue history. Normal hotbar, Turbo, combo, macro, and queued preceding
actions therefore converge on the same rule once FFXIV exposes the transformed
carrier.

Carrier `39183` itself is never dispatched.
The ranged Uncoiled follow-ups `39177` and `39178` use their native 20-yalm range;
the other reviewed follow-ups use 5 yalms. In exact Crystalline Conflict, target
selection uses the shared Smart Action order: reach tier, lowest HP ratio, fresh
team pressure, unavailable Guard, then trusted low MP and stable slot order.
The complete protection snapshot also excludes Chiten, Covered, Paladin LB, and
Dark Knight LB; these Viper follow-ups may bypass only Guard because their exact
metadata says they ignore it. If no ranked winner exists, the exact current hard
target is admitted only when it independently passes the same canonical,
protection, reach, native range, and line-of-sight checks.

The exact adjusted action, chosen actor, context, territory, physical key,
readiness, native range, and line of sight are frozen and revalidated before
every possible call.
Purify retains absolute priority, and this is Viper's earliest held job helper.
Own Guard blocks it; target Guard is not an added blocker. Action,
resource, target-status, or range unavailability yields the framework frame to a
usable lower-priority helper. Only an otherwise-ready exact intent waiting on the
native boundary or its retry throttle keeps Viper's priority. Only a clean client
rejection may use the shared bounded same-intent retry. Acceptance, ambiguity,
retry exhaustion, key release, stable carrier loss, or any identity/action/context
drift is terminal for that frozen episode without an alternate, rerank, target
change, or replay. One false carrier sample is treated as flicker and cannot
rearm a spent exposure. A newly exposed reviewed carrier action is a distinct
opportunity; the Uncoiled `39177` to `39178` transformation is handled by that
same carrier rule.

Once CC has chosen an actor, identity, protection, death, targetability, range,
or line-of-sight drift retires that exact carrier exposure; the same held-key
episode cannot select an alternate. Wolves' Den is available only with the
separate testing option and only
for the exact current hard-target living, targetable native hostile duel opponent
or reviewed combat striking dummy with NameId `541`. Arbitrary NPCs and synthetic
`S1`/`<e1>` identities are rejected. Frontline and Rival Wings remain excluded.
This helper deliberately
does not participate in held-action cast cancellation. A client-accepted return
is bounded diagnostic feedback, not proof that the server applied damage.

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
exact living, targetable, non-self party member at or below 20% HP, or at or
below 35% HP only while a fresh exact incoming-pressure view shows at least
three unique enemies currently hard-targeting or casting at that ally. Unknown,
stale, malformed, or lower pressure cannot raise the original 20% boundary.
FFXIV's native 20-yalm action-range and line-of-sight check must also accept the target. There is
no custom center-distance cap: native reachability remains authoritative and
hitbox-aware. After the jump, Guardian's protection requires the Paladin to
remain within 10 yalms of the protected member. Both your own Guard and Guardian
must be available. Unconditional critical candidates always precede proactive
high-pressure candidates. Critical candidates retain lowest exact HP first;
proactive candidates rank by higher exact pressure, then lower HP, distance, and
stable party identity. One atomic pressure publication is used for the whole
selection and is revalidated only for the same frozen actor; there is no rerank
or alternate target during its retry. When the automatic
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
initiates another combat action, selects another ally, or falls back. If FFXIV
explicitly reports its text-command shell busy before any invocation, the same
frozen Quick Chat may be offered again only until its original 1.5-second
deadline; after one native invocation there is no retry. A locally issued command
is not proof that the party received Quick Chat or saw
both markers. The exact localized row-35 syntax, party display, marker pairing,
and cleanup behavior remain current-patch live-confirmation boundaries.

While your own Guard is active, Seiton Sense blocks all scheduled and automatic action requests,
including Purify, AST same-target healing, SAM counter-CC/Zantetsuken, NIN
Seiton, VPR Serpentiner Geist,
GNB Continuation, reactive counter-CC, Ally Rescue, Guardian, NIN Guard-Shukuchi,
SCH Critical Strategy, DRK Shadowbringer/Hiebsprung, held
Monk combo, Smart Recuperate, Emergency Teleport, Guard, pressure Sprint,
accepted-Eukrasia Kardia, and Monk Earth's Reply. The explicit manual
`/panicshu` and enabled `/seitonbw` commands are the sole exceptions: their one
immediate reviewed dash request is intentionally allowed to break own Guard.
The bounded reactive observer may retain an already eligible enemy
startup/Purify/Guard reservation, but it cannot dispatch it through own Guard.
Only a matching exact live Guard status can arm central cancellation ownership.
Auto-Guard does not dispatch unless both central `UseAction` and
`UseActionLocation` hooks are enabled. Their exact request observation and a
client `true` return remain provisional and block no action. If the status has
not appeared after 1.5 seconds, one retry may cross the native boundary only
when Guard readiness is proven and the original two-second post-Purify lease is
still alive; a clean rejection retracts the generation. After exact status
confirmation, the boundaries block metadata-resolved PvP `Action`/`PvPAction`
calls that can cancel Guard, including deferred or ground-location requests,
until the first exact status absence or hard cap. An incoming or resolved second
Guard press is also blocked during the first two seconds from confirmation. At
the exact two-second boundary it again passes as the deliberate release path and
atomically drops ownership. A manual Guard never arms it.
The dedicated exact command scope releases this ownership only for the matching
NIN location boundary or reviewed directional self-dash boundary, even if the
native action rejects it. Unsupported or unknown actions,
disabled/context/player/identity drift, missing propagation, classification
exceptions, and a hard six-second maximum fail open. Exact live client/server
ordering still needs in-game validation.

The **Reactive counter-CC** module is also default-off and runs only in exact CC
or explicitly enabled Wolves' Den testing. On WHM it
uses Wunder der Natur / Miracle of Nature `29228` at native 10-yalm range; on
BRD it uses Stumme Nocturne / Silent Nocturne `29395` at native 20-yalm range;
on NIN it resolves the PvP Spinning Edge/Aeolian Edge Combo carrier `29500` to
either Forked Raiju `29510` or Fleeting Raiju `29707` at native 20-yalm range.
Both Raiju metadata rows must verify before NIN can arm, and the carrier must
expose the exact variant before an action can be requested. Forked Raiju remains
blocked while the exact local Sealed Forked Raiju status `3195` is present; both
variants remain blocked through exact local Bind `1345`. Protection-end-only
options add PLD Intervene `29065`, RDM Resolution `41492`, exact Forte `41496`
to Vice of Thorns `41493`, and exact Soul Resonance `29662` to BLM Frost Star
`41481`. SAM Soten/Mineuchi uses its separate two-stage exact-target path.
WHM, BRD, and NIN can respond to the exact
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

After an exact plugin-owned request lands, the matching nonzero source sequence,
action, target, status, delay, and target-edge distance can contribute one
bounded timing sample. At most 24 delay/distance samples are kept per supported
action. Predictive release requires at least five valid samples at the current or
a nearer distance, including one eligible sample from the current runtime
session; until then the helper waits for authoritative protection absence. The
lead uses the fastest eligible observed landing with a fixed safety margin, then
revalidates that the same sole protection row, end time, actor, action, and range
still match immediately before the one native call. This cannot guarantee an
unseen faster server effect, so live validation remains required.

For true main-GCD counters—NIN Raiju, RDM Resolution, and BLM Frost Star—the
learned ideal request frame is also the start of one fixed late reservation. If
the main GCD is busy, only that exact action, actor, held-key generation, and
protection episode may fire on the first ready frame strictly before `1000 ms`.
The deadline never slides; waiting neither consumes the held input nor requests
cast cancellation. At or after `1000 ms` the opportunity is lost. oGCD counter
paths do not inherit this main-GCD late reservation.

If multiple exact post-Purify or post-Guard releases are simultaneously eligible,
an exact current key must first be acquired inside the original strict 500-ms
release edge. The best exact actor/key is then frozen once. Guard retires every
simultaneous loser before any higher-priority wait, so the lease cannot rerank or
fall through to an alternate. Native range, line of sight, casting, and the global
action queue remain dispatcher wait gates only until three seconds from the
original release; they no longer have to clear inside the 500-ms acquisition
edge. Among the candidates,
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

The current request order is **Purify > Smart Recuperate > automatic Guard > AST
same-target heal chain > RDM fresh-Guard engage > SAM staged counter-CC / Zantetsuken > NIN
Seiton > VPR Serpentiner Geist > GNB Continuation > reactive counter-CC > Ally
Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH Critical Strategy > DRK
Shadowbringer (Dark Arts) > DRK Hiebsprung > DRK Shadowbringer (safe fallback) >
Monk combo > Emergency Teleport > pressure
Sprint > event Kardia > event Monk**. The job-specific physical-hold helpers use
that deterministic order; recovery and automatic Guard run before the job helpers,
and reactive counter-CC remains before ally cleanse.
Kardia and Monk retain their separate
event-driven origins. At the reactive counter-CC stage, the chosen opportunity
freezes one exact-target intent;
there is no visible selected-target change,
alternate action/target, fallback, or replay. Plugin-owned Miracle, Silent
Nocturne, Raiju, Intervene, Resolution, Vice, and Frost Star requests still pass
through the final action-specific CC-immunity brake immediately before the native
call.

After a client-accepted request, a blue `AUTO CC LANDED` popup appears only if the bounded
ActionEffect observer captures the matching status on that exact pending enemy:
Miracle `3085`, Silence `1347`, Stun `1343`, or Deep Freeze `3219`, with the
exact action and `SourceSequence` created by the plugin request.
A manual use of the same action cannot claim the pending automatic result. A
local client-accepted request
does not count. Even an exact landed popup proves only that the counter-CC status
landed; it does not conclusively prove that Contradance, another limit break, or
its damage was interrupted. In particular, an instant LB already accepted by
the server can resolve before the reactive request arrives. All startup timing,
Purify/Resilience release
ordering, every counter profile's dispatch, and claimed interruption outcomes
remain explicit current-patch live-validation boundaries.

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
job-specific second tier, Smart Recuperate, Emergency Teleport, generic Guard, pressure Sprint, and
event Kardia. The helper
runs in Crystalline Conflict and in explicitly enabled Wolves' Den test mode;
the native direct-call result and exact timer behavior still need a live test.

## DPS Smart Tab native forward-target replacement

Smart Tab is a separate default-off target-writing option for exact Crystalline
Conflict and the reviewed melee and ranged DPS jobs. Toggle it in **Targets** or with:

```text
/smarttab [on|off|toggle]
```

`/sstarget` is the collision-free alias. When ON, paired native hooks scope the
game's own targeting handler and own only its nested forward world-target cycle,
after FFXIV has applied its logical binding and UI/input gates. The usual Tab key
and any remapped keyboard/gamepad binding for that command therefore use Smart
Tab. When OFF, reverse targeting such as Shift+Tab, direct helper calls outside
that targeting-handler scope, UI Tab navigation, chat input, other target
commands, unsupported jobs, and contexts outside exact CC call the native paths
unchanged.

One owned forward-target press resolves unique, living, hostile, targetable,
exact canonical `S1`-`S5` enemies and excludes live Guard. Geometric reach is
hitbox-edge distance: center distance minus both actors' hitbox radii, clamped
at zero. Every geometrically admitted candidate must additionally pass FFXIV's
native range/line-of-sight query through a metadata-verified hostile 25-yalm
probe; geometry still enforces each job's narrower authored cap. Melee jobs first use
the 5-yalm melee tier, then the reviewed gap cap: 20 yalms for MNK, DRG, NIN,
SAM, and VPR, or 15 yalms for RPR. Ranged jobs have no melee preference: BRD,
BLM, SMN, MCH, RDM, and PCT use one 25-yalm tier, while DNC uses one 15-yalm
tier. Tanks, healers, classes, limited jobs, unknown jobs, invalid geometry, and
enemies beyond the reviewed job cap fail closed.

Inside the first non-empty reach tier, ranking is lowest exact HP ratio, highest
fresh positive team pressure, verified Guard cooldown unavailable, lowest
trusted MP ratio, then stable native S-slot. Zero, unavailable, or stale pressure
is neutral. When the current hard target exactly belongs to that eligible ranked
list, the press selects its successor and wraps after the last actor. Otherwise
it starts at rank one. No persistent cursor is stored, so a manual target change
automatically re-anchors the next press. The selected actor's exact slot,
game-object ID, entity ID, address, and reach tier are frozen and revalidated,
including a second native line-of-sight check immediately before the setter.
Missing candidates or any identity, geometry, context, or reach failure consume only that owned
forward cycle without a target write, so the current hard target remains
unchanged. Otherwise Seiton Sense invokes the visible hard-target setter once
and verifies exact readback once. Setter rejection or readback mismatch is
terminal: there is no retry, rerank, or alternate candidate, and the plugin does
not claim that the pre-call target was restored. The native probe only asks for
spatial validity; Smart Tab never sends a combat action or keeps pending work.

## One-shot Smart Action macro

The previous harmful-action targeting helper is now the separately default-off
Smart Action option. `/smartaction` (`/ssaction`) arms one 750-ms Crystalline
Conflict token for the next already incoming harmful PvP macro action. Smart
Action replaces only that call's target ID with its selected Smart Target; it
never changes your visible hard, soft, Focus, or mouseover target. The
recommended authored shape is:

```text
/mlock
/smartaction
/pvpac "Ability" <e1>
/pvpac "Ability" <t>
```

No selected target is required. Arming performs no `S1`-`S5` scan and has no
hard-target or live-`S1` prerequisite; `<t>` is only the user-authored fallback
when the carrier is deliberately invalidated. At action time, Smart Action resolves
the actual non-ground-target hostile action and its native range/line-of-sight
result. It considers only unique, living, targetable exact canonical `S1`-`S5`
enemies. Active Chiten, Covered, Paladin LB Hallowed Ground, and Dark Knight LB
Undead Redemption make an actor protection-blocked. Active Guard does too unless
the exact resolved action's current English description explicitly says its
damage ignores Guard. This is action-specific, so transformed combo steps are
classified after adjustment rather than by the raw hotbar carrier or job. A
protected candidate is skipped and the next safe Smart Target remains eligible. If Chiten
metadata cannot be verified, every Samurai (and any unknown-job actor) is
conservatively excluded. Guard/Covered/LB protection rows use an independent
status-only metadata proof, so unrelated action-cost or recast changes do not
disable this safety path. Reach tier wins first: melee jobs prefer current five-yalm
melee reach, then a reviewed job-specific gap-closer tier; ranged/other jobs use
the general tier. Within a tier the order is lowest exact HP ratio, highest
positive fresh team pressure, observed Guard-cooldown unavailability, lowest
trusted MP ratio, then stable S-slot.

For a target-centered circle, protection safety includes the exact effect radius
plus each protected actor's hitbox. A verified Guard-ignoring action ignores only
Guard-only actors in that geometry; Chiten, Covered, and LB invulnerability still
block, including when combined with Guard. An unreviewed line, cone, or other AoE
shape is not redirected while any non-bypassed protected enemy exists. Direct
single-target actions need the selected actor's exact protection evidence but
do not require unrelated hostile object-table completeness; target-circle and
unknown area shapes retain that complete-world comparison. The one-shot token is
consumed before selection only while it is strictly live; an expired arm remains
on the vanilla path. The selected action/actor tuple is frozen and the
shape-appropriate protection proof is rebuilt immediately before the original call is
forwarded with the exact canonical enemy ID, never a mutable selected-target
carrier. Drift cancels that carrier without reranking, retrying, dispatching a
generated action, or changing the visible target. A short exact-action lease
keeps the authored `<t>` fallback under the same protection check after a native
rejection. Equivalent raw action carriers for the same resolved skill stay
inside the lease; an unresolved exact raw fallback is blocked. Unrelated actions
with a resolved identity are untouched. If resolution is unavailable and an
`Action`/`PvPAction` alias cannot be disproved, supported macro calls stay blocked
only through a fresh post-claim safety lease of at most 750 ms. Acceptance ends
the lease.
This is a final local pre-dispatch check: protection that appears only after a
queued/cast action was accepted, or during projectile/travel time, cannot be
recalled by the target-rewrite hook.
Smart Action has its own explicit opt-in; Near Assist, Near Help, and Far Help
continue to share their existing macro-helper option.

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

## Manual Panic Shukuchi and camera-back dash macros

These are explicit one-line macro commands, not automatic features or part of
the held-action scheduler. The NIN-only forward command is always available;
the multi-job camera-back command is a separate default-off Macro Helpers option:

```text
/panicshu
/seitonbw
```

Both run only in Crystalline Conflict, or in Wolves' Den when **Enable Wolves'
Den testing** is on; Frontline and Rival Wings stay excluded. `/panicshu` remains
exact PvP NIN and projects one terrain point 19.5 yalms along current character
facing. `/seitonbw` supports the closed current PvP self-dash set: NIN Shukuchi
`29513`, AST Epicycle `41506`, DNC En Avant `29430`, DRG Elusive Jump `29494`,
RPR Hell's Ingress `29550`, and PCT Smudge `39210`.

`/seitonbw` reads only the normal gameplay camera. On NIN it projects the exact
19.5-yalm ground point toward screen-back. On the other five jobs it sets only
local character facing immediately before the reviewed native self-action:
forward dashes face screen-back, while Elusive Jump faces the opposite way so
its native backstep still travels screen-back. A synchronous same-thread local-
facing boundary keeps that frozen direction from being replaced by a later
camera-relative dash hook during the single request. The camera never moves and
no hard, soft, Focus, or mouseover target is read, changed, or substituted. An
inactive command installs no active facing detour: the boundary is enabled only
on the first opted-in non-NIN `/seitonbw` attempt. The existing audited
ReAction/MOAction ownership check also runs immediately before that exact call;
camera-relative rotation alone is allowed, while Auto Target, Action Stacks, or
MOAction ownership of either action ID refuses the attempt. An
accepted or acceptance-ambiguous directional request keeps the authored facing
so an immediate restore cannot race movement startup; normal movement input can
then update facing. A proven clean client rejection restores the prior facing.

Each invocation makes at most one native call in the same command callback. It
requires exact startup-validated metadata, unchanged base action, available
charge/resources, standard camera state, and an immediately clean action
boundary. Transformed follow-ups such as Doton, Wyrmwind Thrust, Retrograde, or
Regress never substitute. Missing/non-finite, event/cutscene, spectator, aiming,
or lock-on camera data refuses the command. There is no stored intent, scheduler
claim, wait, lease, retry, alternate action, target search, shorter point, or
later replay. The explicit command may give up plugin-owned Auto-Guard for only
that exact same-thread action boundary. Routine results stay chat-silent and are
available through `/seiton debug`; exact live direction and acceptance remain
in-game validation points.

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
| Enemy LB nameplate icons plus self/ally LB notifications | Yes | No | No |
| Local 4,000/2,000-MP warning sounds | Yes | Yes, when test mode is enabled | No |
| Optional BRD/WHM Ally Rescue | Yes | No | No |
| Optional automatic/held Smart Recuperate | Yes | Yes, when test mode is enabled | No |
| Optional held Emergency Teleport (MNK/BLM/SGE/VPR) | Yes | Yes, when test mode is enabled | No |
| Optional reactive defensive utilities | Yes | No | No |
| Optional PLD Guardian job tool | Yes | No | No |
| Optional WHM/BRD/NIN/PLD/RDM/BLM/SAM reactive counter-CC | Yes | Yes, for the exact current hard target when test mode is enabled | No |
| Optional team-visible Attack1 focus sign | Yes | No | No |
| Optional local Auto Low-MP Focus Target | Yes | No | No |
| Optional MNK Earth's Reply | Yes | Yes, when test mode is enabled | No |
| Optional DRK Hiebsprung held-key helper | Yes | No | No |
| Seiton `S1`-`S5` decision cues | Yes | Synthetic visual `S1` | No |
| Optional NIN Guard-Shukuchi held-key helper | Yes | No | No |
| Optional NIN Seiton held-key helper | Yes | No | No |
| Optional AST held Near Help | Yes | Yes, for living self/party players when test mode is enabled | No |
| Optional SGE Smart Kardia after accepted Eukrasia | Yes | No | No |
| Optional VPR Serpentiner-Geist held-key helper | Yes | Yes, for the exact current hostile duel opponent or reviewed dummy when test mode is enabled | No |
| Optional GNB Continuation held-key helper | Yes | Yes, for the exact reviewed current target when test mode is enabled | No |
| Optional SAM Soten/Mineuchi and Zantetsuken held helpers | Yes | Yes, for the exact reviewed current target when test mode is enabled | No |
| Optional MNK held combo helper | Yes | Yes, for the exact reviewed current target when test mode is enabled | No |
| Manual NIN Panic Shukuchi macro | Yes | Yes, when test mode is enabled | No |
| Optional manual camera-back job dash macro (NIN/AST/DNC/DRG/RPR/PCT) | Yes | Yes, when test mode is enabled | No |
| Optional RDM fresh-Guard held-key engage | Yes | Yes, for the exact current target when test mode is enabled | No |
| Optional DRK Shadowbringer held-key helper | Yes, held Smart Action policy with one exact frozen actor | Yes, exact current duel/dummy target when test mode is enabled | No |
| Optional DPS Smart Tab | Yes | No | No |
| One-shot Smart Action macro | Yes | No | No |
| Near Assist | Yes | No | No |
| Near Help | Yes | No | No |
| Far Help | Yes | No | No |

Wolves' Den support is explicitly a test option. Both Panic Shukuchi commands
choose no enemy and use no target; their destination comes from the local NIN
position plus either exact character-facing-forward or normal-camera-screen-back
terrain geometry. Enemy visuals require one
strict native hostile duel opponent; missing or ambiguous identity shows
nothing. Held RDM, GNB, SAM, MNK, and DRK test paths accept only their exact
reviewed current-target route; Viper accepts the same exact current native hostile duel
opponent or the reviewed NameId-`541` striking dummy. AST uses only living,
targetable self/party players and never an enemy or striking dummy.
Pressure has an additional Wolves' Den opt-in so testing does not create an
always-on pressure display by surprise.

## Settings and schema migration

The sidebar order is Start, Alerts, HUD & Nameplates, Action Helpers, Job Tools,
Macro Helpers, Targets, and Diagnostics. Enemy LB nameplate controls live under
HUD & Nameplates; self/ally LB notifications and local MP sounds live under
Alerts. Reactive defensive utilities, Smart Recuperate, and Emergency Teleport
remain under Action Helpers; independent PLD Guardian, accepted-Eukrasia Smart
Kardia, and the Viper Serpentiner-Geist helper are under Job Tools, together
with the RDM fresh-Guard engage. Reset Defaults clears previews and restores
every action, target-
write, and party-visible communication master to off.

Configuration schema 47 is current. It adds default-on, read-only SMN/Chiten
danger warnings and separate experimental opponent LB bars that remain off by
default pending live layout validation, while retaining the separate default-off automatic
basic-shot cast-cancel permission without changing either automatic helper opt-in
or the independent generic held-helper cast-cancel toggle for an upgrade. Schema
45 added separate default-off automatic Purify and Recuperate options without
changing either legacy held opt-in. Schema 44 adds the default-off RDM fresh-
Guard engage with 80% HP / 50%
MP defaults and the separate default-off `/seitonbw` macro option, and initializes
the local CC map W/L capture toggle to on for fresh, upgraded, and Reset Defaults
configurations. Schema 43 adds
the default-on Blackblood-
preservation sub-option without enabling the default-off Auto Shadowbringer
master, and expands the local rotation panel with seven local-artwork cards.
Turning the sub-option off preserves the earlier Shadowbringer behavior. Schema
42 adds the visible-by-default local Wolves' Den rotation panel and PvP range
helper without changing any targeting or action setting. Schema 41 adds the
default-off AST held Near Help option without
enabling it for upgrades, fresh installs, or Reset Defaults.
Schema 40 integrates the generic one-shot smart action buffer and opt-in native
standard-keyboard-hotbar Turbo directly into
Seiton Sense. The buffer defaults to 1,000 ms, is adjustable from 100-1,500 ms,
and has no PvP-only gate; Turbo remains default-off and has a separate outside-
combat test option. Schema 39 adds the default-off 100-1500 ms PvP latency-
response budget and a legacy read-only external critical-utility coordination
endpoint; the integrated buffer and Turbo do not require another plugin. Schema 38
adds RDM Vice of Thorns and BLM Frost Star as default-off protection-end options and resets unversioned
impact-calibration evidence. GNB Continuation, DRK Shadowbringer, Monk combo,
SAM counter-CC/Zantetsuken, PLD Intervene, RDM Resolution, Vice of Thorns, and
Frost Star remain off for every upgrade, fresh install, and Reset Defaults. Schema 36
adds the local Auto-Guard card/sound defaults without enabling Auto-Guard itself.
The schema-35 migration initializes Emergency Teleport
off, with defaults of 50% HP, 4,000 MP, one direct focuser, 10-yalm minimum
travel, 10-yalm destination radius, and zero nearby enemies. The historical
schema-34 migration still leaves the Viper Serpentiner-Geist helper off, and the
historical schema-33 migration still leaves the target-
writing Smart Tab option off while preserving an older explicitly enabled shared
macro-helper opt-in as the separate Smart Action option. Smart Tab, Smart Action,
Viper, and Emergency Teleport are all off for fresh and
reset configurations; existing unrelated opt-ins are preserved.

The integrated buffer observes only a freshly certified physical press on one
of the ten standard keyboard hotbars. For a direct instant action which the
client rejects solely because its remaining local recast or animation lock is
inside the configured window, it freezes the exact post-Smart-Action target,
action, slot, player, territory, and instance. It then makes at most one later
request for that immutable tuple. A newer physical hotbar press, target or
identity drift, native queue/sequence progress, an unsafe context, a structural
or resource failure, expiry, or an ambiguous native result ends the intent.
Casts, ground-targeted actions, movement actions, macros, mouse clicks, and
cross-hotbar/controller input are not buffered.

The one-shot buffer also fails closed around other action-mutating plugins.
ReAction is admitted only for the audited `1.3.5.1` profile with Auto Target
and Action Stacks inactive; MOAction `4.10.1.0` is admitted only when its
published ownership list proves that neither the requested nor resolved action
is retargeted. Unknown versions, unreadable state, ownership changes, or a
plugin-topology change cancel or quarantine only the generic buffer. Native
input and Seiton's separate Turbo path remain available. Compatibility is
assessed in memory on plugin-change events and at a bounded five-second cadence,
with one final live check when the buffer arms and when it is actually ready to
replay; Seiton does not scan plugin files.

Native Turbo is a separate default-off path. While its exact certified key is
held, it repeats only the same current standard-hotbar slot at the configured
cadence; the newest physical hotbar input owns repetition, there are no catch-up
bursts, and disabling or reconfiguring Turbo requires release before a new hold
can begin. Enabling the outside-combat test scope also starts a new lifecycle,
so a key which was already held cannot be inherited. Each due cadence is exposed
to FFXIV's native hotbar scanner as that binding's pressed result. The game remains
responsible for consuming and executing the slot, so ordinary actions and macros
keep their native slot semantics; if the scanner does not consume the exact slot,
Seiton records only a diagnostic miss and makes no post-scan slot or action call.
Seiton's critical held utilities pause Turbo and final buffered dispatch without
creating a competing queue. Neither feature writes position, range, animation
lock, cast state, or a visible target. The movable learning panel is seeded by
the certified physical direct-slot press, not by repeat pulses, and shows its
key/slot, resolved action, live one-shot countdown, and held/released state.

Historical v0.30.0.0 baseline: schema 32 disabled the retired Combat Frames
master and mapped its optional name-display preference to the ally LB feed,
while enabling the replacement enemy-nameplate, self-banner, ally-feed, and
local-MP sound defaults. Legacy Combat Frames layout/interaction
fields remain compatibility-only and have no runtime or Settings page. The NIN Guard-Shukuchi held
option is forced off for upgrading configurations and remains off for fresh and
Reset Defaults configurations because it both initiates an action and may change
the exact hard target after client acceptance. `/panicshu` remains command-only
and uses the existing global plugin enable plus the existing Wolves' Den testing
option; schema 44 adds the separate default-off `/seitonbw` command toggle, now
shared by the closed NIN/AST/DNC/DRG/RPR/PCT camera-back dash catalog. The generic
held-action cast-cancellation test and schema-46 automatic basic-shot permission
are both explicitly off for fresh, reset, and migrated configurations. An older
explicitly enabled NIN fresh-edge helper still traverses
schema 29 and migrates to the replacement held-key option; the obsolete
compatibility field is then cleared. Every other existing master and helper
choice is preserved. Older configurations still traverse the earlier migrations
first, including schema 28's default-off post-Guard migration. Fresh and reset
configurations keep every action-helper master off; post-Guard defaults on only
behind the disabled reactive-counter master. The What's New acknowledgement
saves only the release version after the user closes the window or presses
**Got it**.

## Install

Add this custom repository in Dalamud:

```text
https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/main/repo.json
```

Then search for **Seiton Sense** in the Plugin Installer. Existing installs
update through the same repository.

## Commands

- `/seiton` or `/ssense` - open settings
- `/smarttab [on|off|toggle]` - control the exact-CC DPS replacement for
  FFXIV's normal forward world-target cycle
- `/sstarget [on|off|toggle]` - collision-free alias for `/smarttab`
- `/smartaction` - arm one optional CC-only 750 ms harmful-action target redirect;
  the authored `<t>` line remains the only fallback
- `/ssaction` - collision-free alias for `/smartaction`
- `/autoseiton [on|off|toggle]` - change whether the held-key NIN Auto-Seiton
  helper is available; ON still requires continuous physical held-key consent
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
- `/panicshu` - on exact PvP NIN, immediately make one Shukuchi attempt at the
  terrain point 19.5 yalms straight ahead in CC or enabled Wolves' Den testing,
  including from own Guard
- `/seitonbw` - when its default-off Macro Helpers option is enabled, make one
  immediate camera-back self-dash on NIN, AST, DNC, DRG, RPR, or PCT, without
  moving the camera or changing target
- `/seiton show` / `/seiton hide` - enable or disable the entire plugin
- `/seiton preview` - preview nameplate indicators
- `/seiton flash` - preview the Seiton popup
- `/seiton debug` - print bounded diagnostics, including Smart Tab, Smart Action,
  and Near Assist,
  selected-target CC-brake resolution, isolation/reactive-defense state, Smart
  Recuperate, accepted-Eukrasia Smart Kardia, LB activation/damage telemetry,
  Auto Low-MP Focus, NIN Guard-Shukuchi, RDM fresh-Guard engage, DRK Hiebsprung/
  Shadowbringer, forward Panic Shukuchi and camera-back job dash,
  retained reactive counter-CC opportunity results, and cast-cancel held/automatic
  switches, current job/cast/adjusted identity, metadata proof, request, and epoch state
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
or key history. Optional ally LB feed names are resolved only from the current
exact actors for drawing and are never persisted or uploaded. The separate default-off
Guardian communication uses ordinary FFXIV
Quick Chat and marker commands, so enabling it creates the described party-
visible in-game side effect through FFXIV. Transient observations and the exact
one-shot action boundary are documented in [PRIVACY.md](PRIVACY.md).

Display-only features such as the resource aura and LB cues never target, press
actions, accept clicks, calibrate/estimate a remote gauge, or mutate native UI.
The experimental opponent-LB strip is off by default pending live layout
validation and retains only fresh current/max values after exact stable native-
row and local-controller proof when enabled. The retired
Combat Frames have no click, hard-target, or native `<mo>` runtime path. Auto
Low-MP Focus is a separate explicit setter. Held DRK Shadowbringer is a default-
off participant in the shared physical-input scheduler and is bounded as
described above. Panic Shukuchi instead has no automatic or held-key trigger:
only a user-authored `/panicshu` command can make its one immediate forward
attempt, while the default-off `/seitonbw` toggle permits one explicit reviewed
camera-back self-dash on NIN, AST, DNC, DRG, RPR, or PCT. For one already incoming,
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
input and never chooses another target or action. Smart Action, Near Assist,
Near Help, and Far Help can each replace only the target ID of one explicitly
armed, already incoming macro action. Smart Tab is separate: while enabled it
may replace only an owned native forward world-target cycle with one exact hard-
target write. Near Help may choose the local player only when the exact resolved action
supports self and passes native target/range/line-of-sight validation. Optional
action helpers use this current request priority: **Purify > Smart Recuperate >
automatic Guard > AST same-target heal chain > RDM fresh-Guard engage > SAM staged counter-
CC / Zantetsuken > NIN Seiton > VPR Serpentiner Geist > GNB Continuation >
reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH
Critical Strategy > DRK Shadowbringer (Dark Arts) > DRK Hiebsprung > DRK
Shadowbringer (safe fallback) > Monk combo > Emergency Teleport > pressure Sprint
> event Kardia > event Monk**. The
job-specific physical-hold helpers use that deterministic urgency order;
reactive counter-CC still leads ally cleanse. Kardia requires its
separate accepted-Eukrasia trigger and does not originate from the physical key;
Monk is an automatic follow-up.
The same continuous hold can authorize later distinct exact held episodes; a
post-Purify reactive Guard no longer requires release/repress. There is no HP/
pressure pre-Guard. While your own Guard is active, all scheduled/automatic
Seiton action-request helpers are blocked; the
explicit manual `/panicshu` and enabled `/seitonbw` commands are the sole
exceptions. During the provisional observation interval after an exact plugin
Auto-Guard request, the short latch still blocks lower helpers, but Purify and
Recuperate yield only to exact live Guard so a failed request cannot suppress
higher-priority recovery. The central `UseAction` and `UseActionLocation` hooks
arm cancellation ownership
only after the matching exact live Guard status, block a second Guard press
during the first two seconds from that confirmation, restore deliberate Guard
reuse afterward, and fail open on clean rejection, drift, or their six-second
cap. Auto-Guard cannot dispatch if either protection hook is unavailable;
the dedicated exact command scope releases ownership only for the matching NIN
location request or reviewed directional self-action request.
Manual Guard never creates this ownership. Ally Rescue labels a
removal `CLEANSED` only after the exact successful status-removal ActionEffect is
observed; attempts and client-accepted requests alone are not success claims.

For the twenty shared physical-hold option trackers, key choice prefers stable movement, then any
other stable held gameplay key, then fresh movement and fresh other gameplay
keys as fallbacks. Each helper evaluates its held lease before fresh input and
retains the exact frozen key until its normal release, ineligibility, reset, or
terminal action-specific boundary. This prevents a later short action-key tap
from displacing a valid long-held WASD lease.

The separate **Cancel my active cast for an otherwise-ready held helper** test
is disabled by default. It applies only to exact frozen physical-hold intents
for Purify, AST same-target Orbis, RDM fresh-Guard engage, SAM counter-CC/Zantetsuken, NIN Seiton, reactive counter-CC, Ally
Rescue, Guardian, NIN Guard-Shukuchi, SCH Critical Strategy, DRK Shadowbringer,
DRK Hiebsprung, Smart Recuperate, Emergency Teleport, Guard, and pressure
Sprint. Smart
Kardia and Monk Earth's Reply are excluded because they do not originate from
held input; every already-incoming manual/Turbo redirect, including Paean, and
all macro helpers are excluded as well. Viper Serpentiner Geist is excluded
because it polls only the currently transformed carrier. GNB Continuation and
held Monk combo likewise wait for a clear cast instead of cancelling it. The cast-cancellation
experiment therefore constructs sixteen reviewed request shapes across seventeen
ordered selection slots; held Shadowbringer occupies separate Dark Arts and safe-
fallback positions through the same exact request adapter.

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
refuse to cancel some actions. Generic held-helper stationary casts and the
separately permitted automatic BRD/MCH mobile casts still require current-patch
live validation. The ordinary
clean-`false` action retry remains independent: calls stay at least 50 ms apart.
Its legacy default is eight attempts; the separate default-off PvP latency-
response option freezes the selected extended budget per exact intent, and
acceptance or ambiguity remains terminal.

Automatic Purify and Automatic Recuperate never inherit the generic held-helper
cast-cancel toggle. Their sole cancellation route is the separate schema-46
default-off permission. The relevant keyless Purify `29056` or Recuperate `29711`
intent must already be otherwise ready, and the local player must be either exact
BRD job `23` casting Powerful Shot `29391` or exact MCH job `31` casting Blast
Charge `29402`. Startup English PvP metadata for that exact pair must verify, and
the active cast ID plus the adjusted raw-action identity must still equal the
same allowed row. Cross-job pairs, missing or drifted metadata, changed cast
signals, instant transformations such as MCH Blazing Shot `41468`, and every
other uncertainty wait for a natural cast end. One cancellation owns that frame;
the automatic helper can act only after its full action-specific preflight passes
again on a later clear-cast frame.

The separate default-off automatic and held Smart Recuperate modes may freeze one
shared exact self Recuperate `29711` epoch when missing HP is at least 16,000 and
MP is at least 2,000. They run in exact CC or explicitly enabled Wolves' Den
testing and freeze that supported context with the intent. Automatic wins if
both modes are enabled; it needs no gameplay key and does not retire the held
generation. Readiness, a cast outside the exact automatic allowlist, or
insufficient MP waits without starving a currently usable lower helper. Only an
explicit client rejection may use the
common bounded same-intent retry; acceptance, ambiguity, or context drift is
terminal with no target change, alternate, or replay.

The separate default-off Ninja helper is Ninja's earliest held job helper after
Purify; Viper's Serpentiner-Geist helper occupies the equivalent job-exclusive
slot on VPR. AST and SAM occupy earlier documented scheduler positions only on
their own jobs, so these mutually exclusive job lanes cannot coexist at runtime. A
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
HP or lower within its strict 10-yalm cap and native reachability. It runs between
Dark Arts Shadowbringer and the safe Shadowbringer fallback; held Monk combo then
closes the job-specific second tier before Smart Recuperate, Emergency Teleport,
and the generic helpers. A
continuous hold repeats only after an observed
cooldown not-ready-to-ready transition, never from a guessed reset, and it has
no selected-target mutation, alternate, rerank, or replay. A clean explicit
client rejection may use only the shared bounded retry for that same frozen
ready epoch.

The separate default-off held DRK Shadowbringer helper uses that physical-input
priority chain. Exact Dark Arts runs before Hiebsprung; the configurable safe HP-
cost fallback runs after it. The default-on Blackblood-preservation sub-option
blocks both paths while exact status `3033` exists. Both paths share a fixed
1.8-second cadence. Continuous safe HP/pressure opens exactly one new fallback
generation when the cadence ends; Dark Arts still wins and ignores the fallback
sliders. Stable Blackblood absence, whether consumed or expired, opens the next
eligible cycle. If a confirmed or ambiguous automatic request's whole short
Blackblood lifecycle occurs between framework samples, a 1.5-second propagation
grace plus one later absent sample prevents a permanent manual-only unlock;
ambiguous calls still latch their physical key until release. Disabling the
sub-option removes only the Blackblood wait. In exact CC, selection uses the existing held Smart Action policy
without requiring its macro toggle. The helper freezes one exact actor, leaves
the visible target unchanged, and cancels rather than reranking if that actor
becomes unsafe; line-AoE protection remains fail-closed. Wolves' Den uses only
the exact current `<t>` duel opponent or striking dummy and treats missing CC
pressure telemetry as known zero for testing while retaining the remaining gates.

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
retry after a native command invocation. A pre-invocation shell-busy result may
re-offer only the same frozen Quick Chat within its original 1.5-second deadline.

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
party-row / current CC-row aura anchoring, enemy LB icon placement, self/ally LB
notification layout, pressure evidence, MP/native-sound timing, optional action
helpers, and the macro helpers with both normal macros and Turbo Hotbar should be
rechecked in the relevant live PvP context after FFXIV, Dalamud, macro, network-
event, or input-handling changes.

For the current source, the exact 562-test Core registry and source checks pin
configuration schema 47, the independent default-off automatic basic-shot
cast-cancel permission, exact BRD/MCH job/cast/adjusted identity and metadata,
automatic/keyless and legacy held Purify/Recuperate intent boundaries, the
deterministic local CC rotation and fail-closed
per-character map W/L capture, the complete
fail-closed 21-PvP-job range catalog, the default-off AST held Near Help sequence, the
generic smart buffer and default-off native Turbo,
the default-off PvP latency-response/coordination path,
ranged Smart Tab, Wolves' Den Smart Recuperate testing,
the default-off Viper, GNB, DRK Shadowbringer, Monk combo, SAM, PLD, RDM, and BLM
paths and Emergency Teleport. Smart Tab checks retain the paired
targeting-handler/helper scope, native binding and UI/input gates, forward-only
ownership, metadata-verified native range/line-of-sight admission, a stateless
current-target-anchored ranked cycle with wrap, complete actor freeze, one
hard-target setter/readback, and no retry or alternate. They additionally pin one 25-yalm tier for BRD/BLM/SMN/MCH/RDM/PCT,
one 15-yalm tier for DNC, and the absence of a melee preference for ranged jobs.
OFF, reverse targeting, and calls outside the scoped handler retain their native
paths. Smart Action remains a separate one-shot harmful-action macro contract.
Its checks now pin target-independent arming, selection with `S1` absent,
shape-scoped caller-proven target protection safety, exact Chiten,
Guard, Covered, Hallowed Ground, and Undead Redemption handling, an exact
resolved-action English metadata gate for Guard-ignoring damage, conservative
target-centered-circle/unknown-AoE complete geometry, direct-target protection
without unrelated object-table completeness, a frozen canonical target ID for the sole
native action call, and a bounded exact-action fallback lease.

The same current checks pin Smart Recuperate's exact CC-or-enabled-Den context
freeze and no cross-context drift. Viper checks pin carrier `39183`, follow-ups
`39174`-`39182`, their 5/20-yalm ranges, direct per-frame carrier exposure without
preceding-action or queue provenance, one exact current-hard-target/action/context/
territory/key intent, shared clean-false retry, and both exact-current-target Den
paths: the native hostile duel opponent or reviewed NameId-`541` dummy. They also
retain the historical default-off schema-34 migration and the deliberate
absence of Viper cast cancellation. These checks validate
source control flow and contracts, not current-client targeting, action
acceptance, range/line-of-sight behavior, or server effects; live exact-CC and
enabled-Den testing remains required.

Current scheduler verification uses **Purify > Smart Recuperate > automatic Guard > AST same-target heal chain > RDM fresh-Guard engage > SAM staged counter-CC /
Zantetsuken > NIN Seiton > VPR Serpentiner Geist > GNB Continuation > reactive
counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH Critical
Strategy > DRK Shadowbringer (Dark Arts) > DRK Hiebsprung > DRK Shadowbringer
(safe fallback) > Monk combo > Emergency Teleport > pressure Sprint > event Kardia
> event Monk**. Twenty physical-hold
option enable edges share the scheduler input. Held-action cast cancellation
constructs sixteen reviewed request shapes across seventeen ordered selection
slots and explicitly excludes Viper, GNB, and held Monk combo.

Emergency tests pin MNK/BLM/SGE/VPR action mappings, strict HP/MP/direct-focus
edges, safety-before-distance ranking, target-specific native action status,
complete enemy geometry, exact actor/key/context freeze, final-preflight
retirement, frame consumption only after final commit, and one committed native
request with no fallback or retry. These are source/build assertions, not live
proof that a current server accepted the movement action.

Historical v0.30.0.0 baseline: the exact 388-test Core registry and source checks
pinned Smart Target's reach-first deterministic ranking, complete exact actor/action freeze,
native range/line-of-sight revalidation, live-Guard exclusion, one-shot carrier
consumption, and `<t>`-only authored fallback. They pin `/autoseiton` plus the
visible tile to one persisted availability switch while retaining physical held-
key consent, and the runtime/cast-cancel order begins **Purify > NIN Seiton >
other held helpers**. They also cover independent 4,000/2,000-MP edges and
hysteresis, critical-only direct double-crossing sound, exact enemy LB nameplate
admission/stacking, safe self/ally LB notification lanes, and version-local
What's New acknowledgement without chat output.

The same historical v0.30.0.0 checks require schema 32, version 0.30.0.0, the
unhidden custom-repository entry (`IsHide: false`), and the absence of the deleted Combat Frames runtime, Settings
page, targeting/mouseover service, renderer, snapshots, and calibrated-gauge
service. Guardian retains unconditional rescue at or below 20% HP and admits
21-35% only from a fresh exact 3+ pressure publication. Protection-end follow-
ups freeze at authoritative release and use one shared non-extending three-
second lease for range, line-of-sight, cast, queue, and animation waits. They
also pin Auto-Guard cancellation ownership to an exact client-accepted plugin
Guard, both central native action hooks, a 1.5-second propagation bridge, full
live-status follow-through, Guard-reuse and scoped Panic-command release, fail-
open drift, and a hard six-second cap. These
checks do not prove current-client rendering, sound playback, macro timing,
native action acceptance, or server effects.

For v0.29.0.1, source checks pin the Auto-Seiton blocker catalog to the exact
target-side Covered rows plus Paladin Hallowed Ground and Dark Knight Undead
Redemption. They also pin the checks before candidate ranking, frozen retry,
cast cancellation, and the sole final `UseAction` boundary, including terminal
retirement of a protection-drifted Unsealed epoch and cue suppression. These
checks do not prove the live server application frame; Guardian, Phalanx,
Eventide, expiry, and Guard-only behavior remain current-patch in-game A/B
boundaries.

For v0.29.0.0, source checks pin the new default-off NIN Guard-Shukuchi held
helper to exact Crystalline Conflict, exact PvP NIN, one living and targetable
canonical `S1`-`S5` enemy strictly below 20% HP, and a finite positive live
Guard / Wehr status `3054` or `3673`. Missing unrelated enemy slots and absent,
zero, unknown, or stale pressure do not block; only fresh positive pressure is a
ranking bonus before exact HP and stable identity. The selected actor is frozen,
revalidated without reranking, and used as the sole Shukuchi destination inside
the native three-dimensional 20-yalm range. Checks pin one reviewed
`UseActionLocation` boundary, the Three Mudra/Doton block, same-actor bounded
explicit-false retries, and a single hard-target write only after
`ClientAccepted`. Own Guard blocks this automatic path, while the explicit
manual `/panicshu` and enabled `/seitonbw` commands remain the sole own-Guard
exceptions. A continuing hold needs a
proven cooldown-unavailable to ready transition before another accepted jump.
These checks cannot prove current-client terrain, line of sight, movement, or
hard-target timing; those remain live Crystalline Conflict validation points.

For v0.28.0.1, source checks pin `/panicshu` as an explicit NIN-only command with
one exact 19.5-yalm forward terrain projection and at most one immediate native
location-action call in the command callback. They prove the absence of pending,
lease, wait, expiry, Guard/Purify/crowd-control, cast, queue, animation-lock,
cooldown/resource, automatic, held-key, and retry inputs. The checks also
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
the current priority **Purify > NIN Seiton > reactive counter-CC > Ally Rescue > PLD
Guardian > NIN Guard-Shukuchi > SCH Critical Strategy > DRK Hiebsprung > Smart
Recuperate > generic Guard > pressure Sprint > event Kardia > event Monk**. The seven job-specific
physical-hold helpers occupy the second tier in that deterministic urgency order;
Kardia and Monk retain their separate event-driven origins.

The same checks pin stable-movement, stable-other, fresh-movement, then fresh-
other key selection and held-lease-before-fresh behavior for all eleven physical-
hold helpers. Cast-cancel checks cover the separate schema-30 generic held toggle,
the independent schema-46 automatic permission, the exact BRD `23` / Powerful
Shot `29391` and MCH `31` / Blast Charge `29402` metadata-verified allowlist,
job/cast/adjusted identity matching, explicit transformed-action exclusions, one
native cancel request per observed cast, void/requested-only diagnostics, no
same-frame helper action, fully revalidated later dispatch, and the absence of
movement/Escape synthesis, queue clearing, cast-state writes, or target mutation.
They cannot prove that FFXIV canceled a live cast; current-patch stationary and
automatic mobile BRD/MCH tests remain required.

The same release checks bounded current-key attachment inside an urgent startup's
original threat lease; exact Purify/Guard actor/action/episode memory without an
event-edge key; strict current-key acquisition inside the original 500-ms
protection-end edge; one frozen actor/key intent that may wait only until the
shared three-second deadline measured from the original release; Guard loser
retirement before a higher-priority wait; and strict generation retirement on
release or text input after binding. It also checks exact
self-target Purify sentinel capture with mandatory live Resilience, bounded
non-extending duration hints with mandatory live status absence, immediate
expected-end Resilience release, early Guard cancellation, ranking before
range/line-of-sight dispatch gates, the shared three-second protection-end lease, exact
source-sequence confirmation, and a terminal Guard tombstone through ambiguous
observations. It also checks Purify's absolute active-CC priority, Smart Recuperate's
inclusive 16,000-missing-HP/2,000-MP gates, Ally Rescue confirmation preservation,
all reviewed WHM/BRD/NIN reactive trigger families, both fully verified Raiju
variants with exact Stun confirmation, accepted-only Guard propagation,
Guardian, pressure Sprint, SCH, DRK cooldown epochs, and NIN base/follow-up as
separate adjusted-action epochs. It also retains Smart Kardia's accepted-
Eukrasia trigger, causal charge/status evidence, exact pressure selection and
direct target without a held-key scanner.

The historical v0.30.0.0 checks prove that the deleted Combat Frames snapshot,
targeting, renderer, options, settings-page, and remote-gauge services stay absent from the
runtime. Replacement LB checks pin fresh exact canonical enemies, native
nameplate anchoring, confirmed live-duration countdowns only, bounded
instant/unconfirmed flashes, safe stacking with the CC emblem, a local self
activation banner, and up to three directly attributable ally damage cards
without HP-delta inference. Local MP checks pin trusted exact 10,000-MP identity,
independent 4,000/2,000 downward-crossing hysteresis, critical-only direct double
crossings, and local built-in sounds. Smart Target checks pin one consumed macro
token, exact non-ground-target PvP action identity, complete unique canonical
eligibility, Chiten/Guard/Covered/PLD-LB/DRK-LB exclusion, protected-AoE geometry,
reach-tier-first ranking, exact target-ID replacement, one frozen actor/action,
the same-action fallback lease, final revalidation, and no retry, hard-target
write, or plugin-created action.
Auto-Seiton checks pin the NIN-only action-bar tile, persisted explicit opt-in,
and the still-required physical held-key consent. What's New checks pin the local
`0.30.0.0` acknowledgement without chat or network output. Configuration checks
pin schema 32 migration, fresh/reset defaults, and default-off action/
communication masters. Hiebsprung checks cover exact DRK/CC context, inclusive
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
job-specific native reachability. Configuration checks pin schema 32, the new LB
and local-MP defaults, the Guard-Shukuchi explicit-off migration, the existing
cast-cancellation explicit-off migration, and the prior NIN opt-in migration to
held consent.
They cannot prove live Eukrasia hook ordering, MP-tick and held-input timing,
native action acceptance/effects, current client range/line of sight, native
nameplate placement, native status/resource telemetry, reactive RemainingTime
hints, LB packet timing, local system-sound audibility,
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
Wolves' Den opt-in, exact hard-target identity as either the native hostile duel
opponent or reviewed NameId-`541` striking dummy, the absence of a synthetic
enemy-slot or arbitrary-target fallback, exact current
combo secondary-cost metadata, and main-thread framework-update cycle priming.
It does not turn a successful Den target trace into proof of live CC behavior;
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
