# E20.g1a - measured results

Start HEAD `b62726ff`; the state at the start and the predicted list are in `README.md`.

## T1 (phase A) - tests first, the skeleton, then the code

Commit of the code of T1: see `git log` (message `feat(e20-g1a): T1 ...`). Files: `Alundra/Hero/CarryRules.cs`, `Alundra/Ai/Objects/LiftableAi.cs`, `PlayerManager.PutInHands` (in `Alundra/Hero/PlayerManager.cs`),
`EntityScriptProxy.CarryLockCounter` (in `Alundra/Entities/EntityScriptProxy.cs`), and the tests below. The stubs `PlayerManager.PlayerTryAction` and `PlayerManager.UpdatePlayerCarriedEntity`, `MovePlayer`, `TickMotion`,
`WorldProxy.DestroyEntity` and `NativeSpriteRegistry.CreateForProduction` are not touched: phase A changes no behaviour.

### New tests (68 test cases in 5 classes and a bench), red on the skeleton, green on the code

The skeleton (every method `throw new NotImplementedException()`, same signatures, `LiftableAi.SpriteTypes` and `Keys` empty) compiled; the new tests run on it: **68 failed, 0 passed**. On the code: **68 passed**.

| Class (folder mirror of its subject) | Cases | Plan item |
|---|---|---|
| `Hero/CarryRulesStepTests` | 7 | (c1) and (c2), the pure half: `StepTowards`, `FreeOffset`, `LockedPull`, `CanDestroyLocked` |
| `Hero/CarryRulesModesTests` | 32 | (c1) mode 0 (7 vectors of `c1_equality`, the trace of 32 calls with X at the 12th, nothing carried, a carried entity without proxy), (c2) mode 1 (counter 0-4 and -3, the fifth call, the thresholds 0x1FFFF / 0x20000 on four triples each, `AttackCount` 0 against 1, no world), (c3) mode 2 (and 255) |
| `Hero/CarryGroupTests` | 13 | (c6) the table of the carrying group (0x06, 0x07, 0x0C, 0x2E, 0x2F, 0x30), the lift gestures 0x05 and 0x0B, `LiftAnimation` |
| `Hero/PutInHandsTests` | 5 | (c5) |
| `Ai/Objects/LiftableAiTests` | 11 | (c4) C23 (not carried / carried without a code / the four codes / the table / the flags / a carrier without proxy), the seven types against the export, the registration in a registry of a test |
| `Hero/CarryBench` | - | the bench (two real proxies of two real engine entities, the sheet, a world that records `DestroyEntity`) |

Each "without effect" case is in the same assertion or the same test as its positive neighbour (counter 0-4 next to 5, thresholds 0x1FFFF next to 0x20000, `AttackCount` 0 next to 1, nothing carried next to carried, not carried next to
carried, the object on a head next to the object on another entity's head, the null-equals-null hero next to the real one).

Not done in T1, by the plan: (w1) to (w12) (T2), the branchings (T3), the registration and (r1), (r2) (T4), the ADR and the mutants of the DLL code (T5). The mutants below are a check of the T1 tests, not the T5 list.

### Real mutations of the T1 code (script `mut.py` in the session scratchpad: apply, rebuild, run the 68 tests, restore the bytes)

30 mutants of `CarryRules`, `LiftableAi` and `PlayerManager.PutInHands`; 28 killed by at least one of the 68 tests, 2 equivalent (proved):

- killed: mode 0 `<` to `<=` (7 red); free step 1 px to 2 px (10); locked step 2 px to 1 px (6); destroy limit 0x1FFFF to 0x20000 (4); counter 5 to 4 (1); counter not reset in mode 0 (9); `AttackCount` rule dropped (2); mode 2 without -1 (3); mode 2 clearing the
  link (2); counter signed to unsigned (1); mode 1 destroy resetting the counter (6); offset read from the offset words (20); group with the heavy test before the Square (1); states 0x06 and 0x2E swapped (4); `NoJump` ignored (1); `NoThrow` ignored (1); lift gesture
  0x0B not leaving at the ground (1); C23 flags not cleared (7); C23 carrier link kept (5); C23 table 2 to 3 (4); C23 riding kept (4); C23 without its carried test (5); `PutInHands` without the marker (1); with the riding kept (1); with null equals null for "on the head" (1);
  with the ground inverted (2); without the Z of the offset (2); head height 32 px to 31 px (9).
- equivalent: `StepTowards` with `gap <= step` (when the gap equals the step, `current +- step` is the target, so both branches return the target for any step > 0) and `StepTowards` with the gap taken by `Math.Abs((long)difference)` cast to `uint`
  (for every 32-bit difference, `|d|` fits an `uint` and equals `(uint)-d` / `(uint)d`).

### Suites

Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets` on build and test), then Debug built last (the copy to `alundra-project/Alundra.dll` not neutralised); `--blame-hang-timeout 60s`; disjoint partitions by test namespace
(`FullyQualifiedName~Alundra.Tests.<ns>.`): 1 = Arcs, Ai, Audio, Cameras, Cells, Combat, Dialogues; 2 = Entities, Events, ExportGuards, Hero, Items; 3 = the negation of the twelve.

| partition | tests | failures Release | failures Debug |
|---|---|---|---|
| 1 | 1 224 | 0 | 0 |
| 2 | 2 713 | 0 | 0 |
| 3 | 1 793 | 0 | 0 |
| total | 5 730 (= 5 662 at the start HEAD + 68, equal to `--list-tests`) | 0 | 0 |

- Existing tests moved: **none**. `--list-tests` before (5 662 names) and after (5 730): none of the 5 662 is missing, the 68 added are the new ones. Predicted list for T1: empty; measured: empty.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing: `TextBoxGpu.cs`).
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: equal after the last Debug build.
- The four `docs/hero-trace-389-*.txt` files were rewritten by the suites (line endings); `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0 after the Release and after the Debug suites, then restored by `git checkout`, never indexed.
- No diff of `RunHeroVerticalTick` or `SnapHeroAtApex`; the engine, the converter and the data are untouched.

### Decisions of T1 (local design, none changes the plan)

- `CarryRules.UpdateCarried(hero, mode, IEntityWorldContext? world)` takes the host that destroys in mode 1 as a parameter (phase B passes `player.ScriptHost as IEntityWorldContext`, the plan's rule): the direct tests then need a world of 5 members, not a full `IScriptHost`.
- A sheet of balance absent (`BalanceRecord` null, a bare montage) is read as `AttackCount` 0 (the binary always has a sheet; a bare proxy has none), so mode 1 is mode 0 there. Phase B tests that need the destruction give a sheet with an attack.
- A code of throw other than 1, 2, 3 and -1 does not release (`LiftableAi.ReleaseAnimation` returns null): the binary reads the words around the table `0x80027400`; no writer produces such a code (mode 2 writes -1, the throw of g2a 1 to 3).
- `PutInHands` lives in `PlayerManager` (D-G-2, "couture interne"), the pure choice of the state in `CarryRules.LiftAnimation`. The marker `HasLogicalZ` is posed by it and never removed.
- Mode 2 writes the raw fields of the object only (like the binary); whether the root of a marked entity follows at once is a question of phase B ((w8)).
- Any `mode` other than 0 and 1 is mode 2 (the model `g1a_models.py upce` and the critic's vector with the mode 255).

### Owed to the slice (not T1)

- The commun T0 (`docs/plan-e20-annexe/e20-g/`: oracle copy and manifest, `c4_climb_lift`, the 16 one-word mutants, the probes P-G-1 to P-G-5) is not on the branch; see `README.md`. T2's (w2), (w4) and (w5) take their series from the plan table and
  from the models of `docs/plan-e20-annexe/e20-g1a/work/`, which the critic replayed; the oracle acceptance line stays open.
