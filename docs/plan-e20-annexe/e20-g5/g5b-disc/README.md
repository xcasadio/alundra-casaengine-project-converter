# E20.g5b: read-only binary reading of opcodes 0x72, 0xA9 and 0xAA (2026-10-23)

Provenance: read-only discovery of 2026-10-23 at HEAD `e49cce2b` (no code change up to `2eff6b46`). Binary `ALUN_CD.EXE` (France), SHA-1
`a705f8dab665645af5dfaf8fb348c7926b5caa58`. The real DLL was measured in a built copy of the HEAD outside the repository, with a throw-away
prototype (`prototype_ops.diff`, proof of feasibility, not a patch); the copy is not kept here. Nothing in the repository was changed.
Plan: `docs/plan-e20-heros.md` §1.24.6, E20.g5b. `SHA1SUMS.txt` lists the files.

- `report.md`: the discovery's report (French), verbatim.
- Binary: `tab.py`/`tab_out.txt` (dispatch table), `dis_*.txt` (handlers, the search helper, the dispatch loop), `scan130.*`.
- Model: `ops_full.py` (9 000 handler vectors and 8 899 search vectors against the real code, mutants, goldens), `ops_full_out.txt`,
  `ops_goldens.json` (70 classes, SHA-256 `c4c75a9146eaa239e8d8b3cb38cb4a2c3da82b6f369f080d0a6acc5ae0027dba`).
- Sites: `sites.py`, `sites_all.txt`, `sites_all.json`, `families.py`, `families.txt`, `dumpall.py`, `decode_events.py`.
- DLL probes: `cases_*.json`, `probe_out_list/`, `probe_out_head/` (HEAD), `probe_out_proto/` (prototype), `summ.py`, `series.py`,
  `phase40.py`, `phase40_summary.txt`, `trace_426_proto_key.txt`, `run_full.sh`, `cmp_trx.py`, `cmp_trx_out.txt` (the full suite with and
  without the prototype: no difference; the two `.trx` files are not kept).
- `work-delta/`: the 0 files of the copied machine that differ from `../../e20-g1a/work/` and `../disc/work-delta/` (203
  byte-identical files are not copied).

The SHA-1 in `SHA1SUMS.txt` are those of the working-copy files when this folder was committed (outputs written on Windows may carry CRLF
there; git stores LF with `core.autocrlf=true`): compare with line endings normalized.
