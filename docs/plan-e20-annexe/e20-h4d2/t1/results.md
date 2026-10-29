# E20.h4d2 T1 - phase A1, results (2026-10-29)

Commits: tests first `81c178ab` (with the compiling skeleton, 52 red / 22 witnesses green, `red_before_the_code.txt`), then the code commit (this file's commit). Branch `chantier/theme2-attaques-objets`.

## What was added (inert in production: the entries `0x2F` stay stubs, `(C, 63)` is not in `CreateForProduction`)
- `Alundra/Ai/Items/TomeWords.cs` (new): `Read274` / `Write274`, the 32-bit word `+0x274` on `Bytes[0..3]`, little-endian, signed (D-H4D2-3). Name accepted by `check_names.py --rules` (0 finding).
- `Alundra/Ai/Items/FireTome1Ai.cs` (new): `SpriteTypes {23}`, `Keys [(C, 63)]`, `BallTick(ball, context)` (production: the count is computed), `BallTick(ball, entities, count, inclusiveBound, sortThreshold)` (the overload with the count), `Cancel(hero)`, `Register(registry)` (C63 for the type 23 only).
- `EntityScriptProxy.SpellTarget` (the reference of the target, D-H4D2-4; not cloned); `TargetQuery.StoredCount(entities)` (D-H4D2-5; h4e1 had not posed it).
- `EarthTome1Ai.SpriteTypes` `{16}` -> `{16, 23}` (one key, no new `Register`).

## Tests (Alundra.Tests/Ai/Items/)
- `TomeWordsTests` (t1): 19 tests. `FireTome1AiTests`: 39 tests = (c1) the cancellation against the 319 comparable vectors (73 columns destroyed with their effect, 113 balls cancelled); (c2) by hand (7 tests); (e1) E3 against the 288 vectors (12 destructions); (e2) the registrations on a fresh registry (+ the registered handlers); (k1) C63 in the binary mode against the 3 524 vectors (313 waits, 3 211 active, 660 acquired, 591 forgotten, 598 draws, 1 956 turns), all green at the first run of the real code; (k2) the grid 32 x 32 against the hand law (240 pairs of numeric gap > 16 take the long side), 14 named cases, 31 -> 0 and 0 -> 31 in eight steps over 24 ticks; (k4) the production values of the two settings and the default call; (k5) the type 10 (attack byte `0x8A`) and the mask ClassA of the query, observed through the targets chosen (the query is not spied: its effect on the choice is).
- Vector adapter: `FireTome1Vectors.cs` + `BuildBall` (T0 (b) rules: a pointer is a slot, an identity read different from the stored one is a dead reference `Destroyed`/`Slot -1`, Z minus one, output arrays ignored).
- (e1) and `C2_TheOtherTypes...` are green on the skeleton too (witnesses): E3 is the unchanged h4c1 function, the widening is checked by (e2).

## Suites
`suite.summary`: 7 886 = 7 828 + 58, 0 failure in Release (neutralised copy) and Debug, seven parts (`part.sh`). `git diff --ignore-cr-at-eol --quiet -- docs/` true, the four `docs/hero-trace-389-*.txt` restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `alundra-project/Alundra.dll`: identical. `check_names.py --rules`: 0 finding.

## Tests moved (against the predicted list of `t0/predictions.md` / `results.md` section 5)
- `EarthTome1AiTests.E2_...` renamed `E2_E3_IsRegisteredForTheTypes16And23Alone_NotThe17_NorThe24_...` (predicted: (e2) of h4c1 -> `{16, 23}`).
- `EarthTome1ProductionTests.P4_...` (the ONE test more found by T0): its body takes the type 16 alone (`new[] { 16 }`) instead of `EarthTome1Ai.SpriteTypes`, so that it stays green in the phase A1 (with `{16, 23}` it would answer `[(C, 63)]` until the phase B). The phase B (p4) covers `{20, 23}`.
- The five sums of the registry did not move (no key added in the phase A1). No other existing test moved.

## Deviations / notes for the plan
1. D-H4D2-6 says the tests pose `InclusiveBound` and `SortThreshold` and restore them: at the HEAD they are `const` (`TargetQuery.cs`), the settings are PARAMETERS of `TargetQuery.Find`; `BallTick(ball, entities, count, inclusiveBound, sortThreshold)` passes them. The registered handler uses the production constants. The world tests of A2 ((w2), (w3)) in the binary mode will need a seam on the registered handler (e.g. an internal static override in `FireTome1Ai`) or a world driven through the overload: to decide in A2.
2. A ball without `CurrentAttack` asks the query for the type 0 (the binary reads the byte at the pointer; no oracle, as the plan says); tested only as "does not throw".
3. The generator of the DLL has more than 32 bits of state: the vectors are compared on the low 32 bits, as the E28 tests do.
4. `Cancel` has no slot filter (as `FireTome2Ai.Cancel`): the entities of the world at the status Normal with the type 20 / 23; the count bound of the binary cannot exclude a live entity (F22).
5. The hero's `git status` shows `README.md` modified: not touched by this slice (present in the working tree before T1).
