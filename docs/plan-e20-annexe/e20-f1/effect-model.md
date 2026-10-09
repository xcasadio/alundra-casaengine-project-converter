> Report returned by one agent of the targeted read-only discovery for E20.f1 (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

# Report effect-model (slice E20.f1): the DLL effect system, the binary semantics, and a creation model

Scratch files are in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-f1/effect-model/`: `bdis.py` (capstone, skipdata), `upwe.txt` (disassembly of `0x8002F49C`..`0x8002F780`), `allcode.txt` (full text disassembly), `fx3.py`. The repository was only read. Nothing was built or run in dotnet. Addresses: file offset = `0x800 + addr - 0x80020000`.

## Things that change the plan

1. **The `+0x7D` test is inverted in the plan, and the aura is renewed every 97 ticks.**
   - FACT, `0x8002F570`-`0x8002F58C`: `lw v0,0x6ff8` loads the handle. `beqz` jumps to the creation at `0x8002F590` when it is null. Otherwise `lbu v0,0x7d(v0)`, then `beqz v0 -> 0x8002F5C4`, which is the creation skipped when `+0x7D == 0`. The aura is therefore created when the handle is null or the slot's destroy flag is non-zero.
   - FACT, global effect 3, animation 0 ends with `Destroy`: 24 frames, 96 ticks, drawn on updates 0..96, freed on update 97 (`docs/plan-e19-g1g3-annexe/anim_table.tsv`, row `alundra 3 0 END 24 96 96 97`; I read the same 24 frames and delay sum 96 in `Data/effects/effects-global.json`).
   - FACT, `Animate` (`0x8003BD58`-`0x8003BD68`): the flag is set at update 96 (`sb 0xFF -> +0x7C`, `sb 1 -> +0x7D`).
   - Consequence: for a charge held forever, MovePlayer creates a new aura at tick c+97 (c = creation tick). The old one is freed in the same tick's pool update. The plan needs a renewal task and a long-hold acceptance (>= c+100).
   - The oracle cannot show this (point 4).
2. **Queued creation cannot work for the hero, only synchronous creation can.**
   - FACT: `Emit` queues and `AlundraEffectStage.RunFrame` applies after all ticks of the frame (`AlundraEffectStage.cs:168-216`, `AlundraWorldProxy.cs:2449`). MovePlayer runs inside the tick loop (`AlundraWorldProxy.cs:2283-2379`, `RunEntityBlock` `:2655`).
   - FACT: MovePlayer reads the reservoir (the handle, `+0x7D`, whether `0x8003BDD8` returned null). ADR-0046's "the scripts never read the reservoir, so the deferral is invisible" does not hold for it. A new ADR is needed (next number 0056).
   - Queued creation also cannot decide the spark sound and the two draws, which depend on creation success (`0x8002F618`/`0x8002F61C`).
3. **A fixed-order stale handle in the binary frees whatever occupies the slot.**
   - FACT, `0x8002F730`-`0x8002F744`: `sw zero,0x68($v0)` on the handle's slot, with no check that it is still the aura. The handle is a bare slot pointer.
   - It is cleared only there, by the create path (`0x8002F5BC`, which stores the return value even when it is 0) and by hero init (`0x80031A28`).
   - If the aura expires while MovePlayer is locked (the `0x34` or `+0x20` branches do not call `UpdatePlayerWeaponEffect`), its slot can be reused. At the next normal tick with timer < 11 the binary frees that unrelated effect.
   - INFERENCE: this is reproducible by code reading only. It needs the author's rule on defects of the original (reproduce by default, or guard on `slot.Anchor == hero && effect 3`). To be asked in T3.
4. **The oracle cannot compare any of the things the review listed.**
   - FACT, `tools/machine.py:165-174`: the effect stubs journal only the 10 argument words and return the same zeroed `0x801E8000` slot (zeroed again on every call). That slot always has `+0x7D == 0`, so the oracle never shows a renewal. The slot fields written after the call, the RNG word `0x80098708` and the pool state are never recorded.
   - FACT: the oracle never advances the generator per frame (`rg 80098708` in `probe.py` and `tools/` finds only the seed poke at `tools/attack.py:77`). The binary does, at `0x8002BD14`.
   - Needed list: section 7.
5. **The hero must expose Z as PosZ + 1 to the effects.**
   - FACT: ADR-0026 says the DLL keeps `PosZ` equal to the binary's `+0x11C` minus 1. The binary creators read `+0x114/+0x118/+0x11C` (`0x8003BEBC`-`0x8003BED4`, and the spark at `0x8002F5F4`-`0x8002F60C`).
   - The anchor and the sparks must use `PosZ + 1`. The plan's own example ("Z 1" at tick 112) is that convention.
   - INFERENCE: the life vessel's live call (`AlundraEventProgramRunner.cs:3125`) uses `hero.PosZ` without the +1, a one-unit pre-existing gap, not in scope.
6. **No engine change is needed (details in section 8).**

## 1. The DLL effect system, end to end

**AlundraEffectStage** (`AlundraEffectStage.cs`)
- FACT, `:170` `Queue(tick, rank, op)` appends to `_queue` with a running sequence number.
- FACT, `:176` `BeginSource(tick, rank)` and `:183` `EndSource` set the mark that `:190` `Emit` stamps on an operation.
- FACT, `:196-216` `RunFrame(ticks, frozenOfTick)`:
  - It sorts the queue by (tick, rank, sequence).
  - For each tick t it applies every operation with `Tick <= t`, then calls `_reservoir.Tick(frozen[t])`.
  - Leftover operations are applied after the last tick, without an update.
  - The queue is empty on return.
- FACT: the reservoir update of tick t runs after the operations of tick t (right order against the binary) but after every logic tick of the frame has already run, because the proxy calls it once after the loop.
- FACT: `AlundraWorldProxy.Update` passes `IsTransitionInProgress ? 0 : ticksThisFrame` (`:2449`). A warp departure applies operations and runs no update.
- FACT: the proxy records `_effectFrozenOfTick` per tick after the map events, before the entity block (`:2334`). The binary reads the flag live at `0x8003C45C`, after the entities. This is a pre-existing small deviation.

**AlundraEffectReservoir** (`AlundraEffectReservoir.cs`)
- FACT, `:126` 128 slots; `CreateCore` `:416-465` takes the smallest `!Active` slot and resets every field (`Destroying = false` at `:459`, `Mode = 0`, forces 0); a full pool returns null, no log.
- FACT, `:201` `Create` (mode 0, the binary's `0x8003BDD8`); `:205` `CreateAttached` (mode 1, `0x8003BE74`: `CreateCore` at the anchor position, then `Mode`, `Anchor`, `DepthOffset`, `OffsetX/Y/Z`); `:237` `Free` is only `Active = false`.
- FACT, `:93` `Destroying` is the binary's `+0x7D`:
  - It is set in `Animate` at `:530` when an animation ends with `Destroy`.
  - It is cleared at `:495` when the target animation changes.
  - `Tick` (`:387-410`) frees a slot whose flag is set, before animating it.
- FACT, `:574-611` `MoveFollower` matches `0x8003C284` mode 1 (I re-read `0x8003C320`-`0x8003C404`):
  - Status 0 sets mode 2 and falls through to the forces.
  - Otherwise position = anchor + offsets, key = anchor key + depth offset.
  - Status 4 sets mode 2.
- FACT, `IAlundraEffectAnchor` (`:11-25`) has no implementer in `Alundra/`. The only one is the test class `AlundraNativeEffectTests.cs:544`.
- FACT: `EntityStatus` is Destroyed 0, Normal 2, FlagToDestroy 4 (`EntityStatus.cs`), so `Status => (int)Status` fits the interface contract.

**Existing precedents and the stage access**
- FACT: `IEntityWorldContext.EffectStage` already exists as a default member (`IEntityWorldContext.cs:182`). `AlundraWorldProxy.EffectStage` is public (`:557`) over the field `_effectStage` (`:616`). `IAlundraScriptHost` has no such member (its last member is `OnPortalTriggerDetected`, `:133`).
- FACT, hit sparks: `EmitHitSparks` (`AlundraWorldProxy.cs:2815-2818`) does `_effectStage.Emit(r => AlundraNativeEffects.CreateHitSparks(...))`, queued with the tick and rank 1.

**ADRs**
- ADR-0045: reservoir and tick in the DLL; a `Destroy` animation of duration S is drawn on 0..S and freed at S+1.
- ADR-0046: operations queued at the tick of emission and applied before that tick's update.
- ADR-0048: the vessel's effects are queued from a pure function. Its Deviation 2 says the draws happen at apply time, after the scripts' draws of the same frame.
- ADR-0049: the push reads the live slot, outside every gate.
- ADR-0053: D-E20-12 says the DLL drives one loop of ticks in the binary order. The reservoir update stayed outside that loop; the E20.a plan lists `RunFrame` as unchanged (`docs/plan-e20-heros.md:788`).

**RNG draws of effects today**
- FACT: the reservoir draws nothing. The draws are in `AlundraNativeEffects.cs:52-53` (vessel, 2 per created spark) and `:83-85` (hit sparks, 3 per created spark). Both happen inside the queued closure, so at `RunFrame` time.
- FACT: other draws (`AlundraEventProgramRunner.cs:625`, `:1499`, `:3403`, `:3406`) happen at script time, inside the tick loop.
- FACT: the generator is global and static (`AlundraRandom.RandomSeed`, a 64-bit accumulation whose low 32 bits are the binary's word). The DLL does not advance it per frame; the binary does after the inventory trigger (`0x8002BD14`, 3 instructions after `0x8004B1D4`). E20.e0b owns that.

## 2. Binary semantics (addresses; ALUN_CD.EXE France)

**Slot allocation and initialisation**
- FACT, `GetFreeEffect 0x8003B9C4`: it walks the 128 slots from `0x80138608`, step `0x80`, and returns the first with `+0x68 == 0`, else 0.
- FACT, `InitEffect 0x8003BB14(slot, rec, recIdx, mode, st+0x10 table flag, +0x14 effect, +0x18 anim, +0x1C X, +0x20 Y, +0x24 Z)`:
  - It copies the `0x80`-byte template `0x8013C608` over the slot, preserving word 0.
  - It then writes `+0x04 = rec`, `+0x30 = recIdx or -1`, `+0x34 = mode`, `+0x68 = 2`.
  - Bytes: `+0x6C = flag != 0`, `+0x6D = flag ^ 1`, `+0x6E = effect`, `+0x6F = ~effect`, `+0x70 = anim`, `+0x71 = ~anim`.
  - Words: `+0x3C/+0x40/+0x44 = X/Y/Z`.
  - INFERENCE: the template is all zero (`0x8013C608` is past the end of the file image, so BSS). So forces, `+0x7D`, offsets and key start at 0.
- FACT, `CreateEffect 0x8003BDD8(tableFlag, effect, anim, X; st+0x10 Y, st+0x14 Z)`:
  - It calls `GetFreeEffect`; on 0 it returns 0 (`0x8003BE0C`).
  - Otherwise it calls `InitEffect(slot, 0, -1, mode 0, ...)` and returns the slot pointer. The only failure is a full pool. It draws nothing.
- FACT, `0x8003BE74(tableFlag, effect, anim, anchor; st+0x10 depthOffset, +0x14/+0x18/+0x1C offsets X/Y/Z)`:
  - `GetFreeEffect`; 0 on a full pool (`0x8003BEA8`).
  - `InitEffect(..., mode 1, X/Y/Z = anchor +0x114/+0x118/+0x11C)`.
  - Then `slot+0x38 = anchor`, `+0x60 = depthOffset`, `+0x48/+0x4C/+0x50 = offsets`. It returns the slot.
- FACT, the hero call (`0x8002F590`-`0x8002F5B4`): a0 = 0, a1 = 3, a2 = 0, a3 = `0x80127D30` (the hero), stack `1, 0, 0, 0x100000`. In DLL terms: `CreateAttached(false, 3, 0, hero, 1, 0, 0, 0x100000)`.

**Slot fields**

| Offset | Meaning |
|---|---|
| `+0x3C/+0x40/+0x44` | position X/Y/Z, 16.16 |
| `+0x54/+0x58/+0x5C` | forces X/Y/Z; the spark code writes only `+0x54/+0x58`, Z force stays 0 |
| `+0x68` | status: 2 active, 0 free |
| `+0x7D` | destroy flag byte, read by the hero and by `UpdateEffects` |

**The pool update**
- FACT, `UpdateEffects 0x8003C410` per slot with status 2:
  - If `[0x800DC4B8] & 0x48` it skips Animate and Move (the slot is still copied to the render list).
  - Else, if `+0x7D != 0`, status becomes 0 and the render copy is skipped.
  - Otherwise `Animate 0x8003BBDC` then `Move 0x8003C284`.
- FACT: the flag is set by Animate at the `Destroy` end (`0x8003BD58`-`0x8003BD68`) and cleared on an animation change (`0x8003BC98`). That matches the DLL (`Reservoir.cs:495`, `:530`).

**The aura free path (`UpdatePlayerWeaponEffect 0x8002F49C`)**
- FACT, timer < 11 (`0x8002F560` -> `0x8002F730`): if the handle (`0x80126FF8`) is non-null, `sw zero,0x68(handle)` then `sw zero,0x6FF8`. The flag is not tested.
- FACT, timer >= 11:
  - Aura create-or-skip first, every tick.
  - Then `(+0x1C & 3) != 0` returns (`0x8002F5D4`).
  - Then timer < 60 plays sound `0x2A` and returns (`0x8002F720`).
- FACT, the spark path (timer = 60):
  1. `0x8003BDD8(0, 0x10, 0, hero+0x114; st Y = hero+0x118, st Z = hero+0x11C + 0x100000)`.
  2. If the result is null, return (`0x8002F61C`): no sound, no draws.
  3. Sound `0x1AD` if `(+0x1C & 7) == 0`, else `0x1AC` (`0x8002F624`-`0x8002F63C`).
  4. `r1 = S*0x7D2B89DD + 0xE06A02E7`, `r2 = r1*0x7D2B89DD + 0xE06A02E7`, state written back = r2 (`0x8002F710`). The state word is `0x80098708`.
  5. `jx = hi(r1 * 0x60001) - 0x30000`, `jy = hi(r2 * 0x40001) - 0x20000`, both unsigned multiplies (`multu`).
  6. `slot.X += 20*jx`, `slot.Y += 20*jy` (Z is not jittered), `slot.ForceX = hero[+0xC4] - jx`, `slot.ForceY = hero[+0xC8] - jy`. In the DLL, `ForceX` is the hero's `+0xC4` and `ForceY` `+0xC8`, by field order after `AlundraEntityScriptProxy.cs:302`.
- FACT: the return value of `UpdatePlayerWeaponEffect` is not used (`0x80031D68`-`0x80031D70`).
- FACT: the timer and the handle are cleared at hero init (`0x80031A18` and `0x80031A28`, no free). The other timer sites are `0x80031BB4`, `0x80031BEC`, `0x80031C5C`, `0x80032614`, `0x80032768` and `0x800327A4`.

**Frame order in the binary**
- FACT, `UpdateWorld 0x8002E058`: `0x8003C67C` (map events), `0x8003B388` (entities), `0x8003C410` (effects), in that order (`0x8002E100`/`E108`/`E110`).
- FACT: inside `0x8003B388` (when `& 0x48 == 0`): `0x80038634`, the programs pass `0x800386D0` whose first call is `MovePlayer` (`0x800386EC`), counters `0x80038998`, lists `0x800384F4`, animation clock `0x80038E18`, physics `0x80038364`, entity effects `0x80038E84`, hit pass `0x80039300`.
- FACT: after `UpdateWorld` come the inventory trigger (`0x8002BC6C`) and the generator advance (`0x8002BD14`).
- So an effect created by MovePlayer at tick k is animated and moved by `UpdateEffects` of tick k. A flag set by that update is read by MovePlayer of tick k+1.

## 3. Creation model, evaluated

**Model Q: queued creation with a handle token. Rejected.**
- The next tick of the same frame sees a null handle (a duplicate aura) unless the token is resolved at queue time.
- `Destroying` is unreadable until `RunFrame`.
- Whether a spark was created is unknown at MovePlayer time, so the sound and the draws cannot be placed.
- The draws move to `RunFrame` time (ADR-0048 Deviation 2) even at one tick per frame.

**Model S: synchronous creation into the reservoir by the hero's tick. Recommended.**
- MovePlayer calls the reservoir through the stage, at once, and keeps the slot reference as the handle (the binary's pointer semantics, including the stale-handle quirk).
- The creation result is known immediately; sound and two draws are placed exactly as the binary does (sound, then draws, then the position and force writes).
- Defined position in the update: the write lands before the update of its own tick. For that, the stage stamps `slot.FirstUpdateTick = _sourceTick` (`BeginSource(tick, 1)` is already called at `AlundraWorldProxy.cs:2669`, before MovePlayer). `Tick(frozen, tick)` skips a slot whose stamp is later than the tick being run. Without the stamp, a slot created at tick k of an N-tick frame gets k extra updates, because `RunFrame` updates every active slot at every tick.
- Exact for one tick per frame: creation, the update of the same tick (position, image), the `+0x7D` read at the next tick, the sound and draw order relative to the hit pass (the hit-spark draws come after, as in the binary), and the free.
- INFERENCE, deviations for a frame of N >= 2 ticks (all documented, none on a normal 50/60 Hz frame):
  1. **Renewal gap.** The `+0x7D` that update c+96 sets is not yet visible to MovePlayer of tick c+97 if both are in the same frame. The new aura comes at the next frame's first tick: the aura is absent for N-1 ticks (20 ms each) once per 97 ticks of a held charge, only when a frame straddles c+96/c+97. A test must pin this gap.
  2. **RNG order.** The hold-spark draws happen at MovePlayer time, before the hit-spark and vessel draws of earlier ticks of the same frame, which are drawn at `RunFrame`. The binary interleaves them tick by tick.
  3. **Slot index.** The hero's slot is taken before the queued rank-0 (map events) and earlier-tick operations of the frame. Only the draw order of two equal-key slots and a full pool are affected.
- Cost: one stamp field, one `Tick` overload, three stage methods. No existing trace or test changes (the stamp is 0 for everything else).

**Optional hardening to remove deviations 1 and 2 (INFERENCE, decision for the lead): lazy catch-up.**
- The stage keeps a cursor of ticks already run. Before the hero acts at tick k it runs the ticks not yet run, up to k-1, with the queued operations of ticks <= k-1.
- It needs the gate list during the frame: the proxy would bind `_effectFrozenOfTick` to the stage once. `RunFrame` would start from the cursor.
- This makes the stamp unnecessary and the hero's reads exact for any N. It touches the stage and one line in the proxy, and changes the behaviour of a frame in which a warp departure starts mid-frame (those ticks would have run). It is a candidate to bundle with E20.e0b, which also needs a per-tick end-of-tick point (the effect update, then the generator advance).

## 4. API changes (E20.f1 T2)

- `IAlundraScriptHost.cs` (next to `ItemTables`, `:107`): `AlundraEffectStage? EffectStage => null;`. `AlundraWorldProxy` needs no change: its public property (`:557`) implements this and `IEntityWorldContext.EffectStage` (`IEntityWorldContext.cs:182`). Test support: `ContactHost` (`Alundra.Tests/AlundraContactTestSupport.cs:41`) gets `public AlundraEffectStage? EffectStage { get; set; }`.
- `AlundraEffectStage` (public, as ADR-0046 made it): `CreateNow(bool mapTable, int effect, int animation, int x, int y, int z)`, `CreateAttachedNow(bool mapTable, int effect, int animation, IAlundraEffectAnchor anchor, int depthOffset, int offsetX, int offsetY, int offsetZ)`, `FreeNow(AlundraEffectSlot)`. Each `Create*Now` stamps the slot; no closure, so no allocation per tick. `RunFrame` calls `_reservoir.Tick(frozen, tick)`; the one-argument `Tick(bool)` stays (called by `AlundraEffectReservoirTests.cs:128`, `:285-292`).
- `AlundraEffectSlot`: `internal int FirstUpdateTick`, reset to 0 in `CreateCore` (`Reservoir.cs:416-465`).
- Hero (`AlundraEntityScriptProxy`): `internal int WeaponChargeTimer` (`0x80126FFC`), `internal AlundraEffectSlot? ChargeAuraEffect` (`0x80126FF8`), both next to `WeaponStepCounter` (`:398`). The proxy implements `IAlundraEffectAnchor` explicitly: `PosX`, `PosY`, `PosZ => PosZ + 1` (ADR-0026), `Status => (int)Status`, and `DepthKey` (see the dependency below).
- `AlundraPlayerManager`: `UpdatePlayerWeaponEffect` becomes `internal static (player, in pad, host)`, called at `:275`. The hero loop in the self-driven mode (`AlundraEntityScriptProxy.cs:1385-1398`) has no `BeginSource`; add `stage.BeginSource(tick, 1)` there for rig tests.
- Core of the charge, in `unchecked`:
  ```
  jx = (int)(((ulong)r1*0x60001)>>32) - 0x30000;  jy = (int)(((ulong)r2*0x40001)>>32) - 0x20000;
  spark.X += 20*jx; spark.Y += 20*jy; spark.ForceX = player.ForceX - jx; spark.ForceY = player.ForceY - jy;
  ```
  with `r1`, `r2` the two `(uint)AlundraRandom.Next()` taken only if `CreateNow(false, 0x10, 0, PosX, PosY, PosZ + 1 + 0x100000)` returned non-null, after the sound.
- Aura: `if (handle is null || handle.Destroying) handle = stage?.CreateAttachedNow(false, 3, 0, player, 1, 0, 0, 0x100000);`. The handle is assigned even when the result is null, like `0x8002F5BC`. Free branch: `reservoir.Free(handle)` unconditionally, handle = null.
- A host without a stage: charge counting, sound `0x2A` and the free logic still run; the aura is never created; the spark counts as "not created" (no sound `0x1AC/0x1AD`, no draws). INFERENCE: a deterministic degradation; production always has a stage.
- The burst (T4) uses the same `CreateNow` and the same Z (`PosZ + 1 + 0x100000`).
- Dependency (not mine): `DepthKey` of the hero anchor is the binary `+0x1BC`. The DLL field labelled `ZUpperBound//1bc` (`AlundraEntityScriptProxy.cs:407`) is probably not it. The `depth-key` report decides. It affects draw order only, not the logic of this model.
- New ADR 0056: the hero writes the reservoir at once (it reads it), its place in the update, the three N >= 2 deviations, the stale-handle decision.

## 5. Test design

New class `AlundraHeroChargeEffectTests`, in the collection `AlundraRandomStaticStateCollection` (the static generator; the tests set and restore `RandomSeed`). Stage over `EffectGolden.BankOf(Root, 163)` (carries the real global companion with effects 3 and 16). A small harness `Tick(k)` does: `stage.BeginSource(k, 1)`, `UpdatePlayerWeaponEffect(hero, pad, host)`, then the counters-pass increment (`hero.FrameCounter++`). A frame ends with `stage.EndSource(); stage.RunFrame(N, new bool[N])`. Count of aura slots = active slots with `!TargetMapTable && TargetEffect == 3`.

1. **Timer 10 -> 11 -> 12 in one 2-tick frame.** Square held, weapon record byte 0 non-zero, current animation with flag b0. Expect after `RunFrame`: exactly 1 aura; slot index 0; `Mode == 1`; `Anchor == hero`; `OffsetZ == 0x100000`; `DepthOffset == 1`; two updates done (tick c and c+1). Same final snapshot (index, Active, Mode, X, Y, Z, ForceX/Y, `CurrentImageSetIndex`, `Destroying`) as two 1-tick frames, and as 3-tick frames.
2. **Drop below 11.** Timer 12, then a tick with Square neither held nor just released: the timer goes to 0, the aura is freed at once (count 0 before `RunFrame`, handle null) and stays 0 after.
3. **Release.** At the release tick (`Released` edge, `Hold == 0`) the timer still counts and the aura stays; the next tick (animation with b0 clear, or no Square) frees it. Compare with the oracle once it journals the pool (section 6).
4. **Renewal at c+97.** With 1-tick frames: the aura index changes at tick c+97 and exactly one aura is active after that tick's update (old one freed, new one alive). Expect the freed tick and drawn range from `anim_table.tsv` (`3/0`: drawn 0..96, freed 97).
5. **Pin of deviation 1.** A frame holding ticks c+96 and c+97: the aura count is 0 after that frame's `RunFrame`... precisely, the slot is freed and not yet replaced; the next frame creates it. Assert the exact N-1 tick gap.
6. **Hold spark.** Seed fixed, timer 60, `FrameCounter & 3 == 0`: a second tick cadence of 4; sound `0x1AC` at `& 7 == 4`, `0x1AD` at `& 7 == 0`. Compare the two `RandomSeed` values (state after = r2) and the slot position and forces against the upgraded oracle's journal.
7. **Pool full.** Fill the 128 slots with other effects: the aura handle stays null, no exception; at timer 60 no sound `0x1AC/0x1AD` and `RandomSeed` unchanged (draws only if created). Free one slot: the next tick creates.
8. **Stale handle.** Aura expires by itself during a `0x34` lock (the hero code does not run), its slot is taken by an unrelated effect, then timer < 11: assert the unrelated effect is freed. This pins the binary's behaviour or, if the author chooses the guard, flips to "survives".
9. **Mutation (red first).** Replace `CreateAttachedNow` by an `Emit`-based creation: test 1 must fail (two auras in the 2-tick frame). Drop the `FirstUpdateTick` stamp: the N=2 snapshot of test 1 must differ (an extra update).
10. **Wiring.** One test through `AlundraWorldProxy.Update` with 0.04 s frames (2 ticks per frame, as `AlundraDialogueBoxOrderTests.cs:189` does for 3) holding Square on a real arc, asserting one aura across timer 10 -> 12.

## 6. What the oracle must record (for T1 and the acceptance)

- Replace the dummy stubs by the real pool code, or by `docs/plan-e19-g1g3-annexe/fxpool.py`. Running the real `0x8003B9C4`, `0x8003BB14`, `0x8003BDD8`, `0x8003BE74` needs nothing but RAM. Running the real `0x8003C410` (Animate) needs the effect bank descriptor `0x80126EC0` loaded; the earlier G1/G3 work did exactly that for 363 animations (`binary-data-verify.md` row 4).
- Journal per tick: the word `0x80098708` before and after each effect call, the slot fields `+0x34/+0x38/+0x3C/+0x40/+0x44/+0x48/+0x4C/+0x50/+0x54/+0x58/+0x5C/+0x60/+0x68/+0x7D` and the handle `0x80126FF8` and slot index, and the call order of `0x8003C410` after the hit pass.
- New probes: hold >= 140 ticks after the aura (renewal at c+97); a pool-full probe (pre-fill 128 slots); a lock interrupt (`0x34` held >= 97 ticks, then released) for the stale handle.
- Run order for T1's open question: neither the oracle nor the DLL advances the generator per frame today, so spark-to-spark comparisons agree; E20.e0b changes both.

## 7. Stop conditions and engine

- No engine change is needed. FACT: the quad push already handles any active slot including mode 1 (ADR-0049); `EffectQuadService.SetFrame` takes a span with no visible capacity limit (`EffectQuadService.cs:58`). The aura has up to 8 quads per frame (`anim_table.tsv`, max_quads 8).
- Stop (per the plan) only if effect 3 or 0x10 data were missing: both are in the global companion (`effects-global.json`, 29 effects, 3 and 16 present).

## 8. UNKNOWN

- Whether the stale-handle free ever fires in the story: the lock/`Square`/slot-reuse chain exists in code but I did not look for a map where it happens. It needs the author's choice (reproduce or guard).
- The exact value of the hero depth key (`+0x1BC`) at the moment `UpdateEffects` reads it; that is the `depth-key` report's job.
- Whether the Z convention of the hero's `PosZ + 1` makes the aura land on the same pixel row at integer positions; the draw-time comparison needs the author's recipe.
- The `& 0x48` read position (before versus after the entity block): pre-existing, not changed by Model S.
