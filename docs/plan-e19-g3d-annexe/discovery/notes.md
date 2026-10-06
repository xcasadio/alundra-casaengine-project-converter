# E19.g G3d discovery: native creator API and `0x82 0x53` (the life vessel). Read only, 2026-10-06 night.

Nothing was built, run, exported, edited, staged or committed inside the repository. Everything below lives in this folder:
`lib/` (copies of `emu.py`, `lib.py`, `fxm.py`, `cmp.py`, `native53.py`, `d.py` disassembler, `idx.py` xref index, generators and golden tables), `data/` (site census).

Labels: **[B]** binary address read in the disassembly, **[R]** proved by running the REAL code of `ALUN_CD.EXE` (France) in the MIPS interpreter, **[F]** file:line of the repository,
**[D]** data (export), **[H]** hypothesis.

## 1. The binary, path of `0x82 0x53`

1. Opcode table `0x80098FAC[0x82] = 0x8003FE7C` [B]. Handler: `a0 = code[1]` (the sub-case), `jal HandleMapTriggerCommand (0x80034108)`, `Result` word `a3 + 0x2C` = 1 when the call returned non-zero else 0, returns **size 2**.
   No other test, no flag, no suspension [B][R: footprint, 2 bytes consumed, `Result` 1, sound [49]].
2. `HandleMapTriggerCommand`: `id < 0x57` jumps through the table at `0x80023234` [B, dumped]: `0x00` -> 0; `0x45..0x48` -> `AddMoney(1/5/10/30)`, 1; `0x4F` -> `IncreaseFalcon2(1)`, 1;
   `0x50` -> `IncreaseMpMaxAndCreateEffect(0x80127D30)`, `0x51` -> `IncreaseMpAndCreateEffect`, `0x52` -> `RestoreMpAndCreateEffect`, **`0x53` -> `IncreaseHpMaxAndCreateEffect (0x800333AC)`**,
   `0x54` -> `AddLifeToEntity (0x80032E2C)` (Hp + 2 capped at HpMax, no effect, no sound), `0x55` -> `AddLowHpAndSpawnEffect`, `0x56` -> `AddMediumHpAndSpawnEffect`; each of them returns **1** (`addiu v0, zero, 1` after the call).
   `a0 = 0x80127D30` is the player entity (entity 0), passed in the delay slot (`0x800341C0`). Default: `IsMapRequirementMet(id)`.
3. `IncreaseHpMaxAndCreateEffect (0x800333AC)`, in this order [B]:
   - `entity.HpMax = entity.HpMax + 1` (`+0x18`, **no cap here**), `entity.Hp = entity.HpMax` (`+0x14`; written in the delay slot of the sound call);
   - `PlaySoundEffect (0x800490FC)(0x31)` (sfx id 49; manifest: 1 tone, `max_voices` 1);
   - 8 ring effects, j = 0..7: `CreateEffectEntity (0x8003BDD8)(bank 0, sprite 0xE, anim 0, x = hero.PosX, y = hero.PosY, z = hero.PosZ + 0x100000)`; `hero` = the player entity `+0x114/+0x118/+0x11C`
     (NOT the entity argument); if the slot was created: `x += OffX[4j] << 13`, `y += OffY[4j] << 13`, `ForceX = -(OffX[4j] << 9)`, `ForceY = -(OffY[4j] << 9)` (ForceZ stays 0);
     `OffX[4j]` = halfwords at `0x80023654 + 8j` = `[0, -543, -768, -543, 0, 543, 768, 543]`, `OffY[4j]` at `0x80023694 + 8j` = `[512, 362, 0, -362, -512, -362, 0, 362]` (a 96 x 64 px ellipse; the full tables have 32 entries, the 32 directions);
   - 4 sparks: `CreateEffectEntity` with the same arguments; **only if the slot was created**: `r1 = lcg(seed)`, `r2 = lcg(r1)`, `lcg(s) = s * 0x7D2B89DD + 0xE06A02E7` (32 bits), the seed word is `0x80098708` (initial value in the image `0xB017C93D`, the DLL's `AlundraRandom` initial seed),
     `ForceX = ((r1 * 0x20001) >> 32) - 0x10000`, `ForceY = ((r2 * 0x30001) >> 32) - 0x18000` (unsigned 32 x 32 products, high word), `ForceZ = 0x40000`, seed = r2. So 2 draws per created spark, 0 to 8 draws per call, none for the ring, none for a spark that finds the reservoir full.
   - returns. **Bank 0 = the global bank** (`0x8003B9FC`: bank 0 -> descriptor `0x80126EC0`, page base 0xB, CLUT base 0x60; bank != 0 -> the map descriptor): global effect 14, animation 0 = 18 frames, 27 ticks, `End` Destroy: drawn on ticks 0..27, freed on tick 28 [D export checked: `effects-global.json` effect 14 has 3 animations, anim 0 sums to 27 ticks and ends Destroy].
4. Footprint of the whole chain (`lib/footprint53.py`, RAM diff of the real `0x8003FE7C`, empty pool) [R]: `0x80127D44` and `0x80127D48` (entity Hp, HpMax), the seed word `0x80098708`, the `Result` word, pool slots 0..11 (a slot is `0x80138608 + 0x80 i`), the stack. **Nothing else**: no `g_playerStats`, no flag, no HUD word, no inventory.
   The sound goes through the hook (not part of RAM).
5. `CreateEffectEntity` = `GetFreeEffect (0x8003B9C4)` (lowest free index, 0 when full, no log) then `InitEffect (0x8003BB14)(slot, record 0, index -1, mode 0, bank, sprite, anim, x, y, z)`: record index `-1`, mode 0. Full reservoir: returns 0, changes nothing. [B][R]
6. The model (`lib/native53.py` with the model of the earlier discovery) was re-run from my copies: 60 random cases (pool pre-filled 0..128, random seeds, random hero positions) **0 mismatch**; this session added `lib/g3d_golden.py` (31 scenarios through the REAL `0x8003FE7C`), and `lib/rules_check.py`, an independent pure-python transcription of the rules below, **0 mismatch on the 31 cases** and 6 of 6 mutants killed.

## 2. Where the hero's life lives (the open point O-E19-39)

- **Binary [B]**: the live life is the player entity `0x80127D30`: `+0x14` Hp, `+0x18` HpMax. `g_playerStats` (pointer at `0x80176318`: Hp `+0`, HpMax `+2`, shorts) is a mirror:
  - entity -> stats: the common tail of `MovePlayer (0x80031B50)`, **every path of MovePlayer reaches it** (97 branches to `0x80032914`, the input-blocked path `& 0x34` included): `UpdateItemEffectState`, then `SetPlayerHpMax(entity.HpMax)` (`0x8004DC68`: clamp [0, 0x32]) and `SetPlayerHp(entity.Hp)` (`0x8004DD30`: clamp [0, stats.HpMax]) at `0x80032920..0x80032930`;
  - stats -> entity: once, at the hero's creation `0x80031974`: `entity.HpMax = GetPlayerHpMax()`, `entity.Hp = GetPlayerHp()` (`0x800319F8..0x80031A08`);
  - `MovePlayer` is called by `UpdateEntitiesEvents (0x800386D0)`, which `UpdateEntities (0x8003B388)` runs only when `PlayerControlFlags & 0x48 == 0` (MenuOpen 0x08, 0x40); a MessageBox (0x10) does NOT stop it;
  - the HUD routine `0x8004BEA4` reads the stats (`GetPlayerHp`/`GetPlayerHpMax`); the save summary (`0x80031198`) reads the entity's HpMax;
  - opcode `0xBC` (`0x80041C00`, size 2): `entity.HpMax = min(entity.HpMax + v1, 0x32)`; **0 sites in the corpus** (not ported today; same seam if ever needed).
- **DLL [F]**: the single source is `AlundraGameState.PlayerStats` (`AlundraPlayerStats.cs:33`, `Hp`/`HpMax` shorts, one instance aliased like the binary's `g_saveData.PlayerStats`); the HUD director (`AlundraHudDirector.cs:537-586`, `UpdateHpAndMpCatchUp`) and the save (`AlundraSaveGame.cs:217, 483`; validation `HpMax` 0..50, `Hp` 0..`HpMax`, `:405-412`) read it;
  the setters are `AlundraPlayerManager.SetPlayerHpMax(state, int)` (`:728`, clamp [0, 50]) and `SetPlayerHp(state, short)` (`:748`, clamp [0, HpMax]). `AlundraEntityScriptProxy.Hp/HpMax` (`AlundraEntityScriptProxy.cs:70-71`) exist but nothing writes or reads them for the hero (they stay 0;
  `AlundraPlayerManager.cs:48-52, 420`: the HP death branch and the end-of-MovePlayer sync are deliberately not ported, "no HP system yet"). **So the life location is established; the slice does not pause.**
- **Consequence for G3d** (design choice, default A): write `PlayerStats` through the setters at the script tick: `SetPlayerHpMax(state, stats.HpMax + 1)` then `SetPlayerHp(state, (short)(oldHpMax + 1))` (the argument is the NEW max before clamping, like the entity value the binary mirrors).
  Equivalent to the binary's entity write followed by its mirror on all 11 real-code cases of `g3d-golden.tsv` rows `P` (including the cap: at 50/50 the entity goes to 51, the stats stay 50/50).
  Deviation to document in the ADR: the binary changes the stats at the next `MovePlayer` (>= 1 tick later for an entity program, same tick for a map-event program run before UpdateEntities); at the 8 sites every dialog around the vessel is a MessageBox (`0D ... 01`, `5C ... 01`: control mode 1, which does not stop `MovePlayer`), so the HUD difference is **at most one logic tick (20 ms)**;
  the entity-side overshoot above 50 (entity 51, stats 50) is unreachable (max 10 + 8 sites). Alternative B (port the hero entity mirror: creation copy, tail sync, entity write) is E14/E13 territory (touches the death branch, every HUD and save test that writes stats), not recommended for G3d.

## 3. The DLL today

- `AlundraEventProgramRunner.cs:1430-1433`: `case 0x82` -> `state.Result = HandleMapTriggerCommand(v[1]) ? 1 : 0; return 2;`.
- `HandleMapTriggerCommand` (`:2995-3029`): `case >= 0x50 and <= 0x56:` -> `LogDegradedOpcodeOnce(0x82, "HandleMapTriggerCommand", "hero life, magic and effects"); return true;`: one debug log per runner (the key is the opcode 0x82, shared with the item-table degradation), trace kind `Degraded`, `Result` 1, nothing written, no sound, no effect. A `case 0x53:` must be written BEFORE the range pattern (a constant after a covering range is a compile error).
- The pinned test: `Alundra.Tests/AlundraItemAndMoneyOpcodesTests.cs:272-284` `Give_0x82_Id0x53_AnswersOne_WritesNothing_AndTracesDegraded` (Result 1, items 0, HpMax 10, trace Degraded) - to be rewritten. Nothing else pins `0x82 0x5x`: `AlundraStoryChainOpcodeAudit.PredicateOpcodes` lists `0x82` generically, `IntroTraceHarnessTests.ImplementedOpcodes` already holds it, the closed list TSV has no `0x82`, **no arc loads maps 134, 258, 298, 302, 398**, no test names those maps' programs.
- The shared generator: `AlundraRandom` (`AlundraRandom.cs`, static `ulong RandomSeed = 0xB017C93D`, `Next()` = `RandomSeed * 0x7D2B89DD + 0xE06A02E7` in 64 bits, returns the low 32 bits as `ulong`): same constants and initial value as the binary's `0x80098708` [B: image value 0xB017C93D]; `Reset()` is called by the tests and never in production (the binary resets at boot only). A vessel = `AlundraRandom.Next()` twice per created spark.
  Other users: `0x0C`, `0x8C`, random modes (`:619, :1427, :3294, :3297`). The libc `rand()` is another stream (`AlundraLibcRandom`).
- G3a creator API (`AlundraEffectReservoir.cs`): `Create(bool mapTable, int effect, int animation, int x, int y, int z)` = `CreateEffectEntity` mode 0, record index -1, forces 0, returns the slot or null when full; slot setters `X`, `Y`, `Z`, `ForceX/Y/Z`, `TargetAnimation` are public; `Mode` is `internal set` (0, 2; "1 and 3 reserved to G3d", `AlundraEffectSlot` doc); `Spawn(int record)` = `SpawnMapEffect(record, force 1)`;
  no `force 0` spawn, no mode 1 / 3 creator, no `Entity`, offset or depth-offset field, `Move` is mode 0 only (mode != 0 returns).
  The stage (`AlundraEffectStage.cs`): `Emit(Action<AlundraEffectReservoir>)` queues with the tick and rank of the source; `RunFrame` applies it before the update of that tick (also when the tick is frozen) and at the end of the frame for what is left.
  So **0x53 needs no new reservoir surface**: `Create(false, 14, 0, hx, hy, hz + 0x100000)` and the slot setters are enough. Modes 1 and 3 are not needed for 0x53.
- The 8 sites (`data/sites53.py`, plain byte scan = linear sweep, 8 and 8): map 134 programs C[4] pc 1083, C[9] 1331, C[10] 1387, C[11] 1443 (`0D dialog ctrl 1`, `B9 0B` XA fanfare, `BA` wait, `A5` stop all sound, **`82 53`**, flag off, spawn entity, destroy entity, end);
  map 258 B[5] pc 614 (`99` spend 500, dialog, `B9`, `BA`, `82 53`, `5D`, `A5`, dialog ctrl 1...); map 298 B[5] pc 668 (`9A` enough money?, `82 53`, `03 If true goto`, `99` spend, dialog ctrl 1); map 302 C[8] pc 624 (`A5`, `82 53`, `If true goto`, flag, gain control, destroy);
  map 398 B[6] pc 1202 (`B9 0B`, `BA`, `82 53`, `51`, `39`, `A5`, dialog ctrl 1). None on the chain. All of them read `Result` (always 1) or ignore it.
- Sound: `IAlundraSoundPlayer.PlaySfx(int)` through `IEntityWorldContext.SoundPlayer` (`IEntityWorldContext.cs:108`), the seam of `0x75/0x12/0xBD`; sfx 49 resolves (`Sounds/sfx-manifest.json`: 1 tone, max_voices 1, `sfx_0049.wav`). `StopAllSfx` (`0xA5`) lets a fresh `PlaySfx` start immediately.
- Hero: `IEntityWorldContext.PlayerEntity` (`AlundraEntityScriptProxy`, `PosX/PosY/PosZ` in 16.16 like the effect slots; the G3b `0xA1/0xA3` read them the same way and capture the values at the emission).

## 4. Rules (what the plan can write)

- **G3D-R1 `0x82` sub-case `0x53`**: size 2, `Result` 1 (always). Order: life, sound, then the effects. Life: `HpMax' = SetPlayerHpMax(state, HpMax + 1)`; `SetPlayerHp(state, HpMax + 1)`. Sound: `SoundPlayer?.PlaySfx(0x31)` at once. Effects: through `EffectStage.Emit` (queued, applied at the tick that emitted it);
  the hero position is read at the emission (`PlayerEntity.PosX/PosY/PosZ`, unchecked 32 bits); no hero or no stage: life and sound still happen, the effects are skipped with a once-per-runner degraded trace (key distinct from 0x82's own, e.g. `0x182`).
- **G3D-R2 effects**: in the closure, in this order and with the reservoir state at application: 8 times `Create(mapTable false, effect 14, anim 0, hx, hy, hz + 0x100000)`; for a created slot `X += OffX[j] << 13`, `Y += OffY[j] << 13`, `ForceX = -(OffX[j] << 9)`, `ForceY = -(OffY[j] << 9)`;
  then 4 times `Create(...)` and, for a created slot only, `r1 = (uint)AlundraRandom.Next()`, `r2 = (uint)AlundraRandom.Next()`, `ForceX = (int)((ulong)r1 * 0x20001 >> 32) - 0x10000`, `ForceY = (int)((ulong)r2 * 0x30001 >> 32) - 0x18000`, `ForceZ = 0x40000`. All `unchecked`.
  The ring tables are 8 constants (the full 32-entry tables are at `0x80023654` / `0x80023694` and serve the 14 pickup natives: carry only the used entries now). A full reservoir creates fewer slots and draws fewer numbers (golden `c18` to `c30`).
- **G3D-R3 life** values: golden rows `P`.
- **G3D-R4 modes 1 and 3 (the native creator interface, optional second commit)**: `CreateAttached(bank, effect, anim, anchor, depthOffset, offset xyz)`: `InitEffect(mode 1, x/y/z = anchor position)`, entity, depth offset `+0x60`, offsets `+0x48..+0x50`; `CreateDetached(bank, effect, anim, anchor, depthOffset, x, y, z)`: mode 3, entity, depth offset;
  `Move`: mode 1: anchor status 0 -> mode 2 else `pos = anchor + offset`, `depth = anchor.depthKey + depthOffset`, status 4 -> mode 2; then FALLS INTO the mode 3 code: `pos += forces`, if status != 0 `depth = anchor.depthKey + depthOffset`, status 0 or 4 -> mode 2 (a status 0 anchor still gets the forces added on that update);
  mode 2 nothing; mode 3 as above. Values: `g3d-modes13.tsv` (8 scenarios from the real code). **Not established in the DLL**: the per-entity depth key `+0x1BC` (no equivalent field; effect draw order is G3c's `RenderSortKey2D` policy) - the anchor abstraction must carry one; no consumer exists (5 attached sites: weapon effect `0x8002F49C`, balance records `0x80039300` x2, break effect `0x8003A59C`, `0x8003A648`; 1 detached: `0x80038E84`).

## 5. Values in advance (generated by the real code)

- `lib/g3d_golden.py` -> `lib/g3d-golden.tsv` (723 rows; `--check` replays): `V` 31 scenarios (4 seeds x 4 hero positions incl. a ring that goes negative and a z that wraps at 32 bits; reservoir pre-fills 1, 5, 118..128; holes `{3,7}` and `{10,50,99}`): result 1, size 2, sounds `[49]`, hp/hpmax out, seed out, number created (12 on an empty pool), number of sparks;
  `S` 290 rows (every created slot right after the call: x y z forces, raw 16.16), `T` 390 rows (6 cases followed after 1, 2, 16, 27, 28, 29 updates: the ring converges to the hero on update 16, the slots are still active after 28 updates and freed on the 29th), `F` (last drawn tick 27, freed tick 28), `P` 11 (life mirror).
  Key values: seed `0xB017C93D`, clean pool: seed after `0xC5BF9705`; pre-fill 118: 10 created, seed `0x88CB7281`; pre-fill 119: 9 created, seed `0xC81B4C37` (= the second `AlundraRandom` value, `AlundraRandomTests.cs:54`); pre-fill 120..127: 8..1 created, seed unchanged; 128: 0 created, life and sound still done; holes: 12 created at 3, 7, 10, ...
- `lib/g3d_modes13.py` -> `lib/g3d-modes13.tsv` (104 rows): creators and per-update values of modes 1 and 3.
- `lib/rules_check.py`: pure python, no interpreter, replays G3D-R2 against `g3d-golden.tsv` (0 mismatch; mutants: draw on failure, no +16 px, wrong spark force, wrong ring sign, seed = r1, wrong multiplier: all caught). The interpreter-based scripts need `lib.py` (the EXE path) and `fxm.py` (DATAS.BIN path).
- Per-site facts: `data/sites53.out`.

## 6. Files to touch (closed list proposal)

Parent repository only (engine, MGUI, converter, analyser: none; effects are not drawn here, G3c does; `effects-global.json` already holds effect 14):
- `Alundra/Scripts/AlundraEventProgramRunner.cs` (case 0x53 before the range, the doc of `HandleMapTriggerCommand`, the degraded key);
- `Alundra/Scripts/AlundraNativeEffects.cs` (new: the ring tables and `CreateLifeVesselEffects(reservoir, hx, hy, hz, Func<ulong>/AlundraRandom)` as a pure function on the reservoir);
- `Alundra/Scripts/AlundraPlayerManager.cs` (`IncreaseHpMaxAndFill(state)` helper next to `SetPlayerHpMax`, using the two setters);
- optional second commit for G3D-R4: `Alundra/Scripts/AlundraEffectReservoir.cs` (anchor interface, `CreateAttached`, `CreateDetached`, `Move` for modes 1 and 3, slot fields), no other file;
- tests: new `Alundra.Tests/AlundraNativeEffectTests.cs` (golden rows `V`, `S`, `T`, `F`, `P`; shared-state collection `AlundraRandomStaticStateCollection`; `xunit.runner.json` already serialises collections), `Alundra.Tests/AlundraItemAndMoneyOpcodesTests.cs` (the 0x53 test rewritten, one more for 0x50 staying degraded), `Alundra.Tests/AlundraEffectReservoirTests.cs` (modes 1/3 only if G3D-R4);
- annex: `docs/plan-e19-g1g3-annexe/g3d-golden.py`, `g3d-golden.tsv`, `g3d-rules-check.py` (and `g3d-modes13.*` if R4), README line;
- `docs/plan-e19-opcodes.md` (section 1.2o.6 G3d, O-E19-39 narrowed to `0x50-0x52`, `0x54-0x56`, D-E19-96 consequences), parent ADR (next free number 0047: `main` 0043, `chantier/e19-suite2` 0046; re-check before the commit), `docs/decisions/README.md`.

## 7. Tests and arcs that move

Only `Give_0x82_Id0x53_AnswersOne_WritesNothing_AndTracesDegraded`. No arc, no trace (`IntroTraceHarnessTests` six traces), no closed-list line, no `MapsWithoutSkippedSite` (already 28 after G3b), no converter test.

## 8. Not established / risks

- Nothing blocking. The hero life location is established (section 2). Default A (PlayerStats written at the script tick) has a measured deviation of at most one logic tick at the 8 sites.
- The vessel effects are queued like the nine opcodes, so the PRNG draws happen when the stage applies the operation (its tick, rank, emission order), not at the script instruction: identical at one logic tick per frame; in a frame of several ticks a later tick's script that draws (0x0C, 0x8C, AI) runs before it. The DLL already runs entity scripts before the world proxy (the reverse of the binary), so draw order between entity and map-event scripts is already approximate.
- Visible effects need G2b free quads + G2e + G3c (global effect 14 is 156/204 deformed quads); until then only the life, the HUD catch-up and the sound change in game. No test save reaches maps 134/258/298/302/398 (`AlundraTestSaves` presets are chain maps): the recette needs a preset, or a headless proof.
- A second vessel inside the lifetime of sfx 49 plays no sound (`max_voices` 1), like the binary.
- `AlundraSaveGame` validation refuses HpMax > 50: the setters keep the clamp (a raw `HpMax++` would break a save at 50).
