# Privacy

Seiton Sense does not create accounts, contact a server, upload gameplay data,
or store combat history. While active, it transiently reads your current job,
supported PvP context, local limit gauge and statuses, visible enemy slots or
the native Wolves' Den duel-opponent entity ID, jobs, HP, MP, Guard statuses,
and the screen bounds of native job icons from the local FFXIV client. It does
not read character names or Home Worlds, and none of that gameplay data is
persisted.

If the experimental Purify buffer is explicitly enabled, the plugin also reads
fresh local key-down edges while an exact Stun or Miracle of Nature instance is
active. It does not record key text or key history, swallow or replay the
original key, change targets, or transmit input. A fresh key can request at most
one native Purify attempt; a rejected or failed attempt is never retried. Other
plugins can still alter that downstream call if they are configured to rewrite
Purify or its target. The experiment is disabled by default.

Only local display settings and the Purify opt-in/timing settings are saved
through Dalamud. Like all third-party FFXIV modifications, use is at your own
risk; Seiton Sense is distributed through a custom repository, not Dalamud's
official plugin repository.
