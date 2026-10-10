# ADR-0060: The break effect is created for every DestroyEntity caller, as the binary does

- **Status**: Accepted
- **Date**: 2026-10-10
- **Source**: this chantier: `docs/plan-e14-ia-native.md`, section 1.3 (E14.a1), question Q-A1-1 answered by the author on 2026-10-10
  (D-E14-9); read-only discovery of 2026-10-10 (`destroy-arcs` and `e14a1-check` reports, summarized in section 1.3b)

## Context

- `DestroyEntity` (`0x8003A59C` in `ALUN_CD.EXE`) takes an effect argument `fx`. When `fx` is -1 it reads the byte `+0x1E` of the
  entity's sprite header; when the effect is not 0 it creates it at the entity (`0x8003BE74(0, fx, 0, e; 1, 0, 0, 0)`).
- The binary has 38 call sites. Besides E0/E1, the pick phase destroys an entity on a slope with `fx = 6` (26 prefabs, 16 with a non-zero
  byte: jars, barrels, crates) and by the VRAM flags with `fx = -1` (107 prefabs, 18 with a non-zero byte). The DLL already passes 6 and -1
  at those two sites, but creates no effect.
- E14.a needs the break effect for the Muruta and the Beannoide (D-E14-2, ADR-0058). Creating it only for those two types would be a
  deviation from the binary; creating it for every caller can move existing arcs that destroy other sprites.

## Decision

- `DestroyEntity(e, fx)` creates the break effect for every caller, as the binary does: E0/E1 of any sprite whose header byte `+0x1E`
  is not 0, the slope destruction (`fx = 6`) and the VRAM-flag destruction (`fx = -1`). (Author, D-E14-9.)
- The work belongs to E14.a1b; it is removed from E20.e0b, and E20.g2 takes it from E14.a1b. (Main session, O-E14-5.)

## Consequences

- E14.a1a must export the header byte (`BreakEffect` in `Data/sprite-records.json`) before E14.a1b reads it.
- Arcs that destroy sprites with a non-zero byte may now show an effect slot (`AlundraDay3SceneArcTests`, `AlundraInoaDayOneArcTests`,
  `AlundraShipBlockArcTests`, `AlundraVisionArcTests`): each move is attributed to a legitimate break effect, read in the export, never
  hidden.
- The anchor of the effect is a snapshot taken at the destruction (status 4, position, `PosZ + 1`, depth key), because the DLL applies
  queued effect operations at the end of the frame (ADR-0046).
