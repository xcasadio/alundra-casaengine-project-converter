> Report returned by one agent of the read-only discovery workflow for E20.e0, E20.f and the dependencies of E20.h and E20.g (night of 2026-10-09), kept verbatim as evidence. The plan holds the reconciled facts.

# f-check: adversarial check of the E20.f discovery reports (f-weapons, f-projectiles, test-means)

How I checked:
- I disassembled `ALUN_CD.EXE` (France) again with capstone, using my own reader `xdis.py`. File offset = `0x800 + addr - 0x80020000`.
- I ran my own census of all 483 `data-extracted/data/map_*.json` and of `map_alundra.json`.
- I re-ran one f-projectiles oracle trace from a copy of its folder. This only shows the trace reproduces; it is not an independent check.
- Nothing was written to the repository and nothing was built.
- Scratch folder: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-weapons/f-check/` (scripts `xdis.py`, `t1.py`, `census.py`, `c0census.py`, `scan.py`, `spawnargs.py`; disassembly files `out_*.txt`; `oracle_copy/`).

Labels: FACT means a binary address or file:line I read myself. INFERENCE comes with its basis. UNKNOWN comes with what would settle it. The verdicts are CONFIRMED, REFUTED and UNVERIFIABLE.

## Things that change the plan

1. **The premise of D-E20-18 is refuted, independently of both reports.**
   - FACT, my census of all 483 maps plus the 257 global records: E17 is used only by global sprites 3-6; C94 only by 7; C60 and E9 only by 10-15; C61 and E10 only by 27-29. No map-local sprite uses any of them.
   - FACT: C54 (`0x80078E34`) is used only by map 326 sprite 190 (codes A16 C54 D27 E11). It has nothing to do with the fire rod.
   - FACT, the Torche handler E20 (`0x8007F6C8`, Torche = bank137) is the only non-hero code that spawns a hero projectile type (type 12, at `0x8007F730`). It then zeroes all six program words `+0x70..+0x84` of the copy (`0x8007F740-0x8007F74C`), sets `+0x88 = 3` and sets flags to `(F & ~0x10) | 2`. C60 and E9 never run on that copy.
   - So by the author's own criterion ("those that enemies may share"), E17, C60 and E9 belong to E20.f. D-E20-18 names them as E14's, so moving them changes the owner of named items. That is an author decision (question Q1).
   - It is not only a scheduling question. Without E17 and E9 the arrows and fire/ice balls can never be destroyed, and every shot leaks one of the 63 slots (detail in (c)). Leaving them to E14 means bows and rods stay unusable until E14, which contradicts "weapons first".

2. **E20.f needs four things that other slices or decisions currently own.**
   - **(a) E20.e0's spawn**, including membership in the same tick (point 3).
   - **(b) The native-handler registry by sprite type (D-E14-1).** Today the DLL dispatches only the save book, through `AlundraSaveBook.TryMatch` (`AlundraEventProgramRunner.cs:283-306`).
   - **(c) The header `BreakEffect` byte.**
     - FACT: `sprite-records.json` has no `BreakEffect` field.
     - FACT: the charged arrow's E1 needs effect 8. E1 calls `DestroyEntity(e,-1)`, which reads header `+0x1E` at `0x8003A5E8`.
     - D-E14-2 places that export "in E14.a", so it would have to move earlier (Q2).
   - **(d) A Z model for non-hero entities.** It covers the gravity bit 0x100, the impulse at `+0xF8`, landing, and wall, edge and terrain-height blocking for entities that fly at a constant Z. The plan gives this no owner (Q4).

3. **A spawned entity runs in its spawn tick; the DLL runs it one frame late, not one tick.**
   - FACT, binary:
     - `MovePlayer` is called at `0x800386EC`, before the pick loop.
     - `InitializeEntity` raises `[0x80134360]` at `0x80039D24`/`0x80039D3C`.
     - The pick loop reads the count again on every iteration (`0x8003889C`), and status 1 gives A plus status 2 (`0x80038754`).
   - FACT, DLL:
     - `CollectTickEntities()` runs at `AlundraWorldProxy.cs:2668`, before `MovePlayerOnTickPad` at `:2676`.
     - Membership needs `TookPartInFrame` (`:2626,:2633`), which is set at the next frame (`:2617-2620`).
   - In a frame with several ticks the shot therefore misses more than one tick.

4. **New DLL gap: the locked branch does not clear the shot.**
   - FACT, binary branch `[0x800DC4B8] & 0x34`: it calls `CreatePlayerAnimationEffects(1)`, then clears the timer (`0x80031BEC`) and the pointer (`0x80031BF8`), then calls `UpdatePlayerCarriedEntity(1)` and `0x800350C0`.
   - FACT, DLL: `AlundraPlayerManager.cs:220-223` only returns.
   - Effect: a shot pending when a message box opens is dropped in the original but fires after unlock in the DLL. The pointer part is an E20.b omission; E20.f must add both clears.

5. **Grass cutting by the hero's swings, flails included, is E20.e work, not E20.f.**
   - FACT, binary: the jump table of `CreatePlayerAnimationEffects` (`0x8002FB14`, table `0x800228AC`, index `+0x88-4`) sends these states to `0x8002FDBC`, which calls `CheckAndTriggerTileEffect(hero)` at `0x8002FDC0`:
     - 4;
     - 0x10, 0x12, 0x14, 0x16;
     - 0x18, 0x19;
     - 0x3F, 0x40, 0x41, 0x42;
     - 0x44, 0x46, 0x49, 0x4B.
   - The bow and rod animations (0x11, 0x13, 0x15, 0x17) are not in that list.
   - FACT, binary: the flail bounce is the tail of that same function, gated to mode 0 by `bnez $s5` at `0x800303D8`. Nothing earlier in the function writes `+0x88`, so the tail can be ported on its own.

6. **The arrow height also affects the proposed test arenas.**
   - FACT: a normal arrow's box spans Z +16..+27 px above the hero's base (anim-0 box `OffsetZ 0`, `Height 11`). A target at the same height is hit only if `SizeZ >= 17`.
   - FACT: map 14 sprite 79 (Beannoïde) has `SizeZ 8` → missed. Map 136 sprite 49 has `SizeZ 16` → missed, so test-means arena A8 fails for normal arrows. Map 16 sprite 173 has `SizeZ 20` → hit.

7. **Keeping the random generator exact depends on effect creation.**
   - FACT: the hold spark draws twice only when `CreateEffect` returns a slot (`0x8002F61C`).
   - FACT: E9 draws twice per puff that is actually created (`0x8007F028`, `0x8007F158`).
   - FACT: the DLL's 128-slot reservoir exists (`AlundraEffectReservoir.cs:126`).
   - So the effect calls must be ported exactly, or the generator drifts for every later consumer, enemy AI included.

8. **test-means' "the kit's item 1 must be set to count 0" is refuted.**
   - FACT: every weapon row in `items-properties.json` has flag bit 0 set.
   - FACT, `GetItemIdFromSlotId` (`AlundraPlayerManager.cs:1214-1263`): with that flag set, the strictly higher rank wins.
   - Item 1 (rank 0) never blocks Steel Edge. Only higher-ranked items of the same slot must be absent; item 8 (rank 4) beats every sword.

## (a) Check table

| # | Claim (source) | Verdict | Evidence |
|---|---|---|---|
| 1 | Handler master table at `0x80098F4C`; C54/60/61/62/94, E1/9/10/12/17/20, D30, A0 addresses (fp §3) | CONFIRMED | `t1.py`: exactly the listed table entries and targets |
| 2 | E17, C60, E9, C61, E10, C94 only on global sprites (fw 1, fp 1) | CONFIRMED | `census.py` over 483 maps and the global records |
| 3 | C54 = map 326 sprite 190, a boss (fw, fp, tm 6) | CONFIRMED as to the user (codes 16,54,27,11). Name "Ronan" UNVERIFIABLE | census |
| 4 | Torche E20 spawns type 0x0C (fw); it zeroes the six program words (fp) | CONFIRMED (fp). fw is incomplete: it implies C60/E9 would run | `0x8007F730`, `0x8007F740-4C`. Also `+0x88=3` and no null check on the spawn result |
| 5 | Item→record table `0x80028C14`; 13-byte records at `0x80028C78` (fw §1, fp §1, tm) | CONFIRMED | 1→0, 2→1, 3→2, 4→3, 5→8, 6→9, 7→10, 8→11, 9→6, 10→7, 14→5, 16→4; others → 0 |
| 6 | Chargeable records: 1, 2, 3, 4, 5, 7, 9; not 0, 6, 8, 10, 11 (fw) | CONFIRMED | record bytes |
| 7 | b0 / b1 sets in `0x80028D14` (fw) | CONFIRMED | b0: 0-4, 0x0E, 0x29-0x2D, 0x35, 0x3E. b1: 0x11, 0x13, 0x15, 0x17, 0x18 |
| 8 | `PlayerTryAttack`: guard `+0x180&0x2000`, release before press, timer≥60, ground `+0x144`, 16 sparks, sound 0x2B, pointer `0x80028C79+13w+8` (fw §2) | CONFIRMED | `0x8002EB1C-0x8002ED38`; pad `0x80126F2A` press, `0x80126F2C` release |
| 9 | Second caller is only state 0x29 (fw) | CONFIRMED | `0x80031F74`; switch table `0x80022D3C[0x29]=0x80031F54` |
| 10 | Burst: forces `tbl*448`, positions `tbl*3<<10`, amplitudes 768 and 512 (fw) | CONFIRMED; the cos/sin naming is swapped | `0x80023654` (used for X) is a sine of amplitude 768 starting at 0; `0x80023694` (for Y) amplitude 512 |
| 11 | `UpdatePlayerWeaponEffect`: timer `0x80126FFC`, (Hold\|Released)&0x80, record byte 0, b0, cap 60; <11 frees the aura; ≥11 attached effect 3 with z 0x100000; sounds 0x2A / 0x1AC / 0x1AD (fw) | CONFIRMED | `0x8002F49C-0x8002F764` |
| 12 | Hold spark draws two LCG steps (fw: INFERENCE) | CONFIRMED, now FACT | second state stored at `0x8002F710`; draws only if the effect was created |
| 13 | jal targets of the weapon functions: only sound, effect and spawn (fw 0.4) | CONFIRMED, list incomplete | it also calls `0x8003BE74` (attached effect, `0x8002F5B4`). No inventory or stats access |
| 14 | MP: one caller `0x80034948`; staff (item 7) skips the cost (fw, tm) | CONFIRMED; fw mislabels the function | `0x8004DF3C` is DecreaseMp (`Mp - a0`, then setter `0x8004DEBC`), not SetMp |
| 15 | `UpdateWeaponStepProgression`: b1 test, counter < delay, spawn at `0x8002F818`, dir `{0,16,8,24}[+0x98]`, result ignored; S = P+delay+1 (fw, fp) | CONFIRMED | `0x8002F768-0x8002F84C`; table `0x80023644` |
| 16 | Spawn ticks: Hunter P+9 / air P+1, William P+9 / R+9, rods P+11 / R+11, wave R+21 | CONFIRMED (arithmetic from the bytes and the call order) | delays 8, 0, 8, 10, 0x14 |
| 17 | Earliest release = S+D+60; aura appears 10 ticks after the first count (fw §3) | CONFIRMED by code order | the increment at `0x80031D68` comes before the state switch. The "Idle at S+D" tick itself is UNVERIFIABLE (oracle only) |
| 18 | Order inside `MovePlayer` (fw §2) | CONFIRMED | `0x80031B70`, `B9C`, `BA4`, `BC4`, `C3C`, `C44`, `CC4`, `D18`, `D68`, `D70`, `D78`, `DAC` |
| 19 | Timer clearing sites (fw: 5) | REFUTED (incomplete) | 7 paired timer/pointer sites: `0x80031A18/A40`, `BB4/BC0`, `BEC/BF8`, `C5C/C68`, `0x80032614/2620`, `0x80032768/2774`, `0x800327A4/27B0`. fw's pointer list is complete |
| 20 | Flail bounce: mode 0, anims 0x12/0x16→0 and 0x40/0x42→1, HitCounter, four corners, height byte <<20, bit 0x40, bytes `08 1F 25 26` (fw §4) | CONFIRMED | `0x80030434-0x80030654`. The cell base is `*[0x800DD86C]+0x604`, row stride 416, column clamp 0..0x33, row clamp 0..0x3B; the first wall corner wins |
| 21 | Bounce adds no force; tick numbers (fw, tm) | UNVERIFIABLE | oracle only, not re-run |
| 22 | C61 = `CheckAndTriggerTileEffect` only; E10 = E1 = `DestroyEntity(e,-1)` (fw, fp) | CONFIRMED | `0x8007A958`, `0x8007F23C`, `0x8007ED10` |
| 23 | C60: effect 0x11 if `type-0xB<2`, else 0x12; z offset 0x80000 for fire; forces `-C4,-C8,-B8` (fp, fw) | CONFIRMED | `0x8007A8A0`. That `+0xB8` is ForceZ (fw's naming) is UNVERIFIABLE |
| 24 | C94: tile cut, then every 4 ticks effect 0x19 with anim `+0x98`, Z+0x80000, forces `(-F)>>1` (fp) | CONFIRMED | `0x8007C0D8`. An arithmetic shift, not a C# division: they differ for odd values |
| 25 | E9: branch A (destroy 6 then continues; `+0xAC` destroy; puff every 8 ticks, 2 draws) and branch B (destroy 6 then return; 5 puffs at Z+0, 2 draws each; `+0x88=1`, flags \|0x40 &~0x30) (fp) | CONFIRMED | `0x8007EF50-0x8007F238` |
| 26 | E17 pseudo-code: mask, inclusive scan including slot 0, skip self / `+0xB4&0x40` / `+0x1CC` / mask; inclusive overlap; exclude `0x1AD`; not found → status 2; turn-back (fp, fw) | CONFIRMED | `0x8007F420-0x8007F654` |
| 27 | The decompilation reads `DamageResponses[5]`; the binary reads `+0x1CC` (fw 10, fp 4) | CONFIRMED | `0x8007F508 lw 0x164(a0)`, `a0 = slot+0x68`. The binary wins |
| 28 | `DestroyEntity` sequence (fp) | CONFIRMED, one slip | `0x8003A59C`. It clears the CHILD's `+0x68` and the entity's OWN `+0x1B8` (fp says the child's for both) |
| 29 | Hit-pass effect 4 for index 6/10 and effect 5 for 7/9 (fw) | CONFIRMED, plus a gate | `0x800395F4-0x80039660`. Skipped when the target's response byte satisfies `(b & 0xC0) == 0x80` (`0x800395E8`) |
| 30 | A spawned entity runs in its spawn tick (fp 2) | CONFIRMED (static) | see point 3 |
| 31 | The DLL builds its tick list before `MovePlayer` (fp 2) | CONFIRMED, and worse | the lag is to the next frame (`AlundraWorldProxy.cs:2617-2638`) |
| 32 | `UpdateAnimation` clears `+0xF8` at every call; balls get no impulse at spawn (fp 5) | CONFIRMED (static part) | clear at `0x80038AE4`, set only on a change at `0x80038B64`. DLL R2: `AlundraFrameSyncPasses.cs:206-226` |
| 33 | Normal arrows miss bodies with `SizeZ ≤ 16` (fp 3) | CONFIRMED (geometry, flat ground, equal base) | arrow anim-0 box Z 0 h11 at +16; Beannoïde body `OffsetZ 0`, `SizeZ 8` |
| 34 | Only `0x8002F818` spawns types 3, 4, 7, 11, 14, 15, 27-29 (fp 1, §7) | CONFIRMED | `spawnargs.py`, 102 sites. fp's addresses for the 236 and 25 spawns are not call sites: the real calls are `0x80074F38`, `750BC`, `75354`, `781C8` (236) and `0x80078F8C` (25) |
| 35 | Item spawn types {1, 2, 0x10, 0x11, 0x13-0x18} (fp, fw) | CONFIRMED | `0x80034284..0x800362A0` |
| 36 | `CheckAndTriggerTileEffect` callers | CONFIRMED, plus one | `0x8002FDC0` (hero), C61, C94 and `0x8007F2D4` (bomb E12, E20.h) |
| 37 | 0xC0 = `SetPlayerWeaponId(op+1)` and \|=0x80, size 2; 0xC1 = &=~0x80, size 1; table entry `0x800992AC` (fw §10) | CONFIRMED | `0x80041CDC`, `0x80041D18`. Table base `0x80098FAC` checked against 0x86 → `0x8003FFD4` |
| 38 | `TryUseItem` gate is the whole value `!= 0` (fw) | CONFIRMED, address slip | the function is at `0x8002ED64`; the gate load is at `0x8002ED80` |
| 39 | 0xC0/0xC1 used only at 472 @327/@378 and 473 @198/@289 (fw) | CONFIRMED | my linear walk of 7692 table entry points (1 unparsable). Map-event programs were not walked |
| 40 | Header data of the global sprites (flags, codes, bodies, BreakEffect 8 on 7-9) (fp §3, fw, tm) | CONFIRMED | `census.py` output |
| 41 | Hero attack rows, projectile sheets, fiches (fw §1, §6-7, tm) | CONFIRMED | `balance.json`. Items 5-7, 9, 10, 14, 16 have a fiche with count 0. Item 8: (6, no equipment bit, 0x6, 50) |
| 42 | Item 8 adds +50 to every hero attack (fw 12) | UNVERIFIABLE | depends on the c1b K2 rule, not re-derived |
| 43 | Highest rank wins in a slot (fw 8, tm 3) | CONFIRMED | all weapon flags = 1 |
| 44 | Item 1 must be zeroed (tm 3) | REFUTED | same evidence as row 43 |
| 45 | `Preset` has no weapon / MP / HP / money field; `TryBuild` starts from the new-game kit (fw 8, tm 2) | CONFIRMED | `AlundraTestSaves.cs:36-46`, `:112-134`. Pinned to two names: `AlundraTestSavesTests.cs:69-72` |
| 46 | `ArcSpec` has no items field; an arc with an arrival skips the new-game inventory (tm 4) | CONFIRMED | `AlundraArcSupport.cs:52-54`, `AlundraWorldProxy.cs:1907-1911` |
| 47 | DLL line references (fw §11) | CONFIRMED | `:178`, `:208-213`, `:275-277`, `:492`, `:525/529`, `:533`, `:539`, `:547`, `:573`, `:1131`, `:1157`, `:1173`, `:1214`. tm's `:567-570` for the stub is wrong: `:567` is the call, the stub is `:573-576` |
| 48 | E1 branch covers indexes ≤1 only, so E10 is not covered (fp §8) | CONFIRMED | `AlundraEventProgramRunner.cs:314-321` |
| 49 | No effect accessor on `IAlundraScriptHost`; reservoir `CreateAttached`/`Free` (fw 6) | CONFIRMED | `IAlundraScriptHost.cs`; `AlundraEffectReservoir.cs:205,237` |
| 50 | `ComputeNewHp` has no production caller (tm 1) | CONFIRMED | only its definition, `AlundraDamage.cs:76` |
| 51 | Facing at the spawn may lag in the DLL (fw 11) | CONFIRMED (mechanism) | `AnimationDirection` is written only in `SyncAnimation` (`AlundraFrameSyncPasses.cs:143`). See correction C8 |
| 52 | Run of the wave oracle (fp §5) | reproduced (not independent) | copy of f-projectiles' `p7.py steel`: R=96, S=117, reset t142, status 4 t143, box +42 px |
| 53 | E3 has 21 users; 88 of 217 targets have `SizeZ ≤ 16`; 0x87/0x95 site counts; map-77 chest content | UNVERIFIABLE | not decision-carrying for the slicing; not redone |

fw = f-weapons, fp = f-projectiles, tm = test-means.

## (b) Corrections

- **C1 (fw §0.1, §0.7).** fw does not say that the Torche copy has its six program words zeroed. That makes the "sharing" moot: nothing in E14 needs C60 or E9.
- **C2 (fw §2).** Two timer-clear sites are missing: `0x80032768` (the `+0x188 & 0x80` branch) and `0x800327A4` (HP == 0). Timer and pointer are always cleared together, at 7 sites.
- **C3 (fw §0.4).** `UpdatePlayerWeaponEffect` also calls `0x8003BE74`. `0x8004DF3C` is DecreaseMp; SetMp is `0x8004DEBC`.
- **C4 (fw §0.6, §2).** The two draws of the hold spark are FACT, not INFERENCE. They happen only if effect 0x10 was created.
- **C5 (fw §2).** The burst tables: `0x80023654` (X) is a sine of amplitude 768 starting at 0; `0x80023694` (Y) has amplitude 512.
- **C6 (fp §4, DestroyEntity).** It clears the child's `+0x68` and the entity's own `+0x1B8`.
- **C7 (fw §6, fp §4, hit effects 4 and 5).** Add the target-response gate `(b & 0xC0) != 0x80` at `0x800395E8`.
- **C8 (fw 11).** This is not an open risk: D-E14-6 already rules that new handlers read the values of the tick. The DLL already holds a per-tick direction row, `BoxDirection` (`AlundraEntityScriptProxy.HitPass.cs:37`).
  - INFERENCE: it is the counterpart of `+0x98` at the tick. The spawn direction and C94's trail argument should read it.
  - INFERENCE: in practice this matters when the facing changes 1 tick or less before the spawn, inside one frame, i.e. Hunter's air shot (delay 0).
- **C9 (fp 2).** The DLL is late by one frame, not by one tick (`TookPartInFrame`).
- **C10 (tm 3, presets `steel-m14` / `demon-m14`).** Zeroing item 1 is not needed; drop it from the presets.
- **C11 (tm §6, A8).** Map 136 sprite 49 (`SizeZ 16`) is never hit by a normal arrow at equal height; only the charged arrow works.
  - INFERENCE: map 77 record 22 (target at z80, ground 48 px): a normal arrow (box +64..+75 px) misses its body (80..112 px).
- **C12 (fp §7).** The spawn call-site addresses are those listed in row 34.
- **C13 (DLL, new).** `AlundraPlayerManager.cs:220-223` must clear `WeaponRecordPointer` and the charge timer, as at binary `0x80031BEC`/`0x80031BF8`.
  - Also documented there: `CreatePlayerAnimationEffects(1)`, `UpdatePlayerCarriedEntity(1)` and `0x800350C0` (E20.g).
  - The `== 0x20` hurt path stays E20.c2's.
- **C14 (fw §0.5).** The flail bounce does not need E20.e. It is a stand-alone tail of `0x8002FB14`. Port `CreatePlayerAnimationEffects(mode)` as a skeleton: the flail tail is real, the rest stays named stubs for E20.e. In the DLL it goes between `AlundraPlayerManager.cs:227` (Gravity) and `:230` (`UpdateItemUseDriver`), matching binary `0x80031C38` → `0x80031C3C` → `0x80031C44`.

## (c) Recommended split of E20.f

All of this is INFERENCE: an order of work, not new decisions.

**What each projectile needs to work at all (FACT, from the flags and the pick rule):**

| Shot | Flags | Missing handler | Consequence |
|---|---|---|---|
| Normal arrow | 0x2074: Impact, Hit, AnimEnd | E17 | After a wall or a hit it sits in status 3 forever: slot leak, no turn-back |
| Fire / ice ball | 0x2154 | E9 | No explosion, no destroy (leak) |
| Fire / ice ball | | C60 | Visual only |
| Wave | 0x205C | E10 | Lingers after its Hold end |
| Wave | | C61 | Grass only |
| Charged arrow | 0x2054, no DeactivateOnHit | E1 | Destroyed at a wall or the edge (effect 8 needs BreakEffect) |

**Rule:** never enable spawning of a type before its E handler exists. Gate `SpawnWeaponProjectile` per type.

**F0, test means and opcodes** (no prerequisite)
- Tasks:
  - `Preset` gains `Weapon` (slot 1-6), plus `Mp`/`MpMax` for E20.h.
  - Presets per test-means §7, corrected as in C10.
  - `ArcRun` helpers `Give`/`Equip`.
  - Opcodes 0xC0/0xC1 in `AlundraEventProgramRunner` (`ForcedWeapon` 0x80 exists, `AlundraGameState.cs:86`).
  - Update `docs/test-saves.md` and the two-name pin.
- Oracle: none (setters are already ported).
- Acceptance:
  - Round trip of the saves, `CheckPreset` bounds.
  - 0xC0 at 472 @327 sets slot 1 and bit 0x80; 0xC1 clears it.
  - `TryUseItem` stays an E20.h stub; note the gate.
- Stop: the save rules refuse a combination.

**F1, charge and release** (needs a host effect accessor)
- Tasks:
  - `UpdatePlayerWeaponEffect(player, pad)` as in row 11.
  - Release branch of `PlayerTryAttack`.
  - The 16-spark burst.
  - The charge record into `+0x88` and the pointer.
  - Timer and pointer clears in the two present DLL branches (C13).
  - The charged swings 0x18 and 0x19 hit through the existing hit pass.
  - Bow and rod charges arm the pointer; the spawn stays a counted stub.
- Oracle: f-weapons `probe.py` C* traces. Copy them into `docs/plan-e20-annexe/e20-f/oracle/` at implementation time.
- Acceptance:
  - Ticks of timer, aura, sound and spark exactly as the trace, for Steel, Fire, William and Crusher.
  - Release at S+D+59 fails; at S+D+60 it succeeds.
  - An air release is lost (D-E20-9).
  - The generator state after N sparks equals the oracle's.
  - Aura freed when the timer drops below 11.
- Stops:
  - The accessor needs a CasaEngine change.
  - The `+0x7D` liveness of an attached effect cannot be read from `AlundraEffectSlot`.

**F2, flail bounce** (no prerequisite)
- Tasks: the C14 skeleton, the flail tail at `0x80030434-0x80030654` on `AlundraCellStore` (column = px/24 via `0x80127168`, row = y>>20).
- Oracle: f-weapons F-* traces and test-means' rebound traces.
- Acceptance:
  - HitCounter-driven: touch at tick N → 0x08 or 0x25 at N+1.
  - Wall-driven: bounce at t10.
  - Air: 0x1F or 0x26, landing unchanged.
  - Force unchanged.
- Stop: the DLL's cell reader disagrees with the binary's indexing.

**PRE, E20.e0's spawn, before F3** (D-E20-24: prerequisites first)
- Spawn by global index into the first free slot 1..63, with same-tick membership (point 3).
- A global index → prefab table. It does not exist; the index lives only in `bankalundra_<N>` names.
- Balance sheet by type.
- Registry seam (D-E14-1).
- Effects of `DestroyEntity(e, fx)`.
- The Z model of point 2(d), if the author gives it to e0 (Q4).
- Loot and pickups are not needed by weapons: `Contents` is 0 on every projectile header. Those parts of e0 can follow later.

**F3, spawn wiring and the wave** (needs PRE)
- Tasks:
  - `SpawnWeaponProjectile` calls the spawn with: hero X, hero Y, Z + `(z<<16)`, dir `{0,16,8,24}[BoxDirection]` (C8).
  - Types 0x1B-0x1D enabled.
  - C61: tile cut as a named stub until E20.e.
  - E10: destroy.
- Oracle: f-projectiles `pworld` `steel_wave` and `_vs_target`.
- Acceptance:
  - R+21, Z raw = heroZ + 1.
  - Box sweep to +42 px by frame 5.
  - One hit per 25-tick window.
  - Reset at S+25, status 4 at S+26.
- Stops: PRE missing; the box per tick is not available for a spawned entity.

**F4, charged arrow** (needs F3 and Q2)
- Tasks: type 7 enabled; C94 (tile-cut stub, trail 0x19 with `>>1`); E1 with BreakEffect 8.
- Oracle: `william_charged_*`.
- Acceptance:
  - R+9 at Z+8.
  - Pierces (no DeactivateOnHit).
  - Hits a body with `SizeZ 8`.
  - Wall: E1 at the next tick plus effect 8.
- Stop: if Q2 is "later", ship without effect 8 as a documented gap.

**F5, normal arrows** (needs F3 and Q1)
- Tasks:
  - Types 3 and 4 enabled.
  - E17 as in rows 26-27, with a pure scan helper reusing `AlundraHitPass.AxisOverlaps`.
  - Needs constant-Z flight with wall / edge / height `ForceAdjusted` (point 2(d)).
- Oracle: `hunter_wall`, `hunter_east_to_map_edge`, `arrow_vs_tile_heights`, `hunter_vs_tall_target`.
- Acceptance:
  - Turn-back at I+1: anim 1, dir+16, flags \|0x140 &~0x30, sound 34, gravity.
  - Destroyed at reset+1.
  - Height byte 1 does not block; byte 2 blocks.
  - Map-16 balloon (type 0x1AD): contact, then the arrow flies on.
- Stops: Q1 refused (defer to E14); the Z model has no owner.

**F6, fire and ice balls** (needs F3, the Z model and Q1)
- Tasks:
  - Types 0x0B, 0x0C, 0x0E, 0x0F enabled.
  - C60 and E9 (rows 23 and 25); `MulHigh32`.
  - Hit effects 4 and 5 with the C7 gate.
  - Destroy with effect 6 on slope 1 or 4.
- Oracle: `fire_rod*`, `ice_rod*`, `fire_ice_vs_target`, `fire_on_deep_water`, `rng_draws`.
- Acceptance:
  - No spawn impulse; lands at S+10 (map-10 gravity).
  - 10 draws at impact; 2 per 8th tick.
  - Explosion 36 / 32 / 56 ticks.
  - Second hit 25 ticks later.
  - Map-303 fire switches react to the fire ball. The sword regression `AlundraFireSwitchRegressionTests` keeps passing.
- Stop: the generator drifts against the oracle.

**F7, play-test (recette)**
- The F0 saves on map 14 (melee and wave), 16 (bows), 303 (fire), 69 (flails: contact only, posts are native D31/E15).
- Before E14 nothing loses HP. The observables are sparks, `HitCounter` and the scripted 0x87 reactions.

**Effect on D-E20-18.**
- F5 and F6 change the owner of the named items E17 and C60 (plus E9, which D-E20-18 did not name). That needs the author (Q1).
- Everything else (F0-F4, PRE) changes only the schedule within decisions already taken (D-E20-17, D-E20-24), except BreakEffect (Q2) and the Z model's owner (Q4).
- C54 does not move: it is a boss handler and not a weapon.

## (d) Questions only the author can answer

1. **Q1, ownership.**
   - Facts: E17, C60 and E9 are used only by the hero's projectile sprites. The Torche's copy zeroes its handlers. C54 is a boss.
   - Do the arrow handler E17 and the ball handlers C60 and E9 move to E20.f, as your own D-E20-18 criterion gives?
   - Recommendation: yes. Otherwise bows and rods stay unusable until E14.
2. **Q2, BreakEffect.**
   - D-E14-2 exports the header byte `+0x1E` "in E14.a", and the charged arrow needs it (effect 8) in E20.f.
   - Should the export, with its format ADR, move to E20.f? Or should the charged arrow disappear without effect 8 until E14.a?
   - Recommendation: move it, same scope, done earlier.
3. **Q3, arrow height.**
   - In the original, normal arrows fly 16 px above the hero's base and pass over low enemies (`SizeZ ≤ 16`, the Beannoïde among them).
   - Keep it, as faithful behaviour with no proven defect, or treat it as a defect of the original?
   - Recommendation: keep it.
4. **Q4, owner of the non-hero Z physics.**
   - Gravity bit 0x100, impulse `+0xF8`, landing, and blocking for entities that fly at a constant Z.
   - E20.e0 (pickups need it too) or E20.f?
   - Recommendation: E20.e0.

## Files

- Binary: `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE`.
- DLL lines checked (no write):
  - `Alundra/Scripts/AlundraPlayerManager.cs:172-277, 492-579, 1131-1263`
  - `AlundraWorldProxy.cs:2610-2700, 1900-1911, 866-876`
  - `AlundraEventProgramRunner.cs:283-321`
  - `AlundraTestSaves.cs:36-68, 105-140`
  - `AlundraFrameSyncPasses.cs:85-226`
  - `AlundraEntityScriptProxy.HitPass.cs:28-45`
  - `AlundraEffectReservoir.cs:109-237`
  - `AlundraRandom.cs`
  - `AlundraDamage.cs:76`
  - `AlundraGameState.cs:80-94`
  - `Alundra.Tests/AlundraArcSupport.cs:52-54`, `AlundraHeroAttackTests.cs:53-69`, `AlundraTestSavesTests.cs:69-72`
- Data:
  - `data-extracted/data/map_alundra.json`, `map_14.json`, `map_16.json`, `map_77.json`, `map_136.json`, `map_303.json`, `map_326.json`, `map_443.json`, `map_472.json`, `map_473.json`
  - `alundra-project/Data/items-properties.json`, `balance.json`, `sprite-records.json`, `AssetInfos.json`
- Scratch: `.../scratchpad/disc-weapons/f-check/` (`xdis.py`, `scan.py`, `census.py`, `c0census.py`, `spawnargs.py`, `out_tryattack.txt`, `out_wfx.txt`, `out_step.txt`, `out_e17.txt`, `out_e9.txt`, `out_mp.txt`, `out_cpae.txt`, `out_ua.txt`, `out_spawnargs.txt`, `oracle_copy/`).
