# Seiton Sense

Seiton Sense is a Crystalline Conflict reaction-cue HUD for every job, with an
enabled-by-default Wolves' Den duel test mode. It adds small status icons beside
the job icon in each enemy's native nameplate, shows local warnings for selected
dangerous debuffs, and gives Ninja a persistent Seiton decision cue. An optional
experimental Purify-on-next-key helper is available but disabled by default.

## What it shows

- **Persistent Seiton cue (NIN only):** when your Seiton resource is ready, an
  enemy is strictly below 50% HP, and the native range/line-of-sight check
  passes, a center-adjacent card remains visible with the enemy's official job
  icon and the exact `SHIFT + 1` through `SHIFT + 5` decision. `SHIFT` is the
  default configurable key label; it does not change your actual keybinds.
- **Seiton preparation:** an optional amber `PREP` card appears from 50% up to,
  but not including, 60% HP. Entering the real execute window still produces a
  short pulse, but the decision card then remains visible while the verified
  window remains valid.
- **Nameplate Seiton:** the native-nameplate indicator and exact `S1`-`S5` slot
  remain available alongside the larger decision cue.
- **Guard unavailable:** after this client actually observes an enemy Guard,
  a crossed Guard icon and optional countdown remain until its 30-second recast
  is estimated ready. Unknown Guard cooldowns are not guessed. KO and revive
  clear the cooldown in CC; changing or losing the strict duel opponent clears
  the estimate in Wolves' Den.
- **Low MP:** a crossed blue Standard-issue Elixir appears below 2,000 MP, the
  current cost of Recuperate. Initial zero values are ignored until MP has been
  observed reliably; entering and leaving the state is debounced.
- **Warnings on you:** Wildfire and Death Warrant receive compact danger
  warnings. Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature
  receive urgent Purify warnings. Each verified status gets an entry pulse and
  a stable remaining-time display instead of repeatedly flashing from
  transient samples.

Guard does not block the Seiton alert because Seiton Tenchu ignores Guard.

In Crystalline Conflict, `S1`-`S5` always follows FFXIV's native
`<e1>`-`<e5>` enemy-list order. In Wolves' Den, the plugin accepts only one
strict hostile duel opponent and labels that player synthetic `S1`. This is a
visual aid only: it does not claim that FFXIV's `<e1>` macro placeholder exists
in a duel. Staying in the same party does not block the test mode because the
native duel-opponent identity and hostile flag remain authoritative. If the
opponent is absent or invalid, the duel HUD shows nothing. Frontline and Rival
Wings are deliberately excluded from this test release.

## Stable nameplate anchoring

The plugin subscribes to Dalamud's nameplate update service and copies only the
visible native job-icon rectangle after FFXIV finishes each nameplate update.
It then draws the three fixed extra slots next to that rectangle. It does not
replace the job icon, create or mutate native UI nodes, or retain native
pointers between frames. A wholly missing handler may retain only its already
copied rectangle for up to 200 ms to absorb a single-frame callback gap; a
present but hidden or invalid nameplate is removed immediately, and stale
anchors fail closed.

Seiton's stable resource state is checked separately from transient facing,
animation lock, casting, and current-target state. A short false-grace prevents
single range/line-of-sight samples from blinking the icon. Cue rearming is
separate, so walking out of and back into range cannot spam the entry pulse.

## Experimental Purify on next key

The optional **Purify on next fresh gameplay key** experiment is disabled by
default. It supports the exact current Purify list: Stun, Heavy, Bind, Silence,
Deep Freeze, and Miracle of Nature. Every type has its own toggle, so Heavy can
be left as a warning while Stun or Silence still trigger the helper. While the
opt-in is active in supported PvP, the plugin keeps a read-only key baseline;
a key already held before the debuff does not count, but a genuine new key edge
on the first observed status frame does.

The original key is never swallowed, replaced, delayed, or replayed. One key
causes exactly one native Purify attempt immediately. FFXIV then decides whether
that normal action request can queue or execute. The attempt is marked consumed
before it is sent, so a client or server rejection is never retried. Temporary
chat/UI focus no longer consumes the debuff window; it simply requires another
fresh key after typing ends. Seiton Sense does not select another action, change
targets, fabricate input, alter packets, or manipulate network replies. Another
plugin configured to rewrite Purify or its target can still alter the downstream
call; disable such rules while testing this experiment.

## Install

Add this custom repository in Dalamud:

```text
https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/main/repo.json
```

Then search for **Seiton Sense** in the Plugin Installer. Existing installs
update through the same repository.

## Commands

- `/seiton` or `/ssense` — open settings
- `/seiton show` / `/seiton hide` — enable or disable the HUD
- `/seiton preview` — preview the nameplate indicators
- `/seiton flash` — preview the Seiton job-icon popup
- `/seiton debug` — print one bounded diagnostic line
- `/seiton reset` — restore defaults

The cue label, scale, position, entry-pulse duration, personal-warning layout,
nameplate icon size, spacing, and individual indicators are configurable. The
experimental Purify helper has one master opt-in plus a separate toggle for
each supported debuff type.

## Scope and privacy

Seiton Sense supports Crystalline Conflict plus the optional Wolves' Den duel
test mode and has no server, account, telemetry, or gameplay upload. It does
not read character names or Home Worlds and stores no combat history or key
history. Only local settings are persisted through Dalamud.

The display features never target or press actions. The opt-in Purify experiment
is the sole feature allowed to request an action, under the one-key/one-attempt
rules above. Guard cooldown is an estimate derived only from a locally observed
Guard status; unobserved state stays unknown. No displayed cue or experimental
action request is guaranteed to succeed.

Like all third-party FFXIV modifications, use is at your own risk. This is a
custom-repository plugin and is not distributed through Dalamud's official
plugin repository.

## Build and validation

The project targets Dalamud API 15 / .NET 10. The local release script performs
a locked restore, warning-free full build, dependency-free core tests,
bounded-input safety checks, source fingerprinting, and ZIP/manifest
verification. Hosted CI rebuilds the dependency-free core and verifies the
committed, source-fingerprinted plugin package because Dalamud's plugin SDK
requires assemblies from a local XIVLauncher installation. Exact visual
placement and the optional Purify experiment still need a real CC or Wolves'
Den duel check after FFXIV, Dalamud, or input-handling changes.
