# Adversarial check of the E20.b discovery

> Report returned by one agent of the E20.b discovery workflow of 2026-10-09, kept verbatim as evidence. The plan section 1.4 holds the reconciled facts.

**E20.b adversarial check of reports A (oracle) and B (facts)**

The oracle work holds up, and so do the binary facts behind the decisions. I found three corrections that matter for the plan. Each is marked in the table and repeated in the summary at the end. I re-ran `run_e20b.py`, which writes only under `oracle/out/`. It exited 0, and the regenerated `out/` is byte-identical to what was there before (146 files, same sha1). I wrote nothing in the repo; my helper scripts are in the scratchpad. Apart from the files listed in the run, all claims were checked with capstone and with the oracle run in memory.

## 1. Report B: binary facts

| Claim | Verdict | Evidence | Correction |
|---|---|---|---|
| Dispatch: `sltiu 0x50` at `0x80031D8C`, table at `0x80022D3C` | CONFIRMED | FACT: `80031D8C sltiu $v0,$v1,0x50`; `80031D98 addiu $v0,$v0,0x2d3c` | — |
| Ground group `0x80031E18` = 04 08 09 10 11 12 13 18 19 25 3F 40 44 49; air group `0x80031DF8` = 0A 14 15 16 17 1F 26 41 42 46 4B; 00/01 → `0x80031E38`; 02 2B 2C 2D → `0x80031E84`; default `0x80032758` = 0D 1A 1B 1E 27 37 38 3D 43 45 47 48 4A 4C 4D | CONFIRMED | FACT: all 80 table words decoded; the lists match exactly | — |
| Ground group sets `+0x88=0x2D` when IsOnGround is 0; air group sets `+0x88=0` when IsOnGround is non-zero | CONFIRMED | FACT: `80031E28 bnez $v0,..2680` / `80031E34 sw $v0,0x88`; `80031E08 beqz` / `80031E14 sw $zero,0x88` | — |
| "Both run TryUseItem (`0x80032680`)" | CONFIRMED with correction | FACT: `80032680 jal 0x8002ED64; 80032688 j 0x80032914`. `0x80032680` is also the handler of ids 21, 31 and 34. | TryUseItem is at `0x8002ED64`. It reads the pad (`8002ED68 lhu 0x6f2a`, `andi 0x20`), so "neither group reads the pad" holds only for the group bodies. |
| TryUseItem returns 1 when Circle has no edge | CONFIRMED | FACT: `8002ED74 beqz → 8002EDA8 addiu $v0,$zero,1` | — |
| Default body = `jal 0x800350C0` then END; END = `0x800307E8`, SetPlayerHpMax, SetPlayerHp | CONFIRMED | FACT: `80032758..60`; `80032914 jal 0x800307e8`, `lw 0x18` + `jal 0x8004dc68`, `lw 0x14` + `jal 0x8004dd30` | — |
| PlayerTryAttack: 0x2000 gate, then release first (`0x80126F2C`), then press (`0x80126F2A`), record = base + 13w (+4 in the air), anim 0 plays sound 3, success writes `0x80127110`, `0x80127114=0`, `+0x88` | CONFIRMED | FACT: `8002EB24 andi 0x2000`; `8002EB38 lhu 0x14($v1)` with `$v1=0x80126F18`; `8002EB54 slti 0x3c`; `8002EB68 beqz` (air release returns 0); `8002EC8C lhu 0x12`; `8002ECCC addiu -0x7387` (=`0x80028C79`); `8002ECFC addiu $a1,$v0,4`; `8002ED08 beqz → 8002ED30 jal 0x800490fc, a0=3`; `8002ED18/20/2C` | — |
| Release and press are disjoint on one bit (INFERENCE) | CONFIRMED | FACT: `AlundraTickPad.cs:56-57`: `pressed = s&(s^h)`, `released = h&(s^h)` | — |
| `0x8012710C` is written at the MovePlayer head; only PlayerTryAttack and UpdatePlayerWeaponEffect read it | CONFIRMED with correction | FACT (full-text scan): readers at `8002EC5C`, `8002ECB0`, `8002ECD8`, `8002F4E0`. Writers at `80031B8C` and also `80031A4C`, an init routine that also clears `0x80127110` and `0x80126FFC`. | Two writers, not one. Resolving the record inside PlayerTryAttack is equivalent only while UpdatePlayerWeaponEffect stays a stub (it needs the record in E20.f). |
| `0x80028C13` = 0 | CONFIRMED | FACT: byte read is 0 | — |
| Item map at `0x80028C14` | CONFIRMED | FACT: non-zero entries are {2:1, 3:2, 4:3, 5:8, 6:9, 7:10, 8:11, 9:6, 10:7, 14:5, 16:4} | — |
| The 12 records at `0x80028C78` | CONFIRMED | FACT: all 156 bytes match the table, including record 8 air delay 0 | — |
| Flag table at `0x80028D14` | CONFIRMED | FACT: byte 0 is set for 00 01 02 03 04 0E 29 2A 2B 2C 2D 35 3E; byte 1 for 11 13 15 17 18; no other value occurs | — |
| Second PlayerTryAttack call at `0x80031F74` (id 0x29) | CONFIRMED | FACT: handler `80031F54` holds id 29; `80031F74 jal 0x8002eaf4` | — |
| "The stub weapon-step pass clears it" | REFUTED (wording) | FACT: `0x8002F768` is real code, called in the MovePlayer prologue (`0x80031D68`). It clears the pointer at `8002F828/38`, and the oracle runs it as binary code. | It is the real UpdateWeaponStepProgression on tick S+1. Only the DLL's copy is a stub (`AlundraPlayerManager.cs:452`). |
| Air swing: ForceX 150524, falling by 4 per tick, then 75244 on tick 16 and 112886 on tick 17; "the jump arc is unchanged" | CONFIRMED for the numbers, REFUTED for "arc unchanged" | FACT (in-memory run: warm-up east, Right+Cross on tick 1, Square on tick 6): ForceZ and PosZ are identical to the plain jump. PosX at tick 21 is 3061766 against 3174912, about 1.73 px shorter. | Only the vertical arc is unchanged. A walking air swing slows the hero horizontally. |
| Exported D = binary D | CONFIRMED (sampled) | FACT: `round(last keyframe/0.02)` gives 21, 10, 21, 31 and 23 ticks for ids 16, 20, 63, 18 and 19, in all four directions. This matches the oracle's chain ticks (22, 15, 22, 32, 24). | — |
| DLL line references (:428, :438-444, :484, :513) | CONFIRMED | Read in `AlundraPlayerManager.cs` | — |

## 2. Report A: the oracle

| Claim | Verdict | Evidence | Correction |
|---|---|---|---|
| D1 still passes in the copy | CONFIRMED | FACT: `S-A`/`S-B` each give "VERDICT PASS: 216 values, 0 differences". The only tool diffs are the annex path (one more `..`, which resolves to `docs/plan-e19-d2c-valeurs.md`) and OUT going to `out/d1`. The D1 traces are byte-equal (`cmp`). | — |
| Two interpreters, mutations, load-delay check, unwritten bytes, determinism | CONFIRMED | FACT: log shows "40 scenarios, all identical", "all 15 mutations behave as expected" and the load-delay check at 0. All 42 `*.machine.txt` files say "must be empty: empty". The re-run output is byte-identical. | — |
| Weapons and items set by the real routines | CONFIRMED | FACT: `attack.py:63-74` reads the slot from `0x800B9FE8+10·item` and calls `0x8004E530` and `0x8004E484`. The record pointers written match the 13-byte layout (for example `0x80028C86` = rec1+1). | — |
| The 0x2000 cell | CONFIRMED | FACT: cells return gp=0x20, `+0x180`=0x2000, and the success tail `0x8002ED14` is never reached. Only columns 3–4, rows 5–6 block. | — |
| Pad words | CONFIRMED | FACT: `world.py:42-43,79-82` writes hold, pressed, released and repeat. Each tick is cross-checked against the binary routine `0x8002E250` (`world.py:203-207`). | — |
| No embedded expected value | CONFIRMED with a caveat | FACT: the outputs are only read back from the trace rows. One constant is embedded: 159744 is used as the warm-up stop condition (`attack.py:131`). It is an input gate (the run raises if never reached), not an asserted output. | — |
| Key ticks match the bank data | CONFIRMED | FACT: the chain tick is S+D for every weapon: 1+21=22, 5+10=15, 1+31=32 (0x12/0x11), 1+34=35, 1+23=24, and 5+14=19 (0x17 facing right, D=14). AT6 sweep: p=22 is lost and p=23 swings. | — |
| "AT8 lands on tick 10" | REFUTED (minor) | FACT: IsOnGround becomes 1 on tick 9. Sound 11, the dust and Moving come on tick 10. | Landing on tick 9, landing effects on tick 10. This matches the AT3 pattern (on ground at 21, effects at 22). |
| "Air swings do not disturb the jump arc" | REFUTED as a general claim | FACT: the AT3 variants are standing jumps (ForceX 0). See the walking air-swing run above. | True for vertical motion only. |

## 3. Does the DLL clock reservation move the swing end?

**No. In a one-tick frame the DLL swing should end on the same tick as the oracle (S+D).** This is an INFERENCE from the code; the DLL attack is still a stub, so it is not measured yet.

- **Press tick S.** In the DLL, MovePlayer sets the target, then the clock pass sees a pending switch, reserves the tick and returns without advancing (`AlundraFrameSyncPasses.cs:230-246`). `SyncAnimation` resets the logical tick to 0 and does not advance when one tick is reserved (`:173-185`). This matches the binary's frame 0 not being counted (animation pass `0x80038E18` runs after MovePlayer pass `0x800386D0`).
- **Ticks S+k.** The logical tick is k. `AnimationFinished` fires when it reaches round(0.42·50)=21 (`AnimatedSpriteComponent.cs:360-371`), on tick 22 = S+D. That is the oracle's chain tick.
- **Press on the chain tick.** On tick 22 MovePlayer runs before the clock pass and still sees 0x10, so the press is lost. This matches the AT6 sweep.
- **Kinematic tick.** It is keyed on `TargetAnimationId` (`AlundraScriptedMotion.cs:86`), and the E20.a pass order is "clock, physics". So the halving with Acc 1 on S and the 0x2D halving on the chain tick use the same animation set as the binary.
- **Catch-up frames.** These are compensated (`reservedTicks-1`), as already pinned by TD9 with pattern "60".
- **One new risk (INFERENCE).** Take a two-tick frame where tick 1 is the chain tick and tick 2 has a new press. The chain's `TakeZImpulse` sets `ZImpulseTaken` (`:257-258`). The press tick then skips `TakeZImpulse` (`:231`), so sound 29 would not play. The binary plays it (AT6 p=23). The end tick is still right. A test should cover this.

## What is still missing for the E20.b plan

1. **Weapon tables in PlayerTryAttack.** How the code gets `AlundraItemTables` in both the world host and `ContactHost` is UNKNOWN. This is a design decision.
2. **Weapon table as data (D-E20-7).** Where the 156 record bytes and the 100-byte item map live (balance.json, a new data file, or a C# constant) is UNKNOWN. It is not in the reports.
3. **Test rig.** `JumpHeroRig` needs a sprite clock and an end bridge for all four directions, or a test map/arrival where the real prefab is left free.
4. **Pins to add:**
   - walking air-swing ForceX (decay by 4, 75244/112886 after the chain) and the shorter PosX;
   - the AT8 ledge cut on tick 2;
   - AT7 blocking only from the four footprint-corner cells;
   - the ladder grab overriding an attack state;
   - the catch-up-frame sound case from section 3.
5. **Scope decision.** Does E20.b port the charge-timer tick? A tap with a chargeable weapon sets it to 1, and a hold counts from tick 22. Or does all of that go to E20.f?
6. **Staff sound 3.** `ApplyGroundOrAirTail` does not receive the host, so a route to the sound player is needed.

Summary of corrections:
- Report B: the "jump arc unchanged" claim (only the vertical arc is), the name and address of TryUseItem (`0x8002ED64`, not `0x80032680`), the second writer of `0x8012710C`, and the step pass being real binary code, not a stub.
- Report A: the AT8 landing tick is 9, not 10, and "air swings do not disturb the arc" holds only for standing jumps.
