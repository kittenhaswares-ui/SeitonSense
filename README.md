# Seiton Sense

Seiton Sense is a display-only Crystalline Conflict HUD for every job. It adds
small status icons beside the job icon in each enemy's native nameplate, using
the nameplate's own final screen position instead of a separate 3D-world
projection.

## What it shows

- **Seiton (NIN only):** when your Seiton resource is ready, an enemy is
  strictly below 50% HP, and the native range/line-of-sight check passes, the
  nameplate gets a Seiton icon with the exact `S1`-`S5` enemy slot.
- **Seiton popup:** the same stable execute window triggers one customizable,
  short popup with that enemy's official job icon and `S1`-`S5` label.
- **Guard unavailable:** after this client actually observes an enemy Guard,
  a crossed Guard icon and optional countdown remain until its 30-second recast
  is estimated ready. Unknown Guard cooldowns are not guessed. KO and revive
  clear the cooldown because CC resets recasts on revive.
- **Low MP:** a crossed blue Standard-issue Elixir appears below 2,000 MP, the
  current cost of Recuperate. Initial zero values are ignored until MP has been
  observed reliably; entering and leaving the state is debounced.

Guard does not block the Seiton alert because Seiton Tenchu ignores Guard.

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
single range/line-of-sight samples from blinking the icon. Popup rearming is
separate, so walking out of and back into range cannot spam it.

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

The popup duration, size, position, background, nameplate icon size, spacing,
and individual indicators are configurable.

## Scope and privacy

Seiton Sense is CC-only and has no server, account, telemetry, or gameplay
upload. It does not read character names or Home Worlds and stores no combat
history. Only local display settings are persisted through Dalamud.

It never targets, presses an action, changes input, modifies a native nameplate,
or guarantees that a displayed execute will land. Guard cooldown is an estimate
derived only from a locally observed Guard status; unobserved state stays
unknown.

Like all third-party FFXIV modifications, use is at your own risk. This is a
custom-repository plugin and is not distributed through Dalamud's official
plugin repository.

## Build and validation

The project targets Dalamud API 15 / .NET 10. The release workflow performs a
locked restore, warning-free build, dependency-free core tests, display-only
safety checks, source fingerprinting, and ZIP/manifest verification. Exact
visual placement still needs a real CC check after FFXIV or Dalamud UI changes.
