# ADR-0055: Native enemy handlers are registered per sprite type, read per-tick values, and arrive with the Muruta and the Beannoide

- **Status**: Accepted; D-E14-4's place amended by ADR-0058 (D-E14-8)
- **Date**: 2026-10-09
- **Source**: this chantier: `docs/plan-conversion-totale.md`, section E14 (D-E14-1 to D-E14-7, the author's answers of 2026-10-09, "fait toutes les recommandations", to the questions raised by the E14.a discovery), `docs/plan-e14-annexe/e14a/check.md` section (d)

## Context

- The binary dispatches a non-bytecode program slot through the master table `0x80098F4C` by handler code (`0x800388F8`-`0x80038944`). In the DLL, `RunSpriteEvent` (`AlundraEventProgramRunner.cs:293-330`) runs the save book and the destroy handlers E0/E1; every other native code is a counted no-op.
- Several sprites share handler codes with the Muruta (C10, D9) and the Beannoide (A1, C1, D1): a C10/D18 sprite (maps 329, 153, 368), the small slime (C1/D23) and the gelatinous slime (A1, D1). Registering by (slot, code) alone would bring them half to life.
- On death, `DestroyEntity` (`0x8003A59C`) plays a break effect whose id is the sprite header byte `+0x1E`; the converter does not export it.
- The loot (rolled at spawn by `0x80032A40`, dropped at death by `0x80032B90`) belongs to E20.e0 (D-E20-17).
- The E20.c1a and E20.c1b oracle arenas use the real map-14 Beannoide with every handler hooked off.
- The DLL's movers read the animation and its direction at the end of the frame (D-E19-13), which differs from the binary only in frames of several ticks.

## Decision

- **D-E14-1.** Native handlers are registered per sprite type: a (slot, code) entry runs only for the listed sprite types; any other native stays a counted no-op.
- **D-E14-2.** The converter exports the break-effect byte (`+0x1E` of the sprite header) within E14.a, with a full export; the exact field is fixed by E14.a's plan and recorded in its own format ADR.
- **D-E14-3.** The loot stays with E20.e0; until E20.e0 lands, killed enemies drop nothing.
- **D-E14-4.** E14.a comes before the hurt hero (E20.c2), so the hall of map 115 becomes playable sooner; E20.c2 re-measures E14.a's arcs. The author's later order (D-E20-24: weapons, item use, collisions) places E14.a after those slices.
- **D-E14-5.** Tests keep the E20.c1a and E20.c1b oracle arenas through a switch that turns the native handlers off; in play, the Beannoide of map 14 come alive.
- **D-E14-6.** The new native handlers read the per-tick values (the box direction and the box animation), like the binary, not the end-of-frame values of D-E19-13.
- **D-E14-7.** E14.a's in-game recipe is the hall of map 115 and the grate of map 116; map 362 (level-8 Muruta) and the wave of map 44 come along without a recipe of their own.

## Consequences

- Sharing sprites (slimes, the C10/D18 sprite) stay inert until their own handlers are ported.
- E14.a includes a converter change and a full export, with the proof of the export required for any converter change.
- Dead enemies drop nothing until E20.e0.
- The c1a/c1b oracle comparisons stay valid; new arcs cover the live handlers.
