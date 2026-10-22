# ADR-0079: The map header's byte 9 is exported as `TileCutEffectId`, renamed at the source

- **Status**: Accepted (text corrected on 2026-10-22, the day it was written, before the author's review: the decompilation is left out)
- **Date**: 2026-10-22
- **Source**: this chantier: the author's answers of 2026-10-22 to Q-E3 and Q-E3b (`docs/plan-e20-heros.md`, section 2; decisions D-E20-49 and D-E20-60 in section 0.1)

## Context

- The analyser's Tiled exporter writes byte 9 of the map header as the map property `BalanceLevel` (`alundra-datas-analyser/AlundraTools/AlundraDataExtractor/TiledMapExporter.cs:275`, from `GameMapInfo.BalanceLevel`; `AlundraTools/docs/datas-bin-structure.md:78` describes it as a "Balance/anim deletion parameter"). The converter carries the map properties over.
- That byte is 2 on all 483 maps. It is not the balance threshold, which the binary reads from byte `0xB` and the export carries as `D` (`Alundra/Combat/BalanceTables.cs:27`).
- E20.e reads it as the index of the tile-break effect played when grass is cut (`docs/plan-e20-heros.md`, section 1.27).
- `balance.json` has its own `BalanceLevel` field, the power tier of an enemy record. It is a different field, and its name is correct.
- The author decided to read the byte and to rename the field (Q-E3). Asked for the name and the place, the author chose the source, the analyser, with the proposed name raising no objection (Q-E3b).
- That proposal also named `GameMapInfo`, but `GameMapInfo` and its reader `GameEngine.cs:2520` live in the decompilation (`AlundraTools/AlundraEngine/`), which the project reads and never corrects. That part is dropped (D-E20-60).

## Decision

- The exported map property is renamed `TileCutEffectId` at the source, in the analyser's Tiled exporter (`TiledMapExporter.cs:275`), and the analyser's format doc notes the exported name.
- The decompilation's field `GameMapInfo.BalanceLevel` and its readers keep their name.
- The maps are extracted again, then mirrored into `data-extracted/`, and the converter export follows.
- `balance.json`'s `BalanceLevel` does not change.
- A slice, E20.eR, does the rename before E20.e, which then reads `TileCutEffectId` (default 2).

## Consequences

- The slice touches the analyser submodule. The author's local changes there are not staged, and the author fetches the submodules themselves.
- Re-extraction is risky. The remaster extracted on 2026-09-19 had undecoded text, so the direction of every difference is checked before mirroring, and the mirror is proven with `diff -rq`.
- A full in-place export with its manifest proof validates the change.
- The test that sets the map property (`Alundra.Tests/AlundraBalanceTablesTests.cs:154`) follows the new name. Living docs that cite the map's `BalanceLevel` are updated; historical documents and the frozen oracle annex scripts are not (D-R-6).
