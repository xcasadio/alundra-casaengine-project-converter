# E20.g2c2 T2 - phases B and C: results (2026-10-26)

Branch `chantier/theme2-attaques-objets`, start HEAD `b046c5f5` (T1). Four commits: the tests (red), phase B, phase C, the preset. The mutants and the Debug suite are T3's.

## The tests (commit 7d46166f, `TorchRun.cs`, `TorchWorldTests.cs`)

The bench `TorchRun` is the probe bench of T0 (`probes/G2c2T0ProbeTests.cs.txt`) made a support: the real world of the map 92, emptied but the hero, on a synthetic field, the native handlers off but for the types asked (the torch 393 and the flame 12 by default), every tick sampled at the "inventory" pass. Nothing is registered by the tests.

| Test | Cases | Checks |
|---|---|---|
| (w1) thrown torch | 1, 2, 3 ticks per frame still, 1 walking | the ticks are READ from `replay/s8_torch.txt` (throw = tick 1): landing T+22, status 3 + animation 2 + flags `0x332346` + sound 142 at T+23, flame born at T+26 (type 12, animation 3, flags `0x132146`, six codes 0, x = 143.5 still / 178.0 walking, y 100) in the tick the torch is status 4, torch freed + sound 41 at T+27, the flame present exactly 39 ticks, the hero never hurt, no spark |
| (w2) contact | 1, 2, 3 | thrown walking against an inert class-A target (sprite 51 of the page): burns at the blow + 1, flame born at the blow + 4 (torch status 4), 39 ticks, the flame touches the target again at the blow + 25 (p_hitswitch) |
| (w3) switches | records 39 to 42 of the map 92 | the real hit pass and the real programs D40..D43 then C40..C43: the flag of the lit switch (145, 155, 165, 175 of word 129) raised, sound 39 once; control without the torch: no flag, no sound |
| (w4) brambles | 238, 239 + sword | torch: animation 1 by the blow + 2, flags with `0x46`, not collidable, gone before tick 220; sword: nothing changes (animation, flags, presence) |
| (w5) rain | 1 | the real lever (record 14, program C[15], the flag 150 raised as the pull): 4 torches at the four points, 4 flames, each at its point, z 0, sounds 142 x4, 41 x4, 55 x1; no tick of the status 3 pinned (D-G2C2-9) |
| (w6) flame on the hero | 1 | the torch thrown still, the hero walks into the flame: life 10 until the hurt tick, hurt after the flame is born, animation `0x31` |
| (w7) water, hole | 1 + 3 | contact above deep water: the flame is born in the air, falls, is destroyed on the water (a few ticks, effect 6); hole (walkability 4): the torch lands T+22, status 4 at T+23, freed at T+24, effect 10, no flame, no sound 142 (g2b2) |
| (w8) natives at rest | 8 maps + the census | the 44 torches of the census (`native-torches-86-94.txt`, read) spawned by record on the real field of the maps 86 to 94 keep flags, animation, position and status 6 ticks; witness |
| (w9) mute flame | 1, 2, 3 | (C, 60) of f6b asserted in the production registry; the flame lives ticks 27..65, no effect 17, six codes 0 |
| (w10) lift (phase C, added) | 1, 2, 3 | the torch at rest is lifted (carried, animation 5, platform = the hero); without 393 in the wired set the Square gives the sword |

### Red before (commit 7d46166f, registry and wired set untouched): 26 red, 9 green

Red: W1 x4, W2 x3, W3 x4, W4 x3 (two brambles and the sword test, whose first form asserted that D34 ran), W5, W6, W7 x4 (water, three holes), W9 x3, W10 x3. Green: (w8), 8 maps + the census. Reasons read: the torch never lands (no C70), no flame (no E20), the switches stay dark, the brambles stay inert. After the phase B alone: only (w10) red (3). After the phase C: none.

The sword test was reshaped in the commit of the phase B into a witness (green before and after): the unburnt bramble hurts the hero who stands next to it (animation `0x31` at tick 4 in the probe) and the sheet of a bramble answers only to the attribute 6, so D34 is not even called by a sword blow; the test asserts the sword swings, the hit pass makes sparks, and the bramble is unchanged. The mutant "D34 burns on any attribute" is killed by (a3) (256 attributes) and (w4) (torch), not by this witness.

## Phases B and C and the preset

- Phase B (commit f11c1037): `NativeSpriteRegistry.CreateForProduction` registers `TorchAi` and `BrambleAi` (21 keys become 24). Five pins rewritten by derivation (`+ TorchAi.Keys.Count + BrambleAi.Keys.Count`, `TorchAi.Register(sum); BrambleAi.Register(sum);`).
- Phase C (commit 5d22a1c3): `ProductionWiredLiftableTypes` gains `TorchAi.SpriteTypes` (25 types become 26); `LiftPureTests` list pin rewritten (393 inserted), the two witnesses (m5) of `LiftWorldTests` and `LiftMap72ArcTests` derived on the bomb (global sprite 2).
- Preset (commit a0830325): `torche-m92`, map 92, tile (34, 11), level 5 (the cell of T0 (e)), the content test `TheTorchPreset_...`, the list test renamed `TheTwentyEightPresets_...`, `docs/test-saves.md`.

## The moved tests against the list of T0 (f)

The nine tests of the list moved and were rewritten, no other: NativeSpriteRegistryTests (ProductionRunner_...), LiftableAiRegistrationTests (R1), ChargedArrowAiTests (C2), WaveSwordLifeTests (V3), BallFlamesTests (M3), LiftPureTests (TheProductionSet_...), LiftWorldTests (M5), LiftMap72ArcTests (M5), TestSavesTests (the list). The files edited besides the new ones are exactly these nine files; the suites below pass with no other test touched. 9 <= 10.

## Suites after the phase C and the preset (Release, neutralised copy, `--blame-hang-timeout 60s`, disjoint partitions)

| Partition | Tests | Red |
|---|---|---|
| `Ai.` apart from `Ai.Projectiles.` | 762 | 0 |
| `Ai.Projectiles.` | 332 | 0 |
| `Hero.Hero*` | 1299 | 0 |
| `Hero.` apart from `Hero.Hero*` | 550 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2208 | 0 |
| everything else (with `SaveGames.`: 416) | 2163 | 0 |
| total | 7314 | 0 |

`check_names.py --rules`: 0 finding (1 existing warning). The suites rewrote the line ends of `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored, not indexed. The Debug build and suite are T3's.

## Observations and deviations (for the main session)

1. **The flame's x drifts by 0.5 px after its birth.** The oracle's flame is at x = 143.5 (still) and 178.0 (walking) in its last snapshot; the DLL's flame is born at those values (T0 P5, equal) and reads 143.0 from the next tick (the sample is the logical `PosX`; `probes/out/p5_thrown_1_still.txt` shows `x=143.50` at the birth tick and `x=143.00` after). (w1) pins the birth value exactly and the following ticks within [x - 1, x]. Not investigated (the plan does not ask); a possible rounding of the logical position against the root.
2. The Z of the flame (and of the flames of the rain) is 0 or one raw unit above (frame of D-E0A-1); the tests accept [0, 0.001] px.
3. (w5) the record-born torches of the lever are born at frame 38 of the run (z 176) and land at the tick 59-60 of the run; the status 3 is not pinned (D-G2C2-9); everything else of the chain (the four flames at the four points, the sounds) is.
4. (w7) water and hole have no oracle of the torch itself: the hole is the g2b2 rule (`DestroyOnVramFlags`, the same ticks as the objects of `PitFallWorldTests`), the water is `DestroyOnSlidingSlope` with the slope 4 read on the flame that lands on deep water. A torch that LANDS on deep water is destroyed by the same rule before burning (not tested, no oracle).
5. (w6) the damage is the c2b injury (no oracle for the amount): the test asserts the loss of life and the animation `0x31` after the birth, not the amount.
6. Brambles hurt the hero who is next to them (probe: sword animation, then `0x31` at tick 4 with the bramble at 130 px, 21 px from the hero); this comes from the export's data (an attack in the animation 0 of the sheets?) and was not compared with the binary. To check by the author if the brambles of the original prick.
7. (w10) is a test added to the plan's list (the witness of the phase C, red until 393 is wired).
