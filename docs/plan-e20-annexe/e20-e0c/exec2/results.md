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

## T2: ADR, mutants, suites

ADR: `docs/decisions/0085-the-pickup-of-a-key-object-cuts-the-music-and-restarts-it-from-the-top-through-the-bgm-fade-director.md` (number recounted on every local branch: the last taken was 0084; no ADR-0085 on any branch). `docs/decisions/README.md` indexed. ADR-0082 status line: "Accepted; amended by ADR-0085 (the music of the key objects)".

Mutants of `Alundra/Ai/Objects/PickupAi.cs`, applied by script (`mutate.py` in the scratchpad), run on `PickupAiFanfareMusicTests` (6 tests, Release, neutralised copy), reverted after each (`git diff -- Alundra` empty). All killed:

| Mutant | Failed tests (of 6) |
|---|---|
| 1 `LoadBgm(0)` removed | M1 key object, M2 key object |
| 2 `StopAllSound()` removed | M1 key object, M2 key object |
| 3 `LoadBgm(1)` instead of `LoadBgm(0)` | M1 key object, M2 key object |
| 4 `StopAllSound()` at the pickup tick instead of state 2 | M1 key object, M2 key object |
| 5 ordinary object also stops the music | M1 ordinary/dropped (both cases), M2 ordinary/dropped |
| 6 stop after the opening of the message | M1 key object |

Suites (foreground, `--blame-hang-timeout 60s`):
- `Alundra.Tests` Release with the neutralised copy: 5434 passed, 0 failed (9 min 5 s).
- `Alundra.Tests` Debug, last, without the neutralised copy: 5434 passed, 0 failed (10 min 2 s). `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`: identical.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding(s), 1 warning (`Alundra.Tests/UI/Shared/TextBoxGpu.cs`, not ours).
- Engine, converter, analyser pointer, data and project: `git diff 6fa6bf81 HEAD` empty on `CasaEngineMonogame`, `alundra-casaengine-project-converter`, `alundra-datas-analyser`, `data-extracted`, `alundra-project`.
- The hero traces `docs/hero-trace-389-*.txt` were rewritten (line endings only, `diff --ignore-cr-at-eol` empty) and restored by `git checkout`; not indexed.

Existing tests moved: none; predicted (T0): none. Prediction holds.
