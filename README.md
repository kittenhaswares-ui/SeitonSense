# Seiton Sense

Seiton Sense is a display-only Dalamud overlay for Ninja in Crystalline
Conflict. When Seiton Tenchu is usable against an enemy below 50% HP, it shows
the matching native enemy-list key above that player: **S1** through **S5**.

## What the alert means

An overhead label is shown only when all of these are true:

- you are currently playing Ninja in Crystalline Conflict;
- the enemy is strictly below 50% HP;
- the enemy passes Seiton Tenchu's native 20-yalm range and line-of-sight check;
  and
- the game currently reports the adjusted Seiton action as usable for that
  exact enemy.

`S1` maps to the game's first enemy slot (`<e1>`), `S2` to `<e2>`, and so on
through `S5`/`<e5>`. The plugin asks FFXIV for those slots directly; it does not
invent an order from player names, jobs, or distance.

The screen flashes after two consecutive eligible scans and only once per
execute window. It rearms after that enemy has remained at or above 52% HP for
400 ms, preventing repeated flashes around the threshold.

## Install

Add this URL to Dalamud's **Custom Plugin Repositories**:

```text
https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/main/repo.json
```

Then search for **Seiton Sense** in the Plugin Installer.

## Commands

```text
/seiton
/ssense
```

Subcommands: `show`, `hide`, `preview`, `flash`, `debug`, `reset`, `help`.

## Scope and limitations

- The first release is deliberately Crystalline Conflict-only because S1-S5
  are the native CC enemy-party slots.
- The alert marks the current execute opportunity; it cannot guarantee future
  HP or that the target will still be valid when you press.
- Only enemies currently represented to your game client can be labeled.
- A game update can change action or arena data and may require a plugin update.
  The strict startup check disables live alerts instead of guessing when the
  verified Seiton or Unsealed metadata changes.
- This is a third-party Dalamud plugin. Square Enix prohibits third-party
  tools; use is at your own risk. It is distributed only through this custom
  repository, not Dalamud's official repository.

## Safety and privacy

Seiton Sense never targets, presses an action, changes input, or sends gameplay
data anywhere. It has no server, accounts, telemetry, or uploads. See
[PRIVACY.md](PRIVACY.md).

## Build

Requires .NET 10 and Dalamud API 15.

```powershell
pwsh -NoProfile -File scripts/Build-Release.ps1
```
