# O-E19-9 discovery: zero-duration Loop animations invisible in the engine

Read-only discovery, 2026-10-07, branch `chantier/e19-suite2`. Nothing built, run, exported, edited or staged in the repository.
Scratch artefacts (this folder): `dump_raw.py` + `raw_dump.txt` (raw frames), `dis_update_animation.txt` (France binary disassembly),
`run_update_animation.py` + `run_update_animation.out` (real routine run in the MIPS interpreter), `scan_anim2d.py` + `scan_anim2d.out`
(9624 .anim2d scan), `xref_0xa8.txt` (who touches NextFrameDelay), `lib/` (scratch copy of `docs/plan-e19-g3d-annexe/discovery/lib/{lib,emu}.py`, unchanged).

F = fact with evidence, H = hypothesis.

## 1. Raw data

- F: the converter reads the first map holding a bank (`Readers/SpriteBankReader.cs:146-170`, `:326-338`): bank 127 from `data-extracted/data/map_75.json`
  (also in map_137), bank 151 from `map_48.json` (also maps 49-53, 435-438). Directions are PreloadedAnims order 0=down, 1=up, 2=left, 3=right.
- F (`raw_dump.txt`): each of the 5 animations is ONE displayed frame + a 1-byte Loop terminator:
  | animation | displayed frame raw Delay | quads | terminator Delay |
  |---|---|---|---|
  | bank127 anim0 left / right | 0x80 | 1 | 0x01 (Loop) |
  | bank151 anim1 down / up / left | 0x80 | 4 | 0x01 (Loop) |
  Siblings: bank127 anim0 down/up 0x82 (2 ticks), bank151 anim1 right 0x8A (10 ticks), same one-frame shape.
- F: convention is `0x80 | ticks`; the reader keeps `Delay & 0x7f` (`SpriteBankReader.cs:561`), so DelayFrames = 0, and
  `SpriteWriter.cs:555-562` accumulates 0 s: the frame key AND the terminator key both land at t = 0.
- F: raw `"Delay": 128` occurs only in the 12 map files holding banks 127/151 (34 hits = 2 or 3 per file, these animations duplicated). No other
  animation in the game has a 0-tick displayed frame.

## 2. Binary (ALUN_CD.EXE France), entity animation routine UpdateAnimation 0x80038AB4

Called once per tick per active entity by UpdateEntitiesAnimation 0x80038E18 (`jal 0x80038ab4` at 0x80038E4C).
- F (disassembly, `dis_update_animation.txt`): entity+0xA8 is NextFrameDelay (32-bit). Steady path 0x80038BD8-0x80038BE8:
  `lw v0,0xa8(s0); addiu v0,v0,-1; bnez v0,0x80038df8 (return); sw v0,0xa8(s0)`: decrement FIRST, then return unless the result is 0.
  Frame read 0x80038BF4-0x80038C18: if `delay & 0x80`, NextFrameDelay = `delay & 0x7f`, frame pointer += 5, images set (0x80038CBC-0x80038D00).
  Terminator 0x80038D14: 0 -> hold/chain, 1 -> loop (0x80038D70: AnimCompleteCounter +0xB0 ++, pointer = first frame, re-read), other -> error print.
- So a 0x80 frame sets NextFrameDelay = 0; the next tick computes 0 - 1 = 0xFFFFFFFF != 0 and returns. It holds for 2^32 - 1 ticks
  (about 994 days at 50 Hz) before the terminator is read. NOT 256 ticks like the G3a effect machine, NOT one advance per tick.
- F (run of the real routine, `run_update_animation.out`, synthetic sprite-info bytes with the exact delay/terminator bytes): for all 5
  animations, SpriteRef.Images (+0x194) points at frame 0's image set on every one of 600 ticks, image count 1 (bank127) or 4 (bank151),
  NextFrameDelay runs 0, 0xFFFFFFFF, 0xFFFFFFFE..., AnimCompleteCounter stays 0. Controls: 0x82 sibling loops every 2 ticks (counter 299
  after 600 ticks), 0x8A every 10 (counter 59), same image throughout. Wrap check (NextFrameDelay poked to 1): the terminator loops back to
  frame 0, same image, counter 1.
- F (`xref_0xa8.txt`): every `lw/sw reg,0xa8(base)` of an entity in the image is inside 0x80038AB4-0x80038E14 (the others are a stack slot
  and a menu table at 0x80053618), so nothing else resets NextFrameDelay. H: indirect writes (block copies of an entity) not enumerated.
- Conclusion F: the binary displays frame 0's image, frozen, for the whole life of the animation, and never counts a loop turn.
  The plan row's claim is now proved.

## 3. Engine today and the minimal fix

- F: `TryEvaluateBool` keeps the LAST key with time <= t (`CasaEngine/Framework/Assets/Animations/Animation2dCompositionSampler.cs:323-340`),
  so `true@0, false@0` samples false at every t: invisible.
- F: Loop of duration 0 (`GetDurationSeconds`, `Animation2dData.cs:78-103` = max key time = 0): `UpdateLooping` `:91-97` sets CurrentTime 0,
  `ApplyTracks(0)`, no loop turn; `Seek` keeps t (`WrapLoopTime :167-175`) and samples it; the logical clock
  (`AnimatedSpriteComponent.cs:339-343`, `:398-401`) returns without AnimationLooped when the duration is 0 ticks. Turns = 0 forever =
  binary counter frozen at 0. No engine change needed.
- Minimal converter fix: in `SpriteWriter.ConvertAnimation`, when `endKind == Loop` and the summed duration is 0 (`cumulativeSeconds == 0f`),
  do not emit the terminator frame's keys (the only one it emits is the per-part `false` Visible key, `:640-645`). Result per part: Visible
  `[true@0]`, duration stays 0, Loop. Visible for any sample time, zero turns: equal to the binary.
  Rejected alternatives: real durations (the binary's hold is 2^32 - 1 ticks, not 256; a finite duration would add loop turns the binary
  never makes); extending `repeatLastFrame` to this case (adds duplicate keys on every track, same render).
- Scan (`scan_anim2d.out`): 9624 .anim2d (5211 Loop, 4413 Once); exactly 5 have duration 0, exactly the same 5 have two keys at one instant,
  and exactly these 5 are invisible at t = 0. A fix guarded on "Loop and duration 0" moves only them.
- Tests that pin them: none (rg for `bank127_anim0|bank151_anim1` outside the export finds only the backlog notes). Existing converter tests
  use displayed delays 10/136/138 and keep a non-zero duration; `SpriteWriterAnimationEndTests` Loop test (3 keys, last hidden) and
  `SpriteWriterTests.cs:340-342` stay green under the guard.

## 4. Slice proposal

Closed file list:
1. `alundra-casaengine-project-converter/Writers/SpriteWriter.cs` (ConvertAnimation guard + comment at `:548-553` and the class doc if it
   describes the terminator key).
2. `alundra-casaengine-project-converter.Tests/SpriteWriterAnimationEndTests.cs` (helper gets an optional displayed-frames JSON, default
   unchanged; new tests).
3. Export in place `alundra-project/` (5 files predicted) + manifest proof; plan row O-E19-9 in `docs/plan-e19-opcodes.md`.

Red-first tests (red at HEAD, green after):
- T1 `ConvertSprites_ZeroDurationLoop_KeepsTheFrameVisible`: one displayed frame `Delay 128` with 2 quads + `{ "Delay": 1 }`; assert Loop,
  `GetDurationSeconds() == 0`, each part's Visible track is exactly `[true@0]`. HEAD: 2 keys, last false.
- T2 same fixture through `Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(...))` (pattern of
  `SpriteWriterCollisionTests.cs:157`): after Reset and 100 `Update(0.02f)`, every `RuntimeState.Parts[i].Visible` is true and
  `LastUpdateLoopTurns == 0`. HEAD: Visible false.
- Guard T3 (green at HEAD and after): one displayed frame `Delay 130` + Loop terminator keeps `[true@0, false@0.04]`.

Predicted export diff: exactly 5 files, `Entities/Interrupteur tricolore/bank127_anim0_{left,right}.anim2d` (1 key removed each) and
`Entities/YM08 barriere u=eau d=feu l=sable r=glace/bank151_anim1_{down,left,up}.anim2d` (4 keys removed each, one per part). Ids, AssetInfos.json,
sprite-records.json, entities and report.json unchanged (no counter counts Visible keys), unless the slice adds a counter.

## Hypotheses and author decisions

- H: in-game impact. Bank 151 ("YM08 barriere", direction = element) anim1 hides the fire/sand/water barriers; the ice one (right, 10 ticks)
  shows. Which scripts select anim1 / anim0 left-right was not traced. Playtest: maps 48-53 / 435-438 (151) and 75 / 137 (127).
- Author: add a report counter or a warning for 0-tick displayed frames (moves report.json)? Generalise to a 0-tick frame inside a longer
  animation (binary: freezes there forever)? No such frame exists in the data today, so the minimal guard is enough.
