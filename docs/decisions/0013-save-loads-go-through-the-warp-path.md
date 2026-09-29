# ADR-0013: A save is loaded through the warp departure path and applied at the arrival map's entry; the recipe keys exist only in Debug builds

- **Status**: Accepted
- **Date**: 2026-09-28
- **Source**: this chantier: `docs/plan-e16-etat-partie.md`, slice E16.d (choices K1 to K9 of the detailed plan, D-E16-33, security review SD1 to SD15), written and executed in AUTO mode at the author's request ("fait tout E16 de façon autonome", 2026-09-28)

## Context

- The original loads a save only at two moments: when a fresh process starts (`LOADER.EXE`) and after death ("Réessayer", E18). In both, the save is copied into the game state and the game then starts from `InitialMapId` at the saved tile.
- The DLL already changes maps through `AlundraWarpDirector`: a departure arms an arrival record, fades out, requests the world, and the arrival map's `InitializeWithWorld` adopts the hero at the recorded position. `InitializeWithWorld` runs `GameState.InstallForMapEntry()` first; nothing it installs afterwards reads the values a save carries, and the first readers (entity programs, map events) run at the arrival world's first tick.
- The engine's save service can only be reached from the DLL through `GameSettings.SaveGames`, whose constructors and result types are internal to the engine (D-E16-31). The launcher is started from the IDE, its local modification by the author ignores its arguments, and an environment variable never reached the game (D-E13-12).
- Every build of `Alundra.csproj`, Debug or Release, copies its DLL into `alundra-project/`, which the game loads.

## Decision

- **Loading goes through the warp path.** A load reads and validates the save (`TryValidate`, ADR-0012), checks that the departure can complete (warp enabled, no transition, a `GameManager`, the start map resolvable by the warp director's own table), keeps the object as a **pending load** in the session director `AlundraSaveGameDirector`, then arms a dedicated departure (`BeginDepartureForLoad`: reset animation `0x36`, direction 0, effect 0, no sound) to `InitialMapId` at the saved tile.
- **The pending load is applied at the arrival map's entry**, right after `GameState.InstallForMapEntry()`, before the arrival world's first tick: the pending load is cleared first, the arrival map must be `InitialMapId`, then the session is reset (control flags, pad, interaction latch, text category index and `\V` variables, `NewGameInventoryInitialized` set to true), the save is applied (`ApplyTo`), the HUD's displayed values and catch-up counters are reset to the loaded stats, and the inventories are reset to their session start. An aborted departure or an early return abandons the pending load.
- **D-E16-33** — The recipe keys (F5 binary save to `debug-binary`, F6 JSON save to `debug-json`, F9 load of the most recent readable slot) are compiled in both configurations but active by default only in Debug (`#if DEBUG`), with a test override; the active switch is logged once. The DLL deployed into the project is the one of the last build.
- **The engine service sits behind a DLL interface**, `IAlundraSaveSlots`, whose results are DLL types over the engine's public status enums; the production adapter catches every exception of the service and turns it into an error result.

## Consequences

- A refused, unreadable or hostile save, a failed service call or an impossible departure leaves the game unchanged; the refusal is a log line (the DLL has no on-screen message).
- The load reuses the tested warp arrival (fade, music, hero placement), and applies the save where the original applies it: before the game runs again, not while the old map is still ticking.
- "Most recent slot" limits, accepted for recipe keys: a future timestamp always wins; a newest slot whose envelope opens but whose data is broken blocks F9 without falling back; listing reads every slot. The player's own slot choice belongs to E16.e.
- A build in Release turns the recipe keys off in the deployed DLL until the next Debug build.
- The inventory can still open during a departure fade, as on ordinary warps (pre-existing); if it opens during a load's fade, the HUD can stay hidden on arrival until the inventory is opened again or a script asks for the HUD.
