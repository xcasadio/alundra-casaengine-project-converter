> Report returned by one agent of the targeted read-only discovery for E20.f1 (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

**Things that change the plan**

1. **Item 4 of the REVISE (no anchor, no hero depth key) is fixable in the DLL with no engine change.** What the aura needs is a `PosY`, an `IDSV` and a `PosZ`. In the common case the binary's `+0x1BC` is exactly `((PosY + (IDSV<<16)) & 0xFFFF0000) + (short)(PosZ>>16)`. Two terms are not covered by that: a neighbour raise (hero on a support, or overlapping a lower collidable) and a platform raise (the `+0x28` carrier, never set for the hero). Neither is in the DLL's hero elevation today either.
2. **The DLL engine ordering uses only `DepthKey >> 20` (the 16-px bucket).** `AlundraEffectQuadBuilder.cs:72` calls `ComputeEntityElevation((int)DepthKey, 0)`. The low 20 bits of the key are never used. So the anchor key should use the same IDSV the hero's sprite elevation uses (`AlundraFrameSyncPasses.cs:436-441`). The aura then lands in the hero's bucket by construction, since `ComputeEntityElevation` ignores the low half.
3. **Probable plan-changer, an INFERENCE that needs a unit test.** The binary draws the aura just after the hero (key+1, same key otherwise). In the DLL, within one bucket the engine compares `SortCoordinate` before `LocalSortOffset` and `StableId` (`RenderSortKey2D.cs`).
   - The builder gives the aura `coordinate = 100*ay` with `ay = (Y−Z)>>16` (`AlundraEffectQuadBuilder.cs:73`).
   - The aura's Z is hero Z + 0x100000, so 16 px higher, so its coordinate is about 1600 lower than the hero's feet coordinate.
   - Ascending sort puts the aura before the hero, so it would draw under the hero's sprite.
   - A fix is DLL-only: for slots with an anchor, use the anchor's coordinate and a `LocalSortOffset` strictly above the sprite's 0. It needs an ADR-0049 amendment.
4. **The hero's `PosZ` in the DLL is the binary's minus 1 raw unit at rest.** See `AlundraEntitySpawnFactory.cs:673-683` and `AlundraEntityScriptProxy.cs:1774-1790`. The aura's anchor Y is `(Y−Z)>>16`. When the hero's `PosY` fraction is 0 (common at rest), that 1 raw unit moves the aura one pixel row. `0x93` and `0xA2` hit the same thing (ADR-0049). The adapter needs a decision: expose `PosZ+1` on the ground, or accept a 1-px deviation.
5. **No per-tick snapshot of the hero is needed for the aura.**
   - A mode-1 slot reassigns X/Y/Z from the anchor on every update (`AlundraEffectReservoir.cs:574-603`).
   - `RunFrame` runs after all ticks, so the last tick's update reads the end-of-frame hero, which is what the binary's last tick sees.
   - Only intermediate updates of a 2-tick frame read a later state, and nothing carries over from them.
6. **The oracle blocker (item 2) is confirmed.** To compare keys it must journal, per tick after the sort: hero `+0x1BC` and `+0x1A8`, `+0x114..+0x11E`, and the slot's `+0x34/+0x38/+0x3C/+0x40/+0x44/+0x60/+0x64` after the update. It must also run `0x800399B8` and `0x8003C410`, or I compute the key from the formula below.

---

## A. The binary: exact computation (FACT, ALUN_CD.EXE France, capstone)

Entity array: base `0x80127D30`, stride `0x294`. Fields used:

| Offset | Field | Offset | Field |
|---|---|---|---|
| `+0x10` | status | `+0x1D8/1DC/1E0` | ModdedPos X/Y/Z |
| `+0x20` | BlockedBy | `+0x1E4/1E8` | ModX/ModY |
| `+0x28` | PlatformEntity (carrier) | `+0x1F0/1F4/1F8` | Width/Height/Depth |
| `+0x6C` | Flags (`0x80` = Collidable) | `+0x1A8` | image-set IDSV (SpriteRef+0x14) |
| `+0xB4` | AnimFlags (`0x80` = NoEntityCollision) | `+0x1BC` | the depth key |
| `+0x114/118/11C` | PosX/Y/Z, 16.16 | `+0x1C0` | ZTop |
| `+0x11E` | integer PosZ (high half of `+0x11C`) | `+0x1CC` | DamagedTickCounter |

Lists, built by the lists pass `0x800384F4` (`0x80038548-0x8003862C`):
- **active** (`0x80127B28`, count `0x80127D28`): status 2 or 3 and `+0x20 == 0`.
- **collideable** (`0x80127C28`, count `0x80127D2C`): `Flags&0x80`, `!(AnimFlags&0x80)`, `+0x28 == 0`. There is no status test.
- **visible** (`0x80134250`, count `0x8013435C`): status 2 or 3 and `(+0x1CC & 3) != 3`.

Sort driver `0x800399B8` (stores at `0x800399FC`, `0x80039A04`, `0x80039AA4`; loads at `0x80039A3C`, `0x80039A94`), three loops over the visible list:

```
L1: e.1BC = 0 ; e.1C0 = e.1E0 + e.1F8                     // ZTop = ModdedPosZ + Depth
L2: if e.1BC == 0: F(e)
L3: e.1BC = (e.1BC & 0xFFFF0000) + (int)(short)(e.PosZ >> 16)   // lh 0x11E, sign-extended
```

`F(e)` is at `0x800397AC`, recursive (stores at `0x80039858` and `0x8003997C`; loads at `0x800397DC`, `0x8003982C`, `0x8003993C`):

```
if e.1BC != 0: return e.1BC
s2 = e.PosY(+0x118, full 16.16) + (e.1A8 << 16)
if (e.Flags & 0x80) && !(e.AnimFlags & 0x80):
    if e.28 != 0:                                   // carried entity: its carrier
        k = e.28.1BC ; if k == 0: k = F(e.28)
        if s2 < k (signed): e.1BC = k ; return k    // early out, skips the loop
    for o in collideable list (o != e):
        if !(o.1C0 < e.1C0 signed) continue         // only entities with lower ZTop
        X: d = (o.114 + o.1E4) - e.1D8 ; d >= 0 ? d <= e.1F0 : (e.1D8-(o.114+o.1E4)) <= o.1F0
        Y: d = (o.118 + o.1E8) - e.1DC ; d >= 0 ? d <= e.1F4 : (e.1DC-(o.118+o.1E8)) <= o.1F4
        if overlap: k = o.1BC ; if k == 0: k = F(o) ; if s2 < k: s2 = k
e.1BC = s2 ; return s2
```

Hero consequences:
- The hero's `MoreFlags` is 140 (`0x8C`), so it is Collidable. It takes part in the loop unless its current animation sets `AnimFlags&0x80` (UNKNOWN which animations do).
- The hero's `+0x28` is INFERENCE null. The decompiled writes of PlatformEntity put the player as the value into the carried entity (`PlayerManager.cs:1117, 2027, 2224`). I did not trace each store base in the binary.
- `RidingEntity +0x12C` is not read by the sort. A support enters only through the overlap loop: the support's ZTop is lower than the hero's, with XY overlap.
- Key shape: high half is `PosY.int + IDSV`. After L3 the low half is the integer PosZ. The PosY fraction never survives L3, though it is visible in the phase-2 values neighbours compare.

**Quirks (FACT from the code, not needed for the first slice):**
- A collidable entity not in the visible list (status 1, a flicker frame, BlockedBy) is never reset by L1. `F` computes its key on demand and L3 never masks it, so a stale key (with fraction bits) feeds its neighbours.
- A visible entity with `(DamagedTickCounter & 3) == 3` is not sorted that tick. Its `+0x1BC` is stale, and an aura attached to it reads the stale value.

**IDSV source** (FACT): the clock pass `0x80038E18` calls `UpdateAnimation 0x80038AB4` for each active entity. At `0x80038CBC-0x80038D08` the frame's image-set index is read (`0xFFFF` means none). `+0x1A8` becomes byte 0 of the image set, or 0 if none. It is per displayed frame, not per animation.

## B. Pass and order inside a tick (FACT)

The draw routine `0x8002E058` runs `0x8003C67C`, then UpdateEntities `0x8003B388`, then UpdateEffects `0x8003C410` (calls at `0x8002E100-0x8002E110`).

Inside `0x8003B388`, in order:
1. If `g_playerControlFlags & 0x48`: lists only (`0x8003B408`).
2. Otherwise: `0x80038634`, `0x800386D0` (programs), `0x80038998` (counters), `0x800384F4` (lists), `0x80038E18` (clock), `0x80038364` (physics), `0x80038E84`, `0x80039300` (hit pass).
3. The tail `0x8003B414` runs always, gate closed or not. It calls the sort `0x800399B8` (`0x8003B478`), then builds the draw list.

So the key of tick k comes from the post-physics, post-hit-pass state of tick k. The effects update then reads it, so the effects see the same tick's key, never the previous one.

## C. Readers of `+0x1BC` (FACT, by displacement scan `0x1BC(`, all of ALUN_CD.EXE)

| Address | Reader |
|---|---|
| `0x800397DC`, `0x8003982C`, `0x8003993C` | `F` |
| `0x80039A3C`, `0x80039A94` | sort driver |
| `0x8003B4AC` | draw-list builder. Copies the key to SpriteRef+0x10 (entity `+0x1A4`) with Pos X/Y/Z at `+0x198/19C/1A0`, and appends the pointer at `[0x80126E6C]`. |
| `0x8003C374` | effect Move, mode 1, first part |
| `0x8003C3E0` | effect Move, common part for modes 1 and 3 |

The other hits are not this field. `0x80038548/85D0` are status reads. `0x80038764` is `+0x6C`. `0x8004F74C-0x8004FCCC` are another struct.

Draw consumption: the list is sorted by `0x8002DA04`, a signed ascending quicksort on `[ptr+0x10]`. It is unstable, which ADR-0049 already notes. The list is drawn in that order through `0x8002DB48`, with OT slot `16*min(key>>20,59)+6` (`sra 0x14` at `0x8002DBD8`).

Effect side, `UpdateEffectPosition 0x8003C284`:
- Mode 0 (`0x8003C2CC-0x8003C318`): `X+=FX; Z+=FZ; Y+=FY; key = (Y&0xFFFF0000) + (Z>>16) + (IDSV<<16)`. This is the same shape as the entity key. The Z term sits in the low half.
- Mode 1:
  - Status `+0x10` equal to 0: mode becomes 2, no position or key.
  - Otherwise: `X/Y/Z = anchor.Pos + offsets` and `key = anchor.1BC + slot.+0x60`; status 4 sets mode 2. This falls through into the common part.
- Common part (modes 1 and 3): add the forces. If status is not 0, `key = anchor.1BC + +0x60` again. Status 0 or 4 sets mode 2.
- The DLL `MoveFollower` (`AlundraEffectReservoir.cs:574-603`) matches this line for line. No change needed there.

`CreateAttached 0x8003BE74` (FACT): the slot starts at the anchor's raw `PosX/Y/Z` (no offsets), mode 1, `+0x38` entity, `+0x60` the depth mod, `+0x48/4C/50` the offsets. The key is not set at creation, only by the first update. For the aura: `(0, 3, 0, hero, 1, 0, 0, 0x100000)`, so `DepthOffset = 1`. That adds 1 to the low half and cannot change the bucket unless PosZ is negative.

## D. What the DLL does today (FACT)

- **Entities.**
  - `AlundraFrameSyncPasses.RunWallInterleaveSortKeyPass` (`AlundraFrameSyncPasses.cs:414-442`, called at `AlundraWorldProxy.cs:2441`) sets `DepthSortable2DComponent.Elevation = ComputeEntityElevation(PosY, idsv)` (`WallPlacementOverlay.cs:393`).
  - That is `16*min((PosY+(idsv<<16))>>20, 59)+6`.
  - `idsv` is **frame 0** of the `(anim, direction)` pair (`BuildIdsvByAnimDirection`, `AlundraEntitySpawnFactory.cs:650`).
  - The call is gated by `_wallPlacementOverlayApplied`.
  - Within a bucket, order is the engine's `-worldY*100` (`DepthSortable2DComponent.cs:107, 132`).
  - **No** PosZ term, no neighbour/platform raise, no stored `ZUpperBound`/`RenderSortKey` (the repo-wide search finds those names only in the decompilation).
- **Effects.** `Build` (`AlundraEffectQuadBuilder.cs:72-90`) uses elevation from `(int)DepthKey`, `coordinate = 100*ay`, `local = N-1-k`, `stable = slot.Index`. `RenderSortKey2D.CompareTo` is ascending over `(pass, layer, order, elevation, coordinate, local, stable)`.
- **Anchor.** `IAlundraEffectAnchor` (`AlundraEffectReservoir.cs:8-24`) is implemented by no production entity, only the test class `Anchor` (`AlundraNativeEffectTests.cs:544`).
- **Data and collidables.**
  - `IdsvAnimDirs[].Frames` holds per-frame IDSV for all 395 banks (`SpriteRecordCatalog.cs:62`).
  - For the hero bank `192c2eeb-bdda-5bb4-92d6-39a4ae298307`, 139 of 376 `(anim, dir)` pairs vary across frames. Examples: anim 1 dir 0 `[2,6,2,2,6,2]`; anim 3 dir 0 `[0,7,4,0,7,4]`; anim 18 dir 0 `[5,3,1,6,1,36,43,38,13]`. Idle (anim 0) is constant per direction (`[4,4,4,4]` dir 0).
  - The collidable list exists (`IAlundraScriptHost.Collidables`, built by `EntitySupport.BuildCollidables`, `EntitySupport.cs:44`).
  - A per-frame table from the logical tick already exists as a pattern: `AlundraFrameBoxTimeline` (`AlundraFrameBoxTimeline.cs`).

## E. Can the hero's anchor key be computed exactly?

| Case | Exact in the DLL? |
|---|---|
| Hero on the ground, no lower collidable overlapping | Yes, for PosY and PosZ.int. IDSV is exact when the current animation's IDSV is constant over frames (idle). It is an approximation on walking, jumping and attack frames (frame 0 stands in). |
| Hero riding or on a support (`RidingEntity != null`) | No in the binary sense. The binary lifts the hero to at least the support's key (a recursive max, one `F` call per overlapping lower entity). The DLL has no such step for the hero sprite either, so a consistent aura/hero pair carries the same deviation. |
| Carried by another entity (`+0x28`) | Not applicable to the hero (INFERENCE). |
| Hero overlapping any other lower collidable in XY | As for riding. |

**PosZ.int equality.** `(short)(PosZ>>16)` is the same in both conventions except when the 16.16 fraction of `(T−ModZ)` is `0xFFFF`. On flat ground, `T = cellHeight*16<<16`, so it is exact.

## F. Minimal port

1. A pure static function in the DLL, for example `AlundraEntityDepthKey.Compute(int posY, int idsv, int posZ)`. The body is `unchecked((uint)(((posY + (idsv<<16)) & (int)0xFFFF0000) + (short)(posZ >> 16)))`.
2. Factor the IDSV resolution out of `AlundraFrameSyncPasses.cs:436-440` (including the `~Target` rule at `:436`) into one proxy helper. Use it in both the pass and the anchor so they cannot diverge. It must not depend on `_wallPlacementOverlayApplied`.
3. A small proxy-owned anchor class implementing `IAlundraEffectAnchor`:
   - `PosX/PosY/PosZ` read from the proxy. Decide the +1 raw on the ground (E.4 above).
   - `Status = (int)proxy.Status`. The enum already matches the binary: `Destroyed=0, Loaded=1, Normal=2, Deactivated=3, FlagToDestroy=4` (`EntityStatus.cs`).
   - `DepthKey = Compute(PosY, idsv, PosZ)`.
   - The class is created once per hero, not per call.
4. The effect-stage accessor on the host (already F-e in `f-weapons.md`).
5. Optional, later and separate: a per-frame IDSV timeline (pattern `AlundraFrameBoxTimeline`) and the neighbour raise over `host.Collidables`. This would change the hero's and every entity's elevation, not just the aura's, so it is outside f1. If done, the stale-key quirk in A has to be ported or the pass deviates.

## G. What an attached effect needs besides the key

- **Status mapping:** the direct cast above. In the DLL the hero never gets 0 or 4 in normal play.
- **Creation vs update order:** the binary creates in the logic of tick k, and the slot's first update runs in tick k's render phase, reading the post-physics hero. The DLL does the same through `Emit` with `BeginSource(tick, 1)` then `RunFrame` (`AlundraEffectStage.cs:170-216`, `AlundraWorldProxy.cs:2669`). Keep the aura creation on that path (the hit-spark precedent is `AlundraWorldProxy.cs:2815-2818`).
- **Handle not visible within the frame:** the op is applied only in `RunFrame`, so in a 2-tick frame the second tick's `MovePlayer` still sees a null handle. This is the REVISE's item 3, separate from the key. It needs a handle token created at emit time that the op later fills. The slot's `Destroying` and the `+0x7D` test must read through that token.
- **Order in `RunFrame`:** ops are applied before `_reservoir.Tick` of their tick. The key read happens inside `Tick` through the anchor, after `RunWallInterleaveSortKeyPass` (`:2441` is before `:2449`). Both see the same frame state.
- **Frozen ticks** (`PlayerControlFlags & 0x48`, `_effectFrozenOfTick`) skip the update in both binary and DLL. The binary's sort still runs on such ticks, but nothing reads it.

## H. Engine change?

No. The key, the anchor and the draw order are all expressible with the existing `RenderSortKey2D`. The only open ordering fix (G.3) is in `AlundraEffectQuadBuilder`, with an ADR-0049 amendment. A per-slot override of coordinate and local offset can be added to the builder or the anchor interface without touching `CasaEngine`.

## I. Suggested checks for the plan

1. Unit: for a hero at `(x, y, z)` with an aura slot `(DepthOffset 1, OffsetZ 0x100000)`, the aura's `RenderSortKey2D` must compare strictly greater than the hero sprite's key. This proves or disproves point 3.
2. Unit: `Compute` against hand-computed binary values, including `PosZ.int < 0` (borrow) and `IDSV` up to 43.
3. Oracle: hero `+0x1BC` equals `Compute(...)` for idle and for walking frames. Add a case with a collidable lower neighbour (a crate or a platform on map 389) to measure the raise.

## UNKNOWN

- Which hero animations set `AnimFlags&0x80`, and whether the hero ever has a non-null `+0x28`. The first can be read from the animation flag table. The second needs the store bases traced.
- The sprite's exact sort coordinate for the hero when airborne (`RenderProjectionComponent.DeriveRenderPosition` policy not read).
- Whether the `Frames` index of `IdsvAnimDirs` aligns frame-for-frame with the sprite component's displayed frames (the doc says terminators are excluded).

Scratch files, all in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-f1/depth-key/`:
- `d.py`, `callers.py`, `scan1bc.py`, `scanoff.py`, `sr.py`, `sr2.py` (scripts)
- `dis_397ac.txt`, `dis_3b4ac.txt`, `dis_3c284.txt`, `dis_384f4.txt`, `dis_38ab4.txt`, `scan1bc.txt` (disassemblies and scan output)
