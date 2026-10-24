# E20.g4a - the rules of the pilot (written at T0 (c), before the arcs)

Plan: `docs/plan-e20-heros.md` section 1.29.1 (E20.g4a), T0 (c). Measured in `README.md` (this folder); the probe that applied them is `dll-probes/G4aT0ProbeTests.cs.txt` (`R_Route`).
The pilot of an arc (T1 (a1) to (a3)) is **a function of the state of the game read before each image**; it never writes the game (no `PlaceHero`, no raw position) after the start of the arc.
An **image** is one `ArcRun.OneFrame(k / 50f)` of **k logic ticks** (k = 1, 2, 3); a pad word is held for the whole image.

## Facts the rules rest on (README T0 (a))

- B1 of the map 392 executes `0x11 @104` at the logic tick 204, then polls `0x2F @107` at the ticks 220, 236, 252, ... (every 16 **ticks**, whatever k; the wait `0x37 [15]` counts ticks). The roll (`0x10 @118`) starts only when a direction is held at the tick of a poll with the hero on the ground.
- With k ticks per image the tick 204 opens an image for k = 1, 2, 3 (204 is a multiple of 6): the anchor image `A` is the image where `0x11 @104` runs (204, 102, 68). The poll m (m >= 1) is in the image **`A + floor(16 m / k)`** (k = 1: every 16 images; k = 2: every 8; k = 3: 5, 5, 6, 5, 5, 6, ... images). Predicted = actual in every probe run (`PollCheck`).

## Rules

1. **State read before each image**: `PosX`, `PosY`, `PosZ`, `IsOnGround`, the phase, and whether the previous image was a poll image (`PreviousWasPoll`).
2. **Poll images**: in every image `A + floor(16 m / k)` (m >= 1; the anchor image itself is the end of `RunUntil(0x11 @104)`, the pilot starts at the image after it) **all directions are let go and no button is pressed**. This overrides every phase. The pilot therefore never holds a direction at the tick of a poll: no roll (checked: `Rolls` = 0 in every run; an observed `0x10 @118` is an arc failure).
3. **Phases** (the transitions are decided from the state read before the image, in this order; the image then holds the direction of the phase it ends in):

   | phase | holds | ends when (state read before the image) |
   |---|---|---|
   | `down` | Down | `PosY >= 288.0` -> `left` |
   | `left` | Left | the jump rule below -> `after_jump` (arc a2); the arc a1 stops there |
   | `after_jump` | Left | `PosX <= 600.0` -> `south` |
   | `south` | Down | `PosY >= 343.5` -> `west` |
   | `west` | Left | `PosX <= 541.0` -> `down2` |
   | `down2` | Down | the departure through the portal 0 (`WarpDirector.Instance.HasPendingArrival`) |

4. **The jump rule** (phase `left` only, once): in the first image after a poll image (`PreviousWasPoll`), with the hero on the ground (`IsOnGround != 0`), `PosY` in **[295.0 ; 297.0]** and `PosX` in **[657.0 ; 687.5]** (the window measured at T0 (f)): Cross and Left are held together in that image (the Cross is pressed with `ArcRun.Press`, the direction held through `HoldDirections`); Left is held in the following images except in the poll images (rule 2). The jump lasts 18 ticks; the next poll falls in the air (`0x70 @114`: no roll).
   If `PosX < 657.0` were reached without a jump the arc fails (it cannot happen: the hero stops flush against the crates at `PosX = 657.0`, which is in the window).
5. **The refusal arc (a3)**: `down` until `PosY >= 304.0` (any value above 297.0 refuses: 300, 304, 320 measured), then `left`; at the first opportunity of rule 4 **without the `PosY` condition** the pilot presses Cross + Left, then holds Left (rule 2 applies) for 70 ticks; expected: the hero ends flush at `PosX = 657.0`, east of the column, with no slide.
6. **The calage** is not a rule of the pilot: it is what the game does to a hero who enters the door from the east with Left held (README T0 (e)); the pilot only reads that `PosY` ended in [295.0 ; 297.0] before jumping.

## Why the window of rule 4 is [657.0 ; 687.5] and not the binary's [657.0 ; 694.4]

The pilot lets go of Left during the poll image that falls in the flight (rule 2); the lost horizontal progress shrinks the upper bound (measured: 692.5, 689.5, 687.5 at k = 1, 2, 3). The same jump with Left held through the flight (no release, the control `F0`) reproduces the binary's bound (694.0 on the 0.5 grid; the binary: 694.406). The window of the pilot is the intersection of the three cadences and of Y = 295, 296, 297.
