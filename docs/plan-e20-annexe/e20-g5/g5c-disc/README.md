# E20.g5c: read-only discovery of 2026-10-24 (StopSoundEffect: opcode 0xBE, opcode 0x75 and the end of C76)

Provenance: read-only discovery of 2026-10-24 at HEAD `b36a78a7` (the sound code is unchanged up to `1a98ef5d`), after the author's answer
D-E20-67 (the stone scraping stops as in the original). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
The real DLL was measured in copies of the HEAD outside the repository (`probe/`, `probe2/`, not kept: their engine and export folders are
junctions to another scratch copy); the two full `.trx` runs are not kept either (`cmp_trx_out.txt` compares them). Nothing in the repository
was changed. Plan: `docs/plan-e20-heros.md` §1.24.6, E20.g5c. `SHA1SUMS.txt` lists the files.

- `report.md`: the discovery's report (French), verbatim.
- Binary: `stop_oracle.py` (StopSoundEffect `0x80049634` and the Play facade against the real code: 3 000 sequences, 0 mismatch, mutants,
  goldens), `stop_oracle_out.txt`, `stop_oracle_mut_out.txt`, `stop_goldens.json` (SHA-256
  `f75208112cbd5b0dba1635b0544d2ed7e63734c89c14de1f8fdb1023908d3e06`); `handlers_oracle.py` and its output (the handlers of `0xBD`,
  `0xBE`, `0x12`, `0x75`); listings `dis_*.txt`; tools `disasm_range.py`, `callers.py`, `refs.py`, `tab.py`, `lib.py`, `work/`.
- Data: `sites_stop.py`, `sites_stop.json`, `sites_stop_summary.txt` (392 `0xBE` sites on 119 maps, 12 on the story chain), `classify_sites.py`
  and its output, `families_sites_out.txt`, `vab_adsr.py`, `vab_release_all.py`, `vab_release_all_out.txt` (the release times read in
  `SOUND.BIN`), `decode_events.py`.
- DLL probes: `prototype_stopsfx.diff`, `prototype_pillar_wiring.diff` (proofs of feasibility, not patches), `G5cProbeTests.cs.txt`,
  `G5cWorldProbeTests.cs.txt`, `cmp_trx.py`, `cmp_trx_out.txt`, `make_probe_dst.ps1`, `extract_docs.ps1`.

The SHA-1 in `SHA1SUMS.txt` are those of the working-copy files when this folder was committed (outputs written on Windows may carry CRLF
there; git stores LF with `core.autocrlf=true`): compare with line endings normalized.
