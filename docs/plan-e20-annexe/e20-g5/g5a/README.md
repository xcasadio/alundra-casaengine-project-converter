# E20.g5a: the pushable pillars (C76), measured results

Plan: `docs/plan-e20-heros.md` section 1.24.6, sub-section "E20.g5a" (revision 0, READY). Discovery annex: `../disc/`. Start HEAD: `2eff6b46` (branch
`chantier/theme1-porter-plateforme`, after E20.e0c2). This file is written task by task; this first version is T0 (the commit of docs) and T1 (phase A).

## T0 - re-reading, oracle replay, prediction, montages

### Oracle replayed from a copy (scratch, outside the repository)

`disc/` and the MIPS machine `../../e20-g1a/work/` were copied to a scratch folder (the scripts write next to themselves and read the binary
`ALUN_CD.EXE` (France) at the absolute path they name).

| replay | command | result |
|---|---|---|
| C76 and its helper against the real code | `python -B -X utf8 c76_full.py 6000 mut gold` | 6 000 vectors, 0 mismatch, instruction coverage 82/82, 14 outcome classes, 73 one-word mutants: 56 killed, 17 survivors (the same 17 as the annex: 10 stack frame, 4 `move` to `subu`, 3 on the `-1` constant of the helper, dead code since C76 pre-tests the contact); the output equals `../disc/c76_full_out.txt` (line endings aside) |
| goldens | same run | `c76_goldens.json` rewritten, byte for byte equal to `../disc/c76_goldens.json`, SHA-256 `c80ffbe3def37599c6671f32bf2020153c6b782c235707eceecce09f4ad90d5a` |
| scenarios P1 to P11 on the real `MovePlayer`, spawner, C76, physics | `python -B -X utf8 g5_scen.py` (2 min 23 s) | equal to `../disc/g5_scen_all_out.txt` (the annex file only ends with one more line, `done`, written by the shell wrapper), and the 22 tables of `out/` are equal file by file |

### The registry, its pins and the movement probe at the start HEAD

- `NativeSpriteRegistry.CreateForProduction` (`Alundra/Ai/NativeSpriteRegistry.cs`) registers `BeannoideAi`, `MurutaAi`, `WaveSwordAi`, `PickupAi`,
  `ChargedArrowAi` and `ArrowAi` in that order; `Register` throws on a taken key and returns an `IDisposable` that removes the entry.
- The pins of the count of the production registry, three of them, all written as a sum of the counts of the classes:
  `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:58` (`6 + 1 + PickupAi.Keys.Count + ChargedArrowAi.Keys.Count + ArrowAi.Keys.Count`),
  `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:185` (`V3_...`, same sum with `3 + 3 + 1`),
  `Alundra.Tests/Ai/Projectiles/ChargedArrowAiTests.cs:225-234` (`C2_TheProductionRegistry_...`, a registry `sum` built by hand with one `Register` per class, then
  `Assert.Equal(sum.Count, production.Count)` and the same literal sum).
  `ArrowAiTests.E5_...` counts only `ArrowAi`'s own keys and does not move.
- The movement probe: `EntityScriptProxy.MoveControllerAndPullPosition` (`Alundra/Entities/EntityScriptProxy.cs`, near line 2424) clears `XCollisionEntity`, moves the
  controller, and sets `XCollisionEntity` to the proxy of `LastContact.H2Obstacle ?? H1Obstacle` (E19.d2b B4). The pillar is not ignored by the probe, so the hero
  stops against it and C76 reads `hero.XCollisionEntity == pillar`.
- The fields C76 reads, by their names in the DLL: `+0x1D8` is `ModdedPosX` (rewritten at the head of the tick for the active entities nothing blocks,
  `WorldProxy.cs` near line 2993, as the binary does; `PickupAi.Overlaps` reads it the same way), `+0x1F0` is `Width` (the same `(size << 16) - 1`),
  `+0x114` and `+0x118` are `PosX` and `PosY`, `+0x88` and `+0x8C` are `TargetAnimationId` and `TargetDirection`, `+0x274` is `Bytes[0..3]`.
  The discovery's prototype read `PosX + ModX` for the first one; the port reads `ModdedPosX`, the word the binary reads.

### Prediction of the existing tests that move, measured by a temporary wiring

Wiring: the discovery's prototype `PillarAi.cs` copied as is to `Alundra/Ai/Objects/` and `PillarAi.Register(registry);` after `ArrowAi.Register(registry);`
in `CreateForProduction`. Release build with the neutralised copy, then the whole `Alundra.Tests` suite in four partitions (a full run is 10 min 51 s, over the
foreground limit): baseline at the start HEAD 5 434 tests, 0 failures.

| partition (filter on `FullyQualifiedName`) | tests | failures with the wiring |
|---|---|---|
| `Alundra.Tests.Arcs` | 76 | 0 |
| `Ai.`, `Audio`, `Cameras`, `Cells`, `Combat`, `Data`, `Dialogues` | 1 084 | 3 |
| `Entities`, `Events`, `ExportGuards`, `Hero`, `Items` | 2 485 | 0 |
| everything else | 1 789 | 0 |

The three failures are the three pins above, and only them:
`NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn`,
`WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem`,
`ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses`.
So the arc TN-3 of the map 346 (a pillar where the hero stays parked) and `ArrowAiTests` (type 430 as a neutral value) do not move. The wiring was removed
afterwards (`git status` clean, the four `docs/hero-trace-389-*.txt` restored).

**Predicted list for T2** (the files and tests that change): the three pins above, rewritten by derivation (`+ PillarAi.Keys.Count`; for the `ChargedArrowAiTests`
test, one more `PillarAi.Register(sum)`); nothing else. T2 measures it again with the final code (it reads `ModdedPosX`, not the prototype's `PosX + ModX`).

### Montages chosen for the world tests (T2)

All through `ArcRun` with `ArcSpec(... RealController: true, Prefabs: true ...)` (the real controller and the real prefabs), `PlaceHero`, `HoldDirections`,
`EntityByRecord`, as the discovery probe did (`../disc/probe_G5ProbeTests.cs.txt`, cases in `../disc/cases*.json`).

| test | map and record | montage |
|---|---|---|
| (w1) east, (w2) north, (w3) speed and release, (w4) vertical offset, (w8) sound | map 26 (`Ancient Shrine-26`), pillar record 29 | hero at (300, 424, 16), `R` for 160 frames (east, stop at 350, hero 329); hero at (324, 452, 16), `U` (north, stop at 406); release and press again for (w3); (w4) the hero placed at `|dy| = 12` and `14` from the pillar |
| (w5) a plate or a switch on the ground blocks, two aligned pillars do not chain | map 26 and a built bench | a plate (type 108) or a switch (type 45) spawned on the path; a second pillar beyond the first |
| (w6) the pit | map 347 (`Lake Shrine (inner)-347`), pillar record 16 | the blockers, records 6 and 11, removed by a write of the harness (`DestroyEntity`), hero at (168, 232, 48) east or (240, 232, 48) west; plus one test with no removal (the pillar stays blocked) |
| (w7) the 13 inert pillars | map 423 (`Nirude (inner)-423`), records of type 430 | the hero pushes against each; no native run, no motion |
