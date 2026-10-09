# D6 - per-frame box proof over the whole corpus (E20.0, D-E20-6)

Labels: FACT (file:line, or binary address + decoded words), INFERENCE (with its basis), UNKNOWN.
Run date 2026-10-09, branch `chantier/e20-heros`. Every number below is in `out/` and is reproduced by the scripts.

## Verdict

**Exact over the whole corpus.** 395 banks (the set `SpriteBankReader.ReadAllBanks` returns), 9620 animations, 30585 frames
with a CollisionData record compared, 30585 identical on all six values, 0 mismatches. No export addition is needed:
the DLL can read the boxes from the exported `collision_keyframes`; D-E20-6 does not take its "else" branch and no
format ADR is needed from D6.

## Method (FACT, all from files read)

- Bank set: replica of `SpriteBankReader.ReadAllBanks` (`alundra-casaengine-project-converter/Readers/SpriteBankReader.cs:273-300`,
  `ReadAllBanks`, and `:302-370` `ReadMapSpriteRecords`): `map_alundra.json` first, then `map_<n>.json` by increasing n (484 files), the first record of a
  bank key is canonical, null records skipped (2658 non-null records in 484 files, 395 distinct banks, 301 with at least one
  collision record). Canonical = first occurrence, as the converter does.
- Export side: `SpriteWriter.ConvertCollisionKeyframes` / `CreateColliderFixture` (`Writers/SpriteWriter.cs:804-899`):
  one Box fixture per keyframe; `Size = (Width, Depth, Height)`; `LocalPosition = Offset + Size/2`; a keyframe is written
  only when the volume changes; a frame without record after a record writes an empty fixture set; the terminator
  (a frame with no image quads, `SpriteFrame.IsTerminator`) and a keyframe landing on the animation duration are skipped;
  an animation with no quad at all is not written (`SpriteWriter.cs:571`).
  Frame start times are the float32 running sum of `(Delay & 0x7F) * (1f/50f)` (`SpriteWriter.cs:124`, `:561`).
- Read-back (`d6_boxes.py`): for each frame with a record, the exported box is the last keyframe whose float32
  `time_seconds` is `<=` the frame's float32 start time (the engine's rule, `Animation2dCompositionSampler.cs:198-211`);
  offset = `local_position - size/2`; the six values must equal the decoded `CollisionData`
  (`OffsetX/Y/Z`, `Width`, `Depth`, `Height`, as the extractor class `FrameCollisionData` reads them).
- Export used: `alundra-project/Entities/**/bank{key}_anim{a}_{dir}.anim2d`, files dated 2026-10-08 20:29-20:30; the only
  converter commit after that (`eb2db6e`, 20:38) is about the music manifest. 9620 anim2d files, names unique, each
  matched by exactly one decoded animation (`out/orphan_anim2d.txt` empty); 5568 of them carry `collision_keyframes`.
  `alundra-project/Entities` is git-ignored, so this is the working-copy export, not a committed one.

## Totals (`out/summary.json`, `out/per_bank.csv`)

| Item | Count |
|---|---|
| map files read | 484 (`map_alundra` + 483 native) |
| non-null sprite records | 2658 |
| banks (canonical) | 395, of which 301 with a record |
| animations (bank x set x direction, non empty) | 9620 (= anim2d files) |
| animations with a record | 5568 (= anim2d files with `collision_keyframes`) |
| frames with a record compared | **30585** |
| identical on the six values | **30585** |
| mismatches | **0** |
| frames WITHOUT a record (index `0xFFFF`) with images, checked: export also says "no box" | 21161, 0 disagreements |
| export anomalies (keyframe without exactly one Box, rotation, profile, non-integral, keyframe off a frame start or off an integer tick, unsorted) | 0 |

Mismatch classes the script knows (each one counted, none occurs): NO_ANIM2D_FILE, RECORD_ON_FRAME_WITHOUT_IMAGES,
FRAME_AT_OR_AFTER_DURATION, TIE_ZERO_DELAY_FRAME_SHADOWED_BY_LATER_FRAME, EXPORT_HAS_NO_BOX, VALUE_DIFF,
EXPORT_HAS_BOX_BUT_RECORD_IS_FFFF. `out/mismatches.csv` has only its header line.

### The check can fail (`mutation_test.py`, `out/mutation_*`)

Real mutations injected into what the script reads back, run separately, each over the full corpus:
swapping Depth and Height on every 997th fixture gives 26 mismatches; OffsetZ+1 on every 991st fixture gives 39;
dropping every 983rd keyframe gives 29 record mismatches plus 20 "export has a box where the index is 0xFFFF". The same
script on the real export gives 0.

## Tick-level proof (`extra_checks.py`, `out/extra_checks.txt`)

For every one of the 9620 animations and every logical tick k from 0 to the sum of the delays T:
box of the frame the binary shows at k (last displayed frame whose cumulative start `<= k`) against the box a DLL gets
from the export with the rule "last keyframe with `round(time_seconds * 50) <= k`": 173408 ticks compared (animations
with a record, or an exported box), 173408 equal, 0 different. The integer tick sum of the delays equals the engine's
`LogicalDurationTicks` rounding for all 9620 animations (`AnimatedSpriteComponent.cs:399-401`).
Frame classes over the canonical banks (A in `out/extra_checks.txt`): 30585 displayed frames with a record, 21161
displayed frames with no record, 9620 transition frames (one per animation, byte0 bit 7 clear, no image, no record);
nothing else exists, so the converter's "terminator = no quads" and the binary's "byte0 bit 0x80 clear" (see below)
select the same frames on this corpus.
Zero-delay displayed frames (`Delay & 0x7F == 0`): 5, all frame 0 of banks 151 (anim 1, down/up/left) and 127 (anim 0,
left/right); none has a record, so none affects D6 (the binary holds such a frame, E19.m7).

## Byte widths and signedness (FACT, binary + data)

Binary `ALUN_CD.EXE`, UpdateAnimation `0x80038AB4`, the block that loads a frame's box (`out/dis_box_load.txt`):
- `0x80038C1C  lbu $v0, 2($s1)` and `0x80038C20  lbu $v1, 1($s1)`, `sll 8`, `addu`: the index is the 16-bit unsigned
  `(byte2 << 8) + byte1` of the frame; `0x80038C2C  ori $v0,$zero,0xffff` + `0x80038C30  beq $a0,$v0,0x80038CB8`: index
  `0xFFFF` goes to `0x80038CB8  sw $zero,0x1D4($s0)` ("no box").
- `0x80038C38  lw $v1, 8($s3)`; `addu $v1,$v1,$a0`; `0x80038C44  sw $v1, 0x1D4($s0)`: the box pointer is table base + index.
- Offsets: `0x80038C48 lb`, `0x80038C58 lb`, `0x80038C68 lb` (bytes 0, 1, 2, SIGNED), each `sll 16`, stored at
  `+0x208`, `+0x20C`, `+0x210`.
- Sizes: `0x80038C78 lbu`, `0x80038C8C lbu`, `0x80038CA0 lbu` (bytes 3, 4, 5, UNSIGNED), each `sll 16`, `addiu -1`, stored
  at `+0x214`, `+0x218`, `+0x21C`.
- HitBox = position + offset: `0x80038064-0x800380B4` (`lw +0x114/+0x118/+0x11C` plus `+0x208/+0x20C/+0x210`, stored at
  `+0x1FC/+0x200/+0x204`).
- Decoded data over ALL 196999 records of all 2658 occurrences (not only canonical): offsets in [-112, 62] (X -112..60,
  Y -79..62, Z -22..48), sizes in [1, 254] (Width 2..178, Depth 1..158, Height 1..254). 0 offsets outside int8, 0 sizes
  outside uint8, 0 negative sizes. Height 254 and negative offsets both occur, so the signed/unsigned distinction is
  exercised by the data and matches `lb`/`lbu`. The extractor reads `sbyte` x3 then `byte` x3
  (`alundra-datas-analyser/AlundraTools/AlundraEngine/DatasBin/FrameCollisionData.cs:12-19`): same widths.
- Index `0xFFFF`: visible in the decoded data. The JSON of each frame carries `CollisionOffset` (the extractor's `short`
  `(TransformIndexHigh << 8) | TransformIndexLow`, `SiFrame.cs`) and the two raw bytes. Over all occurrences: 369006
  real frames, 196999 with a valid index and a record, 172007 with `CollisionOffset == -1` and no record; never an index
  with no record, never a record with `-1`, never an index below -1 (so no index >= 0x8000: the extractor's signed
  `== -1` and the binary's unsigned `== 0xFFFF` agree on this corpus), `CollisionOffset` always equals the raw bytes,
  every valid index is a multiple of 6 (a table of 6-byte records). The 90280 transition frames carry `-1` because the
  extractor's second constructor sets it, not because of bytes; the binary does not load a box for them (byte0 bit 7 clear
  goes to `0x80038D14`, `0x80038BF4-0x80038C04`). The export reproduces the `0xFFFF` frames as "no box" in all 21161
  displayed canonical cases.
- Duplicates: the 2263 non-canonical occurrences of a bank key are identical to the canonical one on (delay, image count,
  record, index) for every frame - the converter's "first occurrence is canonical" assumption holds for boxes.

## How the DLL knows the current frame at a logic tick

- FACT: the animation has no frame index in the DLL or in the engine's public surface; the box timeline of the engine
  (`AnimatedSpriteComponent.UpdateCollisionTimeline`, `.cs:706-734`) follows the REAL-TIME sampler
  (`Update`, `.cs:232-241`), not the logical clock, and the logical clock only raises ends: "the real-time update keeps
  driving the images, the collisions and the authored events" (`.cs:296-297`). So the engine's collision bodies must not
  be used for the hit pass.
- FACT: the E19.c2 logical clock: `AnimatedSpriteComponent.LogicalTick` (`.cs:280`), ticks since the last reset (0 at a
  switch, modulo the duration for a Loop, saturating at the duration for a Once), rate 50 set by
  `AlundraEntitySpawnFactory.SubscribeAnimationEndBridge` (`AlundraEntitySpawnFactory.cs:250`, constant `:218`), cached on
  the proxy as `AlundraEntityScriptProxy.LogicalClockSprite` (`AlundraEntityScriptProxy.cs:162`, set `AlundraEntitySpawnFactory.cs:256`),
  stepped once per logic tick for every entity (`AlundraEntityScriptProxy.cs:1325`) and for the hero (`:1411-1417`) by
  `AlundraFrameSyncPasses.StepAnimationClock`.
- Rule, proven above at every tick of the corpus (`extra_checks.py`, C): `tick = proxy.LogicalClockSprite.LogicalTick`; list =
  `sprite.CurrentAnimation.Animation2dData.CollisionKeyframes` (public, `Animation2d.cs:5`, `Animation2dData.cs:29`);
  the active keyframe is the last one with `round(TimeSeconds * 50) <= tick`; an empty `Fixtures` list, or no keyframe yet,
  means "no box"; otherwise `Width = Size.X`, `Depth = Size.Y`, `Height = Size.Z`, `Offset = LocalPosition - Size/2`.
  No float comparison is needed: the keyframe times are integer ticks (0 anomalies "keyframe off an integer tick").
  This matches the binary's countdown: a frame's delay `d` is loaded at the tick it appears and decremented by later
  ticks (`0x80038BD8-0x80038BE4`, `0x80038C0C-0x80038C10`), so it is shown for `d` ticks, the same cumulative-start rule.
- INFERENCE (not proven here): at the moment of a hit pass the sprite must already be on the animation the proxy has in
  `CurrentAnimationId`. `SyncAnimation` applies a switch at the end of the frame (the kept one-frame lag D-E19-13,
  `AlundraFrameSyncPasses.cs:70` and the `StepAnimationClock` doc), whereas the binary's UpdateAnimation applies it in
  the same tick before the hit pass. E20.c1 must read the box of the animation the proxy asks for (or place the pass after
  the sync); D6 does not decide that, D7 owns the tick order.
- INFERENCE: `8($s3)` at `0x80038C38` is the sprite record's collision table (`4($s3)` at `0x80038B3C` is the frames base).
  Basis: `s3 = *(entity+0x64)` (`0x80038B04`), the record header order (frames pointer, then collision pointer). The disc
  bytes are not read here (see the limit).

## Stated limit

D6 compares the export with the records decoded by the extractor (`data-extracted/data/map_*.json`). It does not reread
the sector bytes of the disc: a decoding defect of the extractor (a wrong table base, a wrong byte order, a wrong entry
in `FrameCollisionPointer`) would give the same wrong numbers on both sides and pass. What narrows that risk without
removing it: the binary's own load code confirms the layout the extractor assumes (16-bit little-endian index at frame
bytes 1-2, `0xFFFF` = none, six bytes, `lb` x3 then `lbu` x3, `<< 16`, sizes `- 1`), every index is a multiple of 6,
no index is invalid, and duplicates of a bank across maps agree. The recettes of E20.c1 remain the net (boxes against
real hits in game). The comparison is also limited to the export of 2026-10-08 (working copy, git-ignored); a converter
change to `ConvertCollisionKeyframes` needs this script re-run on the new export.

## Files

- `d6_boxes.py` - the proof; writes `out/summary.json`, `out/per_bank.csv` (one line per bank), `out/mismatches.csv`
  (empty), `out/class_examples.json` (empty), `out/orphan_anim2d.txt` (empty).
- `extra_checks.py` - frame classes, zero-delay frames, tick-level proof, duration check; writes `out/extra_checks.txt`.
- `mutation_test.py` - sensitivity check; writes `out/mutation_*`.
- `dis_box_load.py` - decoded binary words; writes `out/dis_box_load.txt` (imports `docs/plan-e19-h2-annexe/tools/lib.py`
  by path, never edits it).
- `notes.md` - this file.

## Corrections after the independent check (2026-10-09)

The check of D2 and D6 (`../e20-0-rapports/D2-D6-check.md`) narrows two claims of these notes:
- **Frame delay 0.** The binary decrements the countdown before testing it (`0x80038BE0 addiu v0,-1`, `0x80038BE4 bnez`, `0x80038BE8 sw v0,0xa8(s0)`):
  a delay of 0 becomes -1 and freezes the frame until the animation switches. "A delay d shows for d ticks" holds for d >= 1 only. It does not
  matter for the boxes: the 5 zero-delay frames have no record.
- **Tick-level claim.** `extra_checks.py` compares the export with a cumulative-delay model, not with `UpdateAnimation`, over one period from
  tick 0. What is proven is the mapping from an animation-relative tick to a box. Not proven: the DLL's switch-tick reservation
  (`AlundraFrameSyncPasses.cs:228-245`) against the binary, which shows frame 0 of the new animation, with its box and its new `+0x1C8`, on
  the switch tick itself (`0x80038BAC`); a restart on a direction change (`0x80038B08`); the chain and hold terminators. These belong to
  E20.a and E20.c1.
