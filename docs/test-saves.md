# Test saves

`tools/AlundraTestSaves` writes a prepared save into the game's save folder so that F9 (Debug DLL only) loads it and
the game resumes in the middle of the story, without playing the combats and the days before it (E19.d2a, D-E19-33,
ADR-0021).

## Use

Run it OUTSIDE the Claude app: neither from a terminal tab nor from the Run button of the app, nor by an agent working in it (writes
under `%LOCALAPPDATA%` made from the app are virtualized and invisible to the game). Use a terminal of your own, in Debug, from the
checkout whose DLL is deployed in `alundra-project/`:

```
dotnet run --project tools/AlundraTestSaves -c Debug -- <alundra-project\AlundraGame.json> <preset> [--dry-run]
```

- `--dry-run` prints the map, the tile, the flags posed and cleared, the map table entries and the target slot and
  file, and writes nothing.
- Without it the tool writes the JSON slot with the same metadata as F5/F6 and prints the file path
  (`%LOCALAPPDATA%\AlundraGame\SaveGames\<slot>.sav`).
- Exit code 0 on success, 2 on a usage error, 1 on any refusal (unknown preset, unreadable project, a `ProjectName` the engine
  would refuse as a folder name - `--dry-run` refuses it too -, item tables not loaded, save refused by the rules, write
  failure) and on any unexpected exception (a project file held open by another program, for instance).

Building the tool also builds `Alundra` and copies its DLL into `alundra-project/` (the DLL's own post-build step):
build in Debug, a Release DLL has no F9. Then start the game and press F9.

## What F9 loads, and when it refuses

- The slot is chosen by DATE, not by name: the readable slot with the greatest write time (`LastWriteTime`) of the folder; on a tie the
  greatest name. A newer slot (a recent F5/F6 save, or any slot rewritten since) wins over the test slot, and a time in the future
  always wins. Write the test slot last, or remove the other slots.
- "Readable" only means that the header opened: a most recent slot that is damaged blocks F9 (it does not fall back to the previous
  one). Every slot is read whole (1 MiB at most).
- F9 refuses, with a warning in the log and nothing changed, when: there is no hero in the world; a load is already pending; a dialogue
  box is open; the inventory or the sub-inventory is open (or its portrait not idle); a map transition is in progress; the master BGM
  fade is armed; any bit of the player control flags is posed (a locked scene, a box open); the warp is disabled on the map; the slot
  does not pass the validation rules; no readable slot has a known write time. Press it on a map where the hero walks free.
- The load applies on the arrival map; it does not restore the local state of a
  map (see below).

## Presets

Each preset is the new game (HP 10/10, MP 0/0, no gold, weapon 1, items 1, 17 and 25, game time 0, no retries) plus
what is listed. The slot is fixed, `test-<preset>`.

| Preset | Map, tile, z | Flags posed | Flags cleared | Map table |
|---|---|---|---|---|
| `day3-after-dream` | 179, (17, 7), 1 | G203, G1651, G1660 | none | `[162] = 176` |
| `day4-meeting` | 185, (5, 18), 1 | G204, G1651 to G1655 | G203 | `[162] = 183`, `[176] = 183` |

Expected: `day3-after-dream` starts the scene of map 179 at the first tick, then 176, 179, 176 and map 10, where the
scripted jump stalls until E19.d2c; `day4-meeting` lands on map 185, where the four villagers must be spoken to before
the meeting starts, and Septimus stalls until E19.d2b.

## What a save cannot carry

- the temporary flags (id 0x8000 and above): the tool refuses them;
- the persistent flags past the 64 saved words (id 2048 and above): the tool refuses them rather than lose them;
- the local state of a map (positions of the entities, open boxes, the state of programs): the map restarts from its
  own load programs;
- the hero's direction (always 0) and animation (`0x36`): the save has neither;
- the HUD phase (not saved: after F9 in a fresh process the HUD may stay closed).

The presets pose only what the targeted maps read: another map visited from a test save may behave as an inconsistent
game.
