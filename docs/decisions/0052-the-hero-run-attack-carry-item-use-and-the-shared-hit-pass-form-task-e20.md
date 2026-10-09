# ADR-0052: The hero's run, attack, carrying and item use, with the shared hit pass, form task E20 apart from E14

- **Status**: Accepted
- **Date**: 2026-10-09
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 0.1 (D-E20-1 to D-E20-10), decided with the author on 2026-10-09 after the read-only discovery in `docs/plan-e20-annexe/decouverte-2026-10-09/`

## Context

- No planned task owned the hero's run (Triangle) or attack (Square). The E19 plan handed "swimming (slope 4), transport, run, air attack, grab and hits" to E14 (`docs/plan-e19-opcodes.md`, scope of E19.d2c), while E14 in `docs/plan-conversion-totale.md` only describes the native AI of enemies and NPCs (the per-sprite C, D and E handlers).
- In the DLL, `TryUseItem`, `PlayerTryAction`, `PlayerTryAttack` and the Triangle run are no-ops (`Alundra/Scripts/AlundraPlayerManager.cs:418-424`). The hero's `Hp` is never set, and `HitCounter`, `TouchingEntity` and `DamagedTickCounter` are never written.
- In `ALUN_CD.EXE` one pass, `UpdateBalanceRecords` (`0x80039300`, sole caller `0x8003B3F0`), resolves every hit of the game: the hero against enemies, enemies against the hero, projectiles. Damage is computed on the target side (`ComputeNewHp` `0x8003A374`, 37 callers including the hero's damage entry `0x80031A68`). The pass belongs to no enemy.
- In the binary, `MovePlayer` (`0x80031B50`) runs once per logic tick, first in the entity events pass (`0x800386EC`). `PlayerTryAttack` tests the Square release edge before the press edge (`0x8002EB38`). The DLL runs `MovePlayer` once per rendered frame, and only `AlundraTickPad` carries a released edge.
- The first mandatory fight of the story is the hall of map 115: its event program activates entities 24 to 27 (four Muruta) and waits for each to be destroyed (`0x2C @414-433` in the exported events) before reopening the portals. Map 362 needs a bomb hit on a lid (`0x87 [128,3] @728`), and the crate barricade of map 392 is passed in the original by lifting or climbing.

## Decision

- **Task split (D-E20-1).** The hero's run, attack, carrying (lift, put down, throw) and item use (Circle) form task E20. E14 stays the native AI: the C, D and E handlers of each sprite type. This replaces the E19 plan's hand-off to E14 for the run, the attack, the grab and the hits.
- **Per-tick hero pass (D-E20-2).** `MovePlayer` runs once per logic tick on the tick pad, like the binary. The validated jump and the hero reference traces are re-measured.
- **Shared hit pass in E20 (D-E20-3).** E20 ports the balance records, the per-frame attack boxes, `UpdateBalanceRecords`, the damage computation, the hero's reaction when hit, and opcodes `0x86`, `0x87` and `0x95`. E14 keeps each enemy's own handlers.
- **First E14 slice (D-E20-4).** The Muruta and the Beannoïde form E14.a, planned right after the hit pass (E20.c1). No test save is placed after the hall of map 115.
- **No death before E18 (D-E20-5).** The hero loses HP but does not die; the `Hp == 0` branch of `MovePlayer` (`0x800327A4`) stays unported until E18. This is a temporary, documented deviation.
- **Attack boxes (D-E20-6).** First prove whether the exported `collision_keyframes` give back the binary's 6-byte frame records exactly. If they do, the DLL reads them; otherwise the converter exports the exact records, under a new ADR.
- **Basic sword first (D-E20-7).** The whole weapon table is ported as data; the charge, the flail, the rods and the bows come in a later slice.
- **Analog stick snap (D-E20-8).** A port addition: the left stick gets an angle snap for the run. The keyboard and the D-pad keep the original rule (the run starts on a pure cardinal direction only). The exact rule is proposed by the first slice and approved by the author.
- **Two original behaviours kept (D-E20-9).** A fully charged attack released in the air is lost, and a Square press and release within one tick does not swing. Neither is a proven defect.
- **Carrying and item use in E20 (D-E20-10)**, after the combat slices.

## Consequences

- The E19 plan's hand-off of the run, the attack, the grab and the hits to E14 no longer holds; E14's first slice is E14.a, after E20.c1. Swimming and the mine cart keep no owner until the author decides (O-E20-1).
- Moving `MovePlayer` to the logic tick changes the jump's frame-level timing workarounds and the hero reference traces; E20.a must show each changed value against the binary, and re-check the first frames of the real game.
- Once the hit pass lands, enemies whose animations carry attack boxes may hurt the hero on maps the author already crosses (14, 15); with D-E20-5 the hero cannot die, so the HUD may show zero hearts until E18.
- A converter change for the attack boxes, if the proof fails, needs a full export and its own ADR.
- The stick snap is a documented deviation from the original's input rule, limited to the analog stick.
