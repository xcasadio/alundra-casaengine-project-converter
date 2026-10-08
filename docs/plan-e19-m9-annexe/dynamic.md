# Dynamic measurement (E19.m9 part 4)

MEASURED with the real AlundraEventProgramRunner (MaxIterationsPerCall = 1, TraceSink to detect the end of the first instruction).
`as-is` = production RunScript (InitializeEventData -> Array.Clear(Parameters)).
`keep`  = EMULATION written in the throwaway test: runner.InitializeEventData runs, then Parameters[1..9] are restored to their pre-call values,
          then runner.RunOneScriptCall; resumed B/C calls go through the real RunScript. For A/D/E/F the emulated state is ONE object kept across calls
          (the binary's global state). Nothing in production is edited.
A call count N+2 for `Wait N`: call 1 sets the marker, calls 2..N+1 count, call N+2 ends (counter >= N).

## 4a. SYNTHETIC slots D and F (NO corpus site exists, see census): program X = [Wait N, End], called every frame

| slot | N | as-is | keep | as-is + other program of the slot between calls | keep + other program between calls |
|---|---|---|---|---|---|
| D | 1 | >300 (never) | 3 | >300 (never) | >300 (never) |
| D | 10 | >300 (never) | 12 | >300 (never) | >300 (never) |
| D | 60 | >300 (never) | 62 | >300 (never) | >300 (never) |
| F | 1 | >300 (never) | 3 | >300 (never) | >300 (never) |
| F | 10 | >300 (never) | 12 | >300 (never) | >300 (never) |
| F | 60 | >300 (never) | 62 | >300 (never) | >300 (never) |

## 4b. REAL B programs (map events) whose first instruction is Wait, driven through the real RunMapEventsPass
In-zone ticks until the first Wait ends. continuous = hero stays in; once = hero leaves for ONE tick after j in-zone ticks (j = (N+2)/2, min 1) then returns;
flicker = 1 tick in, 1 tick out, forever (cap 400 in-zone ticks).

| map | B | pc | N | continuous as-is | continuous keep | once as-is | once keep | flicker as-is | flicker keep |
|---|---|---|---|---|---|---|---|---|---|
| 162 (chain) | B[5] | 432 | 10 | 12 | 12 | 18 (j=6) | 12 | >400 (never) | 12 |
| 163 (chain) | B[2] | 216 | 1 | 3 | 3 | 4 (j=1) | 3 | >400 (never) | 3 |
| 163 (chain) | B[3] | 332 | 1 | 3 | 3 | 4 (j=1) | 3 | >400 (never) | 3 |
| 163 (chain) | B[4] | 448 | 1 | 3 | 3 | 4 (j=1) | 3 | >400 (never) | 3 |
| 169 (chain) | B[3] | 256 | 10 | 12 | 12 | 18 (j=6) | 12 | >400 (never) | 12 |
| 176 (chain) | B[3] | 272 | 10 | 12 | 12 | 18 (j=6) | 12 | >400 (never) | 12 |
| 176 (chain) | B[4] | 372 | 1 | 3 | 3 | 4 (j=1) | 3 | >400 (never) | 3 |
| 176 (chain) | B[5] | 420 | 1 | 3 | 3 | 4 (j=1) | 3 | >400 (never) | 3 |
| 183 (chain) | B[3] | 288 | 10 | 12 | 12 | 18 (j=6) | 12 | >400 (never) | 12 |
| 185 (chain) | B[1] | 48 | 1 | 3 | 3 | 4 (j=1) | 3 | >400 (never) | 3 |
| 392 (chain) | B[3] | 156 | 60 | 62 | 62 | 93 (j=31) | 62 | >400 (never) | 62 |

All 154 B Wait-first sites (11 on the chain): the result differs as-is vs keep -> continuous 0, once 154 (chain 11), flicker 154 (chain 11).
Off-chain rows are computed but not listed.

## 4c. REAL C programs, first instruction stateful, after a cross-entity clear (Sp = 0, Codes = null) following j calls
Calls until the first instruction ends (0x0B: the entity is moved radius+1 px at the clear, then stays; cap 60). j=0: no clear (baseline).

| map | C | pc | op | operands | baseline as-is | baseline keep | clear as-is | clear keep | j |
|---|---|---|---|---|---|---|---|---|---|
| 10 (chain) | C[19] | 3328 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 10 (chain) | C[20] | 3408 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 10 (chain) | C[21] | 3480 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 10 (chain) | C[22] | 3584 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 10 (chain) | C[23] | 3636 | 0x0B | 9,100,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 10 (chain) | C[36] | 4264 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 10 (chain) | C[37] | 4364 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 10 (chain) | C[39] | 4464 | 0x0B | 8,48,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 10 (chain) | C[43] | 4616 | 0x0B | 8,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 10 (chain) | C[72] | 6360 | 0x37 | 30 | 32 | 32 | 48 | 32 | 16 |
| 162 (chain) | C[2] | 736 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 164 (chain) | C[2] | 240 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 165 (chain) | C[3] | 804 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 165 (chain) | C[4] | 880 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 165 (chain) | C[5] | 984 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 165 (chain) | C[8] | 1144 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 165 (chain) | C[9] | 1180 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 165 (chain) | C[22] | 2236 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 165 (chain) | C[23] | 2284 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 172 (chain) | C[8] | 588 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 172 (chain) | C[9] | 624 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 172 (chain) | C[22] | 1680 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 172 (chain) | C[23] | 1728 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 176 (chain) | C[2] | 828 | 0x37 | 30 | 32 | 32 | 48 | 32 | 16 |
| 179 (chain) | C[8] | 1092 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 179 (chain) | C[9] | 1128 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 179 (chain) | C[23] | 2260 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 180 (chain) | C[3] | 88 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 180 (chain) | C[5] | 208 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 182 (chain) | C[1] | 20 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 182 (chain) | C[2] | 72 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 185 (chain) | C[1] | 160 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 185 (chain) | C[2] | 248 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 185 (chain) | C[3] | 324 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 185 (chain) | C[4] | 380 | 0x37 | 1 | 3 | 3 | 4 | 3 | 1 |
| 6 | C[26] | 2216 | 0x0B | 1,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 6 | C[27] | 2244 | 0x0B | 1,80,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 6 | C[28] | 2272 | 0x0B | 1,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 41 | C[14] | 1356 | 0x0B | 3,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 41 | C[18] | 1372 | 0x0B | 3,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 42 | C[8] | 656 | 0x0B | 3,104,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 48 | C[32] | 884 | 0x0B | 2,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[2] | 1164 | 0x0B | 1,120,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[3] | 1172 | 0x0B | 1,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[4] | 1180 | 0x0B | 1,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[5] | 1188 | 0x0B | 1,120,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[6] | 1196 | 0x0B | 1,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[7] | 1204 | 0x0B | 1,80,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[8] | 1212 | 0x0B | 1,32,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[9] | 1220 | 0x0B | 1,48,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[10] | 1228 | 0x0B | 1,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[11] | 1236 | 0x0B | 1,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[12] | 1244 | 0x0B | 1,32,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[13] | 1252 | 0x0B | 1,48,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 52 | C[19] | 1288 | 0x0B | 2,48,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 53 | C[15] | 1228 | 0x0B | 2,48,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 77 | C[23] | 1984 | 0x1E | 144,0 | >60 (never) | >60 (never) | >60 (never) | >60 (never) | 1 |
| 79 | C[12] | 356 | 0x0B | 1,120,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 79 | C[13] | 364 | 0x0B | 1,120,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 79 | C[14] | 372 | 0x0B | 7,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 79 | C[15] | 380 | 0x0B | 7,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 158 | C[20] | 732 | 0x0B | 7,70,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 158 | C[33] | 748 | 0x0B | 7,70,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 331 | C[23] | 3636 | 0x0B | 9,100,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 331 | C[39] | 4464 | 0x0B | 8,48,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 331 | C[43] | 4616 | 0x0B | 8,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 335 | C[26] | 624 | 0x0B | 1,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 335 | C[27] | 652 | 0x0B | 1,80,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 335 | C[28] | 680 | 0x0B | 1,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 362 | C[6] | 656 | 0x0B | 1,32,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 362 | C[9] | 712 | 0x0B | 1,8,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 368 | C[2] | 140 | 0x0B | 1,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 368 | C[3] | 176 | 0x0B | 1,96,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 368 | C[7] | 320 | 0x0B | 1,32,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 368 | C[8] | 344 | 0x0B | 1,32,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 368 | C[9] | 368 | 0x0B | 1,32,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 369 | C[4] | 200 | 0x0B | 1,192,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 369 | C[5] | 240 | 0x0B | 1,128,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 413 | C[1] | 328 | 0x0B | 3,64,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |
| 441 | C[15] | 788 | 0x0B | 1,36,0 | >60 (never) | >60 (never) | >60 (never) | 2 | 1 |

291 C sites measured (0x37, 0x0B, 0x1E first): the clear changes the outcome at 267 of them (chain 35).

## 4d. Replica of the existing test RunScript_DefaultSlot_AlwaysReInitializes_EvenAfterASuspend (slot A, Codes {0x01, 0x37, 5, 0x1A, 3, 0xFF}, 8 calls)
- Wait is the SECOND instruction (as in the test), as-is: TargetAnimationId after 8 calls = 0 (the test asserts 0).
- Wait is the SECOND instruction (as in the test), keep: TargetAnimationId after 8 calls = 0 (the test asserts 0).
- Wait is the FIRST instruction (table points at pc 1), as-is: TargetAnimationId after 8 calls = 0 (the test asserts 0).
- Wait is the FIRST instruction (table points at pc 1), keep: TargetAnimationId after 8 calls = 3 (the test asserts 0).
