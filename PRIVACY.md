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

Only local display settings and the Purify opt-in/held-key/per-debuff settings
are saved through Dalamud. The integrated focus preset does not read, modify,
or delete the standalone Super Focus Glow configuration. Like all third-party
FFXIV modifications, use is at your own risk; Seiton Sense is distributed
through a custom repository, not Dalamud's official plugin repository.
