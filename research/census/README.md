# Opcode wait censuses (archived research scripts)

**Status: archive, not a tool.** These Python scripts statically model every scripted walk and wall wait of the
exported corpus, to find the sites where porting a waiting opcode could freeze the game. They were written in
scratch space during slice E19.d and the discovery of slice E19.d2 (2026-10-01 and 2026-10-02) and are kept here so
they are not lost. Their totals are **not authoritative**: the model has known defects (below), and the proof of a
port is always the arcs and tests that run the real DLL. See `docs/plan-e19-opcodes.md`, open point O-E19-25.

## Reports

- `docs/census-0x24-waits.md` — census of the `0x24` (wait for `ForceAdjusted`) sites, corrected model (D-E19-24).
- `walks-0x0b/census0b.md` — census of the `0x0B` and `0x1E` walks under three collision rules (DLL, binary, binary
  plus entity obstacles), E19.d2 discovery.
- `walks-0x0b-check/census0b-check.md` — independent re-derivation of that census, which found the defects listed
  below.

## Layout

- `waits-0x24/` — the `0x24` census.
  - `m.py`: map loader (exported events and tile maps), opcode sizes and `Dispatch` cases read from the DLL sources.
  - `census_exec.py`: first model (superseded: its stop, O-E19-16, came from wrong start positions); `report.py`,
    `story.py` and `lin.py` belong to it.
  - `gstatic.py` → `gstatic.pkl`: static facts of the corpus (flag setters, `0x53` operands, `0x2D` activations,
    portals).
  - `census_v2.py` → `census_v2.pkl`: the corrected model (real starts, real hero arrivals, flag dependencies).
  - `report_v2.py`: **overwrites** `docs/census-0x24-waits.md` from `census_v2.pkl`.
- `walks-0x0b/` — the `0x0B` / `0x1E` census.
  - `census_exec_orig.py`, `gstatic_orig.py` and `m.py`: copies of the `0x24` census modules (`gstatic_orig.py`
    writes `gstatic.pkl` here).
  - `geo.py`: walk geometry under the DLL rule and the binary rule.
  - `c0b.py`: the census; environment variables `MODE` (`DLL`, `ORIG0`, `ORIGnc`, `ORIG`, `FIX`, `FIXC`), `OBST`
    (`cert` or `max`), `CLASSOPS`, `JUMPS`, `PASSES`, `TAG`; writes `rows_<TAG>.pkl`, `prov_<TAG>.pkl`,
    `walks_<TAG>.pkl`.
  - `cloud.py` (obstacle cloud), `cellw.py` (cells rewritten by `0x54`/`0x55`/`0x85`), `detour.py` (can the E4.d
    detour route a blocked walker), `tags.py` (ops that change a record's presence as an obstacle).
  - `rep.py` → `table.tsv`, `table.pkl`, `totals.txt`: site-by-site comparison of the runs; `show.py MAP PC`: one site
    in every run.
  - `emu.py` and `mlib.py`: an emulation of the binary's per-tick entity loop; `scene185.py` runs it on map 185.
  - `jdis.py`: capstone disassembly of `ALUN_CD.EXE`.
- `walks-0x0b-check/` — the checker's own scripts (decoder, population, reachability, oblique slide, flag flow,
  start provenance).

## Running

- Python 3; `capstone` for the disassembly and emulation scripts.
- Inputs: the exported project `alundra-project/` (not versioned), the DLL sources under `Alundra/Scripts/` (opcode
  sizes, `Dispatch` cases), and `ALUN_CD.EXE` (France). Their paths are written as absolute paths of the author's
  machine (`ROOT`, `EXE`).
- Run each script from its own folder (some resolve `.` or `../binary` relative to the working directory).
- Generated files (`*.pkl`, `*.log`, `table.tsv`, `totals.txt`) are not archived and are ignored by git here.
- The only change made when archiving: four imports that pointed at the session's scratchpad now point at the
  script's own folder (`census_exec.py`, `census_v2.py`, `gstatic.py`, `gstatic_orig.py`).

## Known defects of the model

Fix these before any total is reused or pinned (O-E19-25):

1. `0x62` and `0x63` acting on the walker are not modelled. They change ClassA, ClassB, Gravity, NoObstacleSlide and
   Collidable, in the DLL and in the binary. At least 7 "blocked in both" sites are artefacts (for example 15 `@722`),
   and 16 "not reached" sites follow from them.
2. The binary rule slides only cardinal walks; the binary has slide cases for oblique directions too.
3. A start candidate written after the site is used (a "park before self-destroy" `0x64` at (0, 0) on map 1).
4. A site is labelled "rule difference" without checking that the compared runs share the same start.

Other limits, recorded with the reports: no entity contacts in the `0x24` census; cell writes only flagged, not
replayed; jumps other than the four certified ones not modelled; native AI not modelled; the spawn zone filter not
modelled; map 292 does not stabilise after four passes.
