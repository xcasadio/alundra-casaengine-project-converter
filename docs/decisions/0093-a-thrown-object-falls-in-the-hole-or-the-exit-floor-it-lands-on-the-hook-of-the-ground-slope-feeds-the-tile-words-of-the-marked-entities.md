# ADR-0093: A thrown object falls in the hole or the exit floor it lands on: the hook of the ground slope also feeds the tile words of the marked entities

- **Status**: Accepted
- **Date**: 2026-10-25
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.24.5 (E20.g2b2, D-G2B2-1 to D-G2B2-4, D-E20-74, Q-G2B2-1 default A, O-G-2); reading of `ALUN_CD.EXE` (France): the physics pass `UpdateEntityTiles` `0x80038064`-`0x80038360` and the pick phase `0x800386D0` (`+0x180` read at `0x800387AC`, `andi 0x8004` at `0x800387B4`, `DestroyEntity(e, -1)` at `0x800387C4`), replays in `docs/plan-e20-annexe/e20-g2b2/replay/` and `docs/plan-e20-annexe/e20-g2c1-disc/g2c1/`; measured results in `docs/plan-e20-annexe/e20-g2b2/results.md`

## Context

- In the binary, the physics pass writes for every entity of the physics list (statuses 2 and 3) that has the gravity the OR (`+0x180`), the AND (`+0x184`) and the slope (`+0x18C`) of the four corner words. The pick phase destroys the entity that carries `0x200000` (`DestroyOnVramFlags`) when the OR has a bit of `0x8004` (the hole `0x0004`, the exit floor `0x8000`), with `DestroyEntity(e, -1)` (the break effect of the header). One corner is enough (the OR).
- The DLL read the OR in the pick phase already, but `UpdateVramFlags` was called for the hero only (D-T-12: the restriction avoided destroying NPCs on portals); the hook of f6a fed the slope only. A thrown barrel, stone, trunk or plate object stayed posed on a hole; a crate or a jar broke there by E2 (g2b) at T+25 instead of the pick phase at T+23.
- The replay of the binary (T0) gives, for every object including the crate and the jar: the words written at the landing tick T+22, status 4 at T+23 (never 3), the slot free at T+24, the break effect attached, no landing sound, and E2 never called on a hole. On a full cell the chronology is the one of g2b.

## Decision

- The hook of f6a in `EntityScriptProxy.Update` calls `UpdateVramFlags()` after `UpdateGroundSlope()` for an entity marked `HasLogicalZ` only (D-G2B2-1). The entities placed by the map are never marked and keep the words at 0 (D-T-12 kept); the trunk that `FeedsGroundSlope` serves gets the slope only.
- Nothing else changes: the pick phase, the mask `0x8004`, the OR and the gravity gate of `UpdateVramFlags` are the existing ones. The branch `CombinedVramFlagsAND & 4` of E2 is now live for a marked object but not reached by a thrown crate on a hole (the pick phase destroys it first), as in the binary (D-G2B2-3).
- Entities born in play that carry the gravity and `0x200000` (the loot objects) fall in a hole as well, as in the binary (D-G2B2-2); the full suite lists no test moved.
- ADR-0089 stands (the break of E2 on a full cell); its crate-on-a-hole case is superseded by this decision.

## Consequences

- A barrel, stone, trunk, star object, crate or jar thrown on a hole or an exit floor disappears at T+23 with its break effect (10, or 2 for the jar), the slot free at T+24; the puzzles with holes work as in the original.
- No existing test moves (6 301 tests before the line, 22 new then 6 322, plus 7 added by T3: P5 on the trunk, P9, P10 x4, P11).
- Verification: 13 real mutants (applied by script, undone, `docs/plan-e20-annexe/e20-g2b2/t3/`), all killed, among them the six of the plan; three of them first survived and forced the tests P9 (a marked entity without the gravity), P5 on the trunk (the placed trunk) and P10 (each of the four corners) and P11 (the status 3).
- Revert: `git revert` of the line; a thrown barrel stays on a hole, a crate breaks there by E2.
