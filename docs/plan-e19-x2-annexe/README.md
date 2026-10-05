# X2 value annex (one fresh sound system per BGM track)

Written in advance by the value audit of 2026-10-05 (session scratchpad `x2-audit/`) and checked by an independent verification
(`x2-audit-verify/verify.md`). The oracle is the fixed batch's own output: two independent derivations agree byte for byte on all
45 written tracks (A: the extractor's own render and WAV writer called with fresh objects per track in one process; B: the unchanged
`--render-bgm`, one process per track); the same harness in "today" mode reproduces today's `data-extracted/sound/bgm/` and
`bgm.json` exactly.

| File | Content |
|---|---|
| `oracle_sha256.tsv` | Per track: file, bytes, SHA-256 of the fixed render, today's SHA-256, changed or not (track 1 unchanged; 44 changed; track 44 not written) |
| `tracks.tsv` | Per track: today's and new `Frames`, `LoopDetected`, `FirstAudibleFrame`, the driver first-note frame, peaks before it, the extent of the difference |
| `predicted_extract_changes.txt` | Exact `diff -rq` of the re-extraction against `data-extracted/`: 45 `M` (`sound/bgm.json` and 44 WAVs) and 1 `D` (`sound/bgm/bgm_044.wav`) |
| `predicted_export_changes.txt` | Exact export manifest change: 47 `M` (44 `Musics/*.wav`, `Musics/bgm-manifest.json`, `AssetInfos.json`, `report.json`) |
| `export_values.tsv` | Today's and predicted SHA-1 (and SHA-256) of each exported file that changes |

New `sound/bgm.json` SHA-256: `c1e9d6853c8ef04a9d933b42794e7cee2fc077f9f5b558dc8ab8a1083664e641`. `report.json` counters that move: `Audio.Bgm` 46 → 45,
`Audio.WavCopied` 1042 → 1041, `Verify.Assets` 22417 → 22416, `Verify.ExistenceChecked` 2391 → 2390, `Verify.ExistenceChecked.wav`
1042 → 1041, plus the size metrics. `alundra-project/Musics/bgm_044.wav` stays on disk, unreferenced (`AudioWriter` never deletes).
