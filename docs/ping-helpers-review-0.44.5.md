# Ping Helpers review — 0.44.5.0

This review covers the input, timing buffer, Chase buffer, Turbo, response clock,
priority arbitration, native queue ownership, and Smart Action handoff. It is a
source and offline-test review, not a measurement of a live match or physical ping.

## Fixed

**The failure explanation could hide the useful reason.** A rejected timing
request could be described only as a failed range/visibility request. Debug
output now keeps the timing reason and the Chase reason separately.

**SAM could block its own queued cast.** This was in the shared action boundary,
not the generic buffer engine. A client-accepted Ogi request could enable cast
protection before the game started the cast. That protection then blocked the
game's exact queued continuation. A separate owner now tracks the request,
native queue, cast startup, and observed cast. Waiting in the queue does not
freeze movement. Only the exact matching native continuation can inherit the
cast protection; this does not submit an additional action.

## What each part really does

| Part | Actual behavior | Important limit |
| --- | --- | --- |
| Timing buffer | Keeps an eligible early action press until the local action boundary opens. | Generic cast-time, ground-targeted and movement actions are excluded. |
| Chase | Keeps one exact action and target through a proven range/visibility-only miss, including supported single-target casts. | No fresh target search on each retry. Ogi/Tendo area attacks are not a new Chase lane in this update. |
| Smart Action | Chooses a reachable safe target before dispatch; where supported, passes one exact spatial intent to Chase. | An already-started cast never changes target. |
| Turbo | Repeats a certified held hotbar input through the existing input path. | A synthetic repeat is not another physical press and must not steal manual ownership. |
| Priority arbitration | Gives critical helpers precedence without treating a temporary pause as a successful action. | It does not eliminate the game's action, cast or animation restrictions. |
| High-resolution response timing | Uses a monotonic clock for critical-response opportunities. | It does not increase the game's frame rate or reduce network round-trip time. Generic Buffer/Turbo still depend on framework/native input cadence. |
| Native queue ownership | Remembers which exact queued action belongs to a helper or owned SAM request. | Client acceptance is not proof that a heal, Guard, or hit happened on the server. |

## Rules preserved during the review

- The existing `BeingMoved` gate is retained. A proposed removal was rejected
  during review: a repository comment claimed the flag included ordinary
  walking, but no independent evidence established that meaning. The official
  flag description only says commands cannot execute while being moved. Tests
  cover the mapping, not which movement sources make the native flag true.
- A different action, target, adjusted action, actor, territory, or instance must
  not silently become the buffered request.
- Releasing a key does not by itself erase a valid tap-to-land Chase request.
- Expired intent is not replayed in a burst after a pause.
- An accepted or ambiguous native result is terminal where the existing buffer
  policy requires it; it is not turned into repeated blind attempts.
- The final own-Guard check remains in place for helpers, Turbo, buffer replay,
  and their native queued continuations.
- A new user action can replace an older reservation. Simply arming a new macro
  does not manufacture evidence that an unchanged native queue disappeared.
- Compatibility checks and unavailable metadata do not invent action readiness.
- No network-position edits, range extension, packet rewriting, background game
  file scans, or extra downloads were added to the runtime.

## Offline coverage

New tests exercise the actual managed runtime safety mapping, temporal wait and
priority pause, one dispatch, key-release Chase persistence, range becoming
available, and Guard/CC/death/identity rejection. SAM tests exercise the managed
production native-call wrapper, queue continuation, rejected/throwing requests,
owner replacement, observed cast loss, emergency actions, and late-facing claims.

The full build also runs the existing input certification, held-repeat,
compatibility, exact replay, queue ownership, recovery and action-selection tests.
These are not substitutes for an in-game test of another plugin's hooks or an
uncovered native mouse/controller input route.

## Still needs a live check

1. Press an out-of-range supported attack once, release it, and walk into range
   before the configured Chase timeout. Confirm one action, not repeated attacks.
2. Repeat with Smart Action and a supported single-target cast.
3. Queue `/seitonsam` Ogi while another action is finishing. Check WASD and
   both-mouse-button movement separately. Current input hooks do not prove that
   every raw mouse movement route is covered.
4. Use manual Guard and Purify in the relevant situations. Confirm helpers do
   not cancel Guard and movement unlocks when the protected cast ends.
5. Test the optional one-time SAM late facing separately. It is default-off,
   requires the game's automatic-facing setting, and cannot repair real range,
   visibility, or server-side interruption.

No other high-confidence Ping Helper gameplay defect was established in this
review. That is a bounded finding, not a claim that the package is bug-free.
