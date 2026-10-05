# ADR-0029: The dialogue text box follows the binary to the tick

- **Status**: Accepted
- **Date**: 2026-10-05 (decisions D-E19-62 to D-E19-64 of the author, 2026-10-03; D-E19-71 and D-E19-77, 2026-10-05)
- **Source**: this chantier: `docs/plan-e19-opcodes.md` section 1.2j.3 E19.f2a (rules F2-R1 to F2-R6), D-E19-62, D-E19-63, D-E19-64, D-E19-71, D-E19-77; values in `docs/plan-e19-f2a-valeurs.md`. Replaces the "flags set at the display of the page" rule of D-E12-4 (ADR-0025 kept the markers, this ADR draws them at their glyph).

## Context

- The binary updates the text box in the UI callback dispatcher of `RenderScene` (`0x80048054`, slot 0, `MsgBoxRender` `0x80046EF0`), BEFORE the scripts of the same frame, and reads the pad sampled during the previous frame. The model drawn from it (`e19f2` discovery, `model.py`) gives the slide of 18 passes, one step every 4 passes, the cursor of `\A`, the scroll of the third line, the close timer of 360, the latches of `0x4D`, `0x4F` and `0x51`, the sounds 6, 7 and 79 to 82, and the release 18 passes after the close trigger.
- The DLL typed nothing: a page was shown whole, a press turned a page or closed the box at once, the timer ran from the opening, `0x4C` to `0x4F` were skipped by size and the text flags were set when a page was displayed. The pass ran after the map events, on the pad of the rendered frame.
- The engine updates every entity, scripts included, before the world proxy that runs the map events.

## Decision

- **F2-R1** The world proxy's pad pass records the square button of each tick. One loop of ticks always runs, the game blocked or not; at tick k it does the pass of the box on the square of tick k-1, reads the gate again, then runs the map events and the recycling when the hero exists and the gate is open. The triggers in waiting read the gate again after the loop. The old dialogue pass is removed. The intro harness mirrors it: scripts, pass of the box, callback of the test, on a `TickPad` mirror of the previous frame's hold.
- **D-E19-64** The gaps due to the engine's order (entity scripts before the world proxy) are accepted, not dated: an entity script sees the first pass at N and the release at T+19, a map event at N+1 and T+18.
- **F2-R2** The director owns the machine (`AlundraDialogueBox`), exactly the binary's, with D-E19-62 (a `0x4C` clears a `0x4D` in waiting) and D-E19-63 (a released `\A` sets the start-at-once scroll bit only when it arms the scroll of the third line). It runs without a view and keeps running during a choice; the choice list stays the engine's until E19.f3, and the typed text is sent after the answer.
- **F2-R3** A Yarn page is cut into steps from its text and markers; `\A` is the page boundary and the next page is asked of the runner at the release of the cursor; a flag marker sets its flag at its step. The capture presenter sets no flag and hands its lines to the director.
- **F2-R4** Opcodes `0x4C` to `0x4F` are ported by their binary size and never give the hand back; `0x50` sets the close mode; `0x51` is a latch; `0x39` waits for the release.
- **F2-R5** Sounds 6 at the opening, 7 at the close trigger and 79 + voice on the characters of even rank, through the world's sound player given to the director at the install.
- **F2-R6** The presenter receives the page typed so far (glyphs in font3, line breaks); the director holds the whole page for `CurrentLineForTests`.
- **D-E19-77** The pins of the arcs that the faithful box moves are written by a simulation before the change (annex of values), and the box is tested against an oracle (`AlundraTextBoxOracle`) written from the model, never from the director.

## Consequences

- Boxes last as long as in the binary: the arcs' frames, budgets and the scripts' waits on a box move (annex F, G); no export change.
- The recipe stays "the box, then the scripts of the tick": a script that writes `0x4C` to `0x51` acts at its own tick (entity) or the next (map event).
- The box on screen shows the typed prefix only; the frame, slides, three font3 lines, scroll and cursor are drawn by E19.f2b.
- The release clears `MessageBox` and `MenuOpen` also during the save screen's states (risk R2 of the annex): nothing reads it.
- Rolling back is a revert of the commit (DLL and tests only).
