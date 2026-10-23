# E20.e0c2 (fanfare B): results (2026-10-23)

## T0

See `README.md`: no existing test moves (161 tests of the `PickupAi*`, `BgmFadeDirector*`, `MusicPlayer*`, `LootDrop*` and export-guard classes, measured by a temporary wiring, undone); the montage of (m2) is the fake audio backend of `MusicPlayerTests.T1bis_...`.

## T1: red before the code, green after

New file: `Alundra.Tests/Ai/Objects/PickupAiFanfareMusicTests.cs` (mirror of `Alundra/Ai/Objects/PickupAi.cs`, ADR-0080). New support: `Alundra.Tests/Support/RecordingBgmFadeDirector.cs` (a recording `IBgmFadeDirector` that also snapshots the `Block` count and the opened messages of the host at each call) and the property `PickupHost.BgmFadeDirector` (the seam `IEntityWorldContext.BgmFadeDirector`).

Run before the code (Release, neutralised copy, filter `PickupAiFanfareMusicTests`): 6 tests, **2 red**, 4 green.

Red:
- `PickupAiFanfareMusicTests.M1_AKeyObject_CallsLoadBgm0_OnceAtThePickupAfterBlockAndBeforeTheMessage_ThenStopAllSoundOnceAtTheState2_NothingBetween_AndTheJournalIsUnchanged` (no call recorded: `[]` instead of `["LoadBgm(0)"]`).
- `PickupAiFanfareMusicTests.M2_AKeyObject_StopsTheLiveVoiceAtThePickup_AndAFreshVoiceStartsFromTheTopAtTheNextTick` (the voice still alive after the pickup tick).

Green before the code, by nature (they guard what must not change):
- `M1_AnOrdinaryObject_AndADroppedObject_CallNeitherLoadBgmNorStopAllSound(dropped: false)` and `(dropped: true)`.
- `M2_AnOrdinaryObject_AndADroppedObject_LeaveTheVoicePlayingWithoutARestart`.
- `M3_AHostWithoutADirector_TakesAKeyObjectAsBefore_WithoutException`: (m3) cannot be red before the code, the plan's own wording is "as before, without exception"; it is the regression guard of D-E0C2-2 (a null director) and is killed by mutants of T2 that dereference the director.

Code (D-E0C2-1 to -3), `Alundra/Ai/Objects/PickupAi.cs` only: `FanfareStart(world)` calls `world.BgmFadeDirector?.LoadBgm(0)` then journals "start"; `FanfareEnd(world)` calls `world.BgmFadeDirector?.StopAllSound()` then journals "end"; `FanfareIsBusy` stays false; the order at the pickup is unchanged (Block, music, message).

Run after the code: the 6 tests green; the broader filter of T0 (`PickupAi*`, `BgmFadeDirector*`, `MusicPlayer*`, `LootDrop*`, `PickupObjects*`): 167 tests (161 + 6), 0 failed. `python -B tools/dll-structure/check_names.py --rules`: 0 finding(s) (1 warning, in `Alundra.Tests/UI/Shared/TextBoxGpu.cs`, not ours).

Existing tests that moved: none (the predicted list is empty).
