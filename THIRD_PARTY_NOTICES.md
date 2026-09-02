# Third-party notices

## Final Fantasy XIV duty artwork

Crystalline Conflict duty-art textures are owned by Square Enix. Seiton Sense
does not copy or redistribute them; the rotation panel requests the reviewed
icons at runtime from the user's installed game files through Dalamud's texture
service.

## PvP Tracker / PvpStats interoperability

The Crystalline Conflict result-packet layout and the current match-end hook
signature were researched and cross-checked against:

- PvP Tracker / PvpStats by SaMo (`wrath16/PvpStats`)
- Source: https://github.com/wrath16/PvpStats
- License: MIT

No upstream UI, database model, timeline, action parser, or live-combat source
is copied into Seiton Sense. Its independently structured boundaries use the
reviewed result-packet offsets and, for an explicit local history import, read
only the small set of raw BSON fields required to recognize completed CC
matches and player aliases.

## LiteDB 5.0.16

The optional local PvpStats history reader includes LiteDB 5.0.16.

- Source: https://github.com/litedb-org/LiteDB
- License: MIT
- Copyright (c) 2014-2022 Mauricio David

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
