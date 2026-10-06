# E19.f4b design notes, surface "design": the T1 contract (A) and the 48 x 72 flight (B)

Read-only on the repository (HEAD 2909ae3). Everything here is under `scratchpad/f4b-design/`. Scripts in this folder: `common.py` (rig-free runner), `f4_model_h.py` (the
extension, annex model untouched), `check_regression.py`, `flight72.py`, `scenario_s9.py`, `gen_sequences.py`, `mutate_digests.py`. Outputs: `flight72.out.txt`,
`values-s8-72.json`, `values-s9-moving.json`, `sequences-raw-digests.json`.

## FACTS (file:line, or script result)

F1. Frame numbering of values.json (annex `model/scenarios.py`, `run()`): per frame `f`: (1) `box.render` = the text box pass, (2) `box.nb.pass_()` the name draw, (3)
    `box.pt.pass_()` the portrait draw, (4) THEN the script phase: the opening attempt of the current dialogue when `f >= n0` (`n0 = 3`), retried every frame until it returns
    true, the next dialogue is tried the frame after the previous opened. A row records: `frame`, `rel = frame - 3`, `phase` and `box_y` read AFTER the script phase
    (`box.phase()`, `box.y`), `name` and `portrait` = the DRAWS of the pass of step (2)/(3) (before the script phase of that frame, so null on the opcode row N+0),
    `events` = `['opcode 0x.. -> opened|retry (0)']` first, then the box notes of the pass (`typing-done`, `close-trigger`, `DialogClosed`).
    Pad: `pad_every_frame` = held AND just-pressed True on every frame (model.py `pad_every_frame`).
F2. values.json keys: `name` = null | `{frame:[x,140], text:[x,148], clip:[cx,148,cw,34]}`; `portrait` = null | `{x,y,w,h,rgb,phase}` with phase in `in`, `rest`, `out`, `gone`
    (`gone` = the degenerate pass: x=8,y=116,w=0,h=0,rgb=0). Event strings present in the file: `opcode 0xd|0x5c|0xc4 -> opened`, `opcode 0xd -> retry (0)`, `typing-done`,
    `close-trigger`, `DialogClosed`. Box phases present: closed, slide-in, typing, typed, slide-out.
F3. Clip: from `f4_model.py` (`NameBox.pass_`): `cx = min(x + 16, 320)`, `cw = 320 - cx` (the 258 bound never binds, because x >= 64 gives cx >= 80 and 320 - cx <= 240).
    Proved here by brute force (60 real names x 30 slide positions = 1920 checks): the part of the text that is on screen is never outside the clip, 0 cut, minimum
    (text left edge - clip left edge) = 12 px. So the clip is moot for the real names, as dll-notes 3.1 says.
F4. Text box columns: the model keeps the PREVIOUS box's cfg y on the opening row of a second box (S5 frame 43, `box_y` 168), while the DLL's `AlundraDialogueBox.Open()` writes
    `Y = ClosedY` (240) at once (`AlundraDialogueBox.cs` Open, `Y = ClosedY`). The text box position is therefore not comparable on that row (f2a already compares Y only "where comparable",
    `AlundraDialogueBoxOracleComparisonTests.cs`: `FrameEnd.Y`).
F5. Order of the DLL: the engine updates the entity scripts BEFORE `AlundraWorldProxy.Update` (D-E19-64, plan 4603-4620; `DialogueBoxMontage.RunFrame`: `EntityScript`, then `Proxy.Update`),
    and `AlundraDialogueDirector.Pass` runs at the head of the tick loop of `Update` (`AlundraWorldProxy.cs:2186`), so for an entity script the first pass of the box (and, with f4b, of the name and the
    portrait) is in the SAME frame as the opcode: "N", one tick early, for all three together. The intro harness callback order is `RunFrame` (scripts) then `Pass` then the test callback
    (plan 4617-4622): `Pass; script` per iteration = the binary order, with one extra leading pass.
F6. Annex regression (`check_regression.py`): the rig-free runner reproduces annex `values.json` for S1..S7 on every column (phase, box_y, name incl. clip, portrait, events), with the
    annex `F4Box` AND with the h-parametrised `F4BoxH` at h = 56 (0 mismatching scenarios). So the extension is a pure generalisation.
F7. Head point (annex `Portrait.screen`, binary-notes section 3, `AlundraInventoryPortrait.ComputeHeadPoint` `:75-78`): `(x - camX, y - camY - z - 0x20)` on the integer parts of the
    16.16 positions. S1: entity (200,150,0), camera (40,20) -> (160, 98).

## (A) PROPOSAL: the closed T1 contract

Montage = DIRECTOR LEVEL, binary order, NOT the proxy. Per iteration `f` (0 .. 59, 110 for S5):
  1. the test sets the world (S1..S8: nothing moves; S9 and the random runs: entities and camera move at the START of the iteration, before the pass, like `validate.py`);
  2. `AlundraDialogueDirector.Instance.Pass(squareHeld: true, squarePressed: true)`; record the draws (name, portrait), the sounds of the pass (sound 7 = `close-trigger`) and
     whether `IsOpen` fell during the pass (= `DialogClosed`);
  3. if `f >= 3` and a dialogue is pending: a FRESH `EventProgramState`, `runner.RunOneScriptCall(speaker, state)` on the real `AlundraEventProgramRunner` over synthetic bytecode
     (`0x0D t m 0xFF`, `0x5C v1 t m 0xFF`, `0xC4 v1 nl nh t m 0xFF`), a context like `AlundraDialogueSpeakerOpcodeTests.FakeContext` but with a settable `SpawnedEntities`;
     "opened" iff `OpenSerial` incremented (equivalently `state.CodeIndex == size`), else `retry (0)` (`CodeIndex == 0`);
  4. record `Box.Phase`; compare the row with `values.json[Sk][f]`.
Why not the proxy: its camera is not settable to (40,20) and its order is D-E19-64's; the proxy gets its own WIRING test (below), not the 450 rows.
Seams: `AttachToWorld(presenter, state, sounds, scrollSource: () => (camX, camY))` (new optional 4th parameter, see dll/notes.md); `AdvanceProviderForTests = c => AlundraTextBoxOracle.GlyphWidth(OraclePages.ByteOf(c))` (the
precedent of `AlundraTextBoxDrawnStateTests.cs:217`); names through `AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc((0x104,"Jess"), (0x10C,"Septimus"), ...))`
(`AlundraEtcStringTableTests.cs:30`), `ResetForTests` of both statics in `Dispose`; pages "AB" and "CD" in a map asset (`M1_S001`, `M1_S002`, text ids 0x81, 0x82) like `AlundraDialogueSpeakerOpcodeTests.MapAsset`.

Per image (row f of scenario Sk):
| values.json | compared | DLL observable | note |
|---|---|---|---|
| `rel` | index | `f - 3` | |
| `phase` | YES, guard | `director.Box.Phase` after step 3 | pins the image mapping: any off-by-one shows at the first transition |
| `box_y` | NO | | F4: the text box position is f2a's column and the model differs on the 2nd opening row |
| `name` null / not | YES | `director.NameBox.Drawn is null` | |
| `name.frame[0]` | YES | `Drawn.FrameX` | |
| `name.frame[1]` (140), `name.text[1]` (148) | constants | `AlundraNameBox.FrameY`, `TextY` asserted once (the precedent `AlundraChoiceBox.FrameY`) | |
| `name.text[0]` | YES | `Drawn.TextX` | = `FrameX + (112 - w) / 2`, w from `Advance` |
| `name.clip` | NO | not exposed | F3: derived and moot; if f4c needs it (side bands) it adds it with its own pixel test. Optional pure-data test over values.json "text never outside the clip" |
| `portrait` null / not | YES | `Portrait.DrawnThisStep == false` | |
| `portrait.x`, `y`, `w`, `h` | YES | `X`, `Y`, `DrawnWidth`, `DrawnHeight` | on the degenerate pass x,y = the (corrected) rest, w = h = 0 |
| `portrait.rgb` | YES | new `Rgb` (computed, exposed, not rendered, D-E19-89) | degenerate pass: 0 |
| `portrait.phase` | YES | new `Phase` (In, Rest, Out, Gone) | |
| `events` `opcode .. -> opened|retry (0)` | YES | `OpenSerial` / `state.CodeIndex` | |
| `events` `close-trigger` | YES | sound 7 asked during the pass (`PassStampedSoundPlayer`) | same pass as the model note |
| `events` `DialogClosed` | YES | `IsOpen` true -> false during the pass | proves name (T+17) and portrait (T+15) end before the box (T+18) |
| `events` `typing-done` | NO | | box internals, `AlundraDialogueBoxOracleComparisonTests` |
| `frame`, `box_y` | NO | | |
Scenarios: S1..S7 from the annex `values.json`; S8 (`values-s8-72.json`, S1 with a 48 x 72 portrait, produced by `f4_model_h.py`); S9 (`values-s9-moving.json`, speaker walking and camera
scrolling after the opening: head point (160,98) at the opcode, (182,98) at the close trigger T = N+22). Entities: S1 `SpriteType 0x104`, flag, pos (200,150,0), cam (40,20); S2 `0xFF` + flag;
S3 `0x10C` no flag; S4 `0xFF` no flag; S5 two programs `0x0D "AB"` then `0x0D "CD"` same speaker, attempted by the harness exactly like the model (second one tried from the iteration after the first opened);
S6 `0xC4 v1 = 0x80` (the speaker itself), name `0x0C 0x01`; S7 `0x5C v1 = 0x05` with an empty `SpawnedEntities` (no match: `0x5C` also tests `IsLoadedNormalOrDeactivated` of the reference).
Risk to flag: written red-first without being able to run here; S1's "AB" page must give the model's typing-done N+21 / trigger N+22 on the DLL box (f2a's oracle tests establish it for real nodes; the `phase` guard column will show it at once if not).

## (B) PROPOSAL: the 48 x 72 flight (no better-founded rule found in the code or the annex; dll-notes 3.1 reaches the same one)

Rule: the binary machine with its two hard-coded constants made parameters. For an image of height h (56 or 72; width 48 in all 331 records):
  rest = (8, 172 - h) -> (8,116) for h = 56 (the binary), (8,100) for h = 72 (D-E19-49: shown whole, bottom 172 as the original);
  head point = `(x - camX, y - camY - z - 0x20)`, read at the open and again at the close trigger; span = head - rest;
  opening c = 15 .. 1: top-left = rest + trunc(span * c / 15); size = trunc(48 (15 - c) / 15) x trunc(h (15 - c) / 15); rgb = 127 + trunc(128 c / 15);
  rest pass (c = 0): (8, 172 - h), 48 x h, rgb 128 (from N+16);
  return c = 15 .. 1: top-left = head + trunc((rest - head) * c / 15), head read at the close trigger; size = trunc(48 c / 15) x trunc(h c / 15); rgb = 127 + trunc(128 (15 - c) / 15);
  degenerate pass T+15: at the (corrected) rest, 0 x 0, rgb 0; nothing from T+16.
The anchor stays top-left; the last pass lands the bottom-right corner on (56, 172) for both heights. At h = 56 every value equals the annex (F6).

Sizes at h = 72: (0,0) (3,4) (6,9) (9,14) (12,19) (16,24) (19,28) (22,33) (25,38) (28,43) (32,48) (35,52) (38,57) (41,62) (44,67) = the plan's list (checked).
S1 entity and camera (head point (160,98)), rest (8,100), span (152,-2); `flight72.out.txt` has h = 56 as control. Rows (x,y wxh rgb):
Opening N+1 .. N+15, then the rest pass N+16:
 N+1 160,98 0x0 255 | N+2 149,99 3x4 246 | N+3 139,99 6x9 237 | N+4 129,99 9x14 229 | N+5 119,99 12x19 220 | N+6 109,99 16x24 212 | N+7 99,99 19x28 203 | N+8 89,99 22x33 195 |
 N+9 78,100 25x38 186 | N+10 68,100 28x43 178 | N+11 58,100 32x48 169 | N+12 48,100 35x52 161 | N+13 38,100 38x57 152 | N+14 28,100 41x62 144 | N+15 18,100 44x67 135 | N+16 8,100 48x72 128 (rest)
Return T .. T+14 (T = N+22), then the degenerate pass T+15:
 T 8,100 48x72 127 | T+1 19,99 44x67 135 | T+2 29,99 41x62 144 | T+3 39,99 38x57 152 | T+4 49,99 35x52 161 | T+5 59,99 32x48 169 | T+6 69,99 28x43 178 | T+7 79,99 25x38 186 |
 T+8 90,98 22x33 195 | T+9 100,98 19x28 203 | T+10 110,98 16x24 212 | T+11 120,98 12x19 220 | T+12 130,98 9x14 229 | T+13 140,98 6x9 237 | T+14 150,98 3x4 246 | T+15 8,100 0x0 0 (gone)
Degenerate position = the corrected rest (8,100) (invisible; the binary's (8,116) is the h = 56 case of the same rule). The 60-frame table with the name columns is `values-s8-72.json` (name columns identical to S1).
Machine interface this implies: an additive overload `Start(headX, headY, restX, restY, width, height)` that returns whether it was accepted (the old `Start(headX, headY)` keeps the inventory
constants and stays `void`); `BeginReturn` reads the stored rest; WARNING: `AlundraInventoryPortrait` already has `public const int RestX/RestY/FullWidth/FullHeight`, so the instance members need other
names (a const and an instance member cannot share a name).

## ANNEX FILES THE PLAN SHOULD ADD (task F4B-0, before the tests; none exists yet)
`model/f4_model_h.py` (this extension, the validated `f4_model.py` stays byte-identical), `model/common.py` (names from `names_widths.json`, no rig), `model/gen_sequences.py`,
`model/check_regression.py`, `values-s8-72.json`, `values-s9-moving.json`, `sequences-raw-digests.json`, `speaker_sites_retarget.tsv` (see advisories/notes.md).
