# ADR-0094: The treasure chest opens through the native F255 with the object it gives birth to, and the magic seed is one body with three entries

- **Status**: Accepted
- **Date**: 2026-10-25
- **Source**: `docs/plan-e20-heros.md` section 1.26.2 (E20.e0d, decisions D-E0D-1 to D-E0D-11, tasks T2 and T3 results and deviations); `docs/plan-e20-annexe/e20-e0d-disc/report-main.md` and `critic-main.md`; `docs/plan-e20-annexe/e20-e0d/{results,t1-t2,t3,t4}.md`

## Context

The chest (global sprite type 30) had no native F255: Square against it did nothing, so the key of the map 117 (flag 260, consumed by the door of the map 115) and the vase of the map 115 (flag 659, awaited by `0x9F [10]`) never came out. In the binary (`ALUN_CD.EXE`, France) the chest has F255 (`0x8007FC88`), shares A255 (`0x80061BD4`) with the 87 pickup objects, and opens by giving birth to an object (`+0x274 == 2`) whose A255 and C255 branches drive the rise, the message, `ApplyItem` (`0x80033DBC`) and the flag. The magic seed (`0x8003382C`) was a stub in `ItemEffects`, and the opcode `0x82` degraded it. F255 was replayed on 12 000 vectors of the binary with no difference (`f255_oracle.py`). Probes of the DLL (T0) measured that a `Block` kept the blocked sprites animating and that a held-Loaded object was drawn.

## Decision

- **D-E0D-1**: three phases: A the display (A255 on the chest), B the whole opening (F255, the chest branches of A255 and C255, the key-object path with its fanfare, `CanTake`, `ApplyItem`), C the body of the seed and `0x82` for `0x50`.
- **D-E0D-2**: the fanfare of a key-object chest follows the binary: `LoadBgm(0)` at F255, the CD track 11 stays the journaled stub when A255 releases the object, and at state 1 of C255 the mask 3 and `StopAllSound` come the tick after the box opens, the box still open (mask 4 until then).
- **D-E0D-3**: the 4 chests with no flag (maps 11 idx 30, 161, 362, 398) and the duplicates of the map 80 keep their behaviour (game data, not a code defect).
- **D-E0D-4**: a blocked entity keeps its sprite drawn but its controller and animation stop (the binary removes it from the active list only). Applied by `GameplayFreeze.IsFrozen` (`worldFrozen || BlockedByEntity != null`), called from `WorldProxy` where `GameplayFreeze.Apply` is run each frame (hence after `Block`/`Unblock`), not in `FrameSyncPasses.cs` as first planned: it is the one site of the freeze. The hero, blocked too, is frozen as in the binary.
- **D-E0D-5**: the object born by a chest is invisible while Loaded (the 61 ticks of the chest animation). Applied in A255 of the object (`PickupAi.ChestObjectLoad`) and at the birth (`ChestAi.Interact`), not as a general "a Loaded entity is not drawn" rule in `FrameSyncPasses.cs` (wider, and no test would pin it).
- **D-E0D-6**: the texts restore the space before "gained!" and after "to keep" that the Yarn compiler drops (`ItemEffects.ComposeChestMessage`).
- **D-E0D-7**: registering A255 on type 30 draws open the 20 chests with contents 0 (16 scripted, 4 native of the map 325); the map 0 test chests are covered by a data guard.
- **D-E0D-8**: `ItemTables.DropStockCap` exposes the stock cap (column 3 of `item-drop-properties.json`) for `CanTake`.
- **D-E0D-9**: the chest of an object is read through `ParentEntity`; `+0x288` (key object) lives in `AIValues`; the born object takes `BlockedByEntity = null` after `Block`; the birth Z is in the binary's frame (ADR-0026).
- **D-E0D-10**: the seed is one body (`ItemEffects.MagicSeed`: `MpMax = min(MpMax + 1, 4)`, `Mp = MpMax`, sound `0x36`, eight rings and four sparks of the effect 14 in the animation 2), shared with the vessel through `NativeEffects.CreateLifeVesselEffects(..., animation)` (0 the vessel, 2 the seed). Its three entries (`GiveItem`, `ApplyItem`, `0x82` for `0x50`) call it, so the seed's effects are created at the binary's Z (`PosZ + 1`) by all three.
- **D-E0D-11**: four test-save presets: `coffre-m15`, `coffre-m11`, `coffre-m0`, `coffre-m117`.
- `ApplyItem` has no "inventory first" rule (a refused heal is never drunk, D-E20-78); `GiveItem` shares its body for the non-stacked objects.

## Consequences

- Chests open, give their item (or refuse a full bag and close again), raise their persistent flag, and are saved open; the key of the map 117 and the vase of the map 115 come out.
- 23 real mutants of this code were applied by script and all killed by the targeted suite (`docs/plan-e20-annexe/e20-e0d/t4.md`); one survivor (`ApplyItem` replaced by `GiveItem`) led to an added test.
- Open point O-E0D-1, NOT fixed here: `EventProgramRunner.UseLifeVessel` (opcode `0x82`, `0x53`, E19.g G3d) still creates the vessel's effects at the raw `PosZ`, one raw unit lower than `GiveItem`/`ApplyItem` (`PosZ + 1`); preexisting, pinned by its own tests, to settle with the author.
- The consumption of the key by the door of the map 115 (`0x84 [61]`) is not driven by a test; the CD track 11 of the fanfare remains the E19.sm7 stub.
- `WorldProxy.cs` and `GameplayFreeze.cs` are touched outside the tranche's first scope list; the hero is frozen while a chest opens, like the binary.
