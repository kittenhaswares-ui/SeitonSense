# Seiton Sense

Seiton Sense is a local PvP awareness HUD that combines pressure tracking,
stable native-nameplate cues, personal warnings, Ninja Seiton decisions,
one-shot macro assistance, and target highlights. Version 0.12.0.0 adds a
strict, independently default-off post-Purify Stun follow-up beneath the
existing default-off WHM Miracle helper. The suite
combines the useful parts of HOWMANY, CCImmunityWatch, NearAssist, and Super
Focus Glow into one configurable custom-repository plugin.

## Highlights

- **Sharp pressure counter:** an integrated HOWMANY-style counter shows how many
  enemies are currently pressuring you. It combines verified hard targets, cast
  targets, a bounded recent-harmful-action window, and the early MCH limit-break
  marker. The main number uses an explicit pixel-sized game font instead of
  scaling the whole window, with optional attacker job icons, CC enemy slots,
  threat colors, background, locking, and click-through.
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
  redirects, or retries it.
- **Personal warnings:** Wildfire, Death Warrant, supported Purify-removable CC,
  and Marksman's Spite receive stable warnings. The MCH LB card is larger by
  default and can play one selectable built-in FFXIV sound per verified threat.
  Warning-card background opacity is independent from its icon, text, and
  border, so the fill can be fully transparent without hiding the warning.
- **Native-HUD resource aura:** low HP softly pulses red around your visible
  native action bars, trusted low MP pulses blue, and both together pulse
  purple. The same state can be drawn more subtly around exact party-list and
  Crystalline Conflict ally/enemy rows. This is a separate foreground overlay;
  native HUD nodes, action slots, animations, and input are never changed.
- **Ninja Seiton decisions:** persistent job-icon cards, `S1`-`S5`, preparation
  cues, and entry pulses use FFXIV's native CC enemy order and verified
  range/line-of-sight checks.
- **One-shot Near Assist:** an opt-in, CC-only macro helper can redirect one
  already incoming PvP macro action to the exact `<e1>`-`<e5>` hard target of a
  nearby ally. It does not visibly switch your selected target.
- **One-shot Near Help:** `/nearhelp` redirects one already incoming friendly
  PvP macro action to the reachable non-self party member with the lowest exact
  HP percentage. Ability-specific range and line of sight are checked before
  distance is used as the tie-breaker.
- **One-shot Far Help:** `/farhelp` redirects one already incoming, reviewed
  friendly movement action to a reachable non-self party member. It first
  prefers destinations with strictly more than 10 yalms of horizontal
  hitbox-edge clearance from every live enemy, then chooses the farthest one.
  If none can be certified, it still chooses the farthest valid reachable ally.
  Only an exact distance tie prefers healer, then ranged/caster, then another
  job. It supports Guardian, Thunderclap, Aetherial Manipulation, Icarus, and
  Slither. Only no valid reachable ally means no movement; it never falls back
  to your target.
- **Experimental Ally Rescue:** on BRD or WHM, one fresh or explicitly eligible
  held gameplay-key generation can attempt Paean or Aquaveil on an exact party
  member suffering Stun, Silence, Deep Freeze, or Miracle of Nature. Selection
  uses HP, incoming pressure, trusted MP, and distance in that order. A matching
  successful status-removal effect produces a blue `CLEANSED` popup and feeds
  resettable, in-memory match/session counters.
- **Experimental Miracle intercept:** on WHM, one eligible physical held or
  freshly pressed key generation can make one Miracle of Nature attempt against
  the exact enemy starting Marksman's Spite, Zantetsuken, or Furious Backlash /
  Nest der Blutschuppen. MCH/SAM opportunities last 500 ms, VPR lasts 250 ms,
  and the VPR path waits for Hardened Scales to be genuinely absent; no visible
  target change, fallback action, or retry is added. A separate default-off
  subtype can follow an exact enemy self-Purify that removed Stun, but only
  after Resilience was positively observed and then genuinely disappeared.
- **Experimental Monk Earth's Reply:** while exact Earth Resonance is active on
  PvP Monk, a separate default-off helper can make one exact Earth's Reply
  attempt at or below 30% HP or at 1.25 seconds remaining by default. It never
  starts Riddle of Earth, never falls back to it, and never retries the same
  continuous resonance.
- **Target clarity:** the integrated focus glow, independent current-target
  highlight, and fixed target-information card remain optional. The information
  card can also show team pressure and whether that target is pressuring you.
- **Cleaner settings:** the CC-immunity brake, general resource readability and
  the Ninja, Monk, BRD/WHM, and WHM helpers are grouped under a dedicated Jobs
  tab. Overview, Pressure, Warnings, Assist, Targets, and Advanced remain
  focused on their own feature families. Configuration schema 16 preserves
  existing settings; every action-attempt feature remains opt-in.

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

A zero deliberately produced by Seiton's fail-closed Near Assist, Near Help,
Far Help, or legacy-fallback suppression carries explicit provenance and can
never be restored to the selected target. Explicit actor IDs remain
authoritative, and any missing, changed, non-canonical, duplicated, or
otherwise ambiguous target still passes through unchanged.

For the standard CC family, the verified blockers are Guard `3054`/`3673`,
Resilience `3248`, Inner Release `1303`, Meikyo Shisui `1320`, Hardened Scales
`4096`, and the Warden's Paean ward `3143`. Miracle's separate matrix uses
Resilience `3248`, Meikyo Shisui `1320`, VPR Hardened Scales `4096`, the
Warden's Paean `3143`, Relentless Rush `3052`, and Honing Dance `3162`.
Job-owned protections are accepted only on their exact job. Unsupported or
unverified statuses do not become blockers from their display text alone.

The Jobs tab provides one master switch plus separate job and action switches.
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

## Personal warnings and job quality-of-life helpers

Wildfire and Death Warrant receive danger warnings. Marksman's Spite uses its
exact early target-marker event to show the larger `MCH LIMIT BREAK ON YOU`
card before the later damage event. The optional sound uses FFXIV's built-in
sound effects, plays at most once for a verified threat, and has a test button.
It never presses Guard or another action.

Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature receive urgent
Purify warnings. The experimental **Purify on next gameplay key** helper is
disabled by default. Each debuff type has its own automation toggle, and a
separate default-off option allows an already-held gameplay key such as WASD to
trigger once when the enabled debuff appears.

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

Self-Purify observes the shared physical key first. One input generation can
therefore produce at most one helper attempt, and Ally Rescue stores its spent
state before the exact Paean/Aquaveil call with no retry. The BRD metadata check
also accepts the current lowercase leading article in `the Warden's Paean`;
numeric action identity still drives runtime behavior.

A client-accepted action request is not presented as a successful cleanse.
For up to 2.5 seconds after the one attempt, Seiton Sense instead correlates an
exact local-caster, action, and ally-target ActionEffect result of type `0x10`
(`RecoveredFromStatusEffect`). Only the six known Purify-removable PvP statuses
can confirm it: Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature.
Heavy and Bind remain confirmation-only here and still never activate Ally
Rescue. One exact confirmation shows a blue `CLEANSED` popup for 1.5 seconds.

The Overview tab separates attempts, client-accepted requests, and exact
confirmed removals, with confirmed totals for the current match and plugin
session plus per-action/per-status details. These aggregates live only in
memory. The provided reset clears the displayed statistics and does not create
another action or confirmation.

The WHM-only **Miracle intercept** is a separate, default-off Crystalline
Conflict experiment. Its three urgent triggers watch exact server start signals:
Marksman's Spite `29415`, Zantetsuken `29537`, and the VPR Furious Backlash /
Nest der Blutschuppen action `39188`. If an eligible physical key generation is
held or freshly pressed during the bounded opportunity, the exact canonical
enemy remains alive and targetable, and Miracle's native 10-yalm range and
line-of-sight check passes, Seiton Sense may make one Miracle of Nature `29228`
attempt on that enemy.

MCH and SAM opportunities expire after 500 ms; the VPR opportunity expires
after 250 ms. For VPR, the start signal only arms that short-lived opportunity.
The helper waits for live Hardened Scales `4096` to disappear and never predicts
its end from a countdown. Other verified Miracle blockers also prevent deliberately
spending Miracle into immunity. Self-Purify has first claim on the shared
physical input, Ally Rescue second, and Miracle third. A transient higher-
priority claim no longer destroys an already armed exact Miracle opportunity,
but it does consume that physical input generation. The opportunity keeps its
original 500-ms or 250-ms deadline and can act only from a genuinely fresh
eligible generation that arrives before expiry. State and input are consumed
before the one native request; there is no selected-target change, alternate
target, fallback, or retry. Turbo-generated logical repeats do not create new
physical intent.

A fourth **post-Purify Stun** subtype is independently default-off beneath that
existing Miracle master. Only when explicitly enabled does it accept an exact
enemy self-Purify `29056` ActionEffect with one self target and a recovered Stun
`1343` result (`0x10`).
The source must resolve to exactly one live canonical `<e1>`-`<e5>` opponent.
The helper then requires positive live Resilience `3248` membership within
750 ms, waits for that same status to be continuously absent for 150 ms, and
abandons the release wait 3 seconds after positive observation. A confirmed
release opens one 500-ms opportunity; it never predicts the status timer, reads
an internal status address, or uses `RemainingTime` to decide when immunity
ended.

The original urgent MCH/SAM/VPR threats keep priority over this longer
follow-up. The released opportunity otherwise uses the same eligible held or
fresh physical key generation, exact actor revalidation, complete Miracle
protection matrix, and native 10-yalm range/line-of-sight gate. State and input
are consumed before the sole Miracle attempt. There is no alternate target,
fallback, timer-based prefire, replay, or retry. Its 500 ms are always measured
from the original verified Resilience-release edge; waiting behind an urgent
threat never restarts or extends that window.

After that sole helper attempt, the same bounded action-effect capture can show
a blue `MIRACLE LANDED` news flash for 1.5 seconds. It requires the exact local
caster, Miracle action `29228`, pending threat target, server status-add effect
`0x0E`, and Miracle status `3085` within 1500 ms. The subtitle distinguishes
`MCH LB`, `SAM LB`, `VPR NEST`, and the post-Purify Stun follow-up. This confirms
that Miracle landed on the intended enemy; it does not conclusively prove that
the hostile action was interrupted or cancelled. Correlation preserves the
first still-unexpired pending helper
attempt; a later registration cannot overwrite it before it expires. A preview
button is available beside the Miracle settings.

The MCH and SAM signals occur before their later damage presentation in the
current captured event shape, but FFXIV remains authoritative. A locally
accepted Miracle request is not proof that the already-started action was
interrupted. This path is intentionally marked experimental until it has been
rechecked live on the current patch.

`/seiton debug` and the Advanced settings diagnostics now retain aggregate
Miracle opportunity counts and the last opportunity result after leaving CC:
recognized, armed, rejected, protection/range/input/priority waits, and expiry.
The active threat and bounded queues are still cleared on context exit. Live CC
evidence reached the existing native 10-yalm range/line-of-sight gate; this
release keeps that range and every identity, protection, deadline, input,
one-attempt, and no-retry boundary unchanged.

The Monk section contains a separate default-off Earth's Reply helper for PvP
job 20. It requires one exact Earth Resonance `3171`, current metadata
validation, and the adjusted Riddle of Earth `29482` slot to resolve to Earth's
Reply `29483`. The enabled trigger fires at or below the configured HP threshold
(30% by default) or inside the configured expiry window (1.25 seconds by
default).

The continuous resonance is marked spent before one self-targeted normal
`29483` request. A rejected or throwing request is not retried, and `29482` is
never used as an alternate action. A same-frame self-Purify opportunity has
priority, so the Monk helper waits rather than competing with it. The helper
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
checks every exact, live, targetable, non-self party member against that
action's native range and line of sight. It selects the lowest exact HP
percentage first. Equal health uses shorter distance, then native party order
and stable actor identity.

The `<2>` line is only a reliable concrete friendly carrier; it does not make
party member 2 the preferred destination. When a valid candidate exists,
Seiton replaces that one incoming target ID with the selected ally. If no
candidate or validation is available, only an exact authored `<2>` carrier is
made invalid so the following `<t>` line remains vanilla. If your actual
selected target already is party member 2, exact identity handling preserves
the compact `<t>` form instead of mistaking it for a carrier.

Dual-purpose skills are supported when current game metadata explicitly allows
party or ally targets. Self is deliberately excluded from automatic selection;
use the normal authored fallback if self-casting is desired. Near Help and Near
Assist replace each other's pending token. Near Help never visibly switches a
target, sends an action by itself, changes the action ID, accepts generic Queue
mode, or retries.

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
and stable actor identity break any remaining tie. Guardian additionally uses a
strict distance below 10 yalms from you matching its execution condition. The
enemy-clearance test is a map-agnostic preference, not a guarantee that a
destination is tactically safe.

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

## Focus and current-target modules

The focus glow contains the former Super Focus Glow visual controls: projected
hitbox ring, halo, rays, chevrons, label, pulse, color, foreground, rainbow, and
reduced-motion options. The current-target highlight has an independent style.
Both only observe a target you selected through FFXIV and never choose, retain,
assist, or change one.

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
| Native-HUD low-resource aura | Yes | Yes | Yes, without CC team rows |
| Optional BRD/WHM Ally Rescue | Yes | No | No |
| Optional WHM Miracle intercept | Yes | No | No |
| Optional MNK Earth's Reply | Yes | Yes, when test mode is enabled | No |
| Seiton `S1`-`S5` decision cues | Yes | Synthetic visual `S1` | No |
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
- `/nearhelp` - arm one CC-only lowest-health party redirect for the next
  supported friendly PvP macro action
- `/sshelp` - collision-free alias for `/nearhelp`
- `/farhelp` - arm one CC-only, backline-preferred farthest mobility redirect for
  the next reviewed friendly movement action; no valid reachable ally means no movement
- `/ssfar` - collision-free alias for `/farhelp`
- `/seiton show` / `/seiton hide` - enable or disable the HUD
- `/seiton preview` - preview nameplate indicators
- `/seiton flash` - preview the Seiton popup
- `/seiton debug` - print bounded diagnostics, including recent Near Assist,
  selected-target CC-brake resolution, and retained Miracle opportunity results
- `/seiton reset` - restore defaults

## Standalone plugin retirement

Disable or remove standalone HOWMANY, CCImmunityWatch, NearAssist, and Super
Focus Glow before enabling their integrated equivalents. In particular, the old
NearAssist plugin owns `/nearassist`; Seiton Sense cannot register that command
while the old plugin is loaded. `/ssassist` remains available as a collision-free
alias during migration. Seiton Sense does not import, modify, or delete the
standalone plugins' saved configuration.

## Privacy and safety

Seiton Sense has no account, server, telemetry, or gameplay upload. It does not
read character names or Home Worlds and stores no combat, target, or key
history. Transient observations and the exact one-shot action boundary are
documented in [PRIVACY.md](PRIVACY.md).

Display features, including the resource aura, never target or press actions or
mutate native UI. For one already incoming, enabled CC action attempt against
an exact protected enemy, the optional brake can return `false` without calling
the downstream/original action function. The exact native selected target may
be read only to resolve an unchanged native target carrier of `0` or
`0xE0000000`; a zero marked as deliberately suppressed by Seiton's redirect
path is never restored. The brake never stores or replays input and never
chooses another target or action. Near Assist, Near Help, and Far Help can
each replace only the target ID of one explicitly armed, already incoming macro
action. The optional self-Purify, Ally Rescue, and Miracle experiments may each
initiate one exact action attempt, but share one physical-generation ownership
path with self-Purify first, Ally Rescue second, and Miracle third. Ally Rescue
labels a removal `CLEANSED` only after the exact
successful status-removal ActionEffect is observed; attempts and client-accepted
requests alone are not success claims.
The separate default-off Monk helper may initiate at most one exact Earth's
Reply attempt per continuously observed Earth Resonance after self-Purify
declines priority; it has no alternate action or retry.

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
timing, optional Purify/Ally Rescue/Miracle/Earth's Reply behavior, and the macro helpers
with both normal macros and Turbo Hotbar should be rechecked in the relevant
live PvP context after FFXIV, Dalamud, macro, network-event, or input-handling
changes. Live CC evidence reached Miracle's unchanged native 10-yalm range/LoS
gate. The CC-immunity brake's selected-target sentinel resolution, direct-
hotbar and Turbo pulse behavior, hard-return boundary, expiry edge, and
simultaneous server-acceptance race still require continued current-patch A/B
testing. The post-Purify Stun signal, positive Resilience observation, stable
release window, and resulting Miracle attempt also require a live CC A/B test.
The 0.7.0.1 ActionEffect confirmation and
blue popup still require
current-patch live validation. The v0.8 MCH/SAM/VPR start-marker timing and any
actual Miracle interruption likewise require a live CC A/B test; source and
package checks cannot prove that server outcome.
