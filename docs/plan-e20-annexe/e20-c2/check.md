> Report returned by one agent of the read-only discovery workflow for E20.c2 and E14.a of 2026-10-09 (slice E20.c2), kept verbatim as evidence. The plan holds the reconciled facts.

# E20.c2 adversarial check (label c2-check)

**Method.** I worked read-only. I disassembled `ALUN_CD.EXE` (France) with my own capstone scripts. I dumped the jump tables, scanned the whole image for `jal`/`sw` sites, and compared the DLL direction tables against the binary with a script. I also ran my own JSON census of the 483 maps and read the existing H3 oracle rows, the DLL, the tests and the decompilation.
- Scratch folder: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-c2-e14a/c2-check/` (`d.py`, `scan.py`, `tables.py`, `dirtab.py`, `census.py`).
- I built nothing, ran no repository tool, and wrote nothing into the repository.
- DLL paths are relative to `D:/development/repo/alundra-casaengine-project-converter/`.

## Things that change the plan

1. **Moving HP onto the entity touches every map entry, not only save loads.**
   - FACT: `AdoptPlayerPawn` runs on every world install (`AlundraWorldProxy.cs:832`). It calls `InitializeBalance` (`:1978`), which sets `Hp = HpMax = record.MaxHp` = 10 (`AlundraEntityScriptProxy.HitPass.cs:70-71`; balance.json entry 0 has `MaxHp 10`).
   - So an end-of-tick mirror without a pull would reset the stats to 10/10 at the first tick of every map, every load and every Continue.
   - FACT: the binary pulls right after `InitializeEntity`: `jal 0x80039D04` at `0x800319EC`, then `GetPlayerHpMax` `0x800319F8` → `+0x18`, then `GetPlayerHp` `0x80031A00` → `+0x14` (`0x80031A08`).
   - FACT: these are the only callers of the two getters that store into the hero. The other callers are the HUD (`0x8004B738/B79C/C210/C218/C234/CCD0`).
2. **The vessel seam depends on END running in the locked branch.**
   - The vessel opcode runs inside a locked script. The binary reaches END from the locked branch (`0x80031C0C/C20/C28`), so the stats follow the entity during the lock.
   - Moving ADR-0048 onto the entity without routing the DLL's locked `return` (`AlundraPlayerManager.cs:220-223`) through END would freeze the HUD until the lock ends.
3. **D-E20-14, "the driver `0x8003634C` for real", is dead code until E20.h.** All three reports agree and I confirm it.
   - The only non-zero writer of `[0x80127164]` is `0x80034A90`.
   - The driver returns 1 at once when that word is 0 (`0x80036358 → 0x800364B4`).
   - Changing D-E20-14 is the author's call.
4. **The `controlFlags == 0x20` path belongs to the hero-grab sequence of native handlers (E14).**
   - FACT, `0x80062678-0x80062740` (`a1` = the hero): the handler tests hero `+0xB4 & 0x40` and hero `+0x1CC`, then sets hero `+0x88 = 0x56` and `controlFlags |= 0x20`.
   - FACT, `0x80074D40-0x80074DA8`: while `0x20` is set, a touched hero gets `+0x88 = 0x58` and `+0x8C = 0`. On landing (`+0x88 == 0x58`, `+0x144 != 0`) the handler sets **hero `+0x1CC = 0x78`** (`0x80074D98`), clears `0x20` and sets `+0x88 = 0`.
   - In c2, port only the 6 lines and add a test that pokes the flag.
5. **The no-death cut needs no new code.** D-E20-5 already says the branch stays unported, and the DLL has no `Hp == 0` test today (`AlundraPlayerManager.cs:232-234`).
   - Hp can reach 0. Regeneration stops at 0 (gate `0x80030C6C`).
   - **New:** opcode `0xC3` (full restore, `0x80041D6C`: hero `+0x14 = +0x18`) and `0xBC` are unported and unowned (plan-e19 M-05 list).
   - After c2, lost HP comes back only through regeneration accessories (`0x37`/`0x38`), life vessels and item heals (E20.h). This is a product gap for the author.
6. **Blink design.** Use the blink term only, `IsActive && (DamagedTickCounter & 3) == 3`. Do not add the binary's status 2/3 draw filter: that would newly hide `Loaded` (status 1) entities, which is out of scope.
   - It must never un-hide a `FlagToDestroy` entity.
   - Whether the blink is generic or hero-only is an author question (Q3).
7. **Acceptance shape.** The DLL `HitArena` runs on real map 14, which has no hazard cells (census below), and it cannot spawn an Orc.
   - Hazard costs, armour tables, regeneration and the stats clamp are best accepted as **replay tests of pure DLL functions** fed with the oracle's per-tick inputs.
   - Full-tick DLL-vs-oracle comparison only fits the toucher scenarios on the map-14 arena geometry.
8. **Persistence.** In the binary the regeneration counters `0x80126FE8/EC/F0` and the sheet `0x80127008` are globals that nothing resets at map load.
   - FACT: the only accesses are in `0x800307E8`, and its debug tail (`0x80030D88`, `0x80030F84`).
   - `AlundraEquipmentRecords` is "one instance per world" (`AlundraEquipmentRecords.cs:8`). The counters should live in session state, and the sheet should be computed at adoption. Otherwise a hit on the first tick of a map reads an empty sheet.
9. **Pre-existing E20.b gap that c2's END rewrite should close.**
   - The binary locked branch zeroes `[0x80127110]`: the `sw` sits in the delay slot of the `jal 0x8002F884` at `0x80031BF4`. The DLL locked branch (`:220-223`) does not zero `WeaponRecordPointer`. Only the `+0x20` branch does (`:211`).
10. **Split c2.** I recommend c2-0 (oracle), c2a (HP model, END on every exit, `UpdateItemEffectState`) and c2b (the hurt). See (c).

## (a) Check table

Status codes: **C** = CONFIRMED, **R** = REFUTED, **U** = UNVERIFIABLE (by me, statically), **N** = new fact from this check. Report codes: **B** = c2-binary, **D** = c2-dll, **O** = c2-oracle.

| # | Claim (report) | Verdict | Evidence |
|---|---|---|---|
| 1 | END `0x80032914`: `jal 0x800307E8`, `SetPlayerHpMax(+0x18)` `0x80032924`, `SetPlayerHp(+0x14)` `0x80032930`; reached from every exit (B, D, O) | C | disasm `0x80031BBC`, `0x80031C0C`, `0x80031C20/C28`, `0x80031C98`, `0x80032910→0x80032914` |
| 2 | Pull from stats only at hero spawn (B, O) | C | getter caller scan; pull at `0x800319F8/0x80031A00/0x80031A08` inside the function at `0x80031974` (sole caller `0x8003B2D4`). D's "pull at `0x80031974`" names the function entry, not the `jal`s (imprecise). |
| 3 | `InitializeBalance` sets the hero to 10/10; the stats pull is "deliberately NOT ported" (B, D, O) | C | `HitPass.cs:70-71`, `AlundraWorldProxy.cs:1826-1828,1978` |
| 4 | `ApplyPendingLoad` (`:790`) runs before `AdoptPlayerPawn` (`:832`) (D) | C | same method, in that order |
| 5 | `0x800333AC`: `+0x18 += 1`, `+0x14 = +0x18`, no cap, sound `0x31` (B, O) | C | `0x800333D0-0x800333E4`; callers `0x80033E70`, `0x80034080`, `0x800341C0` |
| 6 | Stats clamps (B, D) | C | `0x8004DC68` (`>= 0x33 → 0x32`, `< 0 → 0`); `0x8004DD30` (`HpMax < a → HpMax`, `< 0 → 0`); DLL `:983`, `:1015` match |
| 7 | Heal `0x80032E50` adds `max(5, HpMax/4)` (B) | C, incomplete | it is also capped at HpMax (`0x80032E84-0x80032E90`); callers `0x80033F5C`, `0x800346B0` |
| 8 | AddHp/SubHp wrappers `0x8004DD84/DDB0/DCC0/DCEC` have no caller (O) | C | no `jal`, and none appears as a data word anywhere in the image |
| 9 | Entry `0x80031A68` rule (all three) | C | disasm `0x80031A68-0x80031B4C`; table bytes `00 00 01 00 02 00 03 00` at `0x80028B8C`, read with `lh`; cost `n == 0` returns −1 |
| 10 | Decompilation table `[0, 0x100, 0x200, 0x300]` (D) | C | `StaticVariables.cs:696` (decompilation, read only) |
| 11 | `GetDirection 0x8002E814` returns 0..31 | C/N | `andi 0x1F` at `0x8002E904`/`0x8002E8BC`, so dx = dy = 0 still hurts |
| 12 | DLL `ScriptHelper.GetDirectionToTarget` is bit-for-bit (D, O; B says "not oracle-checked") | C (static) | both tables equal the binary's (`0x80022488`, 256 halfwords; `0x80022688`, 29 words; script compare); quadrant arms match `0x8002E8A8-0x8002E900` |
| 13 | Exactly two callers of `0x80031A68`: `0x80031C14`, `0x80031C44` (B) | C | whole-image `jal` scan |
| 14 | Hurt block: `s2` read at `0x80031B98` before `0x8002F120`; `0x31`/`0x3A` choice; `+0x8C`; `0x78` (all) | C | `0x80031C58-0x80031C9C` |
| 15 | Immunity / `0xC0` responses still give the hurt (B FACT; O INFERENCE) | C | the return value of `ComputeNewHp` is discarded at `0x80031A94`; O's INFERENCE becomes FACT |
| 16 | Hit pass uses the hero's `+0x1C4` (all responses 0), so the hero is never immune there (B) | C | `0x800395D4-0x800395EC`; balance entry 0 `DamageResponses` all 0 |
| 17 | Death test only on a no-damage tick (`0x80031CA8`); table `0x80022FBC` (B) | C | dumped: `0x1C→0x80032898`, `0x1D/0x3E→0x800328F8`, `0x31/0x39→0x80032870`, `0x3A/0x3C→0x80032884`, `0x3B→0x800328CC`, `0x4E/0x4F→0x800327F0` |
| 18 | Decompilation loss on `0x3B` + slope 4 + `+0xAC == 0` (B) | C | binary leaves the state (`0x800328E8`); decompilation `PlayerManager.cs:120-128` writes `0x39` |
| 19 | General state table (B, D, O) | C | `0x80022D3C`: `0x31→0x80032680`; `0x1C/0x3E→0x80032558`; `0x3A/0x3B→0x80031DB4`; `0x1D/0x39/0x3C/0x4E/0x4F→0x80031DD0` |
| 20 | `0x80032558`: `TryUseItem() == 0` → flip `+0x8C` and END; `+0xAC == 1` → flip, `+0x88 = (+0x144 != 0) ? 0 : 1` (B, D) | C | `0x80032558-0x800325A0`, `0x8003270C-0x80032728` |
| 21 | Slope-4 table (B) | C | `0x80022E7C`: `0x1C/0x31/0x3E→0x80032658` (flip, `0x1D`); `0x3A→0x80032680`; `0x3B→0x800326FC` |
| 22 | Timeline `0x31` T..T+3, `0x1C` T+4..T+14, Idle flipped at T+15; ForceX −196608, then −184320 down by 12288 to −61440, then −30720 (B, D, O) | C | existing `e20-c1/oracle/out/H3.txt` rows 18-33 (dir 24→8→24, `c1CC` 119 at 18, hp 9) |
| 23 | Tick order: events → counters → lists → `0x80038E18` → physics → `0x80038E84` → hits; gate `& 0x48` runs the lists only (B) | C | `0x8003B388-0x8003B408` |
| 24 | Draw list is status 2/3 and `(+0x1CC & 3) != 3`, with no `+0x20` test (B, D, O) | C | `0x800385D0-0x8003860C` |
| 25 | Hidden on the hurt tick; hidden ticks T+4j, j = 0..29 (B, O) | C | arithmetic from #23 and #24 (`0x77 & 3 = 3`) |
| 26 | Hit-pass gate `+0x1D0`, `+0x1CC`, `+0xB4 & 0x40`, `Flags & mask` (B, D) | C | `0x8003941C-0x8003945C`; DLL `AlundraHitPass.cs:116-122` |
| 27 | Next touch at the hit pass of T+119, hurt at T+120 (B) | C | hit pass is the last pass (`0x8003B3F0`) |
| 28 | Shadow released on the same bit test (B, D) | C | `0x80038ED0`: `s0 = ent + 0x134`; `+0x98` = `+0x1CC`; `+0x84` = `+0x1B8`. The DLL has no shadow renderer (`AlundraEventProgramRunner.cs:2545`). |
| 29 | Hero `+0x1CC` writers: `0x80031C9C`, `0x80031C2C`, `0x8002BC2C`, `0x8004002C` (B) | **R** (incomplete) | also `0x80074D98` (`a0` = `0x80127D30`, set at `0x80074D58`), native, E14 |
| 30 | No engine change for the blink (D) | C (static) | `Entity.IsVisible` (`Entity.cs:107`), `Draw` returns early (`:513`), `World.cs:823` draws through `entityBase.Draw`. Other renderables (effects) are not covered: U. |
| 31 | `0x20`: whole-word compare at `0x80031C0C`; setters `0x80062740`, `0x80075264`; clearers `0x800621DC`, `0x80062330`, `0x80074DA8` (B; O misses `0x80074DA8`) | C | `sw` scan of `-0x3B48(`; also whole-word zero stores `0x8002C5A0`, `0x8005152C`, `0x80081330`. Owner sequence: item 4 above. |
| 32 | DLL `InputBlockedMask` = `0x34`; nothing in the DLL sets `0x20` (D, O) | C | `AlundraGameState.cs:70-94` |
| 33 | Step 1 (boots level) and its 4 readers (B) | C | `0x80030814-0x8003088C`; `lw` sites `0x80031AD0`, `0x800368BC`, `0x800369E0`, `0x800374FC`; spawn zero `0x80031730` |
| 34 | Step 6: copy 255 bytes (record 0, else `[0x80127EF4]`) and combine (B, D) | C | `0x800309A8-0x80030B3C` (240 + 12 + 3 bytes); combine `0x80030B40-0x80030C18`. The `A`/`B` defaults of `0x10` are set once before the loop and overwritten only when a record exists. |
| 35 | Step 7 regeneration: gates, loop, period = byte 2 + 1 (B, D, O) | C | `0x80030C20-0x80030D3C`; entry 85 has `MaxHp` (byte 2) = 200, giving a period of 201 |
| 36 | Driver `0x8003634C` and its setters (B, D, O) | C | `0x8003634C-0x800364C4`; `sw` to `0x7164` at `0x80032964`, `0x80034A90`, `0x800351A4`, `0x800364A8` |
| 37 | `0x8002F884` returns when `+0x2C == 0` (B, O) | C | `lw 0x7D5C` (= hero `+0x2C`), `beqz` at `0x8002F89C` |
| 38 | `TryUseItem` returns 1 unless there is a Circle edge (B) | C | `0x8002ED64-0x8002EDA8` |
| 39 | DLL early returns skip END (all) | C | `AlundraPlayerManager.cs:208-213`, `:220-223`, END at `:463` |
| 40 | DLL locked branch does not zero `WeaponRecordPointer` | N | binary zeroes it at `0x80031BF8` (gap left by E20.b) |
| 41 | Hazard census: 36 maps, 6738 / 559 / 25 cells (D, O) | C | my own JSON census |
| 42 | Hazard maps include "52 to 60" (D) | **R** | map 53 has none; the list is 0, 27, 36, 52, 54-60, 84, 86, 87, 89, 90, 92, 94, 101, 104, 107, 120, 122, 160, 325, 411, 422, 425, 426, 438, 443-448. O's list is right. |
| 43 | No hazard cells on 14, 15, 44, 115-117, 127, 389-392 | C | census |
| 44 | Hero animation sets of `0x31`/`0x1C`/`0x3A`/`0x3B` (Speed, byte `0xD`, Sfx) (B, D) | C | `sprite-records.json` hero record: 256/64/23, 0/68/0, 256/64/23, 0/68/0 |
| 45 | DLL `Slope_18c` reads 4 on deep water (D) | C (static) | `AlundraEntityScriptProxy.cs:1840` (`0x800 >> 9 = 4`); not run |
| 46 | The c1a pins move (D, O) | C | `AlundraHitPassTests.cs:272-293`, `:296-313`, `:316-335`, `:452-458` |
| 47 | "Touches at 1 and 121" for the contact pin (B §0.8) | **R** (scenario mismatch) | B's `hurt_run.py` holds Right (`RIGHT` as `hold_after`). The pinned test `WhileTheContactLasts` uses pad 0 at 20 px. INFERENCE: a knockback of −32.6 px leaves the hero about 53 px away, beyond the Beannoid's [−18, 20] reach, so only tick 1 within 80 ticks and probably none after. The oracle decides. |
| 48 | 30 `MovePlayer(` sites in 7 files, 6 with `host: null` (D) | **R** | 34 sites in 8 files (`AlundraHeroAttackTests.cs` has 4); 27 pass `host: null` |
| 49 | Lever arc files are untracked (O) | **R** | `git ls-files` lists all 7 `AlundraLever*.cs` |
| 50 | Lever arcs start next to Beannoids on 115-117 and 44 (B) | U | needs the map sprite lists, or the arcs run after c2 |
| 51 | Oracle tools exist and the hit pass is real (O) | C | `e20-c1b/oracle/tools/` has no `scen.py` (copy it from e20-c1, as O says). Runtime and size estimates: U. |
| 52 | The 0xC3 handler writes the entity | N | `0x80041D7C-0x80041D84` (`+0x14 = +0x18`) |
| 53 | Bare rigs with the singleton are exposed to an unguarded mirror (D) | C | `AlundraSaveBookTests.cs:66,113` (`AlundraGameState.Instance` with a bare `HeroAt`, Hp 0/0) |
| 54 | Vessel tests assert the stats right after the opcode | N | `AlundraNativeEffectTests.cs:316-345` (rows P "vessel then end of MovePlayer"), `:372`. They move with the seam. |

## (b) Corrections

- **c2-binary**
  - §0.2: the pull is needed at every world adoption, not only "after a loaded save". It must come after `InitializeBalance`, as the binary pulls after `InitializeEntity` (`0x800319EC` → `0x800319F8`).
  - §4.1: the list of hero `+0x1CC` writers is missing `0x80074D98`.
  - §0.8/§8: "at rest" is wrong; the scenario holds Right. The standing contact test will not show a second touch at 121 (#47).
  - §2.3: the heal is capped at HpMax.
  - §4.3 rule: drop `status in {2,3} &&` from the DLL rule (thing 6).
  - U5: the sequence is the hero grab (`0x56`, then `0x58`), with the handlers around `0x80062678` and `0x80074D40`. Mapping them to sprite indices is still open.
- **c2-dll**
  - Hazard maps: map 53 is not a hazard map.
  - Call sites: 34 in 8 files, 27 with `host: null`.
  - The pull address is `0x800319F8-0x80031A08`, not `0x80031974`.
  - G10 is mostly moot: plan §0.2.5 already says "0 à 3 PV" and cites `0x800385F0`. Plan §0.2.2 lists the entry before `+0x20` and the locked branch, which is the wrong order (wording only).
- **c2-oracle**
  - Lever files are tracked.
  - The `0x20` clearers include `0x80074DA8`.
  - §0.8 is FACT, not INFERENCE.
  - "Second hurt at 138 in H3" is arithmetic, and it holds only if contact resumes (its own U3).
  - Group H (driver probe) adds nothing: drop it and keep the invariant columns.
- **Plan / decompilation traps to record**
  - The decompilation calls the boots level `g_gravityFlag` (`PlayerManager.cs:3175`).
  - `FindWarpFacingDirection` is the damage entry, and `AnimateWarpEffect` is the item cancel. The DLL class doc's "pure visual-effect call" (`AlundraPlayerManager.cs:66-67`) and "no item/HP system" (`:70-71`) are stale.
  - The `:47` doc ("`Hp` stays the C# default 0") is stale since c1a.

## (c) Recommended slice design: split into three

**E20.c2-0: oracle** (`docs/plan-e20-annexe/e20-c2/oracle/`, copy of the c1b tools plus `scen.py` from c1, new files only).
- Scenarios, trimmed from O:
  - A1: H3 extended to 200 ticks, plus a pad-0 contact variant at 20 px.
  - A2: armour × HP grid.
  - A4: bearings.
  - A6: the invulnerability window.
  - B1, B2: hazard costs, boots and floor.
  - D1: the control-flag sweep, including `0xA0`.
  - E2, E3: the entity wins over the stats; spawn from the stats.
  - F1, F2, F4, F5: regeneration.
  - G: boots and sheet, K1-style.
  - C1 (water) and E1 (HP 0) as informational, feeding E20.i and E18.
  - Drop H.
- Columns as listed in O §4, plus `visible` and the stats mirror.
- **Acceptance:**
  - the D1 gate is unchanged;
  - the common columns of H3 equal c1's byte for byte;
  - the second interpreter matches;
  - every mutation is caught;
  - two runs give the same manifest.
- **Stop** if A1's tick-18 row differs from H3.

**E20.c2a: HP model, END and `UpdateItemEffectState`.** Nothing visible changes until c2b, except the stats are kept across maps.
1. Pull `Hp`/`HpMax` from the stats right after `InitializeBalance` in `AdoptPlayerPawn` (`0x800319F8-0x80031A08`).
2. END on every exit, the frozen and locked branches included:
   - the locked branch also zeroes `WeaponRecordPointer` (`0x80031BF8`);
   - END is step 1 → steps 2-5 → step 6 → step 7, then `SetPlayerHpMax(hero.HpMax)` and `SetPlayerHp((short)hero.Hp)`;
   - the mirror runs only for a hero that pulled (bare rigs skip it), documented in the code.
3. Vessel seam onto the entity (`0x800333AC`: `HpMax + 1` uncapped, `Hp = HpMax`). Fall back to the stats when there is no hero. Amend ADR-0048.
4. Step 1 cached and shared with `AlundraScriptedMotion` (`BootsLevel` becomes internal).
5. Step 6: the hero sheet copies bytes 2..13, with byte 2 copied as is and bytes 3..13 combined. It is a reusable `AlundraBalanceRecord`-shaped object, computed at adoption too.
6. Step 7: the three counters live in session state and the method gets the pad hold word.
7. Fix the P4 bug: the clear wipes only the pointers.
8. The driver call at `0x80031CC4` stays a named stub.

- **Acceptance:**
  - replay tests of steps 1/6/7 and of the clamp against oracle groups E, F and G;
  - an `ArcRun` crossing two maps with the stats at 4/12 keeps 4/12;
  - a load at 4/12 survives the first tick;
  - vessel rows P re-pinned "after END";
  - the full suite;
  - `hero-trace-389` and the intro traces unchanged.
- **Stops:** any story-arc or trace pin moving while the stats are 10/10; or any save test (E16) failing.

**E20.c2b: the hurt.**
1. Port the entry as a pure function. Replay it against A2, A4, B1, B2 and D1 (hazard inputs come from the oracle's `vram_or_180` column).
2. Normal-branch hurt block:
   - zero the charge counter and the weapon pointer;
   - call `UpdatePlayerCarriedEntity(2)` and the driver stub;
   - choose `0x3A` when `s2 == 4`, else `0x31`;
   - set the direction and `+0x1CC = 0x78`.
   - Also port the locked `== 0x20` path. Keep the `Hp == 0` and `+0x188 & 0x80` branches unported, documented at the site.
3. States `0x31` (`0x80032680`), `0x1C` (`0x80032558`, shared with `0x3E`, coordinate with E20.d), and `0x3A`/`0x3B` (`0x80031DB4`).
4. Blink in the lists step: for `IsActive` entities only, never touching `FlagToDestroy`; hero-only or generic per Q3.
5. Re-pin c1a:
   - H3 up to the oracle's end (no ignore clause);
   - H1-both;
   - the contact pin from the A1 variant;
   - the reach sweep widened to −18..59;
   - `HitArena` gains columns for direction, forces, `IsOnGround`, stats Hp and `Owner.IsVisible`.
6. Run the lever arcs, the map-127 iron-ball case and the story arcs with a watch on hero `TouchingEntity`/`Hp`. Record every pin that moves.

- **Acceptance:**
  - DLL `HitArena` equals oracle A1 on all compared columns, `stats_hp` and `visible` included (visible taken at the last tick of each frame);
  - the replay suites pass;
  - in-game recipe on map 14: hearts drop (with the gauge open, U), the hero blinks 30 of 120 ticks, he does not die.
- **Stops:**
  - knockback force or position differs from the oracle: stop (a physics issue, not c2);
  - a lever or story arc changes trajectory: stop and report;
  - an engine change is needed for the blink: stop;
  - a second fix/reverify loop fails: escalate.

## (d) Questions only the author can answer

1. **D-E20-14:** may the driver `0x8003634C` and `UpdatePlayerCarriedEntity(2)` stay named stubs at their exact sites? They are dead code until E20.h and E20.g.
2. **D-E20-5 at HP 0:** confirm that 0 hearts are shown and the hero keeps control with regeneration off (the literal reading), rather than a floor at 1 (a deviation).
3. **Blink scope:** should it be generic? That is faithful: entities made invulnerable by opcode `0x86` (76 corpus sites) will flicker. Or hero-only for now (a deviation)?
4. **Healing:** `0xC3` (full restore, entity-borne) and `0xBC` (HpMax +n) are unported and unowned. Should c2 port them as entity writes, or name an owner?
5. **Water:** accept the faithful general tail (a one-tick hurt on deep water in the DLL) until E20.i, or force `0x31` in water as a documented deviation?
6. **Split:** may c2 become c2-0, c2a and c2b as above?
