# E20.g, T0 common (docs/plan-e20-heros.md section 1.24.0): the oracle, the 20 mutants, the probes of the DLL

Written 2026-10-24 by the resumption executor of E20.g1a, after the verifier found the T0 missing (finding F1). Binary `ALUN_CD.EXE` (France), SHA-1
`a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked), read at `D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`.
Nothing in the repository (DLL, engine, converter, data) was changed by this T0; the probes of the DLL were temporary tests, removed.

## `oracle/`: the tools, byte for byte, and what was added

- Copied unchanged from `../e20-g1a/work/orc/` (the reading agent's oracle, 2026-10-17): `gl.py`, `ana.py`, `k0lib.py`, `exe.py`, `exelib.py`, `h.py`, `jt.py`, `g1a_models.py`, `oracle/` (the machine, 40 files), `emu_h/`, `g1_lift.py` to `g18_c4_stone.py`, `run_all.py` and the other survey scripts;
  and from `../e20-g1a/critic-scenarios/`: `c1_equality.py`, `c2_platform.py`, `c3_contact.py` (the critic's three scenarios; its `gl.py`, `ana.py`, `k0lib.py`, `oracle/` are the same bytes as the reading's). `SHA1SUMS.txt` lists every file; `cmp` against the originals gives no difference.
- Added (new files, no scenario changed): `run_json.py` (runs a scenario unchanged and writes the rows of every `gl.run` call as `out/<script>.json`, keys = the names of `HFIELDS` and `CFIELDS` of `gl.py`),
  `run_t0.py` (the 23 scenarios, then `out/manifest.txt`), `c4_climb_lift.py`, `c5_status4.py`, `run_mut.py` and `mut_t0.py` (the mutants).
- `out/`: the text outputs and the JSON (`*.json.gz`: `gzip -n -9`, the manifest hashes the uncompressed JSON). Rerun from a COPY of `oracle/`, never in place: `python -B -I run_t0.py manifest.txt` (about 4.5 minutes).

## Two complete runs, identical manifest

Two runs in two fresh copies (2026-10-24): `out/manifest.txt` of 84 lines (SHA-1 of the 60 text outputs and 24 JSON files), `cmp` identical. The text outputs of g1..g18 and of c1, c2, c3 are also byte-identical to the
ones of 2026-10-17 and 2026-10-18 (`../e20-g1a/work/orc/out/`, `../e20-g1a/critic-scenarios/out/`): the JSON was added without changing a scenario.

## `c4_climb_lift` (written for this T0; prediction written before the run: lifted, `anim_t` 5 or 11)

Square pressed in the climb state (0x0E, `anim_t` 14; the binary calls `TryUseItem` then `PlayerTryAction` at `0x800321D4`-`0x800321E4`). The climb never touches a crate by itself (UP|RIGHT stays a walk, UP alone climbs without a step into the
crate), so the contact word `hero.+0x130` is the ONE poke of the scenario (documented in the script). Results (`out/c4-climb-lift.txt`):
A, climb at z 6 px, contact: lifted at the Square tick, `anim_t` 14 to 11, `hero.+0x2C` = the crate; B, same Square without the contact: nothing lifted, the climb goes on (`anim_t` 14); C, climb at z 14 px, contact: lifted, `anim_t` 11;
D (reference), idle on the ladder with the contact: lifted, `anim_t` 5. The prediction holds: 11 in the climb (feet off the ground), 5 on the ground; (m8) of the plan uses it.

## The mutants: 20 one-word mutants of the REAL code, all killed

`mut_t0.py` patches one word in the machine's RAM after `gl.world` builds it and compares every text output of the scenario with the baseline (a crash counts as a kill). 20 mutants (the 16 named by the plan, with the genre test, the
`GetNumberOfItem` call and its branch, the target, the step and the guards split in two). Result in `mut_result.txt`: 20 killed, 0 survivor, 0 equivalent to argue. M06 (`sltiu 2 -> 3`, status 4 accepted by the loop) survived the 22 scenarios,
because a status-4 entity never has `+0x12C == hero` when the loop runs in a whole tick (the tick clears it); `c5_status4.py` calls the real `PlayerTryAction` directly: status 2 and 3 lift, 4 and 5 do not, and the mutant lifts for 4.
M08 and M20 are killed by a crash of the machine (`StopIteration`, an access outside the RAM), M12 by the fuzz `g10` only.

## Probes of the DLL (temporary tests, run on the end state of g1a at `203b2632`/`b0a39aa9`, then removed; predictions written before)

- P-G-1: a crate of the map 392 (bank 36 of the export, `Caisse en bois g\u00e9n\u00e9rique`) in animation 0 has no attack box. Measure: `FrameBoxTimeline` of the four directions: animation 0 (10 ticks) 0 ticks with a box; 1: 11; 2: 0; 3: 2; 4: 2. Holds.
- P-G-4: the Z raw of a crate let go by C23 with the code of throw 1 (1, 2 and 3 ticks per frame, hero at Z 0): `crate.PosZ - hero.PosZ` = 2097152 = 0x200000 before and at the tick of the release and the 3 ticks after, no loss of a unit, marker `HasLogicalZ` up,
  flags 0x3323E4 to 0x332374, animation 3, links cleared. Holds (the stop condition of g1a is not triggered). Note: in this montage the vertical force stays 0 for these 4 ticks (the throw force is g2a's, MovePlayer writes it).
- P-G-5: the hero landing on a platform that moves west (1, 2 and 3 ticks per frame): `IsOnGround` 1 at the landing tick, `RidingEntity` = the platform at the next tick. Holds, as `c2`.
- P-G-2 and P-G-3 are for g2a (the throw code of `MovePlayer`, `BoxDirection` at the throw, the speed rule): they cannot be run before g2a exists; they stay in the T0 of g2a.

## Owed (not g1a's)

The census of the maps of the recipe (the preset `porter-m<N>`), the "start counts" and the predicted list recounted at the starting HEAD for g2a, g2b, g1b and g2c: the g1a part is in `../e20-g1a/exec/README.md`; the rest belongs to the T0 of the slice that uses it.
