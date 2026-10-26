# E20.g2c2 T1 - phase A: results (2026-10-26)

Branch `chantier/theme2-attaques-objets`, start HEAD `f535d63f`. Two commits: the tests (`c881cd1c`), then the handlers and this file. Nothing is registered in production (`NativeSpriteRegistry.CreateForProduction`, `PlayerManager.ProductionWiredLiftableTypes` untouched); the disposition D-G2C2-9 of the T0 stop is applied (no tick pinned on the rain).

## Code

- `Alundra/Ai/Objects/TorchAi.cs` (new): C70 (`Tick`, nothing while `PlatformEntity` is null, else `LiftableAi.TickCarried(.., C70)`), E20 (`Deactivate(entity, context)`: flame by `SpawnEntityBySpriteIndex(entity, false, 12, X, Y, Z, direction)`, six codes erased, animation 3, `(flags & ~0x10) | 2`, torch destroyed with -1; a null birth gives no flame and the torch is destroyed all the same, D-G2C2-2; the second branch: animation 2, `(flags | 0x40) & ~0x30`, `Bytes[0] = 1` on a contact of type 391), `Register`, `Keys`, `SpriteType`/`SpriteTypes`, `HasNoFlameMark` (the `+0x274` byte, for g2c3).
- `Alundra/Ai/Objects/BrambleAi.cs` (new): D34 (`Touch`), `Register`, `Keys`, `SpriteTypes`.
- `Alundra/Ai/Objects/LiftableAi.cs`: `ReleaseFlagsPolicy.C70` and one explicit case per policy in `TickCarried` (`C23`, `C70`, `C4`); the former `_ =>` that meant C4 now throws `ArgumentOutOfRangeException`.
- No call of `ResyncControllerFromFlags` (as the probe port of T0, which equalled the binary; the bits written are not ClassA/ClassB). T2's world tests will say if a bramble or a flame needs it.

## Tests (commit `c881cd1c`)

New: `Alundra.Tests/Ai/Objects/TorchAiTests.cs` (a1, a2, a4, keys), `BrambleAiTests.cs` (a3, key), `TorchDataGuardTests.cs` (a5), `TorchRegistrationTests.cs` (r1), `TorchVector.cs` (the loader of `vectors/goldens_g2c2.json`, whose SHA-256 is pinned). Support: `AnnexFiles.TorchAnnex`; `CutBench.CutHost` gains `SpawnCalls`, `TableIsFull` and `OnBirth` (additive).

Red before: with the tests alone the test project does not build (`CS0103` `TorchAi`/`BrambleAi` do not exist, `CS0117` `ReleaseFlagsPolicy.C70`); the 29 tests are red by construction until the code exists. Green after: 28 of 29; the 29th is (r1) `R1_TheProductionRegistryHoldsC70AndE20_ForTheTorchAlone_AndD34ForTheTwoBramblesAlone`, red by design until the inscriptions of T2. (a5) passes on the export at once: C70 and E20 are held by {393} alone, D34 by {238, 239}, A19 and C71 by {391}.

Subset run in Release (neutralised copy, `--blame-hang-timeout 60s`): `Alundra.Tests.Ai.*`, `Alundra.Tests.Hero.Lift*`, `Alundra.Tests.SaveGames`: 1548 tests, 1 failure (r1), 0 existing test moved. The full suite is T3's. `check_names.py --rules`: 0 finding. `git diff --ignore-cr-at-eol --quiet -- docs/`: clean.

| Test | Cases | Compared with |
|---|---|---|
| (a1) `A1_C70_TheThirtyVectors...` | 30 vectors: 13 released, 1 carried without code, 6 posed, 10 off the table | words read and written; codes 4 to 7 (animation read past the table as an address) are not released by the DLL, like C23 and C4: deviation pinned apart |
| (a1) table, off-table codes, posed torch whose animation asked is 2 | 4 + 1 + 1 | the flags `(f \| 0x34) & ~0x80` over ten values; revision 2 |
| (a2) `A2_E20_TheThirtyVectors...` | 30 vectors: 9 consumed (3 flames, 1 null birth), 21 second branch (marks on the drop 391) | flame args, order (born before the destruction), the six codes, animation 3, flags, byte store |
| (a2) six non-zero codes; flags of the flame; table full; mark and contact types 0..599; second branch | 5 | revision 2 |
| (a3) D34 | 30 vectors + the 256 attributes + 38 flag values + absent cases | |
| (a4) policy | 3 | C70 is not C4 nor C23 (`0x35`, `0x31`, `0x81`); an unknown value throws |

## Mutants applied to the production files (20, all killed, files restored identical)

`| 0x34` to `| 0x30` (a1, a4); C70 taken for C4 (a1, a4); erasure from the second code (a2); erasure short of the last code (a2); flame animation 3 to 1 (a2); `& ~0x10` forgotten (a2); `| 2` forgotten (a2); `+0xAC` test removed (a2); mark ignored (a2); drop type 391 to 392 (a2); D34 attribute 6 to 5 (a3, keys); `0x46` to `0x42` (a3); `& ~0x80` forgotten (a3); flame `Flags = 2` (a2); C70 guard on the carrier removed (a1); erasure removed (a2); torch not destroyed (a2, handlers); destruction with effect 11 (a2); `0x30` not cleared on the torch (a2); mark written to byte 1 (a2). Not run here (T3's, need T2): the world tests (w*) and the mutant "393 wired without its key" ((r1) second test, green now, killed when 393 is wired in T2).

## Deviations from the plan

- (r1) is committed red, as the plan says ("rouge avant les inscriptions de T2"); it is the only red test of the tree.
- `TorchAi.Deactivate` takes the world context as a second argument (the flame is born by the context, the destruction by `ScriptHost`, as E2/E12 do).
