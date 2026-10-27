# ADR-0108: A held object is always drawn in front of its carrier and a thrown one follows the keys of the binary, through the engine sort anchor

- **Status**: Accepted
- **Date**: 2026-10-27
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.24.8 (E20.g1d, revision 1; D-G1D-1 to D-G1D-7; D-E20-88 (1), D-E20-89 (1) and (2)); `docs/plan-e20-annexe/e20-g1d/` (`README.md`, `results.md`, `t2.md`, `keys/`); ADR-0049 point 3; ADR-0089 (W6 of E20.g1a)

## Context

- The executable orders sprites by the key `((PosY + (IDSV << 16)) & 0xFFFF0000) + (short)(PosZ >> 16)` (driver `0x800399B8`, function `0x800397AC`); it raises to the carrier's key only a held object that is collidable (`0x8003981C-0x80039858`). The bomb is not collidable: it is in front only because its IDSV is at least the hero's and it is higher.
- The DLL set only the coarse `Elevation` row of the `DepthSortable2DComponent` and left the fine order to the engine, which compares the sort coordinate (`100 * (Y - Z)`, height weighing the wrong way). In the same row (about 15 cases of 16) the held bomb was drawn behind the hero (probe P1 of T0: 15 of 16 rows behind), hence the author's capture (bomb hidden by the hair).
- The engine already offers `SortAnchorLocal` and `LocalSortOffset` for a held object; the engine is not touched by this slice.

## Decision

- The pass `FrameSyncPasses.RunWallInterleaveSortKeyPass` writes, for every visited entity, `SortAnchorLocal` and `LocalSortOffset` from the pure rule `EntityDepthKey.ResolveDrawTie` (D-G1D-5): (0, 0) and 0 for any entity neither held nor thrown.
- A held object (any object whose `PlatformEntity` is a proxy) takes the sort coordinate of its carrier, computed on the two RENDERED translations (exact equality even when the hero's height is fractional), and the offset `+K` with `K = 14 > ` the largest `DrawOrder` of the hero (13) and of the liftable objects (6) (D-G1D-2, D-G1D-4).
- An object thrown by the hero (`CarryRules.ThrowCarried` sets `EntityScriptProxy.ThrownBy`; the pass drops the mark on the ground, at another status or when held again) is placed at the thrower's coordinate plus `100 * delta`, `delta` being the difference in pixels of the high halves of the two binary keys, and at the same pixel at `+K`, `-K` or 0 according to the sign of the key comparison (D-G1D-3).
- `IEffectAnchor.SortCoordinate` follows the coordinate the pass wrote (`DrawnSortCoordinate`), so an anchored effect sorts as its object (D-G1D-6).
- Written deviations: (D-G1D-2) every held object is raised to the carrier's `Elevation`, collidable or not, where the binary raises the collidables only, so the bomb is never behind at the take-off of the jump facing up (tick 162 of `bomb_keys2`) (D-E20-89 (2)); (D-G1D-7) the raising by a collidable neighbour, the IDSV of frame 0 instead of the displayed frame, the other airborne entities and the fuse spark (O-G1D-1) stay as they were.

## Consequences

- The held bomb, crates, pots, torches and beans are drawn in front of the hero in all directions and jumps; the thrown object keeps the order of the binary against its thrower (in front when thrown left, right or down; behind from the 4th flight tick when thrown up). Checked against 119 + 241 + 239 + 134 rows of the replayed binary keys, 0 differences apart from the two written exceptions.
- The rule is a pure function that can be replaced if a later slice makes every entity follow the binary's key.
- 20 real mutants were applied by script and undone, all killed (`docs/plan-e20-annexe/e20-g1d/t2/`).
- Known limits: the engine sort is untouched; the fuse spark stays behind the bomb and the hero; other entities keep the coarse-row order.
