# ADR-0017: ForceAdjusted rises only on a tick without progress, like the binary

- **Status**: Accepted
- **Date**: 2026-10-01
- **Source**: this chantier: `docs/plan-e19-opcodes.md`, slice E19.a3 (author decision D-E19-12 of 2026-10-01), after the in-game recipe T5 of slice E19.a2; supersedes decision D-E19-10 of ADR-0016

## Context

- ADR-0016 (D-E19-10) kept the DLL's rule for `ForceAdjusted`: the flag rises when a step falls short of the request by more than 0.01 px on an axis. Once the engine advances a blocked step to contact (D-E19-8), this rule raises the flag on the contact tick, one tick earlier than the original. This was recorded as a minor deviation.
- In `ALUN_CD.EXE`, `ComputeXYPosition` (`0x80037730`) raises `ForceAdjusted` (`+0x13C`) only when no halved sub-step was accepted on the tick (`0x80037d54`, skipped by the guard at `0x800379a4`). A tick that advances to contact leaves the flag at 0.
- The in-game recipe of E19.a2 showed the deviation is visible. At the end of the intro of map 389, sailor 12 walks south 48 px with `0x1F` and reaches the distance on the very tick his last step is shortened against a wall. The next walk, west 72 px with `0x1F`, starts on the same tick, reads the flag left by that tick, and ends without a step (`0x1F` completes when `ForceAdjusted` is set). The sailor stays 72 px east of his place. In the original, the flag is 0 on that tick and the walk proceeds.
- Before the engine change, a blocked step was rejected whole, so the DLL's rule raised the flag on a tick without progress, which matched the original by accident.

## Decision

- **D-E19-12** — `ForceAdjusted` rises when, on an axis, the requested displacement exceeds 0.01 px and the obtained displacement is at most 0.01 px, i.e. the axis made no progress at all. A step that is shortened but advances does not raise it. This supersedes D-E19-10.
- The original's slide along a wall when only one corner touches (`didAdjustForObstacle`) stays with slice E19.h (D-E19-9).

## Consequences

- Every reader of `ForceAdjusted` (`0x1F`, `0x24`, the E4.d walk detour, deactivation on impact, the ladder entry) sees it on the same tick as the original at a contact: one tick later than the E19.a2 rule.
- Scripted walks chained after a walk that ends against a wall are no longer skipped.
- The hero reference traces move their first contact flag by one frame; the contact positions do not change.
- An axis whose remaining distance to the wall is under 0.01 px counts as making no progress, which differs from the original only below that threshold.
- Diagonal walks into a corner still differ from the original until the slide is ported.
