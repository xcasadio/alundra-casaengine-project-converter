# E20.eR: the map header byte 9 exported as `TileCutEffectId` (plan `docs/plan-e20-heros.md` §1.27a)

Provenance: read-only preparation of 2026-10-22 at HEAD `1d0b6953` (parent) and `fc16872` (analyser), with a complete dry run in the
session scratchpad (nothing written in the repository, the remaster or `data-extracted/`). Committed with revision 1 of the plan, before
any change. `SHA1SUMS.txt` lists the files.

- `report.md`: the preparation's facts (French), verbatim.
- `dry_run.sh` (scratch name `dry-run.sh`): the dry run. `git archive HEAD` of the analyser, the rename applied in the copy only, a Release
  build, the extraction into a scratch folder, `diff -rq` against `data-extracted/`. `S` is an absolute scratch path: rerun it from a copy
  whose `S` points to a new empty scratch folder.
- `verify_extraction_diff.py` (scratch name `verify_rename.py`): every line of the diff must be one `data/tiled/map_N.tmj` (N = 0..482,
  once each), and each new file must equal the old one once the property name is put back.
- `make_lists.py`: builds the two lists below from the dry-run diff and the prediction.
- `expected_extract_changes.txt`: the 483 `M data/tiled/map_N.tmj` lines, sorted, LF (SHA-1 `1ae6a9cba062e96effaa535379276ba305c03aa6`).
- `predict_export.py` and `predicted_export_changes.tsv`: the export prediction, a header and 966 rows (483 `.tmj`, 483 `.tileMap`) with
  the SHA-1 before and after of each file, +3 bytes each (163 103 bytes, SHA-1 `bc3730b889540fdcd63b5f2e45acddadaa460f88`).
- `extract_stdout_dry_run.log` (scratch name `extract-stdout.log`): the console of the dry-run extraction, the reference for T3.
- `manifest.py`, `diff_manifests.py`: copied from `../e20-e0x/` (SHA-1 manifest of `alundra-project/`, without `Alundra.dll`,
  `Alundra.pdb` and `.casaeditor/`).

The SHA-1 in `SHA1SUMS.txt` are those of the working-copy files when this folder was committed. The script outputs written on Windows
(`extract_stdout_dry_run.log`) carry CRLF line endings there, the other files LF; git stores LF (`core.autocrlf=true`). Compare with line endings
normalized, or check out with the same setting.
