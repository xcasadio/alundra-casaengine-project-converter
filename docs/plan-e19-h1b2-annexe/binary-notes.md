# E19.h1b2 discovery, surface "binary": 0x20 / 0x22 / 0x23 and the vertical step of entities without a controller

Read-only. Repo `chantier/e19-suite` (HEAD `21db852`). Nothing built, run (no test, no export), edited, staged or committed in the repository;
`CasaEngine.Launcher/Program.cs` untouched. Everything written lives in this directory. Ground truth = `ALUN_CD.EXE` (France).
Tags: **[bin]** read in the disassembly (capstone, `../../e19j-disc/lib.py`, `../../f2b1-disc/oracle/bdis.py`); **[emu]** observed by RUNNING the real
binary code in the MIPS interpreter (`zsim.py` on top of `mips.py`: real physics pass `0x80038364`, real collidable-list builder `0x800384F4`, real
handlers `0x16 0x17 0x1B 0x20 0x21 0x22 0x23 0x25 0x62 0x63`; the script loop of `RunScript 0x8004205C` is 14 lines of python, read at
`0x80042280-0x80042344`; opcode `0x64` re-implemented from `0x8003F610`); **[model]** `zmodel.py` (pure python), equal to [emu] on random corpora (section 5);
**[code]** read in the repository; **[hyp]** hypothesis. Values are 16.16 raw integers (1 px = 65536). "DLL" = the convention of ADR-0026 (absolute Z = binary - 1).

## Summary

| # | Fact | Evidence |
|---|------|----------|
| 1 | `0x20` (size 3) is a pure WAIT on the logic entity: first call at a pc memorises pc and `PosZ` and returns 0; later calls return 3 once `(|memo - PosZ| >> 16) >= (v1 \| v2 << 8)`. Nothing else is read (not `CollidedWithEntityZ`). | `0x8003D9BC-0x8003DA24`; [emu] `t_handlers.out` |
| 2 | `0x22` (size 1): first call memorises `Parameters[2] = record.Height << 19` (record byte 9, **literal, no shift**); later calls: equality -> 1, else one-sided clamp of `ForceZ` toward the target, 0. It never pushes. `0x23` = `0x22` or `CollidedWithEntityZ` (also at the first call). `0x21` = `0x20` or `CollidedWithEntityZ`. | `0x8003DA70-0x8003DB6C`; [emu] |
| 3 | Per-tick order: scripts (`0x800386D0`) -> lists (`0x800384F4`) -> UpdateAnimation (`0x80038E18`) -> physics `P 0x80038364` (forces for all, riders, `MoveEntity` per entity = **`ComputeZ` then `ComputeXY`**, end pass) . A `1B` written by a script moves the entity IN THE SAME tick. Every wait reads the previous pass. | `0x8003B388`, `0x80038364` |
| 4 | The binary has NO controller notion. Entities "without a controller" are prefabs whose three sizes are 0 (a POINT box: width/height/depth 0) or (1,1,1); they go through exactly the same `P`. | `0x80039C40`, `0x80037F28` |
| 5 | Z rules (binary convention): decay `ForceZ - (G << 8)` bounded on BOTH sides to `+-(V << 8)` (gravity bit only); `FinalForceZ = ForceZ`; `F > 0`: cap `0x7800000` else `PosZ += F`; `F <= 0` (F == 0 included): lands iff `PosZ + ModZ + F <= T` -> `PosZ = T + 1 - ModZ`, contact, `ForceZ = 0` with gravity; else `PosZ += F`; end pass `IsOnGround = !(T + 1 < PosZ)` on **PosZ, not PosZ + ModZ**. | [bin] + [emu]; [model] validated |
| 6 | Map gravity / viscosity are the header words `+4` / `+6` of `*(0x800E4334)`, read LIVE every tick for every gravity entity (no per-entity copy, no store anywhere). Corpus: 481 maps 128/4096, maps 159 and 160 3/256. | `0x80036904`, `0x80036B1C`; `xref_maphdr2.out`; census |
| 7 | The two controller-less Z-wait sites of the whole corpus: **47 C[4] @442** (`20 [96,0]`, Sara rec3, falls 0.5 px/tick: returns at the 192nd call after the first) and **260 C[5] @747** (`20 [32,0]`, I33 rec17, rises 0.5 px/tick: 64th call). Both are pure integration far from any ground: no terrain contact, no float root. | [emu] on the real maps and prefabs: `scen_real.out` |
| 8 | No story-chain arc is affected: no `0x20-0x23` is reached on the 30 chain maps; the 5 controller-less chain records (476 rec0, 135 rec26, 173 rec4, 180 rec4, 185 rec10) have no gravity and are not below their terrain, so the `F = 0` landing test never fires. | `chain_scan.py`, probes below |
| 9 | `0x22` with no record (`+0x44 == 0`): the retail error routine `0x8003C840` only calls empty `printf` stubs (`0x800815EC/F4/FC` are `jr ra`) then BIOS B(0x38) (hyp: `exit`), then the code reads byte 9 at address 9. No corpus site. | `0x8003DAA8-0x8003DAB4`, `0x8003C840-0x8003C8FC` |
| 10 | In the DLL convention every literal `Height << 19` target (all 17 sites: multiples of 524288, below 2^27) is exactly representable and reached by 32768-unit steps from a terrain rest `T` (a multiple of 65536); simulated on the float root at 304, 240, 176, 112 and 80 px. The shifted target `(H << 19) - 1` stalls at 304 px (127 rec17/18) and works below 256 px. | `float_root.out` |

## 1. The handlers, re-read

### 1.1 Calling convention [bin]
`RunScript` loop `0x80042280-0x80042344`: `ptr = state.curPtr` (`state + 4`), `logic = *(owner + 0x230)` reloaded before EVERY instruction, `op = byte(ptr)`;
`0xFF` ends the call; `0x00` = `state[8] = 0` (`Parameters[1]`), `ptr + 1`, ends the call; otherwise `r = table[op](a0 = logic, a1 = owner, a2 = &state.curPtr, a3 = state)`;
`r == 0` ends the call (the same opcode runs again at the next tick); `r != 0`: `Parameters[1] = 0`, `ptr += r`, next opcode in the SAME call.
Table `u32(0x80098FAC + 4 * op)`. Offsets: `+0x11C` PosZ, `+0xB8` ForceZ, `+0xEC` FinalForceZ, `+0x13C` ForceAdjusted, `+0x140` CollidedWithEntityZ, `+0x144` IsOnGround,
`+0x44` entity record, record byte 9 = Height, `+0x6C` flags (`0x100` Gravity, `0x80` collidable subject), `+0x1EC` ModZ, `+0x1F8` Depth.

### 1.2 `0x20` @ `0x8003D9BC`, size 3, returns 0 or 3
- `Parameters[1] (a3+8) != *a2` (the opcode ADDRESS, never 0): `Parameters[1] = pc`, `Parameters[2] = logic.PosZ`, **return 0** (`0x8003DA10-0x8003DA20`). The first call never ends the wait, even with a limit of 0.
- else `d = |Parameters[2] - PosZ|` (32 bits), `d >> 16` (`sra`), `limit = lbu(pc+1) | lbu(pc+2) << 8` (unsigned 16-bit, in PIXELS), `slt` signed: returns **3 when `(d >> 16) >= limit`**, else 0 (`0x8003D9D0-0x8003DA0C`).
- Reads nothing else (`+0x140` is NOT read: the size table's "and collided Z" is wrong). Writes only `Parameters[1]`/`[2]`. The memo is NOT cleared by the handler: the loop clears `Parameters[1]` when the opcode returns non-zero.
- [emu] `t_handlers.out`: first call PosZ 3145728 -> memo 3145728 and 0; 4194303 (15.99 px up) -> 0; 4194304 -> 3; 2097152 (16 px down) -> 3; 2097153 -> 0; `CollidedWithEntityZ` 1 with PosZ unchanged -> 0;
  limit `[0,0]`: first call 0, second call 3; limit `[0,1]` = 256 px: 255.99 px -> 0, 256 px -> 3.
- DLL today [code]: `AlundraEventProgramRunner.WaitZDistance` (~2476) is the same function (memo on `CodeIndex`, `>> 16` of the difference, unsigned operands) and `0x21` already calls it (~721-725); `0x20` itself is still skipped. Pitfall already known: a memo keyed on `CodeIndex` collides with the cleared 0 when the opcode sits at pc 0 (never in real data).

### 1.3 `0x21` @ `0x8003DA28`, size 3
`r = 0x20(...)`; `r != 0` -> 3; else `+0x140 != 0` -> 3; else 0. Ends at its FIRST call when the contact flag is already 1 (the memo is still written). [emu] confirmed.

### 1.4 `0x22` @ `0x8003DA70`, size 1, returns 0 or 1
- first call at the pc (`Parameters[1] != pc`): `Parameters[1] = pc`; `s0 = logic.+0x44`; if null: `jal 0x8003C840` (see 1.6); `Parameters[2] = byte(s0 + 9) << 19`; **return 0**. No equality test at the first call.
- later calls: `target == PosZ` (+0x11C, NOT ModdedPosZ) -> **return 1** (ForceZ untouched); else `delta = target - PosZ`: `delta > 0`: if `delta < ForceZ` then `ForceZ = delta`; `delta <= 0`: if `ForceZ < delta` then `ForceZ = delta`; return 0 (`0x8003DAC8-0x8003DB0C`).
  So it only CLAMPS (never pushes, never reverses): an entity at rest or moving away from the target keeps its force and the wait never ends.
- Target = `Height << 19` literal: record Height 4 -> 2097152, 38 -> 19922944 (304 px), max 255 -> 133693440 (fits 31 bits). **There is no `- 1` in the binary**: the binary compares it with a PosZ whose terrain rest is `T + 1`.
- [emu] clamp table (target 2097152): (PosZ 1048576, ForceZ 1572864) -> 1048576; (1048576, 524288) -> 524288; (3145728, -2097152) -> -1048576; (3145728, 65536) -> 65536 (never pushed); (2097152, any) -> returns 1;
  (2097153, 0) -> 0, ForceZ 0; (2097151, 5) -> ForceZ 1; (3145728, -1048577) -> -1048576. Random check: 3000 call pairs, real handler = `zmodel.op22`/`op20`, 0 differences.
- Side effects: writes `Parameters[1]`, `[2]` and the logic entity's `ForceZ`.

### 1.5 `0x23` @ `0x8003DB28`, size 1
`r = 0x22(...)`; `r != 0` -> 1; else `+0x140 != 0` -> 1; else 0. [emu]: with the contact flag 1 it ends at its first call.

### 1.6 Record-less `0x22` (hyp for the BIOS part)
`0x8003DAA4-0x8003DAB4`: `s0 == 0` -> `jal 0x8003C840` with `a0 = 0x80023CB0` ("No InitData."), then `lbu 9(s0)` anyway. `0x8003C840` calls `0x800815EC` and `0x800815FC` several times: **both are `jr $ra; nop`** (the retail `printf`/`puts` are stubbed) and ends with `jal 0x800831E0` (`a0 = 1`), the BIOS B-function stub `t2 = 0xB0, t1 = 0x38`. B(0x38) is `exit` in the PS1 BIOS function table [hyp: reference tables, not verified in this repo], so the retail game would leave to the BIOS shell. No corpus site has a record-less `0x22` logic entity (hero only if both `0x43` fail, impossible on the paths).

### 1.7 Writers of the force the waits depend on
- `0x1B` @ `0x8003D7D0` (size 3): `ForceZ = sext16(v1 | v2 << 8) << 8` ([128,0] = +32768, [128,255] = -32768, [0,255] = -65536, [0,1] = +65536). Writes `+0xB8` of the LOGIC entity directly. [emu] `t_handlers.out`.
- `0x16` @ `0x8003D774` sets flag `0x100` (Gravity), `0x17` @ `0x8003D78C` CLEARS it (the label "Low gravity" is wrong), size 1. `0x63 [128,128,1]` @ `0x8003F590` = `flags &= ~0x0180` of the searched entities (Gravity and the collidable bit), size 4.
- Because the force pass runs AFTER the script in the same tick and applies the decay to a gravity entity, a `1B` on an entity that still has Gravity is eaten the same tick (Z-8 below: `1B [128,0]` -> ForceZ 0). All real sites clear gravity first (`17`, `63`).

## 2. The vertical step of every entity (O-E19-7)

### 2.1 Tick order [bin] `0x8003B388`
`0x80038634` (status promotion) -> `0x800386D0` (scripts, `RunScript` per entity) -> `0x80038998` -> `0x800384F4` (active list `0x80127B28`: status 2 or 3, `+0x20 == 0`; collidable list `0x80127C28`: flag `0x80`, `AnimFlags (+0xB4) & 0x80 == 0`, `+0x28 == 0`)
-> `0x80038E18` (UpdateAnimation: `+0xF8` impulse only at a switch) -> `0x80038364` physics -> `0x80038E84`, `0x80039300`. Map events run even earlier, in the main loop (`0x8002E100` before `0x8002E108`, earlier discoveries).

### 2.2 `P` @ `0x80038364`
1. pre-pass (each active entity): `+0x168 = 0`, **`CollidedWithEntityZ (+0x140) = 0`**, `ForceAdjusted (+0x13C) = 0`, ModdedPos = Pos + Mod.
2. riders `0x800364C8`; **forces `0x80036828`**; rider copy `0x80037364` (entities with `+0x12C`).
3. `MoveEntity 0x80037E34` per entity (skipped when already moved): attached entity (`+0x28`) copies its parent; else **`ComputeZ 0x800375E0` THEN `ComputeXY 0x80037730`**.
4. end pass `0x80038064` per entity: `FloorHeight (+0x134) = 0x80037F28`, **`IsOnGround (+0x144) = !(FloorHeight < PosZ)`** (`0x800380F8-0x80038108`), then the water/slope bookkeeping.

### 2.3 Forces `0x80036828`, non-hero branch `0x80036A84-0x80036BBC` [bin], values [emu]
- attached (`+0x28`): all forces zeroed.
- `+0xF8` (impulse word) != 0: if `lhu == 0x8000` and no Gravity -> `ForceZ = 0` (the iron-grid stop marker) else `ForceZ = IZF << 8` (no decay on that tick).
- else Gravity: `v = ForceZ - (G << 8)`; `lim = V << 8`; **if `|v| > lim` then `v = +lim` (v > 0) or `-lim`** (two-sided; the DLL already bounds both sides since E19.d2c1 R3, `AlundraEntityScriptProxy.cs` ~690-700; the intro harness bounds one side, `ITH:979-990`, harmless: a rising gravity entity never occurs there).
- no Gravity: `ForceZ` untouched (a script force persists).
- then horizontal forces (`0x80036614`, `0x800367E4`, `0x800366FC`), `FinalForceZ (+0xEC) = ForceZ` (`0x80036BB8`), `FinalForceX/Y = (+0xDC/+0xE0)`.

### 2.4 `ComputeZ 0x800375E0` [bin] + [emu]
`F = FinalForceZ`.
- `F > 0` (`0x800375FC-0x80037688`): terrain probe `0x800370C4` -> `+0x138`; ceiling/cap `0x80036D94`: `target = ModdedPosZ + Depth + F`; **`target > 0x7800000` -> `0x77FFFFF`, hit, before any gate**; then (flag `0x80`, `AnimFlags & 0x80 == 0`, no platform) the entities above. Hit: `CollidedWithEntityZ = 1`, `PosZ = out - ModZ - Depth`, `ForceZ = 0` with Gravity. No hit: `PosZ += F`.
- `F <= 0` (`0x8003768C-0x80037714`, **F == 0 included**): probe -> `+0x138`; landing test `0x80036BFC`: `t1 = ModdedPosZ + F`; terrain lands iff `T >= t1`, then `t1 = T + 1`; a subject entity (flag `0x80`, `AnimFlags & 0x80 == 0`, no platform) also scans every other collidable `c` (list `0x80127C28`, count `0x80127D2C`): candidate iff `top = c.ModdedPosZ + c.Depth` satisfies **`t1 <= top < self ModdedPosZ`** (`slt top, selfModded` and not `slt top, t1`, `0x80036CC8-0x80036CD8`) and the XY boxes overlap half-open (`dx >= 0: dx < self.Width + 1`, else `-dx < c.Width + 1`, same in Y); the highest such top wins (`t1 = top + 1`). Landed: `CollidedWithEntityZ = 1`, `PosZ = t1 - ModZ`, `ForceZ = 0` **only with Gravity** (`FinalForceZ` keeps the tick's force). Not landed: `PosZ += F`.
  Consequences [emu]: reaching `T + 1` exactly is NOT a landing (IsOnGround 1, contact 0, one tick before the contact); without Gravity a constant negative force "lands" every tick once at rest (contact 1 every tick); at rest WITH gravity `FinalForceZ = -32768` every tick, contact 1 every tick, `ForceZ` 0 at the end of the tick.
- An entity with F == 0, no gravity, 1 unit BELOW the terrain is lifted to `T + 1` with a contact at the next tick (Z-6); exactly at `T + 1` nothing happens (Z-6b).

### 2.5 `ComputeXY 0x80037730` for an entity with no XY force
Returns at once (`0x800377A0-0x800377A8` -> `0x80037DC0`) when `FinalForceX` and `FinalForceY` are both 0: only recomputes ModdedPos from the new PosZ and re-probes the terrain (`+0x138`). **No 3 px magnet** (the magnet, `0x80037848-0x80037904`, needs an XY force). So for a pure Z entity `TerrainHeight` = the probe at its position, written every tick (also by the Z step).

### 2.6 End pass [bin] [emu]
`FloorHeight (0x80037F28)`: `T + 1`; a non-subject entity returns it at once; a subject entity (flag `0x80`, `AnimFlags & 0x80 == 0`, no platform) raises it to `top + 1` of the highest collidable below its own ModdedPosZ with XY overlap. `IsOnGround = FloorHeight >= PosZ` (PosZ `+0x11C`).
ModZ: the binary compares `PosZ`, the hero's R6 rule in the DLL compares `PosZ + ModZ` (`AlundraScriptedMotion.cs:215`): identical while ModZ == 0 (hero; all 12 controller-less prefabs have OffsetZ 0, census), different for a prefab with OffsetZ > 0 (13 of 395 prefabs).

### 2.7 DLL convention (ADR-0026) of these rules [model] `zmodel_dll.py`
Binary state `PosZ_b = PosZ_dll + 1`; terrain `T` and the force values do not change. Rewritten natively (checked equal to binary - 1 on 200000 random 6-tick runs, ModZ, Depth, cap, impulse included):
`F > 0`: if `PosZ + ModZ + Depth + 1 + F > 0x7800000` -> `PosZ = 0x77FFFFE - ModZ - Depth`, contact (the hero's H3-R2 cap); else `PosZ += F`.
`F <= 0`: if **`PosZ + ModZ + F < T` (strict)** -> `PosZ = T - ModZ`, contact, `ForceZ = 0` with Gravity; else `PosZ += F`. `Floor = T` (terrain) or `top + 1` (entity top, DLL top = `PosZ + ModZ + Depth`); `IsOnGround = PosZ <= Floor`.
Decay, impulse and `FinalForceZ` are unchanged. Entity rest is `top + 1` in both conventions' terms (DLL `PosZ = c.PosZ + c.ModZ + c.Depth + 1`).

### 2.8 What the controller-less set really is [census] `census_nc.out`
12 of 395 prefabs have no controller in the export (3 sizes > 0 and `min(SizeX, SizeY) / 2 > 0.5` fails): 11 are point boxes (0,0,0), 1 is (1,1,1) (key trigger "tres petit"). 67 records use 6 of them. **7 have Gravity (Sara 41 rec1, 42 rec1, 47 rec0, 47 rec3; I33 11 rec33, 260 rec17, 398 rec9) and all 7 spawn exactly at rest (z == T): none falls at spawn.** The other 60 have no gravity; 8 of them float above their terrain by design.
Real terrain under them was measured with the real probe `0x800370C4` on the real cells (flat approximation of earlier notes confirmed on these records).

## 3. Real cases (real maps, real prefab flags, real physics pass, real handlers) `scen_real.py` -> `scen_real.out`
Tick convention: `t = 0` is the tick in which the program's first call runs (script phase), the physics of that same tick follows.

### 3.1 R260: map 260 rec17 (I33, flags `0x230180`, size 0, terrain 0 px), C[5]: `63 [128,128,1]`, `1B [128,0]`, `20 [32,0]`, `BD`, `1B [0,0]`, `FF`
t0: `63` clears `0x180`, `1B` ForceZ 32768, `20` memo (= rest PosZ); physics: PosZ +32768 in the SAME tick (binary 32769, DLL 32768). t = 1..63: +32768 per tick (binary 2097153 / DLL 2097152 after t = 63).
**`0x20` returns 3 at the script call of t = 64** (64 ticks after the first call; total rise 2097152 = 32 px exactly), then `BD`, `1B [0,0]`, `FF` run in that call; from t = 64 the entity stays (ForceZ 0). Rest before: IsOnGround 1, contact 1, FinalForceZ -32768 each tick.

### 3.2 R47: map 47 rec3 (Sara, flags `0x830180`, terrain 16 px at (264,192)), C[4]: `17`, `64 [128,8,1,192,0,160,0]`, `1B [128,255]`, `20 [96,0]`, `1B [0,0]`
t0: `17` clears Gravity; `64` PosZ = (160 << 16) + 1 (binary; DLL h1b1: 10485760); `1B` ForceZ -32768; physics -32768. t = 191: PosZ 4194305 (DLL 4194304 = 64 px). **`0x20` returns 3 at t = 192** (192 ticks after the first call, 96 px exactly); the terrain (16 px) is never reached, no contact, IsOnGround 0 all along. Afterwards Sara hovers at 64 px (gravity stays off).
At 50 Hz PAL: 64 ticks = 1.28 s, 192 ticks = 3.84 s of scene pacing; with `0x20` still skipped today both scenes run on at once.

### 3.3 R127: map 127 rec17 (iron ball, flags `0x32002`, box 24x16x16, terrain(probe) 208 px, target 19922944 = 304 px), loop `16`, `25`, `17`, `1B [128,0]`, `22`, `1B [0,0]`
Binary: spawn PosZ 19922945; t0 `16` (Gravity), `25` first call 0; falls: PosZ t19 = 13631489 = T + 1 with a landing (ForceZ -655360 -> 0, contact 1, IsOnGround 1); t20 `25` returns 1, `17`, `1B`, `22` memo, PosZ 13664257;
rise +32768 per tick: t210 PosZ 19890177 (target - 32767), **t211 `22` clamps `ForceZ = 32767`** (delta 32767 < 32768) and PosZ = 19922944 = target exactly, **t212 `22` returns 1** (192 ticks after the first call: 191 full steps + 1 clamped step).
DLL convention (rest at T = 13631488, spawn 19922944): the same 192 ticks with 192 FULL steps of 32768 and no clamp: `PosZ = T + 192 * 32768 = 19922944` = the literal target.

### 3.4 Synthetic montage values (flat terrain, one controller-less proxy, no XY force), DLL convention = binary - 1: `traces.out` (asserted [emu] == [model] == [model_dll] + 1 tick by tick)
- **Z-1** rise, no gravity, `ForceZ 32768` from rest `PosZ 0`: PosZ 32768, 65536, 98304, ... ; contact 0, IsOnGround 0 from the first tick.
- **Z-2** fall, no gravity, `ForceZ -32768` from 160 px (`10485760`) over a 16 px terrain: tick k PosZ `10485760 - 32768 k`; tick 192: 4194304; contact 0, IsOnGround 0; `0x20 [96,0]` first call in the script phase of the tick whose physics is tick 1, returns 3 at the script call that follows physics tick 192.
- **Z-3** gravity entity from 5 px (`327680`) over flat 0, G 128 V 4096: PosZ 294912, 229376, 131072, **0 at tick 4 with ForceZ -131072, IsOnGround 1, contact 0**; tick 5: ForceZ 0, FinalForceZ -163840, contact 1; then FinalForceZ -32768 and contact 1 every tick.
- **Z-4** at rest with gravity on flat 0: PosZ 0, ForceZ 0, FinalForceZ -32768, contact 1, IsOnGround 1 every tick. **Z-5** at rest without gravity: contact 0, IsOnGround 1.
- **Z-6** no gravity, F 0, 1 unit below a 16 px terrain (`1048575`): tick 1 PosZ 1048576, contact 1, IsOnGround 1; tick 2 contact 0. **Z-6b** exactly at `1048576`: nothing happens (strict landing).
- **Z-7** terminal velocity, G 128 V 4096 from 600 px: ForceZ -32768 k up to **-1048576 at tick 32** and constant after; PosZ tick 30 24084480, 31 23068672, 32 22020096, 33 20971520; lands at tick 78. **Z-7b** maps 159/160 (G 3, V 256): ForceZ -768 k, clamp **-65536 at tick 86** (k = 85 -> -65280).
- **Z-8** `1B [128,0]` on a gravity entity at 10 px with no `17`: tick 1 ForceZ 0 (the decay eats it), PosZ unchanged 655360; then it falls (622592, 557056, 458752).
- **Z-9** cap: no gravity, `ForceZ 64 px`, SizeZ 32 (Depth 2097151), from 1792 px: tick 1 PosZ 121634816, tick 2 **123731967 = 0x77FFFFE - 2097151**, contact 1 every tick, ForceZ unchanged.
- Entity support (two real entities, `t_support.out`): a point entity (size 0, gravity) dropped from 40 px over a crate (24x16x16, rest `PosZ 0`) at x = crate centre: PosZ ... 1146880 (tick 9), **1048576 at tick 10 with a landing (ForceZ 0, contact 1, IsOnGround 1)**, FloorHeight `top + 1`; x in `[88, 112)` overlaps (88 and 111.99 land), 87.99 and 112 fall through to the terrain (half-open).

## 4. Float root of `0x22` (controller entities only) `float_root.py` -> `float_root.out`
Model of the DLL controller path: `root.Z (float32) += F / 65536f`; `PosZ = round(root.Z * 65536)`; binary handler. Assumption: `Controller.Move` adds the Z displacement exactly in float32 (engine not read).
From a terrain rest `T` (even): target `H << 19`: equal at the expected tick for ALL 17 sites (304, 240, 176, 112, 80 px tested), no clamp. Target `(H << 19) - 1`: equal at every site below 256 px (the last step is the 32767 clamp), **never at 304 px**: last step puts the root at 303.99999237 -> float32 304.0 -> PosZ 19922944, `delta = -1`, `ForceZ (32767) < -1` false, no clamp, the ball rises past the target for ever (20185088 after 2000 ticks).
float32 spacing: 1 unit up to 256 px, **2 units from 256 px**. Only 127 rec17/18 (Height 38) are >= 256 px among the 17 `0x22` sites. A controller-less entity has no float root pull (integer PosZ, root derived from it): the issue does not exist for it.

## 5. Model and validation
- `zsim.py`: memory image = real cells of any exported map (`walkability | ground_property << 8` at `TILES + 0x604 + row * 416 + col * 8`, slope byte, height byte), real map header (G, V), real entity layout; runs the real `0x800384F4`, `P`, handlers.
- `zmodel.py`: the rules of 2.3-2.6 in 60 lines. `validate.py`: **460 scenarios x 40 ticks (400 random + 60 directed landing-equality cases; rest, drops, forces, gravity toggles, absolute PosZ writes, impulses incl. the `0x8000` marker, caps, ModZ, Depth, G/V pairs 128/4096, 3/256, ...): 0 mismatch on PosZ, ForceZ, FinalForceZ, contact, IsOnGround, FloorHeight, TerrainHeight**; 3000 random call pairs of `0x20`/`0x22` against the real handlers: 0 mismatch; mutations caught: one-sided terminal (311/460), non-strict/strict landing (30/460, directed cases), `IsOnGround` on PosZ + ModZ (87/460).
- `zmodel_dll.py`: native DLL formulas = binary - 1 on 200000 random runs.
- Not in the model (emulator only): entity support/ceilings with other entities (`t_support.out`), XY motion and the 3 px magnet, UpdateAnimation switches.

## 6. Plan needs (what to write in advance, how)
Rules: section 2.7 (DLL), tick order 2.1, handlers 1.2-1.5. Values: 3.1-3.4 (model for synthetic montages, simulation for real maps). Closed lists:
- controller-less Z waits: `{47 C[4] @442 (0x20), 260 C[5] @747 (0x20)}` (static guard goes empty once the step exists). The census tool `noctrl.py` (E19.h) also printed 388 B[2] @183/@401 and 115 B[2] @220/@367 as targeting controller-less records: **false positives**, read the programs: `43 [1]` (rec1 Zorgia, controller) precedes both 388 sites, `43 [19]` (rec19 block, controller) precedes the 115 ones.
- map 178 C[3] @386 holds a `0x22` (the walk-based census says none on the chain): no record of 178 has C index 3 or 131 and no `0x40` exists there, so it is a dead program; not reached.
- chain controller-less records, probed with the real terrain probe: 476 rec0 Rancune at z 48 px with positions (960,176), (0,176), (792,176) -> T = 0, 48, 0 px (at x = 0 it is exactly `T + 1` binary, `T` DLL: no landing), 173 rec4 / 180 rec4 z 16 over T 16 (x -1/-2 px: T unchanged), 135 rec26 z 0 -> 1 over T 0, 185 rec10 z 32 over T 16 (floating): the `F = 0` landing test never fires, no arc moves.
- Readers of `IsOnGround` in the DLL (non-hero): `0x25` and `0x70` only [code grep]; no corpus site has a controller-less logic entity for either.
Risks: (a) tick order: Z in the same tick as the script, before XY, terrain of the pre-XY box; a step that runs a tick later gives 65/193 calls instead of 64/192; (b) mixed conventions (landing at `T + 1` or writes with `+ 1`) break the `0x20`/`0x22` arithmetic: 47 and 260 are immune (no terrain), 127 is not; (c) a script `1B` on a gravity entity is overwritten the same tick; (d) the `0x22` clamp never pushes: a ball already at its target with a rising force rises for ever (faithful stall, do not fix); (e) B-program waits (map events run after entity updates in the DLL) end one tick earlier than in the binary (known, O-E19-28); (f) PosZ near 1920 px caps.
Product questions genuinely open: none from the binary. Technical choice for the plan: literal target (exact in every convention, 1 unit above the binary's value at the end) versus shifted target (faithful, needs the clamp and breaks on the float root at 304 px).

## Not established
- BIOS B(0x38) behaviour (documentation, not read here); no corpus site depends on it.
- `Controller.Move` exactness in float32 for a pure Z displacement (engine not read): the float model assumes it.
- DLL arcs and unit tests that would move (DLL surface, not this one): from earlier notes only (`AlundraTerrainHeightTests.cs:129-146`, `AlundraCollidedWithEntityZTests.cs:64-90`, `AlundraMovementObstacleProbeTests.cs:383-393`, `AlundraGlobalFreezeEntityUpdateTests.cs:210-245`, the intro harness and its four other users): none re-verified here.
- Entity-top landing with several overlapping candidates, ceilings from entities, XY + Z in the same tick for controller-less entities: emulator-only or untested (no corpus site).
