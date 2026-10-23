# Value vectors kept for the tests of E20.g5b

Provenance: copies, line endings normalised to LF, of annex files of the thème 3 slices that this slice takes over (D-G5B-1). The files are the ones the discovery of E19.sm3 and E19.sm4 froze;
they were replayed from copies in T0 of E20.g5b (`../README.md`) and are equal to the originals.

| file | taken from | content | SHA-256 (LF) |
|---|---|---|---|
| `sm3-a9-aa-vectors.jsonl` | `docs/plan-e19-sm-annexe/sm3/vectors/sm3-vectors.jsonl`, the 202 lines whose `op` is `0xA9` (103) or `0xAA` (99), in file order | one execution of the real handler of `ALUN_CD.EXE` (France) per line: the world, the instruction, `Result` before and after, the length | `67c8a4264744265b67ddf94b4de4dfc01e63297cf1a11845f872c87d544e6643` |
| `sm4-op72.json` | `docs/plan-e19-sm-annexe/sm4/vectors/op72.json`, whole | 67 executions of the real handler of `0x72` (7 by hand, 60 seeded random worlds) | `0158fd87cc773f71ed1cba911c6cf4dd17b6a5db3f8eb4bfa9bfd6d3b5c972e9` |

The tests read them through `AnnexFiles.ButtonAnnex` and pin the SHA-256 after the line endings are normalised (R-SM-5): `Alundra.Tests/Events/ContactVectors.cs` and `LastTargetOpcodeTests.cs`.
The format of a line is described in `docs/plan-e19-sm-annexe/sm3/vectors/README.md` and `docs/plan-e19-sm-annexe/sm4/vectors/README.md`.
