# E20.g1d T0 (d): the predictions, written BEFORE the probes ran (2026-10-27 14:42 +01:00)

Every prediction below was written from the reading (`docs/plan-e20-annexe/e20-g1d-disc/`) and the code at the HEAD of start, before a probe test was written or run. A probe that contradicts its prediction is a stop of the plan.

## P1 - the bomb in the hands of the real hero, the 16 rows of Y

Bench: `BombHandsRun` (the real hero, the bomb given and equipped, the Circle at tick 4), the hero put at the pixel (100, Y) for Y = 100 to 115 (the 16 phases of `Y mod 16`; 107, 108, 109 are the phases 11, 12, 13), standing with the bomb (sampled at ticks 40 to 44, once the lift is over, the carry animation 12). Compared: the sort key of the hero and of the bomb, `DepthSortable2DComponent.BuildSortKey(RenderProjection.WorldMatrixNoScale.Translation)`, `CompareTo`; sign = the bomb relative to the hero.

- Facing down (hero direction 0, IDSV 4 against 5): the bomb is BEHIND in 15 of the 16 rows and in FRONT in 1 (Y mod 16 = 11, where `(Y+5)>>4 > (Y+4)>>4`): the prediction of the reading (15 of 16).
- Facing left or right (direction 8 or 24, IDSV 5 against 5): BEHIND in 16 of 16.
- Facing up (direction 16, IDSV 2 against 5): BEHIND in 13 of 16, FRONT in 3 (Y mod 16 = 11, 12, 13).
- The `Elevation` of the bomb is the bucket of `PosY + (5 << 16)` (not raised: the bomb is not a collidable); the sort coordinate of the bomb is `100 * (Y - 32)` against the hero's `100 * Y`, in every row.

## P2 - the order of the passes in the frame

The sort pass (`WorldProxy.Update`, `RunWallInterleaveSortKeyPass`, line 2692) runs AFTER `PublishEntityFrame` (line 2647: `SyncTransform` -> `UpdateProjection`) of the same frame, and the carried object's root is pushed by `FollowCarrier` inside the tick. So at the entry of the pass the rendered translations (`RenderProjection.WorldMatrixNoScale.Translation`) of the hero and of the carried bomb ARE those of the end of the frame (what the renderer reads): equal, to the bit, at every frame of a carry, a walk and a jump with the bomb. The anchor of D-G1D-2 can therefore be computed on the two rendered translations inside the pass; no fallback on the logical positions is needed.

## P3 - `ThrowCarried` then the program of the object

Bench: `BombHandsRun`, the Circle at tick 4, the Square at tick 50, the hero facing each of the four directions (the pad of the oracle's `throw_probe`: the direction held for the ticks 1 to 3, the Circle at 4, the Square at 50).

- At the tick 50 `ThrowCarried` runs (the hero state becomes 9 on the ground) and the bomb is released at the same tick: `PlatformEntity` null from the tick 50, `Status` Normal for the whole flight, no relift.
- `IsOnGround` of the bomb is 0 in the hands and during the whole flight (ticks 50 to 71) and becomes 1 for the first time at the tick 72, the tick where the oracle's bomb reaches Z = 0 and where the keys of `throw_right` and `throw_left` become EQUAL (`out_throw_probe.txt`: Z 11.0 at 70, 4.5 at 71, 0 at 72). Same landing tick 72 for the four directions.
- So a mark set by `ThrowCarried` (the item reference kept on the item) can be read from the tick 50 to the tick 71 and falls at the tick 72 with `IsOnGround`; nothing resets it before.

## P4 - the `DrawOrder` of the pieces

- Hero: the maximum default draw order of the 376 `Entities/Alundra/bankalundra_0_anim*.anim2d` is 13 (the reading), no animated draw-order track exists.
- Bomb: 6 (12 files).
- The 17 liftable records (`out_liftables.txt`): every one has a maximum of at most 6 (small objects of 1 to 7 pieces); none exceeds the hero's 13. `K = max(14, max + 1) = 14`.
