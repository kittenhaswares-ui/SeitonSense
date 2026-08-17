# Seiton Sense

Seiton Sense is a local PvP awareness HUD that combines pressure tracking,
stable native-nameplate cues, personal warnings, Ninja Seiton decisions,
one-shot macro assistance, and target highlights. Version 0.18.0.0 adds two
separate default-off exact Crystalline Conflict helpers: a set-only local native
Focus Target for a trusted reachable `S1`-`S5` enemy at 2,000 MP or lower, and
an exact two-line DRK/ReAction macro that may weave Shadowbringer once in a
proven Souleater Combo GCD window.
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
- **Optional pressure Sprint:** a separate default-off option may use one held
  WASD/arrow movement-key generation for one exact self Sprint attempt while
  the same direct-enemy count is at least three. The movement key still reaches
  FFXIV. It shares the existing single-action input boundary and never
  substitutes another action or retries; any later native PvP action ends Sprint.
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
- **Ninja Seiton decisions:** persistent job-icon cards, `S1`-`S5`, preparation
  cues, and entry pulses use FFXIV's native CC enemy order and verified
  range/line-of-sight checks.
- **Experimental Ninja Seiton helper:** a separate default-off option can use
  one fresh physical gameplay-key down edge for one Seiton attempt. It selects
  the lowest exact HP ratio among canonical `S1`-`S5` enemies that are strictly
  below 50% and natively reachable. Exact CC context, Ninja job, adjusted action
  readiness, own-Guard safety, and the shared higher-priority helper boundary
  all fail closed.
- **Experimental Scholar Critical Strategy helper:** a separate default-off
  held-key option selects only among the complete canonical `S1`-`S5` enemies
  with live Guard. Fully trusted positive team pressure ranks first, otherwise
  exact HP does; every target still requires native 25-yalm range/line of sight.
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
- **DRK Shadowbringer macro:** the separate default-off `/seitonbringer` helper
  pairs only with the immediately following authored Souleater Combo `<t>` line
  on exact PvP Dark Knight in Crystalline Conflict. With ReAction Macro Queue
  and Turbo, it may attempt Shadowbringer at most once per proven 2.40-second
  GCD, and only at 0.60-0.80 seconds remaining. It never changes a target,
  substitutes an action, or retries.
- **Experimental Ally Rescue:** on BRD or WHM, one fresh or explicitly eligible
  held gameplay-key generation can attempt Paean or Aquaveil on an exact party
  member suffering Stun, Silence, Deep Freeze, or Miracle of Nature. Selection
  uses HP, incoming pressure, trusted MP, and distance in that order. A matching
  successful status-removal effect produces a blue `CLEANSED` popup and feeds
  resettable, in-memory match/session counters.
- **Smart Bard Paean target:** a separate default-off exact-CC option examines
  only an already incoming manual or Turbo Warden's Paean call. It
  may redirect that call to an exact reachable non-self party ally with trusted
  incoming pressure from at least three unique enemies. No initial candidate
  preserves vanilla; drift after one exact redirect is frozen suppresses only
  that call. It never initiates an action or selects an alternate.
- **Experimental defensive utilities:** the default-off CC helper can use one
  physical input generation for a high-pressure Stun Purify, a later-generation
  Guard after positive Resilience, a low-HP pre-Guard, or PLD Guardian on an
  exact critically low ally. An active own Guard, plus the bounded 1.5-second
  status-propagation window after an exact Guard request, blocks all plugin action
  helpers. A separate default-off option can communicate only a client-accepted
  automatic Guardian with localized CC Quick Chat row 35 and an ownership-safe
  Bind2-ally/Bind1-self pair.
- **Experimental reactive counter-CC:** the default-off helper uses WHM Miracle
  of Nature or BRD Silent Nocturne on an exact DNC Contradance startup. It can
  also follow any of the six exact Purify-removable enemy statuses only after
  real Resilience ends and exact team focus reaches two. Existing urgent
  MCH/SAM/VPR startup paths remain WHM-only.
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
  Shared-input actions are shown in their real priority order, while visual,
  macro, and job-specific controls stay in their own pages. Configuration schema
  24 keeps Auto Low-MP Focus, the DRK macro, pressure Sprint, its native system
  sound, the Bard Paean pressure redirect, Guardian team communication, and
  Scholar Critical Strategy as separate default-off options;
  every action-attempt, target-redirect, and party-visible communication feature
  remains opt-in.

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
verified protection matrix. Standard Purify-removable CC and Miracle of Nature
have separate blocker sets, including exact relevant ward statuses rather than
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
- BRD: Silent Nocturne `29395` and Repelling Shot `29399`;
- WHM: Miracle of Nature `29228`;
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

The separate **Seiton on fresh gameplay key** experiment is disabled by default
and runs only in exact Crystalline Conflict on PvP Ninja. One genuinely fresh
physical gameplay-key down edge considers the exact canonical `S1`-`S5` enemy
actors; a key that was already held before the opportunity is not a trigger.
Every candidate must remain living, targetable, hostile, strictly below 50% HP,
and accepted by FFXIV's native action range and line-of-sight result. The lowest
exact HP ratio wins, followed by stable S-slot and actor-identity tie-breaks.
The current adjusted action must be the ready base Seiton Tenchu `29515` or its
verified Unsealed follow-up `29516`.

Self-Purify, defensive utilities, pressure Sprint, Ally Rescue, and reactive
counter-CC retain their existing higher priority over the same physical
generation. Active own
Guard and the bounded post-request Guard-propagation gate suppress the Ninja
helper. Once the exact intent is claimed, its state and input generation are
consumed before at most one native action request. A readiness, identity,
health, or reachability race; a false return; or an exception produces no
second selection, alternate target, fallback action, replay, or retry. The
same frozen S-slot and actor identity are resolved again at the latest safe
point immediately before the native request, and that exact actor's HP is read
again. Exactly 50% or higher cancels the spent attempt. This minimizes wasted
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

The exact shared priority before Scholar is Self-Purify, defensive utilities,
pressure Sprint, Ally Rescue, reactive counter-CC, and Ninja Seiton. One shared
held physical gameplay-key generation can produce at most one frozen
Critical Strategy intent. The intent and generation are consumed before one
native attempt, then the frozen enemy is revalidated only for exact identity,
action readiness, live Guard, and native range/line of sight. Pressure drift
neither reranks nor switches or invalidates the frozen target. The helper never
mutates a hard, soft, focus, or mouseover target, makes a second selection,
chooses an alternate target/action, falls back, replays, or retries, and it does
not swallow the original key. A client-accepted return is dispatch feedback only;
it does not prove that Critical Strategy landed or changed Guard. Exact current-
patch held-input timing, dispatch, and effect behavior require a live CC test.

## Personal warnings and job quality-of-life helpers

Wildfire and Death Warrant receive danger warnings. Marksman's Spite uses its
exact early target-marker event to show the larger `MCH LIMIT BREAK ON YOU`
card before the later damage event. The optional sound uses FFXIV's built-in
sound effects, plays at most once for a verified threat, and has a test button.
It never presses Guard or another action.

Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature receive urgent
Purify warnings. The experimental **Self-Purify physical-key helper** is disabled
by default. Each debuff type has its own automation toggle, and a separate
default-off option allows an already-held gameplay key such as WASD to trigger
once when the enabled debuff appears.

The original key is never swallowed, delayed, or replayed. One physical
press/hold generation permits at most one normal native Purify attempt and is
consumed before dispatch. ReAction Turbo repeat pulses do not create new
physical key generations. A failed or rejected attempt is never retried; FFXIV
still decides whether the action can queue or execute.

The separate **Ally Rescue on next gameplay key** experiment is also disabled
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
local cooldown-ready sample. After the exact ally, status, action, native range,
and line of sight are revalidated, FFXIV receives one normal action request and
decides whether it can queue or execute.

Self-Purify, defensive utilities, and pressure Sprint receive the shared
physical generation before Ally Rescue. One input generation can therefore
produce at most one helper attempt, and Ally Rescue stores its spent state
before the exact Paean/Aquaveil call with no retry. The BRD metadata check
also accepts the current lowercase leading article in `the Warden's Paean`;
numeric action identity still drives runtime behavior.

A client-accepted action request is not presented as a successful cleanse.
For up to 2.5 seconds after the one attempt, Seiton Sense instead correlates an
exact local-caster, action, and ally-target ActionEffect result of type `0x10`
(`RecoveredFromStatusEffect`). Only the six known Purify-removable PvP statuses
can confirm it: Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature.
Heavy and Bind remain confirmation-only here and still never activate Ally
Rescue. One exact confirmation shows a blue `CLEANSED` popup for 1.5 seconds.

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
The existing fresh/held-key Ally Rescue behavior, including Aquaveil, remains
unchanged and separate from this passive Bard-only option.

The **Defensive utilities** module is a separate, default-off Crystalline
Conflict helper. All of its rules require one fresh or explicitly eligible held
physical gameplay-key generation and exact local metadata. At three or more
unique enemies currently pressuring you, an exact Stun can enable one Purify
attempt even if the ordinary Purify helper is off. Guard is never attempted from
that same physical generation: the helper must positively observe live
Resilience `3248`, see the removable CC gone, and receive a genuinely new
release/repress generation inside its bounded follow-up window.

The second defensive rule can pre-Guard at or below 50% HP while the same
three-enemy pressure threshold is known and no Purify-removable CC is already
present. This is a risk reaction, not a prediction that an instant future stun
will occur. The PLD-only rule can attempt Guardian `29066` for an exact living,
targetable, non-self party member at or below 20% HP when FFXIV's native
20-yalm action-range and line-of-sight check accepts that target. There is no
custom center-distance cap: native reachability remains authoritative and
hitbox-aware. After the jump, Guardian's protection requires the Paladin to
remain within 10 yalms of the protected member. Both your own Guard and
Guardian must be available. Candidates rank by lowest exact HP percentage,
then known higher incoming pressure, distance, and stable party identity. When
the automatic Guardian request is accepted locally, a blue 1.5-second
**GUARDIAN TRIGGERED** card shows the selected party slot and explicitly labels
the result **CLIENT ACCEPTED**. This is dispatch feedback, not proof that the
server applied Guardian or intercepted damage.

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

While your own Guard is active, Seiton Sense blocks all of its action-request
helpers, including Purify, defensive utilities, pressure Sprint, Ally Rescue,
reactive counter-CC, Ninja Seiton, Critical Strategy, and Earth's Reply. The
same suppression starts immediately for a bounded 1.5
seconds after an exact local Guard request, covering the short interval before
the live Guard status becomes visible without extending the deadline. This
prevents the plugin from cancelling Guard. It cannot prevent a manual game action
or another plugin from cancelling Guard, and the exact live client/server ordering
of these new rules still needs in-game validation.

The **Reactive counter-CC** module is also default-off and CC-only. On WHM it
uses Miracle of Nature `29228` at native 10-yalm range; on BRD it uses Silent
Nocturne `29395` at native 20-yalm range. Both jobs can respond to the exact
early DNC Contradance `29432` startup signal. The existing urgent Marksman's
Spite `29415`, Zantetsuken `29537`, and VPR Furious Backlash / Nest der
Blutschuppen `39188` startup paths remain WHM-only. VPR waits for live Hardened
Scales `4096` to be genuinely absent, and every path revalidates exact canonical
enemy identity, life/targetability, action-specific CC protection, native range,
and line of sight.

The post-Purify subtype now accepts all six exact recovered statuses: Stun
`1343`, Heavy `1344`, Bind `1345`, Silence `1347`, Miracle of Nature `3085`, and
Deep Freeze `3219`. It requires exact enemy self-Purify `29056`, positive live
Resilience `3248`, and then 150 ms of stable real Resilience absence rather than
predicting its timer. Promotion also requires exact team focus of at least two:
the enemy must be your current hard target and at least one exact ally's hard
target. The resulting opportunity retains its original bounded release edge and
is never extended while another threat has priority.

Self-Purify, defensive utilities, pressure Sprint, Ally Rescue, and reactive
counter-CC share one physical-generation path in that order. State and input
are consumed before one
normal exact-target request; there is no visible selected-target change,
alternate action/target, fallback, replay, or retry. Plugin-owned Miracle and
Silent Nocturne requests still pass through the final action-specific
CC-immunity brake immediately before the native call.

After the sole request, a blue `AUTO CC LANDED` popup appears only if the bounded
ActionEffect observer captures the matching status on that exact pending enemy:
Miracle `3085` for WHM or Silence `1347` for BRD. A local client-accepted request
does not count. Even an exact landed popup proves only that the counter-CC status
landed; it does not conclusively prove that Contradance, another limit break, or
its damage was interrupted. All startup timing, Purify/Resilience release
ordering, BRD/WHM dispatch, and claimed interruption outcomes remain explicit
current-patch live-validation boundaries.

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
never used as an alternate action. The shared priority is Self-Purify,
defensive utilities, pressure Sprint, Ally Rescue, reactive counter-CC, Ninja
Seiton, Scholar Critical Strategy, then Monk Earth's Reply, so the Monk helper
waits rather than competing with an earlier claim. The helper
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

## DRK Shadowbringer two-line macro

This separate helper is disabled by default and supports only exact PvP Dark
Knight in Crystalline Conflict. Author exactly two adjacent macro lines:

```text
/seitonbringer
/pvpac "Souleater Combo" <t>
```

On a non-English client, replace only the quoted `Souleater Combo` text with
the exact localized PvP action name. Keep `/seitonbringer`, the line order, and
`<t>` unchanged. In ReAction, enable both **Macro Queue** and **Turbo** for this
macro.

The command may arm only the immediately following exact Souleater Combo route
against your unchanged current `<t>`, which must resolve to one exact canonical
`S1`-`S5` enemy. The helper recognizes a new GCD cycle only from a proven exact
2.40-second combo recast restart plus action-sequence change. It may claim at
most one Shadowbringer attempt for that cycle, and only while the inclusive
remaining-time window is 0.60-0.80 seconds. A missed window is skipped;
0.50 seconds or less never triggers Shadowbringer. The paired combo call still
reaches vanilla unchanged, and a later Turbo pulse can queue the authored combo
line inside FFXIV's normal queue window.

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
remain current-patch live-trace boundaries.

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
| Optional BRD/WHM Ally Rescue | Yes | No | No |
| Optional defensive utilities | Yes | No | No |
| Optional WHM/BRD reactive counter-CC | Yes | No | No |
| Optional team-visible Attack1 focus sign | Yes | No | No |
| Optional local Auto Low-MP Focus Target | Yes | No | No |
| Optional MNK Earth's Reply | Yes | Yes, when test mode is enabled | No |
| Seiton `S1`-`S5` decision cues | Yes | Synthetic visual `S1` | No |
| Optional NIN Seiton fresh-key helper | Yes | No | No |
| Optional DRK Shadowbringer two-line macro | Yes | No | No |
| Near Assist | Yes | No | No |
| Near Help | Yes | No | No |
| Far Help | Yes | No | No |

Wolves' Den support is explicitly a test option. Its enemy visuals require one
strict native hostile duel opponent; missing or ambiguous identity shows
nothing. Pressure has an additional Wolves' Den opt-in so testing does not
create an always-on pressure display by surprise.

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
- `/seitonbringer` - arm the immediately following exact DRK Souleater Combo
  macro line for the bounded Shadowbringer weave check
- `/seiton show` / `/seiton hide` - enable or disable the entire plugin
- `/seiton preview` - preview nameplate indicators
- `/seiton flash` - preview the Seiton popup
- `/seiton debug` - print bounded diagnostics, including recent Near Assist,
  selected-target CC-brake resolution, isolation/defensive state, Auto Low-MP
  Focus, DRK Shadowbringer, and retained reactive counter-CC opportunity results
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
It does not read character names or Home Worlds and stores no combat, target, or
key history. The separate default-off Guardian communication uses ordinary
FFXIV Quick Chat and marker commands, so enabling it creates the described
party-visible in-game side effect through FFXIV. Transient observations and the
exact one-shot action boundary are documented in [PRIVACY.md](PRIVACY.md).

Display-only features, including the resource aura, never target or press
actions or mutate native UI. Auto Low-MP Focus is a separate explicit setter,
and the DRK macro is a separate explicit action helper; both are default-off
and bounded as described above. For one already incoming, enabled CC action attempt against
an exact protected enemy, the optional brake can return `false` without calling
the downstream/original action function. The exact native selected target may
be read only to resolve an unchanged native target carrier of `0` or
`0xE0000000`; a zero marked as deliberately suppressed by Seiton's redirect
path is never restored. A missing hostile flag can be replaced only by the
strict complete visible five-member party proof in a known public CC territory;
self, party/alliance, native identity, and exact `<e1>`-`<e5>` checks remain.
Plugin-owned Miracle and Silent Nocturne attempts receive the same final
action-specific brake after redirect bypass. The brake never stores or replays
input and never chooses another target or action. Near Assist, Near Help, and Far Help can
each replace only the target ID of one explicitly armed, already incoming macro
action. Near Help may choose the local player only when the exact resolved action
supports self and passes native target/range/line-of-sight validation. Optional
self-Purify, defensive utilities, pressure Sprint, Ally Rescue, reactive
counter-CC, Ninja Seiton, and Scholar Critical Strategy may each initiate one
exact action attempt and share one physical-generation ownership path in that
order. Monk Earth's Reply is a separate automatic follow-up that yields after
an earlier helper attempt. A post-Purify Guard requires a new
physical generation. While your own Guard is active, all Seiton action-request
helpers are blocked; the same gate applies for the bounded 1.5-second status-
propagation interval after an exact local Guard request. Manual game actions remain
outside that protection. Ally Rescue
labels a removal `CLEANSED` only after the exact
successful status-removal ActionEffect is observed; attempts and client-accepted
requests alone are not success claims.
The separate default-off Ninja helper follows those higher-priority helpers. It
can initiate at most one adjusted Seiton `29515`/`29516` attempt from a fresh
physical down edge against the lowest exact HP-ratio candidate among the
canonical, reachable `S1`-`S5` enemies below 50%. Its selected intent and input
are consumed before dispatch, and it has no target mutation, second selection,
alternate, fallback, replay, or retry. The original key is not swallowed, and
a client-accepted return is not a landed-action or kill claim.
The separate default-off Monk helper may initiate at most one exact Earth's
Reply attempt per continuously observed Earth Resonance after every earlier
helper in the listed priority declines; it has no alternate action or retry.
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

The Scholar helper is not display-only either. When explicitly enabled, it may
initiate one Critical Strategy action attempt against its frozen exact guarded
enemy from one shared held-key generation. It never sends chat or markers,
changes the selected target, chooses another enemy after drift, or retries.

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
party-row / current CC-row aura anchoring, pressure evidence, MCH marker/sound
timing, optional action helpers, and the macro helpers with both normal macros
and Turbo Hotbar should be rechecked in the relevant live PvP context after
FFXIV, Dalamud, macro, network-event, or input-handling changes.

For v0.18.0.0 specifically, source checks cover Auto Low-MP Focus's complete
canonical set, trusted MP hysteresis, deterministic selection, stable-empty and
manual-override ownership, frozen final preflight, sole set-only write, exact
readback, and no clear/replace/restore/retry contract. DRK checks cover exact
macro adjacency and carrier shape, proven 2.40-second GCD-cycle ownership, the
inclusive 0.60-0.80 remaining-time window, the never-at-or-below-0.50 boundary,
one token spent before one request, exact target/Guard/range/line-of-sight/
queue/resource gates, unchanged outer combo call, and no alternate, mutation,
replay, or retry. Those checks cannot prove the live Focus setter/HUD/`<f>`
result, ReAction Macro Queue/Turbo mode, native queue and recast-group timing,
server execution, or clipping; both features still require current-patch live
CC A/B traces.

The retained v0.17.0.0 source checks cover the exact direct hard/cast
pressure threshold, warning-entry episode ownership, native-sound one-shot
behavior, held-generation ownership, exact Sprint metadata/readiness, own-Guard
suppression, final pressure revalidation, and one consumed self action attempt
without alternate, replay, or retry. Those checks cannot prove live enemy-target
telemetry, the chosen FFXIV system sound, or Sprint acceptance/effect in the
current client and therefore still require a live CC A/B test.

The retained v0.16.0.0 source checks cover the Bard-only Paean action and
metadata boundary, the exact `3+` incoming-pressure threshold, exact non-self
party identity and native reachability, deterministic pressure/HP/identity
ranking, unchanged vanilla fallthrough before selection, frozen-intent
suppression after final drift, one target substitution on one already incoming
call, and no plugin-created action, alternate, replay, or retry. Those checks do
not prove that a manual or Turbo call was
accepted by the live client or that Paean applied, removed, or nullified CC.
They also retain the Ninja helper's fresh-edge ownership, complete canonical
`S1`-`S5` auto-selection and adjusted-action gates, strict below-50% boundary,
latest-safe frozen-actor HP re-read, exact-50% cancellation,
Guard/priority suppression, and one-attempt/no-retry contract. The Ninja helper
has no hard-target dependency. Guardian communication checks cover accepted-
episode consumption, chat-only occupied-marker behavior, Bind2-before-Bind1
confirmation, both native empty-marker representations, exact per-sign
ownership, partial cleanup, and no post-invocation retry. Scholar checks cover
Guard-only complete `S1`-`S5` selection, trusted-
positive-pressure/HP ranking, held-generation ownership, native reachability,
frozen-intent revalidation, and one attempt without target mutation or alternate.
Localized Quick Chat, marker placement/cleanup, Critical Strategy dispatch/
effect, and the passive Paean redirect still require current-patch live
confirmation.
Existing tests cover Near Help's exact self-target gate, critical-health
override, bounded pressure window, complete-view fallback, and deterministic
pressure/HP/distance ordering, plus the isolation debounce and
fail-closed unknown state, defensive thresholds and generation ownership,
reactive event/status/team-focus rules, Attack1 selection/ownership rules, and
Guardian's delegation to native reachability without a custom center-distance
cap. They do not prove the current client's native 20-yalm line-of-sight result,
Purify-to-Resilience-to-new-generation Guard ordering, pre-Guard/Guardian
dispatch, Contradance startup timing, BRD/WHM counter dispatch, or the native
party-visible marker command and clear path. An `AUTO CC LANDED` confirmation
proves the matching status was observed on the intended enemy, not that a limit
break or damage was stopped. Those outcomes all require a current-patch live CC
A/B test. The deliberately omitted position/Splatoon guide has no runtime or
validation claim.
