# E19.g G1/G3 discovery annex (effects: export, reservoir, opcodes)

Discovery of 2026-10-06, read only, two surfaces, each counter-checked by a fresh reviewer. The plan section is
`docs/plan-e19-opcodes.md`, section 1.2o.6. Nothing in the repository was built, exported or changed by the discovery.

| File | Content |
|---|---|
| `binary-data-notes.md` | Surface A (binary and `DATAS.BIN`): reservoir, opcodes, load, lifetimes, census, emitter, scenarios. |
| `binary-data-verify.md` | Counter-check of surface A, re-derived from its own disassembly, parser, sweep and probes. |
| `converter-notes.md` | Surface B, converter: export format proposal and invariants. |
| `dll-notes.md` | Surface B, DLL: where the stage lives, per-tick loop, push, tests that move, native producers. |
| `engine-notes.md` | Surface B, engine: free quad, effect quad service, sort key, sheet alpha counts. |
| `split-notes.md` | Surface B, proposed slices and their acceptance. |
| `port-verify.md` | Counter-check of surface B (corrections C1 to C10 below). |
| `fxpool.py` | Python model of the reservoir, fed by the `data-extracted` JSON (checked against the real code). |
| `anim_table.tsv` | Lifetime of every animation (end kind, frame delays, destroy and free ticks). |
| `anims_oracle.json`, `load_oracle.json` | Oracle values from the real binary: per-animation timeline, per-map load slots. |
| `scenarios_out.txt` | Pinned scenarios (476 aura, 391, 163 rays) from the real code. |
| `predict.py` | Independent prediction of the G1 export counts from `data-extracted`. |

The notes cite their scratch folders (`../dll/notes.md`, `data/…`, `binary/…`); those scripts were not versioned. The
facts that carry the plan are restated in the plan section with their proof.

## Corrections that win over the notes

- **O-E19-71 is decided** (D-E19-92, screen resolution, ADR-0048 of the engine not revised). Every "waits for O-E19-71"
  in the notes is stale.
- **The 476 reference frame** of `engine-notes.md` is not an oracle: it uses an integer texel sample, while the
  hardware-verified rule is `floor(u + 0.5)` (G2b annex). Only interior probes (160,120), (150,100), (170,140) survive;
  (200,150) becomes (8,0,24).
- **Map 391** has five `0xA2` (pc 228, 236, 244, 252, 408), not four; the pool ends with 10 slots.
- **Delays**: the JSON `Delay` is `0x80 | ticks`; the export writes `Delay & 0x7F`.
- **Depth slot**: `16 * min(key >> 20, 59) + 6` with an arithmetic shift; negative keys clamp at 0 (no corpus operand
  produces one).
- **Scripts reaching native creators**: `0x82 0x53` (8 sites, maps 134, 258, 298, 302, 398) and also `0xBB` (retry or
  game over, maps 347 and 477, E18).
- **Modes**: records and scripts create mode 0 only; modes 1 and 3 come from 6 native sites.
- **Map 10** also spawns global effects 12 and 3 (deformed) through `0x90`; 135's `0xA3 [1]` is global 13.
- **Hero before load spawn**: established; the hero entity and its tile exist before `0x8003C1A4`.
- **Mirrored effect quads** (3762 of 20 269 references) need the no-face-culling path of the free quad.
- **`AlundraWorldProxy.cs` line numbers** in `dll-notes.md` are 4 lower than at `731dcab`.
