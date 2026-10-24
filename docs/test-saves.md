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

A preset may also set the equipped weapon SLOT (`Weapon`: -1 or 1 to 6), the Circle item (`Item`), life (`Hp`, `HpMax`), magic
(`Mp`, `MpMax`) and gold (`Money`). They are optional (a missing one keeps the new game's value), applied after the new-game kit and
the item counters through the DLL's own setters, and refused outside the save's domains (HP max 0 to 50, HP 0 to its max, MP max 0 to
4, MP 0 to its max, gold 0 to 9999, weapon -1 or 1 to 6, item 0 to 98): they are never clamped silently.

### Story presets

| Preset | Map, tile, z | Flags posed | Flags cleared | Map table |
|---|---|---|---|---|
| `day3-after-dream` | 179, (17, 7), 1 | G203, G1651, G1660 | none | `[162] = 176` |
| `day4-meeting` | 185, (5, 18), 1 | G204, G1651 to G1655 | G203 | `[162] = 183`, `[176] = 183` |

Expected: `day3-after-dream` starts the scene of map 179 at the first tick, then 176, 179, 176 and map 10, where the
scripted jump stalls until E19.d2c; `day4-meeting` lands on map 185, where the four villagers must be spoken to before
the meeting starts, and Septimus stalls until E19.d2b.

### Weapon presets (E20.f0)

For the weapon and item recipes. All six carry the loadout of `docs/plan-e20-annexe/e20-armes/test-means.md` section 7: HP 4/10 (so
that the herbs heal), MP 4/4, the bomb (35), nine herbs (36), the two elixirs (37, 38), the fortifier (41) and the four scrolls (43, 45,
47, 49), on top of the kit's items 1, 17 and 25. No flag is posed and the map table stays the identity. The arrival tile is
accepted by the save rules (the map exists and the tile is inside it and inside the camera limits); that it is a free cell is the
author's to see in the game.

The weapon items by slot: 1 sword (1, 2, 3, 4, 8), 2 bows (5, 6), 3 flails (9, 10), 4 ice rod (14), 5 fire rod (16), 6 magic staff
(7). In a slot the highest rank owned is the one equipped, so the kit's item 1 never hides a better sword. The books (44, 46, 48,
50) win over the scroll of their slot.

| Preset | Map, tile, z | Weapons owned (item numbers) | Equipped slot, then item |
|---|---|---|---|
| `arsenal-a-m14` | 14, (8, 6), 0 | 1 (kit), 5, 9, 14, 16, 7 | 1, item 1 (basic sword) |
| `arsenal-b-m14` | 14, (8, 6), 0 | 1 (kit), 4, 6, 10, 14, 16, 7 and the books 44, 46, 48, 50 | 1, item 4 (Holy Sword) |
| `steel-m14` | 14, (8, 6), 0 | 1 (kit), 2 | 1, item 2 (Steel Edge) |
| `demon-m14` | 14, (8, 6), 0 | 1 (kit), 3 | 1, item 3 (Demon Buster) |
| `fire-m303` | 303, (38, 11), 1 | as `arsenal-a-m14` | 5, item 16 (fire rod) |
| `arrow-m16` | 16, (34, 31), 3 | as `arsenal-a-m14` | 2, item 5 (hunter's bow) |

From `arsenal-a-m14` the inventory (Start) reaches the slots 1 to 6 as items 1, 5, 9, 14, 16, 7; from `arsenal-b-m14` as items
4, 6, 10, 14, 16, 7. The tool is run, as above, outside the Claude app, in Debug; the author generates the files.

### Swim preset (E20.i)

| Preset | Map, tile, z | Owned | Equipped |
|---|---|---|---|
| `swim-m14` | 14, (40, 48), 1 | the kit's items 1, 17, 25 and the swim boots (item 0x1B, level 2) | the basic sword |

The arrival tile is on the bank (height 1) of the pond of map 14: 81 deep cells at the tiles x 40 to 46, y 43 to 59 (height 0), a bank of 16
pixels all around. Walk south off the bank into the water: the hero falls, floats, then swims (0x1D, 0x0F); the Triangle, the Cross
and the Square start a burst of 42 ticks (0x28), the Circle does not. Known gaps of the port: no splash, no wake, no ripple of the burst
(E20.e); a hero without the swim boots swims too, where the original stops him at the edge of the deep water (O-E19-22); the pond has
no exit at the same level but its south edge, a bank is a wall for the swimmer. Do not go near the cascade of map 416 (currents,
O-E20-16).

### Pickup presets (E20.e0c)

For the recipes of the objects (docs/plan-e20-heros.md section 1.26). Both carry the new game's kit (the basic sword) with HP 4/10 and MP 2/4, so that a heart, a herb and a crystal show. No flag is posed.

| Preset | Map, tile, z | What is near |
|---|---|---|
| `pickup-m11` | 11 (Debug zone), (27, 18), 10 | to the north on the same column the herb (27, 16), the Small Potion (27, 15), the Large Potion (27, 14); the Magic Extract (29, 15), the Brew (29, 14), a key (24, 20); the 22 key objects are in the rows 22 to 30 |
| `loot-m14` | 14 (Beannoides arena), (19, 34), 3 | a Beannoid 72 pixels to the east at the same height (record 1, tile (22, 34, 3)); kill several, what they let fall depends on the generator |

Walk onto an ordinary object: its name and "gained!" in a box, the world stands still under it, the object is gone when the box closes.
Take a key object: the same message, the hero and the other entities are held while it is up, the fanfare of the CD is not played yet (E19.sm7) and the music goes on.
Kill a Beannoid: what it let fall rises, bounces, is taken on contact (money, hearts, magic, herb) with its sound, or goes out after 600 ticks.

### Cut-of-the-grass preset (E20.e)

For the cut of the grass (docs/plan-e20-heros.md section 1.27). The loadout of the weapon presets with the Steel Edge in the slot 1 (life 4/10, magic 4/4, no flag).

| Preset | Map, tile, z | Owned | Equipped |
|---|---|---|---|
| `herbe-m14` | 14, (31, 43), 1 | the kit, the weapon loadout, item 2 (Steel Edge) | 1, item 2 |

The arrival tile is on the flat ground (height 1) east of the 16 bushes of the cells 26 to 29, rows 40 to 44. A save does not carry the direction (always south): walk west, then tap the Square: the sword cuts the bushes under its
box (the bush goes, the global effect 2 flashes at the middle of the cell, the sound 31; one time in eight a coin or a heart appears, with the sound 84 first). Hold the Square and let go: the wave crosses the bushes of the same rows and cuts them
too. A cut bush grows back when the map is entered again. The steps, the dust and the landing are E20.e2.

### Lift preset (E19.d2c2 D5b)

For the recipe of the lift of the map 22 (docs/plan-e19-opcodes.md section 1.2h.3.2b, D-D5B-10). The new game's kit, no flag.

| Preset | Map, tile, z | Owned | Equipped |
|---|---|---|---|
| `elevator-m22` | 22, (46, 8), 2 | the kit's items 1, 17, 25 | the basic sword |

The arrival tile is south of the right-hand lever. A start already on the platform is impossible: the state of the lever (the temporary flags T1 and T3) is not saved. Strike the lever, go down into the hollow, jump onto the platform and wait:
Alundra rises with the platform, with no slide and no fall (before D5b he went through it and ended at the bottom of the hollow). On the way up, a barrel or a crate put on the platform rises with it too.

### Barricade preset (E20.g4a)

For the barricade of the ship Klark (docs/plan-e20-heros.md section 1.29.1): the way out of the corridor of the portal is shut by crates, and the hero passes them by the jump. The new game's kit, the flag G1641 posed (the start of the arc A5).

| Preset | Map, tile, z | Owned | Equipped |
|---|---|---|---|
| `barricade-m392` | 392 (Ship Klark, night, inner), (29, 13), 4 | the kit's items 1, 17, 25 | the basic sword |

The map program walks the hero 48 pixels east and then gives him the hand. Hold Down, then Left along the north wall of the door (the game lines the hero up on y = 295 plus a multiple of half a pixel), and when he is flush against the crates
(x = 657, y between 295 and 297) press Cross and Left together: he lands on the low crate, walks off its west edge, goes round the crate in his way by the south and reaches the portal. Entering the door further south (y above 297) refuses the jump.
Do not hold a direction when the ship polls the pad (every 16 ticks): the hero rolls in a random direction.

### Lifting preset (E20.g1b)

For the lifting of jars, crates and stones (docs/plan-e20-heros.md section 1.24.4, D-E20-71): the map 72 (Coastal Cave) has the three things of the recipe within a few steps. The new game's kit, no flag.

| Preset | Map, tile, z | Owned | Equipped |
|---|---|---|---|
| `porter-m72` | 72 (Coastal Cave), (10, 12), 3 | the kit's items 1, 17, 25 | the basic sword |

The arrival tile is on the west bank (48 px high). A save does not carry the direction (always south): walk west into the jar 48 px away (the "Cruche generique" of the record 37) and press the Square: Alundra lifts it over her head (36 images), then carries it.
Carry it west and jump (Cross + Left) onto the barrel behind it, a top of 16 px (the barrel itself is not liftable: the Square against it swings the sword). Throw the jar with the Square: it flies and breaks.
The platform "Plateforme flottante (grande)" shuttles 96 px east and west by itself over the lagoon, at 64 px (its top is at 72); the 80 px peninsula that touches its west end is not reachable on foot from the bank (it is 32 px up), so riding it with a jar
is shown by the arc of the tests, not by this save.

## What a save cannot carry

- the temporary flags (id 0x8000 and above): the tool refuses them;
- the persistent flags past the 64 saved words (id 2048 and above): the tool refuses them rather than lose them;
- the local state of a map (positions of the entities, open boxes, the state of programs): the map restarts from its
  own load programs;
- the hero's direction (always 0) and animation (`0x36`): the save has neither;
- the HUD phase (not saved: after F9 the jauge is armed by the map entry like the original's, so it slides in when the
  saved flags carry the latch G1662, and stays hidden otherwise).

The presets pose only what the targeted maps read: another map visited from a test save may behave as an inconsistent
game.
