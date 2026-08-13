# Seiton Sense

Seiton Sense is a local PvP awareness HUD that combines pressure tracking,
stable native-nameplate cues, personal warnings, Ninja Seiton decisions,
one-shot macro assistance, and target highlights. Version 0.7 adds action-aware
lowest-health party help on top of the integrated v0.6 awareness suite, which combines the
useful parts of HOWMANY, CCImmunityWatch, NearAssist, and Super Focus Glow into
one configurable plugin. Hotfix 0.7.0.1 makes Ally Rescue less fragile and
adds exact cleanse confirmation and local feedback. It remains a
custom-repository plugin.

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
- **Personal warnings:** Wildfire, Death Warrant, supported Purify-removable CC,
  and Marksman's Spite receive stable warnings. The MCH LB card is larger by
  default and can play one selectable built-in FFXIV sound per verified threat.
  Warning-card background opacity is independent from its icon, text, and
  border, so the fill can be fully transparent without hiding the warning.
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
- **Experimental Ally Rescue:** on BRD or WHM, one fresh or explicitly eligible
  held gameplay-key generation can attempt Paean or Aquaveil on an exact party
  member suffering Stun, Silence, Deep Freeze, or Miracle of Nature. Selection
  uses HP, incoming pressure, trusted MP, and distance in that order. A matching
  successful status-removal effect produces a blue `CLEANSED` popup and feeds
  resettable, in-memory match/session counters.
- **Target clarity:** the integrated focus glow, independent current-target
  highlight, and fixed target-information card remain optional. The information
  card can also show team pressure and whether that target is pressuring you.
- **Cleaner settings:** features are separated into Overview, Pressure,
  Warnings, Seiton, Assist, Targets, and Advanced tabs. Configuration schema 12
  preserves existing settings; both action-attempt experiments remain opt-in.

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

## Personal warnings, Purify, and Ally Rescue

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
| Personal warnings and optional self-Purify | Yes | Yes | No |
| Optional BRD/WHM Ally Rescue | Yes | No | No |
| Seiton `S1`-`S5` decision cues | Yes | Synthetic visual `S1` | No |
| Near Assist | Yes | No | No |
| Near Help | Yes | No | No |

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
- `/seiton show` / `/seiton hide` - enable or disable the HUD
- `/seiton preview` - preview nameplate indicators
- `/seiton flash` - preview the Seiton popup
- `/seiton debug` - print one bounded diagnostic line, including recent Near
  Assist decisions
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

Display features never target or press actions. Near Assist and Near Help can
each replace only the target ID of one explicitly armed, already incoming macro
action. The optional self-Purify and Ally Rescue experiments may each initiate
one exact action attempt, but share one physical-generation ownership path with
self-Purify first. Ally Rescue labels a removal `CLEANSED` only after the exact
successful status-removal ActionEffect is observed; attempts and client-accepted
requests alone are not success claims.

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
fresh live in-game confirmation. Exact nameplate placement, pressure evidence,
MCH marker/sound timing, optional Purify/Ally Rescue behavior, and Near Assist
with both normal macros and Turbo Hotbar should be rechecked in the relevant
live PvP context after FFXIV, Dalamud, macro, network-event, or input-handling
changes. The 0.7.0.1 ActionEffect confirmation and blue popup also still require
current-patch live validation.
