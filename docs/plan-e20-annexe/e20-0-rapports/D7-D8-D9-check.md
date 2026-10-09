# Check of the binary facts of D7, D8 and D9

> Report returned by the adversarial checker of 2026-10-09 (read-only, capstone on ALUN_CD.EXE), kept verbatim as evidence.
> The plan section 0.2.10 holds the reconciled facts.

Six claims hold as stated, two hold with a material nuance (#2 and #7), and one is refuted on a detail (#9).

| # | Verdict | Evidence (FACT unless marked) | Correction |
|---|---|---|---|
| 1 | CONFIRMED | **Call order:** UpdateWorld = 0x8002E058, called only from 0x8002BC4C (`0c00b816`). It makes three calls in order: `0c00f19f` jal 0x8003C67C at 0x8002E100, `0c00ece2` jal 0x8003B388 at 0x8002E108, `0c00f104` jal 0x8003C410 at 0x8002E110. Before them it resets the draw list: [0x80126E6C]=0x8012CB60 at 0x8002E0F8 and [0x8012CE60]=0 in the delay slot at 0x8002E104. **0x8003C410:** loops over 128 records of 0x80 bytes at 0x80138608. A record is active when +0x68==2. It skips the update when [0x800DC4B8]&0x48 (0x8003C45C). If +0x7D is set it frees the record; otherwise it calls 0x8003BBDC (frame step) and 0x8003C284 (motion modes on +0x34). It then always queues rec+0xC into the draw list. The allocator 0x8003B9C4 returns the first record with +0x68==0. INFERENCE: this is an animated sprite-effect / particle pool. **Pad vs UpdateWorld:** the main loop body is 0x8002C3F4 jal 0x8002AC2C (a debug pass on pad 2, gated by [0x801EB440]<0), then 0x8002C3FC jal 0x8002BD60 (INFERENCE: render), then 0x8002C404 jal 0x8002BAEC(0). The loop branches back at 0x8002C45C. Inside 0x8002BAEC, the pad read (jal 0x8002E38C at 0x8002BB5C) has no branch around it. Every later path reaches UpdateWorld at 0x8002BC4C. | Yes: the pad is updated before UpdateWorld, in the same call, every frame. |
| 2 | CONFIRMED with nuance | 0x8003C69C `andi 0x48` + `bnez` → 0x8003C81C, which is the epilogue. The whole pass over 64 records of 0x48 bytes at 0x8013C688 is skipped. 0x8003B394 `andi 0x48` → 0x8003B408 jal 0x800384F4 (`0c00e13d`). | "Runs only 0x800384F4" is too strong. Both paths continue at 0x8003B414: camera copy from [0x801345FC]+0x116/11A/11E into 0x80134350..58, then jal 0x800399B8 (0x8003B478), then the draw-list submission loop. With the mask set, 0x800384F4 is the only update call; lists, camera and draw still run. |
| 3 | CONFIRMED | 0x800386EC `0c00c6d4` jal 0x80031B50 (MovePlayer), first call. 0x800386F4 `addiu s2,zero,1`, and s1 = 0x80127FC4 = 0x80127D30+0x294 (slot 1, stride 0x294). Exit test is `slt v0,[0x80134360],s2`, so slots 1..max inclusive. In 0x80038998: `slt a1,s1` with s1 starting at 0, no status test, `bltz` skip if max<0. Field writes: sw 0 at +0x224 (0x800389D8), +0x12C (0x800389DC), +0x220 (0x800389E0); +0x1C += 1 (0x800389EC); +0x1CC and +0x1D0 decremented if nonzero (0x800389F4, 0x80038A08). | Two details missed. (a) 0x80038634 runs before 0x800386D0: it rebuilds any status-4 slot from a template at 0x80134368 and sets [0x80134360] = highest non-empty slot. (b) 0x800386D0 has two phases. Phase 1 computes a code from -1 to 6 into +0x228. Phase 2 calls table [0x80098F4C+code*4] at index +0x70 (or 0x8004205C when +0x4C&0x7F), and repeats the slot pass until no handler ran (0x80038968). |
| 4 | CONFIRMED | 0x80031CD4 `lhu 0x6f28` (Hold); s3 = Hold>>12; s1 = table 0x80022C6C[s3]; if -1 then s1 = +0x8C (0x80031CF8). Table is {1:0x10, 2:0x18, 3:0x14, 4:0, 6:0x1C, 8:8, 9:0xC, 12:4, others -1}. Dash table 0x80022CAC is {1:0x10, 2:0x18, 4:0, 8:8, others -1}, so diagonals cannot dash. State 0x29 handler (entry 0x80031F54): Triangle held check at 0x80031FA8 (`andi 0x10`); +0xB0 check at 0x80031FBC; lookup at 0x80031FD0..E8 sets +0x8C and **state 3**. State 0x2A (0x80032118): counter [0x80126FF4] = counter+1; `slti 0xb` at 0x80032190; lookup at 0x8003219C..C0; 0x800321C4 `beqz s3`, else clear at 0x800321D0. | Extra conditions not stated in the claim: 0x8002ED64 must return non-zero, 0x8002EEAC must return 0, and +0x144 must be non-zero (otherwise state 0x2D). In state 0x2A the counter only advances if it is already non-zero, with Triangle held and +0x13C==0; otherwise it is cleared. The counter is seeded to 1 at 0x80032088 (entry into 0x2A). Once the counter is above 10 and the lookup gives -1, the counter keeps increasing. |
| 5 | CONFIRMED | Cone rows are read at 0x800320A0..E0 as +0x98*12 against s1. 0x80022CEC holds {1C,0,4}, {C,10,14}, {4,8,C}, {14,18,1C}. 0x80038AB4: row = +0x98*32 in 0x800237F4, column = (+0x8C+2)&0x1C (addu at 0x80038AF8, lw at 0x80038AFC). The rows are [0,0,2,1,1,1,3,0] ×2 and [0,2,2,2,1,3,3,3] ×2; the 5th row is ASCII. The sectors where a facing is kept equal the cone rows. | None. Note: 0x80038AB4 is generic for all entities. It runs in the post pass 0x80038E18 over list 0x80127B28, after MovePlayer. INFERENCE: the cone test reads the facing from the previous tick. |
| 6 | CONFIRMED | 0x8003298C `lw -0x78f8` with lui 0x800A gives 0x80098708. Next state = s*0x7D2B89DD + 0xE06A02E7. Column = hi(s*16) = s>>28, row = 0x80028E0C + (b&0x7F)*16. Loop while b&0x80; b≥0x100 returns 0; b<0x62 is kept, otherwise 0. All 447 resolvable 0x8708 references use lui 0x800A; none use 0x800B. | Also: the same word is advanced once per frame at 0x8002BD0C..34, after UpdateWorld, and is used inline by about 600 sites (hero, enemies). |
| 7 | CONFIRMED with nuance | The only caller of 0x80032A40 is 0x80039F38, inside init 0x80039D04, which sits just before 0x80039F58. Veto 0x80032A00: items 0x26/0x51/0x52 → `sltiu` on 0x8004DDDC, which returns `lh [[0x80176318]+6]`. Spawn: 0x80032B90 calls 0x80039F58 with a2=item+0x1E, then sets +0xB8=0xA0000, +0x274=1, +0x6C&=~0x80, +0x278 = 0x258 or -1 from byte 0x800C5F7C[item*8+4], +0x27C=0, +0x284=0xA0000, +0x280=killer+0x40, and plays sound 0x54. The only caller of 0x80032C7C is 0x8003A988. | **The loot is rolled at spawn time** (0x80039D04 → 0x80032A40 → RNG, result in +0x3C) and dropped at death (0x8003A59C → 0x80032B90). The veto is applied at drop time. +0xB8 being "Z force" is INFERENCE: it is used next to +0x11C writes in motion code at 0x80036xxx–0x80037xxx. |
| 8 | CONFIRMED | 102 `jal 0x80039F58`, no `j`, no pointer. Dispatch table of tables 0x80098F4C = [0x800C4B34, 0, 0x800C4F34 (C, code 2), 0x800C5334 (D, code 3), 0x800C5734 (E, code 4), 0x800C5B34 (F, code 5)]. Handler words: [0x800C5764]=0x8007F27C (E12), [0x800C5778]=0x8007F420 (E17), [0x800C5024]=0x8007A8A0 (C60), [0x800C5028]=0x8007A958 (C61), [0x800C50AC]=0x8007C0D8 (C94). | None. |
| 9 | REFUTED (detail) | 0x80032614 clears 0x80126FFC; 0x80032620 clears 0x80127110; 0x8002F884(2) at 0x8003261C; switch on +0x88 < 0x50 through 0x80022E7C; mode table 0x80022D1C[4] = 0x8003260C. **0x800326D0 reads `lhu 0x6f2a`, which is the PRESSED edge (pad +0x12 = new&(new^old) from 0x8002E260), not Hold.** | Correct the 0x0F/0x1D path to: set +0x8C=s1; return if 0x8002ED64()==0; then 0x8002E910() → 0: newly-pressed &0xD0 gives 0x28, else s3 gives 0x0F, else 0x1D; → 2: 0x1D; any other value: no change. Land states set 0x1D at 0x8003267C **before** calling 0x8002ED64 (return ignored). States 0x1C/0x31/0x3E also flip +0x8C by 0x10. 0x28/0x3A only call 0x8002ED64. 15 states (0xD, 0x1A, 0x1B, 0x1E, 0x27, 0x37, 0x38, 0x3D, 0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D) and ≥0x50 go to 0x800350C0. 0x8002ED64 = Circle newly pressed → 0x8003499C, otherwise returns 1. |

## Important points the reports missed

**Where the DLL's tick pad must advance (FACT):**
- One frame runs in this order:
  1. 0x8002BD60 (INFERENCE: render of the previous state).
  2. Pad read and edge computation at 0x8002BB5C (0x8002E250 per pad).
  3. UpdateWorld at 0x8002BC4C.
  4. Menu check on the pressed mask &0x803 at 0x8002BCA4.
  5. 0x8004B1D4.
  6. RNG advance at 0x8002BD14.
- So the pad must advance exactly once per tick, immediately before UpdateWorld. Edges (+0x12 pressed, +0x14 released, +0x16 repeat) are relative to the previous read.
- The RNG must advance once after UpdateWorld on every tick, or the loot draw sequence will drift.
- The transition loop 0x8002C490 also reads the pad each frame without UpdateWorld, so a press made during a warp is consumed there.
- 0x8002AC2C reads the pad-2 pressed state before the new read (debug only).

**Pad struct layout (FACT):**
- 0x80126F18 is pad 1 and +0x18 is pad 2.
- Hold = 0x80126F28 and Pressed = 0x80126F2A.

**Post-tick passes after the counters (FACT):**
- Order: 0x800384F4 (lists), then 0x80038E18 (animation and facing select), then 0x80038364 (INFERENCE: motion/collision; calls 0x800364C8, 0x80036828, 0x80037364, 0x80037E34, 0x80038064), then 0x80038E84 (effects), then 0x80039300.
- MovePlayer therefore runs before collision, facing resolution and every other entity.

**Freeze mask 0x48:** it also freezes the sprite-effect pool, but those effects are still drawn.

**UNVERIFIED:** whether a freshly spawned entity is ticked in the same frame through the phase-2 repeat. +0x228 is copied from the template; I did not inspect the template contents.
