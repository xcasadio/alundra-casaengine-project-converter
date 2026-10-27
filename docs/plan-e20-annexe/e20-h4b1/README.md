# E20.h4b1 T0 - results (annex, no code)

Run at the starting HEAD `b8853232` (branch `chantier/theme2-attaques-objets`). Plan: `docs/plan-e20-heros.md` section 1.31.2 (revision 2, READY) and
section 1.31 (facts 1 to 8). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked at the path `cmips.py` reads:
`D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`). Nothing in the DLL, the engine, the converter or the data
was touched. Python 3.14.2. The line numbers of the plan date from 2026-10-18; the symbols are re-read at the HEAD in section 3.

## 0. Verdict

No stop of the plan is reached: (a) the replays give the filed outputs; (b) every byte the query reads has a member in the DLL; (c) the corrected-mode
vectors are written and the vectors that distinguish each setting are named; (d) the predicted list is empty.

## 1. (a) The oracle replays, from a copy

The scripts of `docs/plan-e20-annexe/e20-h4b/critic/` were copied to a scratch directory and run there (the scripts read the binary at an absolute path and
write next to themselves). The copy of `c9_replay.py` differs from the filed one on one line only, `VEC` (line 11): it points to the **absolute path**
`D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-h4b/bin/vec` instead of the scratchpad of the discovery. The copies
used are filed in `oracle/` (the `c3_query.py` copy carries the added variant "corrected", section 4; with it the filed variants give the same counts).

| Command (in the copy) | Result | Compared |
|---|---|---|
| `python -B c3_query.py 4000` | 4 000 worlds, 211 with tied keys, **model = real code on 4 000 of 4 000 (0 mismatch)**; variants that differ from the real code: inclusive 234, status2only 832, ign20 533, sort_from2 157; coverage query 94/94, sort 91/91 | identical to `critic/run_summary.txt` lines 49-53 (the added "corrected" variant makes 379, section 4) |
| `python -B c9_replay.py` (`VEC` = `e20-h4b/bin/vec`) | 770 vectors, **0 failure** (`vec_query_8003AF70.json`: 120 of 120, from an empty memory, the real MIPS code, strict reads) | identical to `critic/run_summary.txt` lines 70-81 (`oracle/c9_replay_out.txt`) |
| the same with `VEC` = `e20-h4b/critic/vec_extra` | 29 vectors, 0 failure (`vec_extra_query.json`: 10 of 10) | `oracle/c9_replay_extra_out.txt` |
| `python -B c10_extra_vectors.py` (regenerates `vec_extra/` in the copy) | the five files `vec_extra_*.json` byte-identical to the filed ones (`cmp`) | - |

The outputs are filed in `oracle/` (`c3_query_out.txt`, `c9_replay_out.txt`, `c9_replay_extra_out.txt`).

## 2. (b) The bytes the query reads and the members of the DLL

`oracle/classify_reads.py` classifies every read of the 130 query vectors (the 120 of `bin/vec/vec_query_8003AF70.json` and the 10 of
`critic/vec_extra/vec_extra_query.json`, sentinel among them): `oracle/classify_reads_out.txt`. Entity base `0x80127D30`, stride `0x294`.

**Class (i), the inputs of the call** (130 reads each, not modelled as entity members): the count word `[0x80134360]` (the parameter `compte` of `Find`) and
the fifth argument `[0x801FFF10]` (always `0x800CC158`, the keys array, not compared). The output arrays `0x800CC058` (the pointers, compared as the list of slots)
and `0x800CC158` (the keys) are written, never read.

**Class (ii), the entity bytes** (every read is a 4-byte word; the number of reads in the 130 vectors in brackets):

| Offset | Binary role | DLL member at the HEAD (symbol) | Type | Reads |
|---|---|---|---|---|
| `+0x10` | status | `EntityScriptProxy.Status` (`EntityScriptProxy.cs:88`), filtered by `EntityStatusExtensions.IsActive()` (statuses 2 and 3; **not** `IsLoadedNormalOrDeactivated`) | `EntityStatus` | 1 504 |
| `+0x20` | entity blocked by (non-null = excluded) | `EntityScriptProxy.BlockedByEntity` (`:99`) | `Entity?` | 962 |
| `+0x6C` | flags tested against the mask | `EntityScriptProxy.Flags` (`:161`) | `uint` | 871 |
| `+0xB4` | animation byte (bit `0x40` excludes) | `EntityScriptProxy.HitAnimByte` (`EntityScriptProxy.HitPass.cs:30`; **not** `AnimFlags`, one frame late) | `int` | 509 |
| `+0x114` | X, 16.16 | `EntityScriptProxy.PosX` (`:356`) | `int` | 396 |
| `+0x118` | Y, 16.16 | `EntityScriptProxy.PosY` (`:357`) | `int` | 396 |
| `+0x1C4` | the balance record pointer (0 = none) | `EntityScriptProxy.BalanceRecord` (`EntityScriptProxy.HitPass.cs:24`), null = 0 | `BalanceRecord?` | 459 |
| `+0x1CC` | hurt-blink counter (non-zero = excluded) | `EntityScriptProxy.DamagedTickCounter` (`:497`) | `int` | 574 |
| `+0x1D0` | frame-collision counter (non-zero = excluded) | `EntityScriptProxy.FrameCollisionTickCounter` (`:498`) | `int` | 618 |

The slot of the entity is `EntityScriptProxy.Slot` (`:82`, internal, `InternalsVisibleTo("Alundra.Tests")`, -1 outside the table, 0 for the hero, set at
`WorldProxy.cs:208`, `:2312`); the born entities come from `IEntityWorldContext.SpawnedEntities` (`Maps/IEntityWorldContext.cs:35`).

**The balance record bytes** (345 one-byte reads, `fiche + type + 2`, each checked to be at `[+0x1C4 of an entity] + type + 2` of its vector; none else): the
offsets read are 2, 5, 10, 11, 12 and 13 (the types 0, 3, 8, 9, 10 and 11 of the vectors). Member: `BalanceRecord.ByteAt(offset)`
(`Alundra/Combat/BalanceRecord.cs`): offset 2 is `MaxHp`, offsets 3 to 13 are `DamageResponses[offset - 3]` (the offset 13 is the type 11), and it goes on
with the following record beyond `RecordSize` (`Following`). **Every byte has a member; no stop.**

**Class (iii), anything else**: none. The reads of the 130 vectors fall in classes (i), (ii) and the fiche bytes only (`classify_reads_out.txt`: 0 `BAD`).

## 3. (d) The predicted list, recounted at the HEAD

Plan expectation: empty (no caller in production). Recount: `rg -n -i "TargetQuery|8003AF70|8003AE04|InclusiveBound|SortThreshold" Alundra Alundra.Tests --glob '*.cs'`
finds **nothing**; `Alundra/Combat/TargetQuery.cs` does not exist; `tools/dll-structure/*.tsv` and `table.md` have no `TargetQuery` entry. No existing test
exercises the query, so **no test moves** (predicted list: empty, recounted: empty). `EntitySearchService` and its tests (`Alundra.Tests/Entities/EntitySearchServiceTests.cs`,
not `Alundra.Tests/EntitySearchServiceTests.cs`: the path moved with the restructuration) are another algorithm and do not change.
Symbols re-read at the HEAD: `EntityScriptProxy.Slot`, `Status`/`IsActive()`, `BlockedByEntity`, `Flags`, `HitAnimByte`, `BalanceRecord`/`ByteAt`,
`PosX`/`PosY`, `DamagedTickCounter`, `FrameCollisionTickCounter` and `SpawnedEntities` all exist as the plan names them (the line numbers above are the HEAD's).
The same statuses filter exists in the hit pass (`Alundra/Combat/HitPass.cs`, the loop that tests `FrameCollisionTickCounter`, `DamagedTickCounter`, `HitAnimByte`, `Flags`).

## 4. (c) The vectors of the corrected mode

The copy of `c3_query.py` has a combined variant `"corrected"` (inclusive bound `range(1, count + 1)` and sort from 2 results), beside the separate variants
(`inclusive`, `sort_from2`). `oracle/vec_corrected.py` runs it on the worlds of the 130 query vectors with the rule named **"an absent byte is 0"** (the memory
is the sparse `reads` of the vector, every unlisted byte reads 0; for this oracle only: the real code never reads the slot `compte`, so only the sentinel
vector lists it). The result is the list of slots in order. In binary mode the model reproduces the real code's list on **130 of 130** vectors.
Output: `vec_query_corrected.json` (per vector: file, id, case, caller slot, mask, type, count, `binary`, `inclusive_only`, `sort2_only`, `corrected`);
`oracle/vec_corrected_out.txt`. Over the 4 000 random worlds of `c3_query.py` the corrected variant differs from the real code in 379 worlds (expected: it is not the binary).

Which vectors distinguish each setting (the corrected list differs from the binary list in **8 of 130**):

- **The bound** (`InclusiveBound`): only the **sentinel vector** `vec_extra_query.json` index 0 (a live eligible candidate at slot = count = 4): binary `[3, 2, 1]`,
  corrected `[4, 3, 2, 1]`. No other vector tells the bound (the other 129 have no live eligible entity at the slot `compte`, the absent bytes read 0).
- **The sort threshold** (`SortThreshold`): the vectors with exactly two results whose order the sort changes, 7 of the 11 vectors with two results:
  `vec_query_8003AF70.json` ids 15 (`[1, 3]` -> `[3, 1]`), 26 (`[4, 6]` -> `[6, 4]`), 41 (`[2, 6]` -> `[6, 2]`), 42 (`[8, 14]` -> `[14, 8]`), and
  `vec_extra_query.json` indices 2 (far one at the lower slot, `[1, 2]` -> `[2, 1]`), 3 (equal keys, `[1, 2]` -> `[2, 1]`: Hoare swaps equal keys) and 9 (caller slot 2,
  `[1, 3]` -> `[3, 1]`). The other four two-result vectors (`vec_query` ids 6, 64, 89 and `vec_extra` index 7) are already in key order: they do not tell the threshold.
- Each distinguishing vector differs under exactly one setting (the bound or the threshold), so the combined list is the composition of the two (1 + 7 = 8 vectors, the two sets are disjoint).
- 48 of the 120 vectors of `vec_query_8003AF70.json` expect an empty list (they cannot make a test red when `Find` is neutralised, as the plan says).
