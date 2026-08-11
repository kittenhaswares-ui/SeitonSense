# Privacy

Seiton Sense does not create accounts, contact a server, upload gameplay data,
or store combat history. While active, it transiently reads your current job,
supported PvP context, local limit gauge and statuses, visible enemy slots or
the native Wolves' Den duel-opponent entity ID, jobs, HP, MP, Guard statuses,
the screen bounds of native job icons, and—when the corresponding optional
display is enabled—the manually selected current/focus target, its job, HP,
distance, and CC enemy slot when available from the local FFXIV client. It does
not read character names or Home Worlds, and none of that gameplay data is
persisted or transmitted.

When the Marksman's Spite warning is enabled, a read-only local action-effect
observer checks only action ID `29415`, the early target-marker effect, the
caster entity ID, and whether the sole target entity ID is your local player.
The later damage/miss event is explicitly rejected. The short warning is kept
only in memory, never triggers Guard or another action, and is neither logged
nor uploaded.

If the CC-only Near Assist macro helper is explicitly enabled, the plugin also
transiently reads nearby party/alliance membership, ally positions and hard
targets and jobs for the optional smart role preference, FFXIV's native
`<e1>`-`<e5>` identities, and the immediately following
hostile macro action. It uses these values only to validate one 500 ms token.
On success it may replace the target ID of that one already incoming action; on
failure the original target ID is preserved. It does not persist ally or enemy
identity, change the visible hard/soft/focus target, initiate an action, try an
alternate target, retry, or transmit this data. The feature is disabled by
default.

If the experimental Purify helper is explicitly enabled, the plugin also reads
current local key-down states while you are in a supported PvP context. This
read-only baseline distinguishes physical press/hold generations when Stun,
Heavy, Bind, Silence, Deep Freeze, or Miracle of Nature appears. The separate
held-key option is off by default. A held generation is eligible only after a
real observed press outside text input, and is consumed until that key is
released. The plugin does not log or persist key text/history, swallow or replay
the original key, change targets, or transmit input. One physical generation can
request at most one native Purify attempt; a rejected or failed attempt is never
retried. Other plugins can still alter that downstream call if configured to
rewrite Purify or its target. The entire experiment is disabled by default.

Only local display settings (including the MCH warning toggle), the Near Assist opt-in/search distance, and the
Purify opt-in/held-key/per-debuff settings are saved through Dalamud. The
integrated focus preset does not read, modify, or delete the standalone Super
Focus Glow configuration. Like all third-party FFXIV modifications, use is at
your own risk; Seiton Sense is distributed through a custom repository, not
Dalamud's official plugin repository.
