# ADR-0096: The hero is hurt by an entry, a hurt block and the state 0x31, and every active entity blinks from its invulnerability counter

- **Status**: Accepted
- **Date**: 2026-10-25
- **Source**: `docs/plan-e20-heros.md` section 1.12.1 (E20.c2b, decisions D-C2B-1 to D-C2B-14, answers Q-C2B-1 to Q-C2B-6 through D-E20-83); `docs/plan-e20-annexe/e20-c2b-disc/` (the discovery); `docs/plan-e20-annexe/e20-c2b/results.md` (T0 to T4) and `t5.md` (the mutants)

## Context

Before this slice the hero was already a target of the hit pass (sparks, `TouchingEntity`) but lost nothing and never recoiled: the DLL had no entry of the damage, no hurt block, no `case 0x31` on land and no invulnerability blink. In the binary (`ALUN_CD.EXE`, France) the entry is `0x80031A68` (58 instructions, two callers, both in `MovePlayer`); its toucher branch runs `ComputeNewHp` and bears away from the toucher, its hazard-cell branch costs `{0, 1, 2, 3}[(+0x180 & 0x180) >> 7]` life (armour ignored, none with boots of level 3). The hurt block `0x80031C58-0x80031C9C` zeroes the charge and the pending shot, lets go an object in the hands (`UpdatePlayerCarriedEntity(2)`), runs the item driver, then writes the state (`0x3A` if the slope read at the head of the tick is 4, else `0x31`), the facing and `+0x1CC = 0x78`, and ends the tick. The draw list takes an entity of status 2 or 3 only if `(+0x1CC & 3) != 3`, for every entity with a `+0x1CC`.

## Decision

- **D-C2B-1** The slice joins theme 2 after E20.e0d and E20.b2 and before E20.e0k5, f6b, h0a, g1c, h1, h2 and h3a; D-H-7 of h3a lapses (D-C2B-11).
- **D-C2B-2** The hazard cells are in the same slice, as one function, tested on real maps.
- **D-C2B-3** The entry is a pure function, `Alundra.Combat.HeroInjury.Enter` (file `Alundra/Combat/HeroInjury.cs`; tests in the mirror folder, ADR-0080), taking the hero, the sheet, the equipment records, the frozen animation and the boots level. It returns the facing direction or `-1`, writes the life, and writes nothing else. A hero with neither toucher nor cost bits gets `-1`; a bare entity as toucher is read as standing on the hero (24).
- **D-C2B-4** The hurt block is in `PlayerManager.MovePlayer`, between the creation of the free-mode effects and the item driver, in the binary's order; the slope that chooses `0x3A` or `0x31` is retained at the head of the tick, before the portal, as the binary reads it; the tick of the hurt ends by `EndOfMovePlayer` so that the stats mirror the life at once; the item driver stays the named stub of h0a.
- **D-C2B-5** The locked path is ported: only the whole control word `0x20` reaches the entry, and then only `+0x1CC = 0x78` is written (no state, no facing, no recoil); any other lock (`0x24`, `0x30`, `0x10`, `0x04`, `0xA0`) loses the hit. Reached only by tests (no production writer of the forced sequence); the item cancel `0x800350C0` stays h0a's.
- **D-C2B-6** `case 0x31` on land calls `TryUseItem` and ends the tick; the four ticks and the chain to `0x1C` come from the animation, the recoil from the physics along `+0x8C`.
- **D-C2B-7** The blink is generic: the lists pass records on every proxy whether the draw list of the tick leaves it out (active status and `(DamagedTickCounter & 3) == 3`), and the frame publication applies the mark of the LAST tick of the frame. The blink shows again only what it hid itself and only while the entity is active: an object a chest holds Loaded and an entity flagged for destruction are never shown by it. The shadow does not exist in the DLL yet (ADR-0078); its rule (same test) is left to the engine slice.
- **D-C2B-8** At 0 life there is no death (D-E20-5, D-E20-20); the `PV == 0` test of `0x80031CA8` stays a named, unported site (E18).
- **D-C2B-9** A world without a sheet (a montage without tables) leaves the life unchanged, as `Damage.ResolveBalanceTarget` already does; the binary would take 1, which production never shows (the sheet is set at the adoption).
- **D-C2B-10** The test montages that put the hero in an enemy's body during their settling frames wait for the hero to be placed before they enable the targets' attacks (`HitArena`); each moved test is rewritten by derivation from the oracle, never by copying the output.
- **D-C2B-11** D-H-7 of h3a lapses: a `0x80` response of the sheet removes only the life loss, the hero stays hurt; h3a is to be revised with a hurtable hero.
- **D-C2B-12** Two recipe presets, `blessure-m15` (map 15, tile (7, 33, 7), the orcs) and `blessure-m14` (map 14, tile (5, 29, 5), the Beannoids): life 10/10, the four armours, the bracelet, the charm and the boots of level 3 in the bag.
- **D-C2B-13** Kept as in the original: a hit received during a dialogue or a scene is lost (sparks only); hit in the air, one frame of walking pose before the fall; at 0 hearts the game goes on; the carried object falls and breaks.
- **D-C2B-14** The exit square (O-H-2) stays out of the slice.

Deviations found while doing the tasks T0 to T4 (kept as decisions of the port):
- The cell branch is wired with the toucher branch in T2, not T4: it is one function with two branches, so the cells hurt from T2 on and the "T4 removes alone" rollback no longer holds literally.
- The bench of the thrown objects (`C4Run`) stands the hero on cost-2 cells of real maps; it gives him the boots of level 3 so that the oracle values of C4 stay untouched (21 cases moved by the cells, none by the blink).
- The pending-shot pointer reset of the hurt block is not observable in the oracle (the pointer is cleared at once at the harness's poke); it is asserted by a poked pointer.
- After a hurt the carried hero returns to `0x0C` while the program of the object does not run: what clears `+0x2C` and breaks the object belongs to the object's own program (E14), not to the entry.
- With the four armours in the bag the game wears the best one, so the presets cannot walk the "4, 3, 2, 1 hearts by armour" of the recipe; to settle with the author (one bag per armour under test, or one preset per armour).
- The blink runs for every entity with a proxy, not only those that took part in the frame (a dropped object or a target of `0x86` may not have).

## Consequences

- The hero loses 4, 3, 2, 1, 1 or 3 life to the real orc (kit, leather, ancient, silver, no armour, bracelet), recoils by exactly 2 168 832 raw (33.09375 px), is invulnerable 120 ticks and blinks 30 ticks of 120, tick by tick against the oracle at 1, 2 and 3 ticks per frame. Dropped objects and the targets of `0x86` blink their last 120 ticks.
- A hazard cell costs 1, 2 or 3 every 120 ticks, the armour not counting, boots of level 3 sparing the hero; boots of level 2 do not.
- 28 real mutants were applied by script and all killed, each by the test it names; one equivalent (the slope read after the portal, nothing writes the slope between the two reads) is written with its reason (`docs/plan-e20-annexe/e20-c2b/t5.md`, `mutants/`).
- The suites of the touched classes stay green in Release and Debug (6 665 tests at the end of T4).
- Not done here: the death and the reload at 0 life (E18), the item driver and the item cancel (h0a), the shadow (engine, ADR-0078), the bomb and explosion touchers (E14.b: they will hurt through the same entry), the exit square.
