# E20.i2 - the two read-only discoveries of 2026-10-25

Deposited by T0 of E20.i2a (docs/plan-e20-heros.md section 1.14.2, "E20.i2a"), 2026-10-27. Source: the lecture at HEAD `8174c0cb`
(`Alundra/` unchanged up to `5e67722d`) and its fresh critique. The critique's values stand where it corrects the lecture.

- `reading/` - the lecture: `report-main.md`, the scripts (`s0_header.py` to `s9_belt_target_push_probe.py`, `bx.py`), and their outputs
  (`out_*.txt`: the scans of the displacements `0x188` and `0x4334`, the callers, the census of the belts, the drift tables, the belt and wall probes, the portal census).
- `critic/` - the critique: `report-main.md`, its scripts (`c1` to `c10`, `r_s5` to `r_s9`, `s5_belt_probe.py`, `bx.py`) and their outputs (`out_*.txt`, among them the maps 14, 72 and 416 grids).

Not deposited (copies of repository files or caches): `reading/head/` (the DLL sources at the lecture HEAD, extracted by `git show`),
`reading/w1-ref/` (identical to `e20-d/oracle/out-swim/w1`, `diff -rq` empty, checked at T0), `oc/base/` (a copy of the `e20-d` harness), every `__pycache__`.

To replay the probes: copy the scripts next to a copy of `docs/plan-e20-annexe/e20-d/oracle/` named `oc/` (so that `oc/base/` holds the harness), then
`python -B -I <script>` (capstone is added to the path inside each script; `s1_scan.py` and `s3_callers.py` take their addresses as arguments, e.g.
`s1_scan.py 0x188`, `s3_callers.py 0x80037488 0x800373e4 ...`; the outputs `out_*.txt` are their redirected standard output; `s8_portal_deep.py` writes `out_portal_deep.txt`).
The replay of T0 of E20.i2a is recorded in `docs/plan-e20-annexe/e20-i2a/README.md` (b).
