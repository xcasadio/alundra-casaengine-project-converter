# E20.0 D1 - the MovePlayer oracle (2026-10-09)

Contract: `docs/plan-e20-heros.md` section 1.2, task D1. Ground truth: `ALUN_CD.EXE` (France), PS-X EXE, text at `0x80020000`, file offset
`0x800 + addr - 0x80020000`. Labels: **FACT** (decoded instruction words or a measurement, with where), **INFERENCE** (with its base),
**UNKNOWN**. Nothing in the E19 annexes, in `Alundra/`, `Alundra.Tests/`, the converter, the engine, `alundra-project/` or `data-extracted/` was
modified. Everything below lives in this folder.

## 1. Result

**PASS on S-A and S-B.** The oracle runs the hero's tick passes of the binary on an emulated machine and COMPUTES the trace; the annex values
(`docs/plan-e19-d2c-valeurs.md` B.1) are read only by `tools/compare.py`. For each scenario 24 ticks x 9 fields = 216 values are compared
(current animation, ForceZ, PosZ, ForceX, ForceY, PosX - start, PosY - start, IsOnGround, CollidedWithEntityZ); 0 differences
(`out/comparison.txt`, every value in `out/comparison-detail.txt`). The annex PosZ is compared as `oracle PosZ - 1` (the annex follows the DLL
convention, the binary rests at terrain + 1; plan D-E19-59): the oracle's rest value is `PosZ = 1`.

The match is not an artefact of the comparison: `out/mutations.txt` shows that leaving out a pass, changing gravity or ZViscosity, locking the
hero or zeroing his HP makes the comparison FAIL (or the S-B warm-up impossible), while the changes that cannot touch the hero's trace
(pass `0x80038634`, the counters pass, the invulnerability counter, a held Cross, the start direction, 3 idle ticks before S-A, four other
start positions) stay PASS.

## 2. How to run

```
python -B docs/plan-e20-annexe/d1-oracle-moveplayer/tools/run_all.py          # about 25 s, rebuilds out/ except diff2 and explore
python -B docs/plan-e20-annexe/d1-oracle-moveplayer/tools/run_all.py --full   # + diff2.py (two interpreters, about 2 min) + explore.py (about 1 min)
```

Needs Python 3 with `capstone` (the E19 tool `lib.py` imports it) and the disc file `D:/development/repo/Alundra Remake/Alundra (France)/Alundra
(France)_extracted/ALUN_CD.EXE` (+ `DATA/BALANCE.BIN`) and `data-extracted/data/map_alundra.json`. Run with `python -B` (no `__pycache__`). Exit
code 0 = both scenarios equal to the annex. Single steps: `oracle.py [outdir]` (computes the traces), `compare.py [outdir]` (the only reader of
the annex), `startstate.py`, `pass38634.py`, `padcheck.py`, `mutations.py`, `hazards.py`, `diff2.py`, `explore.py`, `dump_dis.py`.
The run is deterministic (two runs give identical `*.trace.json`).

### Files

| File | Role |
|---|---|
| `tools/` (E19 copies) | `emu.py`, `lib.py`, `mips.py`, `zsim.py`, `m.py`, ... copied from `docs/plan-e19-h2-annexe/tools/` unchanged (`cmp` equal); `run_update_animation.py` and `dis_update_animation.txt` copied from `docs/plan-e19-o9-annexe/`. Only change: `run_update_animation.py` now imports `emu` from its own folder (the original pointed at a `lib/` folder absent from its annex); its output equals the E19 annex's `run_update_animation.out` (checked) |
| `tools/machine.py` | the machine: `emu.Emu` + shadow map of initialised bytes (reads of never-written bytes are recorded), call trace, PC coverage, stubs with a journal, `lwl/lwr/swl/swr/mthi/mtlo` (absent from `emu.py`) |
| `tools/bank.py` | the hero's sprite record laid out in RAM from `map_alundra.json` |
| `tools/world.py` | synthetic field, map header, BSS words, balance blob, new-game stats, hero (real `InitializeEntity`), pad port, the tick |
| `tools/oracle.py` | scenarios S-A and S-B, traces |
| `tools/compare.py` | the comparison with the annex (parses the annex text) |
| `tools/mutations.py`, `padcheck.py`, `pass38634.py`, `startstate.py`, `hazards.py`, `diff2.py` + `interp2.py`, `explore.py`, `dump_dis.py`, `callgraph.py`, `run_all.py` | checks and dumps (sections 7 and 8) |
| `out/S-A.trace.txt/.json`, `out/S-B.trace.txt/.json` | per-tick traces (S-B with its 3 warm-up ticks first); fields: tick, hold word, target and current animation, direction, ForceX/Y/Z, PosX/Y/Z (binary convention), IsOnGround, CollidedWithEntityZ, `+0xB0`, `+0x1C`, counters `0x80126FF4` and `0x80126FFC` |
| `out/*.machine.txt` | functions entered, reads of unwritten bytes (setup and ticks, both empty), MovePlayer reach points, journal of the stubbed calls |
| `out/comparison.txt`, `comparison-detail.txt` | the field-by-field comparison |
| `out/mutations.txt`, `padcheck.txt`, `pass-80038634.txt`, `start-state.txt`, `load-delay-check.txt`, `diff2.txt`, `explore.txt` | check outputs |
| `out/dis_passes.txt`, `dis_pad.txt`, `dis_moveplayer.txt`, `callgraph_moveplayer.txt` | the disassembly the notes cite (address, instruction word, decoded) |

## 3. The tick and the pass table as built

**FACT** (`out/dis_passes.txt`, `0x8003B388`): with `[0x800DC4B8] & 0x48 == 0` the tick calls, in this order, `0x80038634` (`0x8003B3A0`),
`0x800386D0` (`0x8003B3A8`), `0x80038998` (`0x8003B3B0`), `0x800384F4` (`0x8003B3D0`), `0x80038E18` (`0x8003B3D8`), `0x80038364`
(`0x8003B3E0`), `0x80038E84` (`0x8003B3E8`), `0x80039300` (`0x8003B3F0`). The oracle keeps `[0x800DC4B8] = 0` and runs the passes in that order,
once per logic tick, after writing the pad words.

| # | Pass | In the oracle | Evidence |
|---|---|---|---|
| 1 | `0x80038634` slots | **real code** | section 4 |
| 2 | `0x800386D0` events | **real code**: `jal 0x80031b50` (word `0c00c6d4` at `0x800386EC`) runs MovePlayer first; the two entity loops (heads `0x8003871C`, `0x800388E0`) are not entered because `[0x80134360] = 0` (`blez` at `0x80038708` and `0x800388D8` taken); no other entity in D1 | `out/S-A.machine.txt`: `0x800386EC` called, loop bodies not reached (coverage) |
| 3 | `0x80038998` counters | **real code**, all slots | `+0x1C` incremented (the traces show `+0x1C` = tick count), `+0x1CC`/`+0x1D0` decremented, `+0x220`/`+0x224`/`+0x12C` cleared (words `0x800389D0-0x80038A08`) |
| 4 | `0x800384F4` lists | **real code** | builds the active list `0x80127B28` (count `0x80127D28`) and the collidable list `0x80127C28` (count `0x80127D2C`) from `[0x80134360]` |
| 5 | `0x80038E18` animation | **real code**: calls `0x80038AB4` for each active entity (the hero) | over the hero bank of section 6 |
| 6 | `0x80038364` physics | **real code**, nothing stubbed | the rider copy `0x80037364`, which `zsim.py` stubs, is never entered (not in the call list) |
| 7 | `0x80038E84` effects | **stub, no effect** (journalled as `pass_38E84_effects_stub`) | plan table |
| 8 | `0x80039300` hits | **stub, no effect** (journalled as `pass_39300_hits_stub`) | no target in D1; D2 runs it for real |

Called inside MovePlayer, all **real code** (call counts in `out/*.machine.txt`): `0x8002F120`, `0x8002F49C`, `0x8002F768`, `0x8002F884`,
`0x8002EAF4` (PlayerTryAttack), `0x8002ED64` (TryUseItem), `0x8002EEAC` (PlayerTryAction), `0x8002E910` (CheckEntityInteraction, only from Idle and
Moving), `0x80031A68` (damage entry), `0x8002FB14` (CreatePlayerAnimationEffects), `0x8003634C` (called once per free tick at `0x80031CC4`; its
callees `0x80035204 ...` never run in S-A/S-B), `0x800307E8` (UpdateItemEffectState), `0x8004DC68` (SetPlayerHpMax), `0x8004DD30` (SetPlayerHp),
`0x8004E030`, `0x8004E0F8`, `0x8004E18C`, `0x8004E428`, `0x800445C0`. MovePlayer's own routes are checked by coverage (`MP_POINTS` in `oracle.py`,
printed in `out/*.machine.txt`): the entry reads `+0x20`, none of the early exits is taken (`0x80031BB4` `+0x20 != 0`, `0x80031BDC` locked, `0x80031C58`
damage, `0x800327A4` death, `0x80032768` `+0x188 & 0x80`), the prologue calls and the state switch `0x80031D8C` and the epilogue `0x80032914` are reached.

## 4. Verdict on `0x80038634`: real code, not a stub

**FACT** (`out/dis_passes.txt`, complete function, 41 words):

```
80038634 00005021 move t2,zero            t2 = last non-empty index
80038638 01404821 move t1,t2              t1 = i
8003863c/40/44/48/4c/50  t0 = 0x80127D30 (slot 0), t5 = 4, t3 = 0x80134368 (staging entity), t4 = t3 + 0x290
80038654 8d020010 lw v0,0x10(t0)          status of slot i  (the hero for i = 0)
8003865c 144d0012 bne v0,t5,0x800386a8    status != 4 ?
80038668..0x80038690  copy 0x290 bytes from t3 to the slot (16 per loop), then 0x80038694..0x800386a4: one more word, and  sw t1,0(t0)  (slot.index = i); jump to 0x800386b4 (t2 not updated)
800386a8 10400002 beqz v0,0x800386b4      status == 0: empty
800386b0 01205021 move t2,t1              status != 0 (not 4): t2 = i
800386b4 25290001 addiu t1,t1,1 ; slti v0,t1,0x40 ; bnez -> 0x80038654 (64 slots, stride 0x294)
800386c4 3c028013 lui v0,0x8013 ; 800386cc ac4a4360 sw t2,0x4360(v0)      [0x80134360] = t2 (jr ra in the slot of the sw)
```

Meaning: it promotes a staged entity (status 4 slot) from `0x80134368` and sets `[0x80134360]` = the highest index of a non-empty slot. It reads the
hero's slot (`+0x10`) and writes the one word that the lists pass, the events pass (`0x80038700 lw v1,0x4360`) and the counters pass
(`0x800389AC`) use as the entity count. `pass38634.py` runs the real code on prepared arrays (`out/pass-80038634.txt`): hero alone gives 0 whatever
the word held before; slots 0, 5, 9 non-empty give 9; a slot with status 4 receives the staging entity, gets its index, and is counted by the next run.
In D1 (hero alone, no staged entity) its effect is the constant 0, so leaving it out does not change the hero's trace (`out/mutations.txt`); the oracle
nevertheless runs it for real, as the plan rule says (it reads the hero's slot).

## 5. Stubs, each journalled (`out/*.machine.txt`)

| Address | What | Rule |
|---|---|---|
| `0x800490FC` | PlaySoundEffect | notes `a0` (the sound id), returns 0. S-A journal: tick 1 sound 10 (take-off), tick 2 sound 0 (animation 45), tick 22 sound 11 (landing), sound 0 (Idle) |
| `0x8003BDD8`, `0x8003BE74` | effect creation | notes `a0-a3` and 6 stack words, zeroes the dummy slot `0x801E8000` (0x300 bytes) and returns its address. Hit once per landing (id 7 dust, 3 calls at tick 22) |
| `0x80039F58` | SpawnWarpEntity | notes `a0-a3` and 4 stack words, returns 0 (not hit in S-A/S-B) |
| `0x80038E84`, `0x80039300` | two passes | not run; journalled once per tick |

The pseudo-random generator state `[0x80098708]` (`lui 0x800a; lw -0x78f8` at `0x80032988`/`0x8003298C`; EXE image value `0xB017C93D`) is set to
`0x12345678` (noted in the trace json). Correction of 2026-10-09 after the check of D1: the first version poked `0x800A8708`, an unrelated data word
(`0x0004FFFF`, sound-table row 48); the state is consumed by the landing dust at tick 22 of S-A and S-B (3 draws). No compared field depends on it.
The loot routine `0x80032968` is real code and is not entered in S-A/S-B.

## 6. What the oracle builds

### 6.1 Pad (relation to `AlundraTickPad`)

**FACT** (`out/dis_pad.txt`): the binary updates a pad with `0x8002E250(a0 = pad struct, a1 = new state)`, called by `0x8002E38C` once for pad 0
(`a0 = 0x80126F18`, `a1 = BIOS word & 0xFFFF`, `0x8002E3B4`) and once for pad 1 (`a0 + 0x18`, `srl 16`). Fields: `+0` MaxNbFrameHeld (set to `0x14` by
`0x8002E34C`), `+4` RepeatInterval (0), `+8` IsOverMax, `+0xC` NumberOfFrameHold, `+0x10` hold, `+0x12` pressed, `+0x14` released, `+0x16`
pressed-with-repeat. Words used by MovePlayer: hold `0x80126F28`, pressed `0x80126F2A`, released `0x80126F2C`.

- `0x8002E250..0x8002E260`: `new ^ old`, `new & (new ^ old)` stored at `+0x12`. `0x8002E264..0x8002E274`: `old & (new ^ old)` stored at `+0x14`.
  **AlundraTickPad.cs:57-58 (`ButtonsJustPressed = buttonState & (buttonState ^ ButtonsHold)`, `ButtonsReleased = ButtonsHold & (buttonState ^ ButtonsHold)`)
  match the binary formulas exactly.** The repeat logic (`0x8002E278..0x8002E304`: reset when `old != new` or `old == 0`; silence while
  `NumberOfFrameHold < 20`, then the held state every tick because RepeatInterval is 0) matches `AlundraTickPad.cs:60-101` too.
- Proof beyond reading: `padcheck.py` runs the real routine and the Python port of `AlundraTickPad.Update` (`world.Pad`) on the S-A and S-B hold sequences, a
  128-tick long-hold sequence and 40 random sequences of 300 ticks over the 8 buttons: the four words are equal on every tick (`out/padcheck.txt`).
  The oracle does the same cross-check on every tick of every run (a second pad struct at `0x801E9000` driven by the real routine; a difference
  raises).
- Layout of the hold word as MovePlayer reads it: directions in the high nibble (`lhu 0x6f28` then `srl 12`), Up `0x1000`, Right `0x2000`, Down `0x4000`,
  Left `0x8000`; Triangle `0x10`, Circle `0x20`, Cross `0x40`, Square `0x80` (`andi 0x40` pressed at `0x80031ED8`; `andi 0x10` hold at `0x80031F1C`).
- UNKNOWN: the BIOS word's own bit convention (`0x8008A688` is a BIOS wrapper outside the EXE image); the DLL's hold word is assumed to be in the
  convention above, as `AlundraTickPad`'s consumers already do.
- Scenario driving: S-A, hold `0x0040` at tick 1, `0` after (a one-tick tap; the held-Cross variant gives the same trace, `out/mutations.txt`). S-B, `0x2000` on the
  warm-up ticks and the following ticks, `0x2040` at tick 1.

### 6.2 World

Flat synthetic field, 52 x 60 cells of 24 x 16 px: walkability 0, ground property 0, slope 0, height byte 0 (`zsim.py`'s cell function); the pixel to
column table `0x80127168` as in `zsim.py` (`min(x // 24, 51)`); tile table base `[0x800DD86C] = 0x801D1000` (cells at `+0x604 + row * 416 + col * 8`).
Map header at `[0x800E4334] = 0x801D0000`: `+4` gravity 128, `+6` ZViscosity 4096, `+8` SlideEffectId 0, `+9` BalanceLevel 2, `+0xA` C 7, `+0xB` D 0, the
constants of `map_10.json` Info (S-A/S-B are measured with the map-10 constants). The hero starts at (100, 100) px (any start gives the same deltas:
four other positions checked).

### 6.3 The hero's slot, stats and balance sheet

- **Slot 0 is built by the real `InitializeEntity` `0x80039D04`** (a0 = `0x80127D30`, a2 = the hero sprite record, a3 = 0 = no map record, stack arguments:
  balance index 0, position (100 px, 100 px), PosZ 0, TargetAnimationId 0, TargetDirection 24). It sets `+0x6C` flags `0x0003118C`, offsets/sizes through `0x80039C40`,
  `+0x1C4` = the balance record, HP `+0x14` and `+0x18` from record byte 2, then PosZ = terrain + 1 = 1 (the binary's resting convention) and calls UpdateAnimation once.
  Then the harness sets status `+0x10` = 2 (the function leaves 1) and HP `+0x14` = 10. Full dump: `out/start-state.txt`. Values the plan asked for: HP 10, HP max 10,
  `+0x20` 0, `+0x180` 0, `+0x188` 0, `+0x224` 0, `+0x1CC` 0, `[0x80127000]` 0, status 2, `[0x800DC4B8]` 0, WeaponId 1.
- **Stats and inventory run on the new-game code**: `0x8004DAC0` (InitPlayerStats: block `0x801EB82C`, `[0x80176318]` points at it, inventory table at
  `+0x12` = 128 x 4 bytes, `[0x8017638C]` = 0x63), then `0x8004E530(i)` (AddItem) for every item whose property byte 6 (`0x800C5F7C + 8i + 6`) has bit
  `0x80` - the loop of `0x80031868-0x80031890`; it adds items 1, 17, 25 (base sword, cloth jacket, normal boots, as the plan says) - then `0x8004E484(1)`
  (SetPlayerWeaponId(1), `0x80031894`). No armour or boots slot is equipped by this code (`g_playerStats + 0xC` stays 0 = no armour), so `[0x80127000]` (boots level) is 0.
- **Balance blob**: the disc file `DATA/BALANCE.BIN` (11169 bytes) is loaded at `0x80143DF8` (base used by `0x80044550`: `lhu table[index*2]` then `base + offset`); its 512 u16
  offsets equal the `FileOffset` of every entry of `data-extracted/data/BALANCE.BIN.json` (checked by `world.check_balance`, 0 differences). `0x80044520(D)` runs for real
  (`[0x80143DF0]` = the map header byte `+0xB`, here 0 as in `map_10.json`). The hero's record (index 0): level 255, size 169 (`0xA9`), MaxHp 10, 11 damage
  responses 0, 77 attacks. After the first tick `0x800307E8` has copied 0xFF bytes into the working sheet `0x80127008` (`FF 0F 00 10 10 ...`, neutral responses); the hero's `+0x1C4`
  keeps pointing at the blob record, and `ComputeNewHp` uses `0x80127008` for the hero (`0x8003A3B0 addiu a1,v0,0x7008`; INFERENCE from the one reference, not exercised in D1).
- **BSS words read before any write**, set to 0 explicitly (`world.BSS_ZERO`, each with its reason): `0x800DC058` (sign bit = a screen transition runs; zero = none,
  `0x8002AB64` clears it), `0x80126FF8` (charge-aura entity pointer), `0x80127110` (pending delayed-shot sheet pointer), `0x801EB838` (equipped armour id), `0x80127D2C`,
  `0x80134360`, `0x80134600`, `0x80126FF4`, `0x80126FFC`. The PS1 BSS is zero at power on (INFERENCE: standard startup; the EXE does not carry these bytes). With these set, the shadow map
  reports **no read of an unwritten byte** during the setup or the ticks of S-A and S-B (and none in 60 random 80-tick scripts, `out/explore.txt`).

### 6.4 The hero bank (animation data)

**Source** (FACT): `data-extracted/data/map_alundra.json`, `SpriteInfo.SpriteRecords[0]`, the record with `Header.Sector5Id` 0 - the bank `SpriteBankReader` calls `alundra_0`
(94 animation sets). The bytes are produced from the decoded fields; the layout is the one `0x80038AB4` reads (`out/dis_passes.txt`, `tools/dis_update_animation.txt`) and the
extractor's readers (`SpriteRecord`, `AnimationSet`, `SiAnimation`, `SiFrame`, `FrameCollisionData`, `SiImageSet`, `SiImage`):

| RAM | Content |
|---|---|
| `0x801A0000` record | `+0` pointer to the animation sets, `+4` to the frame bytes, `+8` to the collision records, `+0xC` to the image sets; `+0x10..0x1F` the 16 header bytes (`Ubuff`: MoreFlags 0x8C, CanPickup 0x11, FlagsPortraitShadowType 3, programs 0, offsets -10 -7 0, sizes 21 15 32, ...). This is `entity +0x64` |
| `0x801A0020` animation sets | 94 x 14 bytes: 4 x u16 byte offsets (down, up, left, right; `0xFFFF` none), u16 Speed, u8 Unknown, u8 Flags (the pair is the signed IZF half-word read by `lh 0xA`), u8 Sfx, u8 Acceleration. 1316 bytes |
| `0x801A0550` frame bytes | frames of 5 bytes (Delay with bit 7, u16 collision offset or `0xFFFF`, u16 image-set index = byte offset / 2) and terminators of 1 byte (Delay 1 = loop) or 2 bytes (0, chain/hold byte). 15375 bytes |
| `0x801A4160` collision records | 6 bytes (sbyte offX, offY, offZ; byte width, depth, height) at the frame's collision byte offset. 1782 bytes |
| `0x801A4860` image sets | byte DepthSortValue, byte NumberOfImages, then 14 bytes per image. 90174 bytes |

Consistency checks in `bank.py`: each decoded frame's extractor address equals `animation offset + 5 * i`; the 94 sets write 0 contradicting bytes where two animations share one. The layout is
proved right by use: the real `0x80038AB4` chains animation `0x2B` into `0x2D` (terminator `0, 45`), applies IZF 1280 and Speed 208/196/336 and ends the jump in S-A and S-B exactly as the annex.

## 7. Other checks (all in `run_all.py`)

- **Two independent interpreters.** The oracle runs on `emu.py`; `diff2.py` drives the same machine through `mips.py` (a different interpreter; unaligned loads/stores, `div`, `mthi/mtlo` re-implemented
  bytewise in `interp2.py`) on S-A, S-B and 60 seeded random pad scripts of 80 ticks (all eight buttons: attack, sprint, items states are visited): after every tick the hero's 0x294-byte slot, the
  stats block, the globals the passes write and the stub journal are byte-identical on both (`out/diff2.txt`: 62 of 62 scripts equal).
- **Load delay slot.** Both interpreters ignore the MIPS-I load delay. `hazards.py` checks every instruction executed (3432 distinct, 702 loads) that no load is followed by a use of its register
  (the `lwl; lwr` pair excepted, it is the architecture's unaligned-load idiom): 0 hazards (`out/load-delay-check.txt`), so ignoring the delay is exact for what runs.
- **Reach beyond S-A/S-B** (`out/explore.txt`, informational): 60 random 80-tick scripts visit animations 0x00, 0x01, 0x02, 0x03, 0x04, 0x10 (sword on the ground), 0x14 (sword in the air), 0x29, 0x2A, 0x2B, 0x2C, 0x2D with
  real code and no unwritten read. Those paths are NOT validated against any reference here (only the jump arcs are); they are what later slices will compare with the DLL.

## 8. Limits and open issues

1. **Only the jump arcs of S-A and S-B are validated against a reference.** Attack, sprint, items, damage routes run as real code and agree between the two interpreters, but D1 has no annex for them; a slice that uses them as proof needs its own reference.
2. **Synthetic world.** Flat field, no cell property, no slope, no other entity, no map events, no dialogue/menu flags (`[0x800DC4B8] = 0`), no transition (`[0x800DC058] = 0`). The hero's start state is the real `InitializeEntity` result plus the plan's values, not a state captured from a running game.
3. **Hero balance copy.** UNKNOWN whether the real game re-points the hero's `+0x1C4` to `0x80127008` somewhere; D1 does not need it (hits are stubbed).
4. **BSS-zero words** of section 6.3 are an INFERENCE (power-on zero); `0x800DC058`'s exact role (transition) is an INFERENCE from its readers and `0x8002AB64`.
5. **Attack record layout** (2 bytes per attack after the count at record `+0xE`) is an INFERENCE from `RecordSize 169 = 15 + 77 * 2` and the raw bytes `85 02` for the "Tackle" entry; not used by D1.
6. **`0x8003634C`** runs as real code every tick; its role (plan D5) is not decoded here; in S-A/S-B its callees are never reached.
7. **Pad timing.** The oracle gives each tick the hold word of the scenario; it does not model when the binary's main loop calls `0x8002E38C` relative to the tick (E19.f3d/pad-age annex). The relation of "the word MovePlayer sees at tick N" to the DLL's input sampling is E20.a's concern (D7).
8. `emu.py` is used with three additions made in `machine.py` (unaligned memory operations, `mthi/mtlo`); the copy of `emu.py` itself is unchanged.
