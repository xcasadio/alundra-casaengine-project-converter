# ADR-0106: The spell target query is a pure function with the corrected bound and sort threshold, inert in production

- **Status**: Accepted
- **Date**: 2026-10-27
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.31.2 (E20.h4b1, revision 2; D-H4B1-1 to D-H4B1-3, Q-H4B-2, Q-H4B-3, Q-H4-2b B / D-E20-62); `docs/plan-e20-annexe/e20-h4b1/` (`README.md`, `results.md`, `t2.md`); `docs/plan-e20-annexe/e20-h4b/` (the reading of the binary)

## Context

- The binary query `0x8003AF70` lists the entities a spell can aim at and its sort `0x8003AE04` orders them by squared distance. It scans the slots 1 to count - 1 (the highest occupied slot is missed) and sorts only from three results, so a pair stays in slot order. Both are defects of the original; the author's rule is to correct the proven defects of the original (Q-H4B-2, Q-H4B-3).
- `EntitySearchService` is the query of the script opcodes, another algorithm pinned by its own tests.
- Nothing calls the query yet: the spells that aim (vent 1, C63, C65, C75) will.

## Decision

- `TargetQuery.Find(entities, count, caller, mask, type, inclusiveBound, sortThreshold)` (`Alundra/Combat/TargetQuery.cs`) is a pure function over the proxies of the born entities, scanning by `EntityScriptProxy.Slot` (never by list index), with the filters of the binary, the key `dx^2 + dy^2` modulo 2^32 on 32-bit differences, and the Hoare sort ported as is (pivot fixed at the entry, unstable).
- The production values are the corrected ones: `InclusiveBound = true`, `SortThreshold = 2`. The binary mode (`inclusiveBound: false`, `sortThreshold: 3`) is the one the 130 real-code vectors replay.
- `EntitySearchService` does not change. The stored highest-slot field is not ported (the live entities suffice with the inclusive bound).
- The slice is inert in production.

## Consequences

- No behaviour change in the game; the spell slices (h4d2, h4e1, h4e2, h4f1) call `Find` with the production defaults.
- 28 tests (`TargetQueryTests`) pin it against the binary vectors, the corrected vectors and hand-made worlds; 25 real mutants were applied, 24 killed at once and one (pivot recomputed) killed after a test was added (`docs/plan-e20-annexe/e20-h4b1/t2.md`).
- Known limit: the pivot wrap `(K[lo] + K[hi]) mod 2^32` only matters when both keys are 2^31, where the binary reads memory before the array; kept as an unsigned wrap, not tested.
