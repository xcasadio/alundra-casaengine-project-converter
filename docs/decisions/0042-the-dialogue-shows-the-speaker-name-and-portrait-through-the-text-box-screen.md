# ADR-0042: The dialogue shows the speaker's name and portrait through the text box screen

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: `docs/plan-e19-opcodes.md` (E19.f4c, E19.f4c2, D-E19-100, D-E19-101); annex `docs/plan-e19-f4c-annexe/`

## Context

E19.f4b ported the logic of the speaker's name box and of the dialogue portrait (binary ALUN_CD.EXE, France: name frame cells at (64, 140), name text palette 8 at y 148,
one modulated opaque quad for the portrait, drawn in the ordering table after the text box rows and before the name). The view was missing. The original's portrait colour
runs from 255 down to 128 while opening and from 127 back up to 255 while returning; MGUI could not exceed a tint of 1 until `Image.Brightness` (MGUI ADR-0021).

## Decision

- The existing text box screen (`UI/Screens/TextBoxScreen.xaml`) gains `PortraitImage`, `NameFrame` and `NameText` after `TextClip`, in the binary's order. The portrait sits at (8, 116) and
  its flight moves and scales it through the bound render transform (MGUI ADR-0020), its tint through the bound `Brightness` = Rgb / 128 (D-E19-100: the ramp is rendered).
  A 48 x 72 portrait keeps CanvasTop 116; the gap is in the translation.
- `AlundraTextBoxPresenter` writes the view model once per tick from the box, the name box and the portrait machine; the screen stays pushed while any of the three is drawn
  (so the second speaker of two in a row, and a portrait left at rest with no box, keep it), and `Frame`, `Clip` and `Cursor` fold when only the speaker is drawn.
- The name text and the portrait image are locked at the opening (`AlundraDialogueNameBox.Text`, `AlundraDialogueDirector.PortraitSource`); the view never reads the speaker's live fields.
- The flight is read at the resolution of the screen (centre of the screen pixels), not with the PS1's texel rule (D-E19-101): during the 0.3 s of the flight about a third of the texels
  differ from the console's. At rest the portrait is exact.
- No preload: the three largest portrait sheets decode in 9.6, 5.3 and 5.7 ms (under one tick).

## Consequences

- Tested on a real GPU against references composed from the binary's value model (classes exact and tint8, `docs/plan-e19-f4c-annexe/classes.tsv`); the GPU tests are skipped without a GPU.
- `InventoryPortraitViewModel.Brightness` is owned here; E19.f4c3 sets it for the inventories.
- The name's binary clip is not ported (it never cuts a name: 60 names x 30 positions).
