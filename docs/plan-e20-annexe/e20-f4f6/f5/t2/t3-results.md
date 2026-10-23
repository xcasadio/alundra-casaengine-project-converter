# E20.f5, T3 (phase B): results (2026-10-23)

Start HEAD: T2 commit. Change: `PlayerManager.ProductionWiredWeaponTypes` = `{3, 4, 7, 27, 28, 29}` (the normal arrows are born in production; the only behavior change of f5).

Green after: the 4 cases of `ArrowProductionTests` ((p1) x2, (p2) x2), red before (T2).
Suites: `Alundra.Tests` Release (neutralised copy) 5 428 passed, 0 failed (5 424 + 4); Debug (last, deposits the DLL) 5 428 passed, 0 failed; `cmp` of `Alundra.dll` with `alundra-project/Alundra.dll` identical; `check_names.py --rules` 0 finding, 1 known warning.
Removal proof (set {3, 4} removed, then restored): exactly (p1) x2, (p2) x2 and S5 (derived from the classes, as in f4) turn red.

Existing tests moved in phase B, against the predicted list (`../results.md` section 3): S1 first block (the empty set is now the test set `Wire()`), S5 (the set is derived from the classes, plus the normal arrows 3 and 4 of `ArrowAi.SpriteTypes`), `ChargedArrowProductionTests.P1` (the lowest free slot of the charged arrow is 2 when the production set holds the start-of-hold type 4, derived from the set). Predicted: 3, measured: 3 (S1 did not fail on its own: it was
rewritten in advance because its first block depended on the production set; the full suite failed only on P1 of f4 before the rewrite). No test outside the list moved.
