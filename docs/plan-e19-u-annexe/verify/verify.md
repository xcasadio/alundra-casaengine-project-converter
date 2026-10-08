# O-E19-78 counter-check (fresh) - verdict

Base: repository `486fdf5`, engine `f86a981b`. All runs in my physical copy `verify/repo/`; details and commands in `notes.md`.
F = fact (address, file:line at HEAD, measurement in my copy); H = hypothesis.

**Verdict: CONFIRMED.** The recommendation holds: move the inventory block to after the map events of its own tick, and include R3. Every claim
that carries the decision reproduces. On three schedules the discovery did not measure, the candidate matches the binary at 1, 2 and 3 ticks per frame.
There are three reservations: a behaviour change the discovery did not list, evidence that is weaker than stated, and gaps in the closed file list.

## Claims re-derived

| # | Claim | Result | Evidence |
|---|---|---|---|
| 1 | Binary order: RenderScene, then Update(0); trigger after UpdateWorld | CONFIRMED (F) | Own disassembly: `jal 0x8002BD60` at 0x8002C3FC, `jal 0x8002BAEC` at 0x8002C404; sampler at 0x8002BB5C, UpdateWorld at 0x8002BC4C, delay 0x8002BC54-68, trigger 0x8002BC6C-0x8002BD00 (whole-word ctrl test at 0x8002BC70) |
| 2 | Map events, entities and effects each read `ctrl & 0x48` inside UpdateWorld, before the trigger | CONFIRMED (F) | 0x8003C680/9C, 0x8003B38C/94, 0x8003C454/5C |
| 3 | Release 0x8004501C clears bits 0x18 | CONFIRMED (F) | 0x80045038-0x80045048 |
| 4 | Port HEAD runs the inventory block before the dialogue pass, the save screen and the map events | CONFIRMED (F) | `AlundraWorldProxy.cs:2206-2237` vs `:2268-2318`; cited line numbers spot-checked and correct |
| 5 | R1: only the inventory and sub-inventory read `GameState.TickPad` in the proxy's passes | CONFIRMED (F) | grep: `AlundraInventoryDirector.cs:404/432/629`, `AlundraSubInventoryDirector.cs:502`. The save screen's argument-less `Tick()` (`:427`) also reads it, but the proxy never calls it (`:2292` uses the two-word overload) |
| 6 | Binary window 1..37 / 1..74 / 1..112; box release plus Start; map-event box wins | REPRODUCED (F) | Discovery scripts rerun in my copy, identical output (`bin-repro-*.txt`) |
| 7 | Candidate freezes on the binary's ticks | CONFIRMED and extended (F) | See the schedule table below |
| 8 | Red-first tests: 7/7 red at HEAD, 7/7 green on the candidate | CONFIRMED (F) | `results/head-probes.trx`, `results/cand-probes.trx` |
| 9 | No existing test moves | CONFIRMED (F) | Musics copied: HEAD 3214/3214, candidate 3214/3214, no outcome difference |
| 10 | Entity scripts are unchanged | CONFIRMED at flag level (F) | Per-frame end-of-frame flags and inventory/sub/post-process state are identical between HEAD and the candidate (L1 and back schedule, 1 and 2 ticks per frame). Entities read the gate at frame start (`AlundraEntityScriptProxy.cs:1193`), and `AlundraGameplayFreeze` reads the end-of-frame flags |
| 11 | Residual 0/3 vs 4/6 inversion is harmless | CONFIRMED, guards hold (F) | The binary's inventory code (0x80052000-0x80058400) only calls OpenSlot(4) (0x80052628) and OpenSlot(6) (0x8005562C) and never opens a box. Every `0x800450F0` caller sits outside it |
| 12 | Save screen closing plus Start on the same tick now opens | PORT MEASURED (F); binary H | Save screen inactive after frame 47 (flags 08 to 00 in the same frame). Start at 47: lost at HEAD, opens on the candidate. Start at 48 opens in both |

Schedules the discovery did not measure (relative to the open tick):

| Schedule | Binary (`my_window.py`, real UpdateWorld) | HEAD | Candidate (1, 2 and 3 ticks per frame) |
|---|---|---|---|
| Close at +24 | 1..42 | 0..41 | 1..42 |
| Close at +30 | 1..48 | 0..47 | 1..48 |
| L1 at +24, back at +66, close at +108 | 1..126 | 0..125 | 1..126 |

## What the discovery missed (with plan impact)

1. **Unlisted behaviour change: a map-event warp on the trigger tick (F, probe P4).** A map event starts a departure on the frame whose sample carries Start.
   - HEAD: the inventory opens, posts MenuOpen and freezes the map events, so the departure does not start on that tick.
   - Candidate: the departure starts and the trigger refuses Start through its own `IsTransitionInProgress` guard (`AlundraInventoryDirector.cs:419`, D-E13D-30).
   - Binary: the departure starts and the inventory is still armed (the defect D-E13D-30 corrects).

   The candidate therefore applies D-E13D-30 to map-event warps too. **Impact:** list it in the ADR and the risks, revisit the D-E13D-30 comment (`:410-418`), and optionally add a test.
2. **The trace evidence is overstated (F).** The six trace files cannot see this change. `IntroTraceHarnessTests` mirrors the passes by hand (`:588`), and `HeroTraceHarnessTests` builds no `AlundraWorldProxy` and no inventory. **Impact:** drop "traces identical" as evidence; only the new red-first tests guard the order.
3. **The closed file list is incomplete; comment and doc drift only (F).**
   - `AlundraWorldProxy.cs:2287` says "TickPad only holds the last tick's edges here". This becomes false.
   - ADR-0040 line 15 makes the same "TickPad only holds the edges of the last tick" claim, not only line 22.
   - The class doc of `AlundraDialogueBoxMontage.cs:91` says "the pad pass, then at each tick ...".
   - Five test comments name the "per-tick pad loop": `AlundraInventoryDirectorTests.cs:924`, `AlundraSubInventoryDirectorTests.cs:120`, `AlundraTickPadTests.cs:109`, `Scripts/AlundraSubInventoryPresenterTests.cs:259`, `Scripts/AlundraInventoryPresenterTests.cs:191`.

   **Impact:** add these files to the list, comments only.
4. **Why no pin moved (F).** The test helpers drive the inventory alone (`SaveGameDirectorTestSupport.cs:208-215`, `AlundraSubInventoryDirectorTests.cs:121-127`, `AlundraSaveGameLoadResetTests.cs:227-234`), in the same internal order the candidate keeps. The plan row's "épingles de l'inventaire à reprendre" turns out to be empty.

## Unconfirmed

- Binary side of the save screen closing plus Start on the same tick. The memory-card process 0x8005EC98 was not emulated, so it is unknown whether 0x800C4980 and MenuOpen return to 0 in the same RenderScene (H).
- Real entity path: not driven with real `AlundraEntityScriptProxy` entities; only the flag-level argument in claim 10 supports it (H, strong).
- The order of inventory sounds after map-event sounds in the per-frame anti-duplicate table (H, harmless).
- The candidate diff is a probe form: it keeps an empty `for (... < 0 ...)` loop and leaves the `if` body unindented. The production edit must remove and reindent them; I did not review that edit.

## Answers to the discovery's questions (my view)

1. R3 belongs in this slice. Without it the pending triggers stay frozen at P..P+36 (the order-only variant shows this), while the binary's UpdateEntities runs before the trigger.
2. The two seams are acceptable.
3. A new ADR plus annotations on ADR-0040 lines 15 and 22 is the right form; ADR-0050 is the next free number at HEAD.
4. Add to the recipe the warp case (a map-event portal or 0x53 combined with Start on the same frame).
