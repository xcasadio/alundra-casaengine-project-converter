# E19.f3 discovery: the binary's CHOICE box, to the tick (ALUN_CD.EXE, France)

Read-only. Repo `chantier/e19-suite` (HEAD `9956d56` at the start). Nothing built, run, exported, edited or committed in
the repository; `CasaEngine.Launcher/Program.cs` untouched. Everything written here lives in this directory.
Ground truth = the binary. Tags: **[bin]** read in the disassembly (capstone, `../../e19j-disc/lib.py`,
`../../f2b1-disc/oracle/bdis.py`); **[emu]** observed by RUNNING the real binary code in a MIPS-I interpreter
(`emu.py` + `drive.py`: opener, the four update functions, draw, pad update 0x8002E250, UpdateUiBoxesPosition 0x80047DD0,
opcode handler 0x8003E88C; python stubs only for libc, the sound call, RenderTextBitmap, the cell initialiser 0x80048304 and
the string lookup 0x800816D4, all of them logged); **[model]** `model/choice_model.py`, equal to [emu] frame by frame on
2000 random pad sequences (`validate.py`, `validate_out.txt`: 0 mismatching cases, 523 of them resolved to the end;
`mutate_check.py`: 7 deliberate model mutations all caught); **[code]** read in the repository; **[hyp]** hypothesis.
N = the script tick in which the opener runs (opcode 0x44 first entry); "frame" = one main-loop iteration (f2a convention:
RenderScene = dispatcher slots 0..12, then Update(0) = pad sample + scripts); the pad word the boxes see in frame f is the one
sampled in frame f-1's Update(0).

## Summary

| # | Fact | Binary evidence |
|---|------|-----------------|
| 1 | ONE opener `0x80050BA8(label1, label2, &result)`; plus a variant `0x80050C00` that starts on option `*result - 1` (default NON) and ONE debug preset `0x800506AC`. Six call sites of the opener: `0x44`, the save/memory-card file menu, the save book, and three that are memory-card flows (one unreachable, one dead code, one real failure prompt). See section 1. | xref over the whole image (`xref.py`, `wordref.py`) |
| 2 | The two options sit **side by side** (labels 48 px apart); the pad moves with **Left (0x8000) / Right (0x2000)**, never Up/Down. | `0x80050108-0x8005017C`; only three pad reads exist in the whole box code (`0x8004FFDC`, `0x8005010C`, `0x80050144`) |
| 3 | **Cross (0x40, just-pressed edge) validates**. Circle and every other button do nothing; there is **no cancel**. Left/Right read the **by-interval** word (first edge, then after 20 held frames every frame). | `0x8004FFD8`; `0x8002E250` |
| 4 | The box **slides in from the right** (x 320 -> 176, 15 steps + 2 settle passes), is interactive from **N+19**, and slides out to x 320 after the press, **closing 18 frames after the press frame C**. The result word is written in frame C+18 (callback), so `0x44` continues in script tick C+18: **earliest L = N+37** (OUI), **N+38** (NON). | `0x800501FC`, `0x800501A4`, `0x8004FFA8`, `0x8004FEFC`, `0x80047DD0`; [emu] |
| 5 | Sounds: **4 open** (opener tick N), **1 move** (only when the selection changes), **5 click** + **2** (first option) or **3** (second option) in the press frame. | `0x80050648`, `0x80050138/0x80050174`, `0x800500D0`, `0x800500E8` (3) / `0x800500EC` (2) |
| 6 | Result word: 0 pending, **1 = first option, 2 = second**; opcode 0x44 turns it into program `Result` = 1 for the first, 0 for the second. The word is written ONLY at the end of the slide-out. | `0x80050B98`, `0x8003E900-0x8003E930` |
| 7 | Frame at **(176,144), 128 x 32** (16 x 4 cells), labels at **(192,152) and (240,152)** (width = font3 advance sum: OUI 19, NON 23; height 16), cursor 16 x 16 at **(cfg.x + 16 + 48 i + 4 * strlen(label) - 8, cfg.y - 8)** = (196,136) / (244,136) at rest, image = counter / 10 (u B0, C0, D0, E0, v 38), counter per DRAWN pass, wraps at 40, **never reset at open**. No clip. | `0x8004FCE8`, `0x800507E4`, `0x800A4FEC`; [emu] |
| 8 | The choice box is independent of the text box: no shared state, no wait either way. The text box keeps typing under it (save book: choice opens while the question types), stays up after typing until `0x51`, and is drawn BEFORE the choice (OT slots 0/2 vs 5/6). | whole-image search: the slot test `0x80047C8C` has two calls (slots 0 and 0xB, in `0x80055570`), no code reads slot 3's flags at `0x8015307C` directly, the choice globals are private (every reference to `0x8017E640` is in `0x8004FCE8-0x80050C00`) |
| 9 | The 0x44 first-entry marker (program `+8` = pc) is **never cleared**; a second visit to the same pc re-polls the stale result word. | `0x8003E8B8-0x8003E8F8` |
| 10 | The model reproduces the binary exactly for the choice box; value tables for 9 scenarios are in `values.md` / `values.json`. | `validate.py` |

## 1. The opener and every caller

### 1.1 Opener, internals **[bin]** (disassembly in `dumps/d_50000_50d80.txt`)
- `0x80050BA8(a0 = label1 ptr, a1 = label2 ptr, a2 = result ptr)`: stores the two label pointers at `0x8017E3F8/0x8017E3FC`
  (`0x80050BB8`, `0x80050BCC`), calls `0x800505FC(callback 0x80050B98, 1, &0x8017E3F8)`, then stores the result pointer in
  `0x8017E3F0` (`0x80050BDC`) and the selection `G+0x30` = 0 (`0x80050BE4`); returns 1.
- `0x800505FC`: `G+0x268` = callback, `G+0x264` (halfword) = 1 (**never read**), `strncpy(G+0x26C, label1, 6)`,
  `strncpy(G+0x273, label2, 6)` (`0x8008202C`, so a label is at most 6 bytes, 7-byte slots), `0x800506FC(G)` (initialises the two
  cursor SPRT prims, one per display buffer), `PlaySoundEffect(4)` (`0x80050648`), `OpenSlot(3)` (`0x80047F94`: copies entry 3
  of the table `0x800A731C` to the runtime slot `0x80153028 + 0x1C*3`, flags |= 1, no init function). It does NOT touch any
  text-box state and does not check whether slot 3 is already active.
- `G = 0x8017E640`: +0 result (selection + 1, set at the press), +4 cursor tick, +8 / +0x1C cursor SPRT (db 0 / db 1),
  +0x30 selection, +0x34 label SPRTs (`0x3C` per label, `0x78` per display buffer), +0x268 callback, +0x26C / +0x273 labels.
  `S = 0x8017E8BC` (= G + 0x27C) is the slide block read by `UpdateUiBoxesPosition`: +0 tick, +4 steps (15), +8 mode, +0xC/+0xE
  start x/y, +0x10/+0x12 end x/y, +0x18/+0x1A origin x/y. All the references to G in the image are in `0x8004FCE8-0x80050C00`.
- Callback `0x80050B98`: `*(*0x8017E3F0) = a0`. This is the ONLY way the result reaches the caller.
- Variant `0x80050C00` (same body): selection = `*a2 - 1`, then `*a2 = 0` (`0x80050C38-0x80050C4C`). [emu] checked: word 2 -> selection 1,
  word zeroed, then Cross gives result 2 (`t_variant.py`).
- Labels are decoded in the first update pass: `{c` -> `c + 0x50`, `}c` -> `c + 0x90` (`0x80050254-0x800502B8`, same mapping as
  the text box); the cursor uses the DECODED length. Both labels are drawn by `RenderTextBitmap(label, 0x8014A4E8, 960 + 32*i,
  464, pen 0, rank 0, 128, 16)` into VRAM (960,464) and (992,464) (`0x8005032C`, `0x8005035C`) [emu log].

### 1.2 The call sites (xref of `jal`/`j`, data words and `lui`/`addiu` pairs over the whole image)
| # | Site | Function | Labels | Result word | Default | Notes |
|---|------|----------|--------|-------------|---------|-------|
| 1 | `0x8003E8E4` | opcode 0x44 handler `0x8003E88C` | ETC `0x43` / `0x44` ("OUI" / "NON": `Etc_0067`/`Etc_0068` in `alundra-project/Dialogues/Etc.yarn`) via `0x800816D4` | `0x8013D8D0` (zeroed at `0x8003E8C0`) | OUI | polls every tick; program Result 1 / 0 |
| 2 | `0x80059504` | file menu update `0x80058F24` (slot 10; installed by `0x800583EC`: `lui/addiu` 0x80058F24 at `0x80058404/0x8005841C`, `sw` to entry `+0x14` at `0x80058420`) | ETC `0x4A` / `0x4B` ("OUI"/"NON", `Etc_0074`/`0075`) | `0x80180124` | OUI | opened when `F2E & 0x40` (Cross, by-interval) in the menu, state 8; no text box; the poll (`0x8005912C-0x80059150`) accepts 1 or 2 |
| 3 | `0x8007BA54` | `AI_ProcessWarpTransitionState 0x8007B998`, state 2 (the save book) | ETC `0x41` / `0x42` (`Etc_0065`/`0066`) | flag `0x80191260` | OUI | opened 61 ticks after the ETC 0x40 box (still typing, see section 6) |
| 4 | `0x8005EDA8` | `UpdateSaveGameTransition 0x8005ED2C`, state 0x2710 | Shift-JIS literals `0x80026C18` (`83 5A 81 5B 83 75`) and `0x80026C20` (`81 40 81 40 81 40`), message literal `0x80026C04` through `0x80060CF8` | `0x800C4988` | OUI | **unreachable**: this is process kind 3; the only caller of the process start `0x8005EC44` is `0x80031594` and passes kind 1 (`0x80031590`) |
| 5 | `0x8005FC68` | `StartMemoryCardProcess 0x8005F458`, save-failure branch | ETC `0x81` / `0x82` ("OUI"/"NON") after the message box `0x80060CF8(ETC 0x93 "Impossible d'enregistrer l'histoire.", ETC 0x94 "Est-ce bien clair?")` | `0x800C4988` | OUI | 1 -> state `0x3F5`, 2 -> `0x3ED` (`0x8005FC78-0x8005FCB8`) |
| 6 | `0x80060DF8` | `0x80060D8C` (message/choice helper: both labels empty -> state `0x11`, result `0x3E8`) | arguments | argument | OUI | **dead code**: no `jal`/`j`, no data word, no `lui`/`addiu` reference |
| 7 | `0x8005F9D0` -> variant `0x80050C00` | `StartMemoryCardProcess` | ETC `0x81` / `0x82` after the message box (ETC `0x8F` "La Carte Mémoire n'est pas encore formatée.", `0x90` "Formater cette Carte Mémoire maintenant?") | `0x800C4988` preset to 2 at `0x8005F9B0` | **NON** | result read at `0x8005F9E4-0x8005FA30` |
| 8 | `0x80055614` -> `0x800506AC` | inventory/menu opener `0x80055570` (Start/L2/R2 just pressed with Down HELD and `[0x800AF2B0] == 0`) | label array at `0x800AF2C0`, which in the image holds cell data (`0x00600070`, `0x000368D8`), not string pointers | none | OUI | callback `0x80050670` opens slot 8 (result 2) or slot 5 (result 1) = the analyser's `InitializeDebugBgmMenu` / `UIDebugManager` (`UIManager.cs:472`, `UIDebugManager.cs:398`): **debug**, not to port |

So of the plan's "three unidentified callers": `0x8005EDA8` = memory-card "save test" (Japanese debug text, unreachable),
`0x8005FC68` = memory-card save-failure confirmation (reachable in the French game when the card save fails; the DLL replaced the
memory card with files, ADR-0014), `0x80060DF8` = dead helper. Two more users exist that the sketch did not list (7 and 8).

## 2. State machine, frame by frame **[bin]** + **[emu]**

Slot 3 = entry 3 of `0x800A731C`: `{0, cfg 0x800A4FEC, +8 = 16, +0xA = 8, +0xC = 32, +0xE = 4, init 0, update 0x800501FC, layer 5}`.
cfg `0x800A4FEC` = `{X 176, Y 144, 16 x 4 cells, cell arrays 0x800A45EC / 0x800A4AEC}`. The dispatcher `0x80048054` calls
`update(&0x8015319C)` every frame while flags bit 0 is set; the return value is ignored. The update pointer replaces itself:

| Frame | Update function | What it does |
|-------|-----------------|--------------|
| N (script phase) | opener | sound 4; slot active; selection 0; result word 0 (`0x44` zeroed it); the 0x44 handler returns 0 (yield) and stores its pc marker |
| N+1 | `0x800501FC` (init pass) | decodes the labels, renders them to VRAM, builds the label SPRTs (u = 128 i, v = 0xD0, w = text width, h = 16, clut `*0x80146E38`), writes S = {tick 0, steps 15, mode 2, start (320, 144), end (176, 144), origin (176, 144)}, one `UpdateUiBoxesPosition` (tick 0: cfg.x = 320), `0x80048304` (cell init), then sets update = `0x800501A4`. **Nothing is drawn** (no label, no cursor); the cells sit at x 320.. (off screen) |
| N+2 .. N+18 | `0x800501A4` | `UpdateUiBoxesPosition` then the draw `0x8004FCE8`. cfg.x = 320 + trunc(-144 k / 15) for k = 1..14, then 176 (k = 15, N+16), 176 (settle, N+17); the 18th `UpdateUiBoxesPosition` call of the slide (N+18, counting the init pass) returns 1 and installs `0x8004FFA8`; the draw still happens in N+18 |
| N+19 .. | `0x8004FFA8` | pad handling (section 3) then the draw |
| C+1 .. C+17 | `0x8004FEFC` (installed in the press frame C) | `UpdateUiBoxesPosition` (start = current x, end x = 320, 15 steps, same truncation) then the draw while it returns 0 |
| C+18 | `0x8004FEFC` | the slide returns 1: cfg restored to (176,144) from S+0x18/+0x1A, `0x80047CB0` clears the slot flags (so the cells are not chained any more), callback(result) writes the caller's word. **Not drawn.** |

- cfg.x per frame **[emu]**: slide-in N+1 320 (not drawn), N+2 311, 301, 292, 282, 272, 263, 253, 244, 234, 224, 215, 205, 196, 186, N+16 176, N+17 176, N+18 176; slide-out C 176 (press frame, drawn at rest), C+1 176, 185, 195, 204, 214, 224, 233, 243, 252, 262, 272, 281, 291, 300, 310 (C+15), C+16 320, C+17 320 (both off screen), C+18 closed.
- The first drawn frame is N+2; the frame is fully inside the screen from N+16; fully outside from C+16.
- A Cross seen before N+19 (frames N+1..N+18) is **lost**: those passes never read the pad ([emu]: Cross at N+18 never resolves).
- During the slide-out the pad is not read at all.
- Left/Right in the SAME pass as Cross: the result was already stored from the OLD selection (`G+0` = sel + 1 at `0x80050008`, before the
  Left/Right code); the cursor then moves and sound 1 plays while the box slides out (scenario V5). Cross and the sounds 5 + 2/3 first, then 1.

## 3. Input **[bin]** (pad update `0x8002E250`, init `0x8002E34C`)
- Pad word layout = `AlundraPadState` (`AlundraPlayerController.cs:23-36`): Cross 0x40 (the hero's Jump), Circle 0x20, Square 0x80, Triangle 0x10,
  Up 0x1000, Right 0x2000, Down 0x4000, Left 0x8000.
- `+0x12` justPressed = `new & ~old`; `+0x16` byInterval: whenever the held word changes (ANY bit, including an unrelated button) or is 0:
  byInterval = justPressed and the counter restarts; while the word stays identical and non-zero: 20 passes of 0 (`[0x80126F18] = 0x14`), then
  byInterval = the whole held word, and every pass afterwards (`[0x80126F1C] = 0`). So the first repeat comes on the **21st** consecutive pass
  after the press pass. Nothing else writes `0x80126F18/0x80126F1C` (xref of the pad base).
- Box reads: `0x8004FFDC` `lhu 0x80126F2A & 0x40` (Cross, just-pressed), `0x8005010C` `lhu 0x80126F2E & 0x8000` (Left), `0x80050144` `lhu 0x80126F2E & 0x2000` (Right).
- Left: if selection == 1 play sound 1; selection = 0. Right: if selection == 0 play sound 1; selection = 1. So holding a direction plays
  sound 1 once, and the selection saturates. Cross: result = selection + 1, sounds 5 then (2 if result == 1 else 3), slide-out armed.
- No other pad read exists in `0x8004FC94-0x80050C00`: no Circle, no Triangle, no Start, no Down/Up, **no cancel**.
- The file menu (caller 2) opens the question on `F2E & 0x40`, i.e. Cross BY-INTERVAL: a held Cross can reopen it; the box itself still needs a fresh edge.

## 4. Sounds **[bin]** / **[emu]**
| Id | Event | Where | Frame |
|----|-------|-------|-------|
| 4 | open | `0x80050648` (in `0x800505FC`) | N (opener tick) |
| 1 | cursor moved (selection actually changes) | `0x80050138`, `0x80050174` | any active pass |
| 5 | Cross pressed (click) | `0x800500D0` | press frame C |
| 2 | first option (OUI) validated | `0x800500EC` | C, right after 5 |
| 3 | second option (NON) validated | `0x800500E8` (delay slot of the `bne` at `0x800500E4`) | C, right after 5 |
The ids are the `PlaySoundEffect` space (`0x800490FC`); `alundra-project/Sounds/sfx_0001..0005.wav` exist (manifest ids 1-5: sample rates 9604, 24214, 4274, 11348, 12016 Hz; volumes 90, 60, 80, 115, 105; max voices 2, 2, 1, 1, 1).
`0x800490FC` returns at once while the BGM countdown `*0x80175850 != 0` (e19h notes), not re-checked here.

## 5. Result and closing
- Written once, by the callback, in frame C+18 inside RenderScene (before Update(0) of that frame). Every consumer is polled after the dispatcher:
  the 0x44 handler (script phase), the save book (entity script), the file menu (slot 10 runs after slot 3). So all of them see it in frame C+18.
- 0x44 re-entry (`0x8003E900-0x8003E930`): word 0 -> return 0; word 1 -> `Result = 1`, return 1; else `Result = 0`, return 1; the program continues in the
  SAME tick. First entry (`0x8003E8B8-0x8003E8F8`): marker `+8 != pc` -> word = 0, open, marker = pc, return 0.
- The marker is never cleared: a later visit of the same pc (a retry loop) takes the polling path and reads the old word (1 or 2) without asking.
- Closing needs no `0x51` from the choice: the text box (if any) is closed by its own rules (close mode 4 + `0x51` latch, `max(L+1, E+1)`).

## 6. Coexistence with the text box **[bin]** + **[model]**
- The question box (slot 0) is not touched: it keeps typing, scrolling and running its timers. The choice is slot 3, its prims go to OT slots 5 (frame cells and cursor) and 6
  (labels) [emu: `t_ot.py`: slot 5 head = cursor SPRT `0x8017E648` at (196,136), slot 6 = NON (240,152) then OUI (192,152), SPRT code 0x65]. Slots 0/2 (frame, text bands, clip) are earlier,
  slot 3 restores the full draw area, so the choice is **drawn over** the text box and never clipped by the text clip. The name box (slot 5/6, x 64..175) lies directly left of the
  choice (x 176..303); the name text clip (80,148,240,34) contains the labels.
- Overlap: the choice frame spans y 144..175, the text box frame starts at y 168: the choice covers the top 8 pixel rows of the text box frame and rows 173..175 of text line 1
  for x 176..303 (cells are opaque, corners transparent per F1).
- The text box does not wait for the choice and the choice does not wait for the text (nothing reads slot 3's flags; G is private).
  The only coupling is the script: sailor 12 (map 389) opens the choice at the `\999` flag step G (N+94 with pad A), then `0x51`: the box stays typed until L+1.
  The save book opens the choice at tick 61 while the 24-character question types (scenario V9: 11 glyphs typed at the opener, the box closes only after typing, E+1).
- `ctrl` bits (`0x800DC4B8`) are not touched by the choice. Cross is also the hero's Jump (`0x80031ECC`, just-pressed 0x40): the binary relies on the hero being locked by the interaction
  (not verified for each caller, see section 10).

## 7. Geometry and drawing **[bin]** + **[emu]**
- Frame: cells `SPRT 8x8` x 64 at `(cfg.x + 8 c, cfg.y + 8 r)`, `SetSemiTrans 0`, `SetShadeTex 1`, clut `*0x80146E28` (palette #0 of WIND.CL) (`0x80048304-0x80048468`); rest position x 176..303, y 144..175.
  The baked sprite is F1's `g_uiBoxesConfigurationBackgroundMessageChoice` (raw cell x0/y0 already at the rest position).
- Labels: SPRT at `(cfg.x + 16 + 48 i, cfg.y + 8)`, w = `CalcTextWidth` (OUI 19 = 7+8+4, NON 23 = 8+7+8), h 16, source (u = 128 i, v = 0xD0) of the VRAM page (960,256) 4-bit, clut `*0x80146E38`
  (palette #8 of WIND.CL, as the text box bands). Rest: (192,152), (240,152). No clip, no centring.
- Cursor: SPRT 16 x 16 at `(cfg.x + 16 + 48 sel + 4 * strlen(decoded label) - 8, cfg.y - 8)`: OUI (196,136), NON (244,136) at rest, i.e. centred above the selected label and overlapping
  the frame's top 8 rows (drawn after the cells). Image: `G+4` +1 per draw (0x800507E4), wraps at 40, image = tick/10, (u,v) from `0x800A58CC + 40*image` = (B0,C0,D0,E0 ; 38), clut `*0x80146E38`. The same four
  `wind_150/173/201/228` sprites as the text box wait cursor. The counter is **not reset at open**: the first drawn pass shows image `(anim0+1)/10`.
- No `DR_AREA`/clip by the choice. Draw order inside OT slot 5: DR_MODE, cells, cursor; slot 6: DR_MODE, labels.
- Drawn passes: N+2..N+18, N+19.., C..C+17 (not N+1, not C+18).

## 8. Model and value tables
- `model/model.py` = unmodified copy of the f2a model; `model/choice_model.py` adds `PadWords` (the real pad update), `ChoiceBox` (per-pass logic and drawn state), `op44` (handler + stale marker),
  `run_choice` (text box slot 0 + choice slot 3 + script tick per frame), the sailor-12 and a bare script. `validate.py` runs the real binary code beside it on random pad sequences.
- `values.md` / `values.json` (`model/scenarios.py`): per-frame tables (update function, drawn, frame x, label x, cursor x, cursor image, selection, sounds).
  V1 validate the default at once; V2 Right then Cross; V3 Cross held through N+19; V4 Right held (auto-repeat) then Left then Cross; V5 Cross + Right together; V6 variant opener
  (default NON); V7 cursor counter left at 17; V8 sailor 12 with the real box (pad A and pad B); V9 the save book (box still typing).
- Pins worth keeping (all relative to N; **[emu]**-checked for the bare ones):
  * V1: init N+1, first drawn N+2, interactive N+19, Cross seen N+19 -> sounds (5,2) at N+19, closed + Result 1 at N+37.
  * V2: Right seen N+19 (sound 1, cursor at 244 in the SAME pass), Cross N+22 -> (5,3) at N+22, Result 0 at N+40. Earliest NON: Right N+19, Cross N+20 -> N+38.
  * V3: Cross held N+10..N+27 gives no press at N+19; a new press at N+30 -> N+48.
  * V4: Right held from N+19: sound 1 only at N+19; byInterval repeats from N+40 (no effect); Left N+60 -> sound 1, Cross N+62 -> N+80.
  * V8 pad A: choice opens N+94, Cross N+113, resolved N+131, `0x51` N+131, text close trigger N+132 (was N+96 with the f2a placeholder L = G+1), next open N+150; pad B: opens N+319, resolved N+356, close trigger N+357, next open N+375.
  * V9 (book, Cross at the earliest): opener at tick 61, resolved 98, text typing ends 115, close trigger 116, released 134.
  * The save screen's question (caller 2) is V1 shifted to its own opener frame S: slot 10 runs after slot 3, so the first slot-3 pass is S+1, the sound 4 is in S, and the screen sees the result in C+18.

## 9. Facts the DLL port needs (no decision)
- Pad inputs needed per tick: just-pressed Cross; by-interval Left/Right (delay 20, interval 0, restart on any change of the held set). `AlundraPadState` carries only hold + just-pressed.
- Layout is horizontal (OUI left, NON right, default OUI); the engine's vertical StackPanel of buttons and the mouse picking have no equivalent in the binary.
- A test hook replacing `SelectChoiceForTests` must cost the ticks: OUI resolves at N+37 at the earliest, NON at N+38; the arcs that select through it today see the answer in the next tick.
  Pressing Cross during the first 18 passes is lost.
- The result reaches the script in the tick C+18 itself (same-tick continuation); the `0x51` that follows is latched in that tick and the text box sees it in its NEXT pass: close trigger L+1 (or E+1 if typing is not done yet).

## 10. Not proven
1. **No emulator/hardware run of the whole game.** [emu] is the real code of the choice box executed with stubs; the stubs (libc, sound, VRAM upload, cell initialiser, ETC lookup) are logged but not replaced by their real bodies; the display buffer index `*0x80146F50` is fixed to 0 (prims are per buffer; no logic depends on it).
2. **Pixels.** Which texels the cursor and the label glyphs paint, the exact tpage of OT slots 5 and 6 (the DR_MODE words come from the runtime table `0x80146E48`, built at `0x80044974-0x80044A9C`; slot 6 is assumed to be the font page 960,256 because the name box text is drawn through it and the labels are stored at (960/992,464)), and the final look of the overlap with the text box were not rendered.
3. **Hero lock.** Whether the hero is always locked while a choice is open (Cross is also Jump): not checked per caller; the 0x44 case relies on the interaction's control lock, the save book on `ControlLocked` from the F-interact entry (E16.e notes).
4. **Callers 4-8.** Reachability is static reading only (kind 3 never started; `0x80060D8C` unreferenced; the shortcut at `0x80055614` depends on `[0x800AF2B0] == 0`, a runtime word cleared at `0x80048514` and set at `0x8004E85C`, so it may really be reachable with Start + Down held: its label array is not strings in the image, so the result would be garbage or a runtime-filled table I did not find). The memory-card state values after the answer are only listed, not followed.
5. **Strings.** OUI/NON are the exported French ETC lines (`Etc.yarn`), not re-read from the binary's table `0x801EBB40` (loaded at run time from disc data).
6. **Save-book timing.** The 61-tick wait is taken from the E16.e notes (`e16def/e16e-notes.md`, state 2) and the DLL, not re-derived here.
7. **Counter persistence.** `G+4` is only written by `0x800507E4` in the image; whether a boot or load clears it (memset of .bss) is not shown. The model carries it across boxes (`anim0`).
8. **Frame rate.** Frames are main-loop iterations (f2a convention); nothing here says how many iterations per second the original ran on NTSC/PAL.
9. **Simultaneous open.** Opening while slot 3 is still active (no guard in the opener) restarts the slide from the init pass without calling the old callback: not exercised.

## 11. Questions for the author (none blocks the plan)
1. Layout/pad: reproduce the binary (horizontal, Left/Right, Cross, no cancel) and drop the engine's vertical list for choices, including mouse picking? (the plan sketch says "validation with Cross" only).
2. Keep the cursor tick persistent across boxes (faithful) or reset it at open?
3. Fix the stale 0x44 first-entry marker (rule of 2026-09-25: a proven original defect is fixed) or keep the pc marker as is?
4. The memory-card prompts (callers 5 and 7: save failure, format) are not part of the file-based save of ADR-0014: out of scope for f3?

## 12. Files (this directory)
`emu.py`, `drive.py` (interpreter and frame driver), `validate.py` + `validate_out.txt`, `mutate_check.py` + `mutate_out.txt`, `t1.py`, `t_ot.py`, `t_variant.py`, `tbl.py`, `dump.py`, `xref.py`, `wordref.py`, `funcstart.py`, `widths.py`,
`dumps/` (disassembly), `model/model.py` (copy), `model/choice_model.py`, `model/scenarios.py`, `values.md`, `values.json`.
