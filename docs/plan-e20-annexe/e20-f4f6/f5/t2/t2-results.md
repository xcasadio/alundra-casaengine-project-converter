# E20.f5, T2 (production tests): results (2026-10-23)

Start HEAD: `a96c1b20` (phase A). Branch `chantier/theme1-porter-plateforme`. `ProductionWiredWeaponTypes` is still `{7, 27, 28, 29}`.

New file `Alundra.Tests/Ai/Projectiles/ArrowProductionTests.cs` (mirror folder, ADR-0080), bench `ArrowRun(production: true)` (no birth hook, no test set of wired types, the arena switch on for the arrow type only):
- (p1) x2 (hunter item 5 / type 3 / key `arrow3_east_wall`; William item 6 / type 4 / key `arrow4_east_wall`): the shot is asked with the type; born iff the production set holds the type (derived); born at S = 10, of the type, daughter of the hero, lowest free slot (1), statics `flags=0x12074 atk=(131,1|2) box=24x16x11 hbz=16`, the life S..S+62 equals the key (bounce S+43, absent S+61).
- (p2) x2 (types 3, 4): full table (64 held): asked, nothing born; one free slot: one arrow at S = 10, daughter of the hero.
- (p3) and (p4) are out of T2 (plan, "Revision au HEAD": the negative witnesses are the phase-A rewrites; (p4) is never f5's).

Red on the HEAD (`ProductionWiredWeaponTypes` without 3 and 4): the 4 cases fail (P1 x2: `Assert.NotNull(run.Born)`; P2 x2: expected one arrow, got none). Other tests of the class filter `ArrowProductionTests` (the 4 of `ChargedArrowProductionTests`) stay green.
