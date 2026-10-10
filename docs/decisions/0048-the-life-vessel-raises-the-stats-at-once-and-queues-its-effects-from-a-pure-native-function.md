# ADR-0048: The life vessel raises the stats at once and queues its effects from a pure native function

- **Status**: Accepted; the life seam (variant A, "Life" decision and deviation 1) superseded by ADR-0056
- **Date**: 2026-10-07
- **Source**: `docs/plan-e19-opcodes.md` (E19.g G3d, section 1.2o.6, G3D-R1 to R4); annex `docs/plan-e19-g3d-annexe/` (`discovery/notes.md`, `discovery/verify/verify.md` which wins, `discovery/lib/g3d-golden.tsv`, `discovery/lib/g3d-modes13.tsv`)

## Context

Opcode `0x82` sub-case `0x53` (`0x8003FE7C` to `HandleMapTriggerCommand 0x80034108` to `IncreaseHpMaxAndCreateEffect 0x800333AC`) is the life vessel: maximum life + 1, life filled, sound `0x31`, then eight ring
effects and four sparks of the global effect 14 at the hero. It answers `Result` 1, has size 2 and never suspends. Until now the DLL answered 1 and wrote nothing (degraded, O-E19-39).

The binary's life lives in the hero entity (`+0x14`, `+0x18`); `g_playerStats` is a mirror copied at the end of every `MovePlayer` (clamps 0..50 and 0..HpMax). The DLL has one source, `AlundraGameState.PlayerStats`
(the HUD and the save read it); the entity-side life and the end-of-`MovePlayer` sync are deliberately not ported. The vessel has two other native callers (`0x80033E70`, `0x80034080`, from the overlay function
`0x8007C174`: chests whose `Contents` is `0x53` and the `I83` entities); that they go through that path is not confirmed (the `Contents` to `+0x68` path was not read).

## Decision

- **Life** (variant A, provisional seam): `AlundraPlayerManager.IncreaseHpMaxAndFill` calls `SetPlayerHpMax(state, HpMax + 1)` then `SetPlayerHp(state, old HpMax + 1)`, through the setters (the cap of 50 and the validity of the save hold).
  It equals the real chain (vessel, then the end of `MovePlayer`) on 1326 cases of the binary.
- **Order**: the life, the sound (`SoundPlayer?.PlaySfx(0x31)`) at once, then the effects queued on the effect stage (`EffectStage.Emit`) with the hero position read when the script runs. Without a stage or without a hero,
  the life and the sound are still done, the effects are skipped, with one `Degraded` trace per runner under the key `0x182` (apart from the key `0x82` of the other sub-cases, which stay degraded).
- **Effects**: `AlundraNativeEffects.CreateLifeVesselEffects(reservoir, heroX, heroY, heroZ)` is a pure function of the reservoir and a position (E14 reuses it for the chests and the pickups): eight ring `Create`s
  (global table, effect 14, animation 0, z + 0x100000) moved by the 8-entry ellipse tables, then four sparks with two `AlundraRandom` draws per created slot; a full reservoir creates fewer slots and draws fewer numbers;
  everything `unchecked`. The values are the rows `V`, `S`, `T`, `F` of `g3d-golden.tsv`, produced by the real code.
- **Modes 1 and 3** (the native creator interface of D-E19-96): see the second commit of the chantier: attached and detached creators on an anchor interface (position 16.16, depth key, status), `Move` as the binary
  (mode 1 re-derives the position from the anchor, then falls into the mode 3 code, so an anchor of status 0 still receives the forces on that update). No entity of the DLL implements the anchor yet.

## Consequences

- Deviation 1: the binary updates the stats at the next `MovePlayer`, the DLL at once: at most one logic tick (20 ms) at the eight script sites (maps 134 x4, 258, 298, 302, 398; every dialogue around the vessel is a
  message box, which does not stop `MovePlayer`). Porting the end-of-`MovePlayer` sync one day requires moving to the entity-borne life (variant B) at the same time, otherwise the sync would overwrite the stats with the
  entity's zero values.
- Deviation 2: the binary draws the random numbers at the script's position; the DLL draws them when the stage applies the queue, after the draws of the scripts of the same frame. The order differs as soon as another
  script draws in the same frame (none of the five maps has an `0x0C` or `0x8C` instruction; the random-walk modes were not swept).
- The chests and the pickups (two other native callers, not confirmed) come with E14; the pure function takes the position as a parameter for them.
- Nothing is visible before E19.g G3c (the draw); the sound, the life and the HUD catch-up are. No test save reaches the five maps: proof without a head only.
- A second vessel within the lifetime of sound 49 plays no sound (`max_voices` 1), as in the binary.
