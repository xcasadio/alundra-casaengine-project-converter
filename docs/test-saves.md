# Test saves

`tools/AlundraTestSaves` writes a prepared save into the game's save folder so that F9 (Debug DLL only) loads it and
the game resumes in the middle of the story, without playing the combats and the days before it (E19.d2a, D-E19-33,
ADR-0021).

## Use

Run it OUTSIDE the Claude app (writes under `%LOCALAPPDATA%` made from the app are virtualized and invisible to the
game), in Debug, from the checkout whose DLL is deployed in `alundra-project/`:

```
dotnet run --project tools/AlundraTestSaves -c Debug -- <alundra-project\AlundraGame.json> <preset> [--dry-run]
```

- `--dry-run` prints the map, the tile, the flags posed and cleared, the map table entries and the target slot and
  file, and writes nothing.
- Without it the tool writes the JSON slot with the same metadata as F5/F6 and prints the file path
  (`%LOCALAPPDATA%\AlundraGame\SaveGames\<slot>.sav`).
- Exit code 0 on success, 2 on a usage error, 1 on any refusal (unknown preset, unreadable project, item tables not
  loaded, save refused by the rules, write failure).

Building the tool also builds `Alundra` and copies its DLL into `alundra-project/` (the DLL's own post-build step):
build in Debug, a Release DLL has no F9. Then start the game and press F9: it loads the MOST RECENT readable slot of
the folder, so a newer slot (a recent F5/F6 save) wins over the test slot.

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
