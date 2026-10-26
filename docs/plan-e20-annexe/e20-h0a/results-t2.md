# E20.h0a T2 - results (oracle tests)

Branch `chantier/theme2-attaques-objets`. Tests commit: see `git log` (`test(e20-h0a): T2`). Plan: `docs/plan-e20-heros.md` section 1.30.1, T2; T0 in `results.md`, T1 in `results-t1.md`. No production code touched in T2.

## 1. What was written

(o1) The traces of T0 (b) that h0a makes exact, compared field by field with the oracle, the sound 3 included:
- `W3-O-idle`, `W3-O-swim`: `HeroSwimTests.W3_TheCircle_DoesNotBurst_ItIsTheItemButton` (existing test, exception removed).
- `W9-circle-idle`, `W9-circle-swim`: `HeroSwimTests.W9W10_TheOtherCases_AreTheOracle` (existing theory, exception removed).
- `R7-circle-windup`: `HeroRunTests.G7_InThePreparation_TheSquareSwingsTheSword_TheCrossAndTheCircleDoNothing` (existing, `ignore: tick == 10 && sounds` removed).
- `W9-item21`, `W9-item22`, `W9-item28`, `W9-item2A` (the four holes of the item table): NEW theory `HeroSwimTests.W9_TheHolesOfTheItemTable_RefuseWithTheSound3_AsTheOracle` (4 cases; also asserts exactly one sound 3, at tick 10). `RunMontage.Start` learns the inventory `itemNN` of these traces (boots 0x1B of the swim kit, the item NN owned and equipped; owned `[1,17,25,27,NN]`, item NN as in the trace `meta.inventory`).
- `R7-circle-sprint` was already exact (state 0x03 never calls `TryUseItem`). The 16 other W9-item traces (1F 20 23 24 25 26 27 29 2B-32) get their sound 3 from the start of their own object in deep water (T0 section 2): they wait for the slice of their object, not compared here.

(o2) The tests of the predicted list, without their exception of the sound 3: `HeroRunTests` (the `ignore` of tick 10) and `HeroSwimTests.StubbedSounds` (`id == 3 ||` removed; the sounds 21 and 6 of a landing stay stubbed, E20.e). Comments fixed: `HeroSwimTests.cs` class summary and `StubbedSounds`, `HeroRunTests.cs` class summary and the circle comment, `RunMontage.cs` (`ignoreSound` comment). The `ignoreSound` parameter of `RunMontage` stays (used by 21 and 6).

## 2. Moved tests against the predicted list (T0 section 4)

Exactly the predicted ones: `HeroRunTests.cs` (G7 circle-windup), `HeroSwimTests.cs` (`StubbedSounds`, hence W3-O-idle/W3-O-swim and W9-circle-idle/W9-circle-swim), `RunMontage.cs` (comment only plus the `itemNN` inventory case for the new theory). No other test file touched; no other test went red.

## 3. Proof of retraction (real mutation by script, undone, `git diff` empty after)

`TryUseItem` returning 1 before `StartItem` (the stub): of the 12 tests of (o1)/(o2) (`W9_TheHolesOfTheItemTable` x4, `W9W10_TheOtherCases` x6, `W3_TheCircle...`, `G7_InThePreparation...`), **8 red** = the circle-idle, circle-swim, the 4 item holes, W3 circle, G7 circle ("sounds: dll [], oracle [3]"); the 4 green are the `W10-*` cases of the same theory, which have no Circle. Green before the mutation: 12 of 12.

## 4. Suites

Release (`skipcopy.targets`), five slices (the whole suite is above 10 minutes): `Hero.` 1 849 (1 m 34), `Arcs|Maps|Support|Combat` 686 (2 m 09), `Audio|Cameras|Cells|Data|Dialogues|Entities|Events` 1 759 (3 m 27),
`Ai.` 903 (7 m 04), the rest (all the others excluded) 1 869 (1 m 07): **7 066 tests, 0 failure** (7 062 of T1 + the 4 new cases). Debug (built last, no neutralised copy), the same five slices: 7 066, 0 failure.
`cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 old warning, `TextBoxGpu.cs`).
`docs/hero-trace-389-*.txt` rewritten by the suites (end-of-line only; `git diff --ignore-cr-at-eol --quiet -- docs/` clean), restored by `git checkout`, never staged. Engine, converter, data: no diff (the submodule pointers are unchanged).

## 5. Deviations from the plan text

1. The new (o1) cases for the four holes needed the inventory of the item traces in `RunMontage.Start` (a test-support addition, not in the plan text).
2. The 16 object-start traces are not compared (T0 section 2 already said so).
