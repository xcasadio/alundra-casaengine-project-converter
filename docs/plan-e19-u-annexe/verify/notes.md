# O-E19-78 counter-check - working notes

Repository `chantier/e19-suite2` HEAD `486fdf5`, engine `f86a981b`, MGUI `18b2c14a`, NvgSharp `9c0da031` (read-only use: git archive
+ tar copy of the export, Musics included). Physical copy `repo/` built by `setup.sh`. Nothing written in the repository, in
"Alundra Remake/" or in data-extracted/. F = fact (file:line at HEAD, address, measurement), H = hypothesis.

## Files
- `setup.sh` physical copy; `apply_mine.py` variants (head / cand = the discovery's `candidate-pending.diff.txt` applied with GNU patch,
  `--seams` the two test seams, `--probe` copy-only counters); `run.sh head|cand probes|suite`.
- `VerifyO78Probes.cs.txt` my probes P1-P4 (copy only); outputs `out/{head,cand}/p*.txt`; trx in `results/`.
- `lib/` (lib.py, d.py, emu.py from the g3d annex; `xr_jal.py` mine); `my-dis-*.txt` my disassembly; `xref-box.txt`.
- `bin_repro/` the discovery's binary scripts copied (reproduction) + `my_window.py` (mine: real UpdateWorld, hook on RunMapEvents,
  new schedules); outputs `bin-repro-window.txt`, `bin-repro-box.txt`, `bin-my-window.txt`.

## Binary (own disassembly, ALUN_CD.EXE France)
- Main loop: RenderScene `jal 0x8002BD60` at 0x8002C3FC, then Update(0) `jal 0x8002BAEC` at 0x8002C404 (a0 = 0). F.
- Update(0): pad sampler `jal 0x8002E38C` at 0x8002BB5C; UpdateWorld `jal 0x8002E058` at 0x8002BC4C; warp delay 0x800DC4B4 decrement
  0x8002BC54-0x8002BC68; trigger: `lw 0x800DC4B8` whole word at 0x8002BC70 (bnez -> skip), 0x80127D50, 0x80127164, pressed `& 0x803` at
  0x8002BCAC, delay == 0 at 0x8002BCB8, held `& 0x100` at 0x8002BCD0, 0x800C4980 == 0 at 0x8002BCDC, `jal 0x80055570` at 0x8002BCEC. F.
- UpdateWorld: RunMapEvents `jal 0x8003C67C` at 0x8002E100, UpdateEntities `jal 0x8003B388` at 0x8002E108, effects `jal 0x8003C410` at
  0x8002E110. Each reads `0x800DC4B8 & 0x48` at its head (0x8003C680/0x8003C69C, 0x8003B38C/0x8003B394, per slot 0x8003C454/0x8003C45C). F.
- RenderScene: ... `jal 0x8005EC98` 0x8002BE54, dispatcher `jal 0x80048054` 0x8002BE5C, `jal 0x80044C5C` 0x8002BE64. F.
- Release 0x8004501C: frees the slot (`jal 0x80047CB0`), `ctrl &= ~0x18` (0x80045038-0x80045048). F.
- Box openers `jal 0x800450F0`: 0x80042464, 0x8007BA00, 0x8007C320, 0x8007C684, 0x8007C6D8, 0x8007FDF8; OpenSlot 0x80047F94 inside the
  inventory code range 0x80052000-0x80058400 only with a0 = 4 (0x80052628) and a0 = 6 (0x8005562C). The inventory never opens slot 0/3. F.

## Port runs (my copy)
- Red-first tests (discovery's file): HEAD 7/7 red, candidate 7/7 green. F.
- Full Alundra.Tests: HEAD 3214/3214, candidate 3214/3214 (Musics copied), no outcome difference (trx.py). F.
- Six trace files after the candidate run: identical to HEAD modulo EOL. F. But neither harness drives AlundraWorldProxy.Update or the
  inventory (IntroTraceHarnessTests mirrors the passes by hand, :588; HeroTraceHarnessTests has no AlundraWorldProxy): no evidence value. F.
- P1 tick-rate invariance (frozen map-event ticks relative to the open tick, Start held during the whole frame):
  HEAD 0..41 / 0..47 / sub 0..125; candidate 1..42 / 1..48 / sub 1..126, identical at 1, 2 and 3 ticks per frame. F.
- Binary on the SAME new schedules (`my_window.py`, real UpdateWorld): 1..42, 1..48, 1..126; and 1..37 for close 19. F.
- P2 end-of-frame flags + inventory/sub/post state per frame (open, L1 19, L1 56, close 94; 1 and 2 ticks/frame): HEAD == candidate. F.
  -> the entity scripts (gate read at frame start, AlundraEntityScriptProxy.cs:1193) and AlundraGameplayFreeze (end-of-frame flags) are unchanged.
- P3 save screen (StartFailure flow) inactive after frame 47 (flags 08 -> 00 in the same frame): Start at 47: HEAD lost, candidate opens;
  Start at 48: opens in both. Binary side not measured (H).
- P4 map event starting a warp departure (BeginDepartureForLoad) on the frame whose sample carries Start: HEAD inventory opens, MenuOpen,
  departure never called (map events frozen); candidate departure starts, inventory refused (TryTrigger's IsTransitionInProgress guard,
  AlundraInventoryDirector.cs:419). Without Start: departure in both. F. Not listed by the discovery.

## Processes
- My builds use --disable-build-servers -nodeReuse:false UseSharedCompilation=false; no process referencing this folder remains. A
  VBCSCompiler (11:32:43) and nodeReuse:true MSBuild nodes (11:33:20) seen running are not mine (their flags differ); untouched.
