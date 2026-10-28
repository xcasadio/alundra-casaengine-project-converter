# E20.h4d1 - T0 (b): the keys the slice uses

All in `critic/keys_faithful/` (made by `critic/c/h4o/fire2_key_c.py` and `fire2_targets_c.py`, `FAITHFUL=1`, `--pf`), each replayed twice from a copy by `t0/replay/replay_keys.sh`: the two runs are identical (`diff -rq` empty, `replay/manifest_run1.sha1` = `replay/manifest_run2.sha1`) and each file is identical to the filed one (`sha1sum -c critic/SHA1SUMS_keys_faithful.txt`: 20 of 20 OK).

| Key | Status | Tests (plan) | SHA-1 of the `.json` |
|---|---|---|---|
| `fire2_cast_pf` | replaces `disc/keys/fire2_cast_pf` (REFUTED by R-EL-1) | (w1), (w8) | `b6c7fa10792dd1d0cdaef228358b910eeff8b7ef` |
| `fire2_hurt170` | replaces `disc/keys/fire2_hurt170` (REFUTED) | (w5) | `98c60727579f3d83d54d74ea7233ac3e19f8bbf8` |
| `fire2_targets_pf` | replaces `disc/keys/fire2_targets_pf` (REFUTED) | (w6) | `e9b620f76679c70a25c2db7b8b9f9def81902f5b` |
| `fire2_cast_preset_block` | replaces `disc/keys/fire2_cast_preset_block` (REFUTED) | the preset round trip | `b7568fb3be213f9a06c034622c0ec19a7e48fff4` |
| `fire2_hurt60` | kept by the plan; the faithful remake is the one to read (see section 3 of results.md: 9 rows of the discovery file differ at the birth of the column) | (w2), (w10) | `9ec3ca2a5f1721bd8268c6c9c30bb37db1cce2bc` |
| `fire2_hurt20` | kept; identical to the discovery file row by row | (w3) | `270bd68aaee8bba28a1420f57724840c67d30b1f` |
| `fire2_hurt20_zerobuf` | kept; identical to the discovery file row by row | (w3) | `3d2d3980234298cac08bbb01ba0cc6e5cda6501a` |
| `fire2_hurt20_stale` | kept; identical to the discovery file row by row | (w3) | `10f16a68bdc600a0464684ef17958463743f218e` |
| `fire2_cast_preset_limit0_pf` | ADDED (the preset with zero limits `[1, 0, 0, 7, 8, 0, 0, 1, 1]`) | (w4) | `fc32adf7d8ebac0812bc63b58d1de8b3955076b2` |
| `fire2_targets_edge_pf` | ADDED (the edge pairs (200,100) (201,100) (100,186) (100,187)) | (w6) | `9e9a0bcccd5544d3dedd2dcf5dee1661498897f2` |

Retired without a replacement: `fire2_cast_nopf` (the discovery key without the frame-end step; no test uses it, revision 2 of the plan). The discovery keys of `disc/keys/` stay in the annex as history (REFUTED for the four first rows).
