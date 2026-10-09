# Completeness critic

> Read-only discovery of 2026-10-09 for E20 (docs/plan-e20-heros.md, section 0.2). Raw output of one workflow
> agent, kept as evidence. Where this file and its binary check disagree, the check and the plan section 0.2 win.

# Completeness critique before the "hero sprint and attack" plan

Labels: FACT (with source), INFERENCE (with basis), UNKNOWN. Binary means `ALUN_CD.EXE` (France). The re-checks marked "re-checked here" were decoded in this pass with capstone; nothing was written to disk.

## 1. Contradictions, and which side the binary supports

| # | Survey says | The other side says | Who is right |
|---|---|---|---|
| C1 | Sprint §1: in PrepareSprint, "the first test of the tick is airborne". | The sprint check (row 4) puts the `lw 0x144` at 0x80031F88, after the three `jal` calls. `TargetDirection` is written in the delay slot `ac91008c`. | The check. FACT. |
| C2 | Sprint §2: the SprintDash handler gives "Jump". | Check: it writes 0x2D and ignores TryUseItem's result (`j 0x80032914`). | The check. FACT. |
| C3 | Sprint §6: sprint dust is thrown at −0.5 × force on every slope. | Check row 12: slopes 1, 2 and 4 use −0.25 × force, and dash/stop dust has zero force there. | The check. FACT. |
| C4 | Sprint §4/§9: pushing blocks while sprinting is UNKNOWN. | Check, missed item 1: `jal 0x8002E910` sits only in the Idle/Moving case. | The check. FACT: no interaction happens from states 0x29, 0x03 and 0x2A. |
| C5 | Attack §4: 0x80031C5C is a "warp" path. | Attack check, missed item 4, and hits check row 13: it is the hero-damaged path (`jal 0x80031A68`, return ≠ −1). | The checks. FACT. |
| C6 | Hits §4: HP == 0 is tested "right after the damage entry". | Hits check row 13: after a hit, 0x80031C98 jumps to the end. 0x80031CA0 is reached only on a tick with no damage. | The check. FACT: death starts one tick late. |
| C7 | DLL §2: 0x86 "adds" to `DamagedTickCounter`. | Hits check row 18: `sw s0,0x1cc` with `s0 = b2\|b3<<8`. | The check. FACT: it is a SET. |
| C8 | DLL §4: hero animations 0x41 and 0x4E are "not extracted". | Attack §8 gives durations for 0x41. | Re-checked here. FACT: `alundra-project/Entities/Alundra/bankalundra_0_anim65_down.anim2d` and `…anim78_down.anim2d` exist (376 files). The DLL survey simply did not read them. |
| C9 | Chain §8, slice 1: the levers in 115 NW, 116, 117 and 44 work "with no E14". | Chain §3 rows 4 and 5: the portals to 115 NW are restored only after `0x2C` sees the four Muruta destroyed. Row 6a: the 116 grate needs Beannoïde rec0 and rec1 gone. | INFERENCE from the chain survey's own table: none of these levers can be reached in normal play without a Muruta death, which today lives in C10/D9 (E14). Testing them needs a test save, which is the chain survey's own Q4. |
| C10 | Sprint SP0 / attack A1 treat TryUseItem as a no-op. | Nothing says what the stub must return. | Re-checked here. FACT: 0x8002ED64 returns `v0 = 1` (`24020001` at 0x8002EDA8) when there is no Circle edge (`lhu 0x6f2a`, `andi 0x20`, `beqz`). The callers abort on 0 (`beqz → END`). So **the stub must return 1**. If it returned 0, it would silently kill every attack, jump and sprint. |
| C11 | Same kind of gap for PlayerTryAction. | — | Re-checked here. FACT: 0x8002EEAC returns 0 (`move v0,zero` at 0x8002EFC8) only when hero +0x2C == 0, and there is no Square-edge pickup from +0x130, and no entity with status 2/3, +0x20 == 0 and +0x12C == hero. When +0x2C ≠ 0, it forces anim 0x0C (ground) or 0x30 (air) and returns 1 **without reading any button**. The stub returns 0 only while carrying is not ported. |

There are no other contradictions. On the balance data, re-checked in `balance.json`: hero record `Attacks[a+1]` gives attr 0 for anims 0, 1, 2, 3, 0x29, 0x2A, 0x3E and 0x31, attr 133 (Tackle, power 2) for anim 4, and attr 129 for anim 0x10. This matches the sprint and hits surveys.

## 2. What is still missing or unverified

**Tick model and pad (blocking).**
- FACT: the hero pad snapshot has no released-edge word. `AlundraEventProgramRunner.cs:3154` says ButtonsReleased "is not backed by AlundraPadState"; only `AlundraTickPad.cs:44,58` has it.
- FACT: PlayerTryAttack tests the release edge first, at 0x8002EB38 (`lhu 0x14`).
- So the attack press path cannot be ported correctly on the per-rendered-frame pad. A release on a tick blocks the press on that tick.
- Other per-tick counters depend on the same choice: the charge timer, the StopSprint counter, `FrameCounter` (+0x1C), the delayed-spawn counter, and the hit pass.

**Consumers that were not decoded.**
- `UpdateItemEffectState` 0x800307E8 fills the three equipment balance records. FACT, re-checked here: the stores to 0x80146E00 sit in 0x80030874–0x80030F44, and the only reader is 0x800446E0. Which items fill these records is still decompilation-only (`PM:1138-1213`). The basic sword's +1 on every attr-bit-7 attack, the tackle included, depends on it.
- `0x8003634C`, called on the hurt path: what it does is UNKNOWN.
- `0x8002EDBC`, the pickup routine: not decoded.
- `0x8003499C`, Circle item use: not decoded.
- The reader of `PlayerControlBits.ForcedWeapon` (opcodes 0xC0/0xC1): UNKNOWN. It is not in PlayerTryAttack.
- `RandomlySpawnItem` and the item entity (C255), which grass cutting needs.
- `SpawnWarpEntity` by global sprite index, which the bow, wand and charged sword need.

**Behaviours not surveyed.**
- Enemy contact damage. Nobody checked whether enemy idle or walk animations carry `FrameCollision` boxes plus a non-zero `CurrentAttack`.
  - INFERENCE: if they do, porting the hit pass alone makes standing enemies on maps 14 and 15, which the author already crosses, start hurting the hero.
  - The hero would then reach HP 0, and the DLL deliberately skips the HP == 0 branch.
- Camera during the sprint: decompilation only (`GameEngine.cs:1747`).
- Corner sliding while sprinting: UNKNOWN.

**Fidelity of the frame boxes.**
- The `.anim2d` fixtures are "min corner + size/2" Box shapes. The binary test is an inclusive integer test with `(n<<16)-1` sizes and a ±1 widening.
- Nobody proved that the export round-trips to the 6-byte record. The DLL `PosZ` is also offset by −1 (O-E19-45).

**Data that was not read.**
- The hero frame delays of 0x29, 0x2A, 0x04 and 0x3E are bank data. The sprint durations are INFERENCE from the exported keyframes.

**Test approach not established.**
- FACT (DLL survey §6): no tool emulates `MovePlayer` 0x80031B50.
- The attack survey ran PlayerTryAttack, 0x8002F49C and 0x8002F768 in `emu.py`, but the driving commands are not in the repo.
- Sprint acceptance through "binary tick traces" needs new stubs for the pad globals and the effect/sound callees. The cost is UNKNOWN.
- `JumpHeroRig` does not fire animation-end chains.
- Pinned test that the sprint slice will invert: `AlundraHeroJumpStatesTests.cs:216-219` (Triangle alone gives Idle).

**Order of the per-tick calls.**
- 0x8002F49C, then 0x8002F768, then 0x8002F884(0) run at 0x80031D68–0x80031D78, before the dispatch at 0x80031D8C.
- FACT from the DLL survey: the DLL climb gate already runs before the case switch, "regardless of the current animation" (`AlundraPlayerManager.cs:243-255`, carrying conjunct included). INFERENCE: the sprint-into-slope-6 climb priority (sprint check row 14) comes for free. It needs one test.

**Order of the passes.**
- FACT (hits survey §7): the DLL runs entities before the world tick, the opposite of the binary.
- The hit pass must follow physics. `g_balanceAnimIndex` must be latched at tick N.

## 3. Decisions the author must take before planning

1. **Where the hero's state pass runs.**
   - (a) Move `MovePlayer` to one call per logic tick on `AlundraTickPad`, which gives the released edge. This touches the pinned jump behaviour and `JumpStartTickStamp`.
   - (b) Keep it per rendered frame, add tick stamps for every counter, and add a released-edge word to the hero pad.
   - Evidence favours (a): the charge, sprint, spawn and hit counters are all tick-based.
2. **Scope of the new task.**
   - (a) Swing plus sprint only, with hits left to E14.
   - (b) Also the shared hit core (`UpdateBalanceRecords`, `ComputeNewHp`, the counters pass, balance loading) and hero damage reception.
   - The hits survey proposes (b). Without it the swing has no effect on targets.
3. **What is needed to clear map 115.**
   - (a) Muruta C10/D9/E1 move into this task.
   - (b) They stay in a first E14 slice.
   - (c) A test save after the hall (G262, G569, G1650) is used to test the levers.
4. **Before E18, what HP == 0 does.**
   - (a) The hero cannot die.
   - (b) A frozen death pose.
   - (c) No hero damage at all until E18.
   - This ties to the contact-damage UNKNOWN in §2.
5. **How frame boxes are read.**
   - (a) A new exact export of the 6-byte records, so the DLL reruns the integer test.
   - (b) The existing `collision_keyframes`.
6. **Which weapons come first.**
   - (a) Only the basic sword, which New Game gives (w = 0, not chargeable).
   - (b) All weapon rows, with charge, bow and wand, which also need `SpawnWarpEntity`.
7. **Stick input.**
   - (a) The original rule: a cardinal-only sprint start, with the stick thresholded per axis.
   - (b) An angle snap, as a port addition.
8. **Original quirks to keep or fix** (under the "fix original bugs" rule):
   - A full charge released in the air is lost.
   - A tap that presses and releases Square on the same tick never swings.
9. **Who owns PlayerTryAction (lift, carry, throw) and TryUseItem.**
   - Map 362 (bomb) and possibly 392 need them.
   - The E19 plan line ~3297 gave "grab" to E14.

## 4. Recommended first reversible slice

**E20.a: per-tick hero hook and a ground and air sword swing, with no damage.** It is on the critical path to map 115, needs no hit core and no E14 work, and can be reverted alone.

Content:
- The hero state pass reads a pad that has the released edge (per decision 1).
- TryUseItem, PlayerTryAction and PlayerTryAttack are called in the binary order in Idle/Moving, the jump states and PrepareSprint. TryUseItem returns 1. PlayerTryAction returns 0.
- Weapon tables 0x80028C14 and 0x80028C78, with no weapon resolving to w = 0.
- Release branch first: with timer < 60 it returns 0.
- Press edge `& 0x80`, the `+0x180 & 0x2000` gate, and sound 3 for anim id 0.
- Attack-state handlers: ground attacks go to 0x2D when airborne, air attacks go to 0 when landing.
- The hero's `FrameCounter` is incremented per tick.

Acceptance:
- Unit tests:
  - Square on the ground gives anim 0x10, which chains to 0 after the exported duration (~21 ticks).
  - Square in the air gives 0x14, which chains to 0x2D.
  - Square and Cross on the same tick give an attack, not a jump.
  - Press and release on the same tick give no swing.
  - A Square press at a button-interact entity gives Idle and no swing (CheckEntityInteraction returns 2).
  - With the `0x2000` cell bit, no swing.
  - Triangle-only behaviour is unchanged until the sprint slice.
- The six reference hero traces are identical.
- In-game recipe on any map with the New Game kit: the swing is visible, with sfx 29, and the hero brakes on the ground.

The sprint (SP1) is the alternative first slice. It is self-contained in the DLL, but the chain survey finds nothing that requires it to progress.

## Open questions for the author

Questions 1 to 5 in §3 change the slice structure. Questions 6 to 9 change the slice contents.

## Suggested slice boundaries

- **E20.0: discovery, read-only.**
  - Contact-damage census of enemy animations.
  - `UpdateItemEffectState` and the fill of 0x80146E00.
  - 0x8003634C.
  - Round-trip proof of the frame-box export.
  - Emulator stubs for MovePlayer.
- **E20.a: swing**, as in §4.
- **E20.b: sprint state machine**, with the check's corrections C1 and C2 and the 0x3E airborne exit.
- **E20.c: hit core and hero damage**, with a stub for HP == 0, according to decisions 2 and 4.
- **E20.d: charge, effects and sounds.** Covers CreatePlayerAnimationEffects (sprint dust and steps, aura, sparks) and grass cutting with drops.
- **E20.e: projectiles and spawns**, after `SpawnWarpEntity`.
- **E14 or a split-off task:** Muruta and Beannoïde, according to decision 3.

Files cited: `D:/development/repo/alundra-casaengine-project-converter/Alundra/Scripts/AlundraPlayerManager.cs`, `.../Alundra/Scripts/AlundraTickPad.cs`, `.../Alundra/Scripts/AlundraEventProgramRunner.cs`, `.../Alundra.Tests/AlundraHeroJumpStatesTests.cs`, `.../alundra-project/Data/balance.json`, `.../alundra-project/Entities/Alundra/`.
