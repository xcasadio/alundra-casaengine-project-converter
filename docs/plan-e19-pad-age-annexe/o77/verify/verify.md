# O-E19-77 counter-check (fresh context, 2026-10-07)

Verdict: **CONFIRMED (no pad-age gap on the inventory paths).** Nothing in the repository was built, edited, staged or committed;
its only dirty entries (engine `CasaEngine.Launcher/Program.cs` 2026-10-01, analyser `AlundraTools/log.txt` 2026-10-03) predate
this check. All runs below were made in the discovery's physical copy `../repo/`.

## 1. Binary, own disassembly (`lib.py`/`emu.py` copied from `docs/plan-e19-pad-age-annexe/`, `d.py`, `xr.py`, `scanw.py`, `wref.py`)

- Main loop `0x8002C3F4`: `jal 0x8002AC2C`, `0x8002C3FC jal 0x8002BD60` (RenderScene), `0x8002C404 jal 0x8002BAEC` with a0=0
  (Update(0)), `beqz` back to `0x8002C3F4` at `0x8002C45C`. Sampler `0x8002E38C` xrefs: `0x8002BB5C` (Update, unconditional, at its
  head), `0x8002C490` (warp transition loop), `0x8004F2D8` (a wait-for-key loop). One sample per frame, in Update(0).
- Pad block `0x80126F18`: +0x10 held, +0x12 pressed, +0x16 by-interval (pad update `0x8002E250`; first delay 20, repeat 0).
- Dispatcher `0x80048054` called only at `0x8002BE5C` (inside RenderScene): 13 slots of 0x1C from `0x80153028`, callback at +0x14,
  then the post-process tail (`0x80052618` / `0x80055570` at `0x800481D4`). Setup `0x80054F1C` writes flag 5 and installs
  `0x80056598` at `0x80055554-0x80055564`.
- Trigger: `0x8002BCA4 lhu 0x12` & `0x803`, `0x8002BCC8 lhu 0x10` & `0x100`, after `jal 0x8002E058` (UpdateWorld) at `0x8002BC4C`.
- Slot 6: by-interval reads re-read at `0x80056784` (lui in the delay slot of `0x800565DC`, which my linear scan also missed),
  `0x800567C8`, `0x8005680C`, `0x80056870`, `0x800568D4`, `0x8005691C` (& `0x813`), `0x80056948` (& `0xC`). Slot 4: all six
  (`0x8005354C` ... `0x80053658`) resolve to `0x80126F2E`.
- `0x800555B8` (held) is skipped when `0x800A82B0` != 0; set to 1 at `0x8004E85C` (reached from `0x8002C03C` via `0x800815C4`); the
  only zero write `0x80048514` sits behind `0x8009A858` != 0, which is 0 in the image and has no store found. Dead in play (and in
  Update anyway).

## 2. Runs (`my_bin.py`, `O77VerifyProbeTests.cs.txt`, `my_cmp.py`)

Own binary driver: the real dispatcher, then the WHOLE real Update(0) (UpdateWorld and `0x8004B1D4` no-ops; drawing stubs from the
discovery's list). Own port probe: real `AlundraWorldProxy.Update` with elapsed = `FixedTickSeconds`, one tick per frame asserted.
Comparison of the FULL state per sample (flag6, sel6, flag4, sel4, post, MenuOpen, sounds): binary after RS(s+1) vs port after tick s.
10 schedules (8 of the 21 incl. close/reopen P+37/P+38, sub Right P+55/P+56, back-to-main Down P+94, held repeat; 2 new: sub
Up/Left/Triangle then L2 reopen; Triangle close with Start+Left held across the completion): **10/10 EQUAL** (`cmp-today.txt`).
Mutant M2 (`O77MUT=2`): **10/10 DIFFERENT** (`cmp-m2.txt`: Down P+18 taken, every reaction one sample late, P+38 reopen lost), and
the 3 `AlundraInventoryPortraitWiringTests` pins go red (9/9 green at baseline).

## 3. Adjacent observation

- Order difference: **F** (binary RS(k) slot 0 -> UpdateWorld(k) -> trigger; port loop 1 trigger/per-frame -> loop 2 Pass, save
  screen, map events; `0x8004501C` clears bits 0x18 of `0x800DC4B8`, the word the trigger tests at `0x8002BC70`; the port clears them
  in `Released()` inside `Pass`).
- Its stated consequences (Start on the box-closing sample; map event vs trigger on one tick): **H**, unmeasured.
- New, measured: the map-events freeze window is one tick early at both ends. Binary: MenuOpen set in RS(P+1), cleared in RS(P+38)
  (`menuopen_phase.py`); RunMapEvents `0x8003C67C` skips on `& 0x48`, so it is skipped in Update(P+1..P+37). Port (copy-only counter
  `instr.py`, `port-mapevents.tsv`): skipped on ticks P..P+36, runs again at P+37. Entity scripts run before the proxy, so they are
  probably not shifted (H, not measured).
  This is frame order, not pad age, and belongs in its own open point.
