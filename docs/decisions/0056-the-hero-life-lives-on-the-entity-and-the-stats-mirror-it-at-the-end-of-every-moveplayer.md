# ADR-0056: The hero's life lives on the entity and the stats mirror it at the end of every MovePlayer, as the binary does

- **Status**: Accepted
- **Date**: 2026-10-10
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 1.12 (E20.c2a), decisions D-E20-20 and D-E20-22 (ADR-0054); evidence `docs/plan-e20-annexe/e20-c2/` (`c2-binary.md` sections 0 and 6, `c2-dll.md` section 0, `check.md` points 1 to 3, 5, 52 to 54) and the oracle `docs/plan-e20-annexe/e20-c2/oracle/` (groups E and F)

## Context

- ADR-0048 (E19.g G3d) made the life vessel write the player's stats at once, as a provisional seam ("variant A"): the DLL had one source of the hero's life, `AlundraGameState.PlayerStats`, read by the HUD and the save. Its Consequences added that porting the end-of-`MovePlayer` sync "requires moving to the entity-borne life (variant B) at the same time".
- The binary's life is the hero entity's `+0x14` (Hp) and `+0x18` (HpMax). `MovePlayer` ends at `0x80032914` with `UpdateItemEffectState`, then `SetPlayerHpMax(hero +0x18)` and `SetPlayerHp(hero +0x14)`; every exit reaches it, the frozen branch (`+0x20 != 0`) and the locked branch (`& 0x34`) included. The stats are pulled into the entity only when the hero is created, right after `InitializeEntity` (`0x800319F8-0x80031A08`).
- The DLL's `InitializeBalance` sets the hero's Hp and HpMax to 10 from the balance record at every adoption of the hero (every map entry, load and Continue). A mirror at the end of `MovePlayer` without a pull would put the stats back to 10/10 at the first tick of every map.
- The vessel's binary code (`0x800333AC`, `0x800333D0-0x800333E4`) does `HpMax += 1` with no cap and `Hp = HpMax` on the entity; the cap of 50 is only in `SetPlayerHpMax`, applied by the mirror. Opcode `0xBC` (`0x80041C00`) adds its operand to the entity's HpMax capped at `0x32`; opcode `0xC3` (`0x80041D6C`) sets the entity's Hp to its HpMax, fills the magic, and calls `InitializeHpAndMp` (`0x8004B730`), which snaps the four displayed counters of the HUD.
- The unit tests use bare proxies (`new AlundraEntityScriptProxy`) as heroes; their Hp and HpMax stay at the C# default 0, and many set up the stats on the singleton state.

## Decision

- **The entity is the source of the hero's life.** `AlundraWorldProxy.AdoptPlayerPawn` copies `PlayerStats.HpMax` then `PlayerStats.Hp` into the hero right after `InitializeBalance` and marks it `LifePulledFromStats`. The end of `MovePlayer` (`EndOfMovePlayer`, reached by the `BlockedByEntity` branch, the locked branch and the normal path) runs `UpdateItemEffectState`, then `SetPlayerHpMax(hero.HpMax)` and `SetPlayerHp(hero.Hp)`.
- **The mirror is guarded for heroes that pulled.** A hero that did not pull (a bare proxy of a test montage) is not mirrored: its default life of 0 would otherwise be written over the stats the test set up. This is the one deviation of the epilogue; every hero of a world pulls.
- **Amends ADR-0048.** For a hero that pulled, the vessel (`IncreaseHpMaxAndFill`), `0xBC` and `0xC3` write the entity as the binary does (vessel: `HpMax + 1` uncapped and `Hp = HpMax`; `0xBC`: `HpMax + n` capped at 50, the life untouched; `0xC3`: `Hp = HpMax`, `SetPlayerMp(MpMax)`, then the four HUD counters set to the stats' maxima). The vessel at 50/50 gives an entity of 51/51 and stats of 50/50. Without a hero that pulled, the three write the stats directly, as ADR-0048's seam did (same setters, same caps). The vessel's order (life, sound, effects) and its pure effects function are unchanged.
- **Documented deviation 1 of ADR-0048 disappears** (the stats were updated at once; they now follow at the END of the next tick, as in the binary). Its deviation 2 (the draw order of the random numbers) is unchanged.

## Consequences

- The stats keep their values from map to map and across a load: an `ArcRun` crossing two maps with the stats at 4/12 keeps 4/12.
- A scripted heal or maximum raise is visible in the stats only after the next `MovePlayer`; the END runs during locked scripts, so the HUD does not wait for the lock to end.
- A hero at 0 life keeps control and does not regenerate (the regeneration gate `Hp != 0`); the death branch stays unported until E18 (D-E20-5, D-E20-20).
- The hurt itself (the damage entry `0x80031A68`) is E20.c2b; until then nothing lowers the hero's life.
- A test that builds a bare hero and wants the entity-borne behaviour must set `LifePulledFromStats` and the entity's Hp and HpMax.
