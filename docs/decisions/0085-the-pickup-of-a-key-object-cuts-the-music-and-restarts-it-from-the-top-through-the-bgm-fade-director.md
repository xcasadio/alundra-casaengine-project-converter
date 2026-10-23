# ADR-0085: The pickup of a key object cuts the music and restarts it from the top through the BGM fade director

- **Status**: Accepted
- **Date**: 2026-10-23
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.26.1 (E20.e0c2, revision 0, D-E0C2-1 to D-E0C2-4, D-E20-65) and the author's answer of 2026-10-23 to Q-E0C-9 ("B"); reading of `ALUN_CD.EXE` (France), C255 key-object branch `0x8007C648-0x8007C694` and state 2 `0x8007C6F4` (`docs/plan-e20-annexe/e20-e0c/posed-rev3/report.md` section 1.2); `docs/plan-e20-annexe/e20-e0c/exec2/`

## Context

- At the pickup of a key object the binary calls `0x80049B7C(0)` (`LoadBgm`, the routine of the opcode `0xA6`), then asks for CD track 11 (the fanfare); at state 2, when `CdBusy()` is false, it calls `0x80049AF4` (`StopAllSound`, the routine of the opcode `0xA5`).
- ADR-0082 ported the pickup with a journaled stub for these two calls (D-E0C-7, default A): the music was not touched. The XA tracks are not played yet (E19.sm7, ADR-0076).
- The DLL already carries both routines in `IBgmFadeDirector` (`LoadBgm(0)` stops the voice and keeps the track loaded and rewound; `StopAllSound()` restarts a loaded, silent track from the top), reached through `IEntityWorldContext.BgmFadeDirector`, which the opcodes `0xA5` and `0xA6` already use.

## Decision

- `PickupAi.FanfareStart` calls `world.BgmFadeDirector?.LoadBgm(0)`, then keeps its journal entry "start". `PickupAi.FanfareEnd` calls `world.BgmFadeDirector?.StopAllSound()`, then keeps its journal entry "end". `FanfareIsBusy` stays false, so the music is silent for one tick and then restarts from the top (D-E0C2-1).
- Without a director (a test host) nothing happens and nothing throws (D-E0C2-2).
- The binary's order at the pickup is kept: `Block`, the music stop, the message (D-E0C2-3).
- CD track 11 stays a named stub, the seam E19.sm7 will plug. Ordinary objects, dropped objects and the chest are not touched.

## Consequences

- Taking a placed key object cuts the music and restarts it from the beginning, as in the binary, even before the fanfare is audible.
- Amends ADR-0082 on the fanfare stub only (the music part); the rest of ADR-0082 stands.
- Revert: `git revert` of the code commit makes the stubs mute again.
