# E20.e0b - the pseudo-random generator: e0b1 (the advance, once per tick), e0b2 (the loot roll at creation)

Annex of slice E20.e0b1 (`docs/plan-e20-heros.md`, section 1.17 head and 1.17.1, revisions 1 and 2, READY at the extra closing review). This file is written and committed BEFORE any code
of the slice (task T0). The measured values of the later tasks are added in the section "Results" at the end. e0b2 will add its own sections.

## What is proved (e0b1)

The real frame function `0x8002BAEC` of `ALUN_CD.EXE` (France), run on the MIPS machine of the E14/E20 oracles (the discovery probe `s7_frame.py`, byte copy): it ends with ONE step of the
generator word `[0x80098708]`, `s <- s * 0x7D2B89DD + 0xE06A02E7 (mod 2^32)`, at `0x8002BD0C-0x8002BD34`, after the sound frame, whatever the gate word `[0x800DC4B8]`, the inventory result
or `a0` are. The DLL gets the same step once per logical tick, after the tick of the effect reservoir (D-E0B-1), never for an image without tick and never while a warp departs (D-E0B-2).

## How to run (from the repository root)

- `python -B docs/plan-e20-annexe/e20-e0b/oracle/run_e0b1.py` writes `oracle/out/` (about 3 s; exit 0 = every check of `out/summary.txt` is PASS). Two runs give the same `out/manifest.txt`.
- `python -B docs/plan-e20-annexe/e20-e0b/oracle/mutants_e0b1.py` writes `oracle/out/mutations.txt` (about 10 s; exit 0 = every mutant CAUGHT).
- Needs Python 3 with capstone, the disc EXE named in `oracle/tools/lib.py` and the map JSONs of `data-extracted/data/` (read only). Nothing outside `oracle/out/` is written.
- `oracle/tools/` holds the seven tools the probe needs (`lib`, `emu`, `machine`, `mips`, `c1world`, `world`, `bank`), byte-identical to `docs/plan-e20-annexe/e20-e0a/oracle/tools` (and
  `lib`, `emu`, `machine`, `mips` to the discovery harness); `espawn.py` is the e0a one; `s7_frame.py` is the byte copy of the discovery probe. `oracle/checksums.txt` lists the SHA-1 of every file.

## Expected values (the prediction, written before the run)

1. **The six words of the advance** (`out/advance_words.json`, an independent model against the constants of the plan): `0 -> E06A02E7`, `FFFFFFFF -> 633E790A`, `80000000 -> 606A02E7`,
   `B017C93D -> 35E36190`, `12345678 -> B806E07F`, and four steps from `12345678` -> `AFBE9EAC`.
2. **One advance per call of the frame function**, on the ten rows of the probe (`out/frame_vectors.json`): the machine journals one `ADVANCE` event (the store at `0x8002BD34`) per call, the last operation
   of the call; with the gate `0x48` shut the call makes exactly one step of the word (only the list pass runs), with the gate open it makes four (three marker draws of the stubbed passes, then the advance).
3. **`a0` has no effect on the number of steps**: `a0 = 0` and `a0 = 1` give the same word on the same gate (`a0` only commands the clearing of `[0x800DC4C4]`, which the rows 5 to 7 observe).
4. The ten rows (picked from the 24 calls of the probe, whose `frame_trace.json` keeps the md5 `4943e0d29796bdee75e0a876f82b6b48` of the discovery): gate `00` and `48` with `a0` 0 and 1 (rows 1 to 4);
   Start pressed with `DisplayInventory` returning 1, returning 0 (the request word `[0x800DC4C4]` ends at 1) and the post-load call `a0 = 1` returning 0 (the request is cleared again, rows 5 to 7); gate `08`
   with the trigger refused (row 8); the advance alone from the seeds `00000000` and `B017C93D` under the gate `48` (rows 9 and 10). The predicted values are computed by `run_e0b1.py` from the formula
   (3 marker draws if the gate has no `0x48` bit, then one advance), never read from the machine.

(The plan says "ten rows" without naming them: the choice above is this annex's; the 24 calls of the probe are all kept in `out/frame_trace.json` and all 24 are used by the mutants.)

## Qualitative prediction of the DLL (from the discovery, to be compared in T3 with the measured list)

The advance moves the generator by one step per tick; every test that reads the stream at a point where the number of ticks since the seed matters can move. At risk: `AlundraShipCorridorArcTests` (A5, A5r: the
`0x0C` of the 220th image), the arcs with a real AI (Beannoids and Muruta: maps 14 outside the arena, 44, 115, 116, 117), the arc of map 167 (`0x5A` mode 4) and the arcs of maps with `0x0C` / `0x8C`;
`AlundraPrefabArcSupportTests.ArcRun_ResetsTheSharedRandomStream...` (its seed is read right after the construction). The figure "about a dozen tests" is the critic's estimate, not a measure. Not at risk:
the direct calls of `RunFrame` without a callback (70 calls in 15 files), the grouping tests (`AlundraHeroChargeArenaTests` with 2 and 3 ticks per image, `AlundraHeroAuraWorldTests`) and
`AlundraTickStructureTests`: the callback is optional and the grouping invariance is kept by the place of the call (per tick, after the reservoir). The measured predicted list (rule of T1) is copied below
before phase B.

## Starting state (T0 measures, 2026-10-15, base commit `dbceb5e`, quiet window: nothing else built or ran)

| Measure | Value |
|---|---|
| `Alundra.Tests` Release | 4 833 passed, 0 failed, 3 m 27 s |
| `Alundra.Tests` Debug | 4 833 passed, 0 failed, 3 m 44 s |
| The five traces (`git diff --ignore-cr-at-eol --exit-code`) | unchanged (EOL noise restored) |
| `alundra-project/Alundra.dll` lock | not locked at T0 |

## Oracle results (T0)

- `run_e0b1.py`: every check PASS (`out/summary.txt`, 22 lines): the md5 of `frame_trace.json` equals the discovery, 24 calls, the six words, the ten rows (each with one `ADVANCE` event, last operation, steps
  and word as predicted, request word as predicted), `a0` without effect, one advance on each of the 24 calls. Two runs give the same `out/manifest.txt`.
- `mutants_e0b1.py`: 20 one-word mutants of the binary (RAM patches, never saved; the old word is asserted at each address), all CAUGHT, none survived, no equivalence to argue (`out/mutations.txt`).

| Family | Mutants (address) | Caught by |
|---|---|---|
| constants | multiplier halves `0x8002BD0C`, `0x8002BD18`; increment halves `0x8002BD20`, `0x8002BD24` | the word after the advance (every row) |
| operation | `mult` removed or squared (`0x8002BD1C`), `addu` -> `subu` or product only (`0x8002BD2C`) | the word after the advance |
| polarity of the `beqz s3` | `beqz` -> `bnez` (`0x8002BD30`) | row 7 (the post-load call must clear the request) and rows 5/6 |
| store of the advance | removed or moved to a neighbour word (`0x8002BD34`); read from a neighbour word or another base (`0x8002BD14`, `0x8002BD10`) | the word (steps 0 or a wrong value) |
| clearing of `[0x800DC4C4]` | removed (`0x8002BD3C`) | row 7 |
| result of `DisplayInventory` | request store removed (`0x8002BD00`), `bnez` -> `beqz` (`0x8002BCF4`) | row 6 and rows 5/7 |
| gate test | mask `0x48` -> `0x40`, `0x08`, `0` (`0x8003B394`), polarity (`0x8003B398`) | the step counts of the gate rows (gate `08` and `40` among the 24 calls, rows 2 and 8) |

## Predicted list (rule of T1)

To be filled in T1, before phase B: the tests where `divergent_draws + divergent_seed_reads + ambient_draws > 0` in `e0b1-shadow.txt`.

## Results

(T1 to T4 add their measures below.)
