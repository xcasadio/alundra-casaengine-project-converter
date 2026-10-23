# E20.g5: read-only discovery of 2026-10-23 (the true pushables)

Provenance: read-only discovery of 2026-10-23 at HEAD `46589b86` (branch `chantier/theme1-porter-plateforme`), asked by the author's
answer D-E20-63 (the true pushables enter theme 1). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`,
disassembled with capstone and run in the MIPS machine of `../../e20-g1a/work/`. The real DLL was measured in a built copy of the HEAD
outside the repository (`probe/`, not kept here: it holds full copies of `alundra-project/`). Nothing in the repository was changed.
Plan: `docs/plan-e20-heros.md` §1.24.6. `SHA1SUMS.txt` lists the files.

- `report.md`: the discovery's report (French), verbatim.
- Census: `census_g5.py`/`.txt`/`.json`, `census_signature.txt`, `census_export.py` and its output, `sensors_g5.*`, `pillar_context.*`,
  `pillar_refs.*`, `progs_g5.*`, `opcode_hist_g5.json`, `op_sites_all.*`, `op_be_sites.*`.
- Binary: `c76_full.py` (model of C76 against the real code: 6 000 vectors, mutants, goldens), `c76_full_out.txt`, `c76_goldens.json`
  (SHA-256 `c80ffbe3def37599c6671f32bf2020153c6b782c235707eceecce09f4ad90d5a`), listings `dis_*.txt`.
- Machine: `g5_scen.py` (scenarios P1 to P11 on the real `MovePlayer`, spawner, C76 and physics), `g5_scen_all_out.txt`, `out/`.
- DLL: `probe_G5ProbeTests.cs.txt` (the probe tests run in the copy), `cases*.json`, `probe_out/` (HEAD), `probe_out_proto/` (with the
  throw-away prototype `prototype_PillarAi.cs.txt` and `prototype_registry.diff`: proof of feasibility, not a patch), `make_probe.ps1`.
- `work-delta/`, `g2c-delta/`: the 0 files of the copied machine and scripts that differ from `../../e20-g1a/work/` and
  `../../e20-g2c/disc/` (214 were byte-identical and are not copied).

The SHA-1 in `SHA1SUMS.txt` are those of the working-copy files when this folder was committed (outputs written on Windows may carry CRLF
there; git stores LF with `core.autocrlf=true`): compare with line endings normalized.
