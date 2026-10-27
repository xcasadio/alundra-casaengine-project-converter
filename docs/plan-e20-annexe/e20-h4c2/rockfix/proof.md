# E20.h4c2 rockfix - the patch changes nothing where the rock is not held in the air (T0 (a2), 2026-10-27)

Same scripts, same arguments, with (`--fix`) and without the in-memory patch of E13; md5 of the JSON written (two runs identical, `replay/MANIFEST_all_run1.md5`):

| Scenario | Without the patch | With the patch | Filed key (unmodified binary) | Identical with and without |
|---|---|---|---|---|
| mid-map, hero (600, 480) | `877bb8126673bc57495320b31ded335d` | `877bb8126673bc57495320b31ded335d` | `../critic/keys/cast_2c_pf_mid_x600y480.json` (same md5) | **yes** |
| interruption T = 60 | `dc136586efc74666fb064a3e6400929c` | `dc136586efc74666fb064a3e6400929c` | `../disc/keys/cancel_2c_pf_t60.json` (same md5) | **yes** (the stdout, with the sound journal, also: `ba56358b1a46da87e686cd070390a81c`) |
| interruption T = 100 | `f927708bfa4a828e53c72c2639c735c3` | `f927708bfa4a828e53c72c2639c735c3` | `../disc/keys/cancel_2c_pf_t100.json` (same md5) | **yes** (the stdout: `c94def475c6e38298d29fa0803acfdd7`) |
| cast, hero (100, 100) (the edge rock) | `16bc521d3d47cba4ebf2f7187aba87f7` | `2952e7e586d5301abcb39b5e2ab52049` | `../disc/keys/cast_2c_pf.json` (= without) | no (the rock 11 is held by the edge) |
| plateau byte 16 | `79cd6cab586ccdb2222b7cf28e05cdbc` | `f8f701c68458de84f32ef2d53531f8c7` | `../critic/keys/cast_2c_pf_plateau_hb16.json` (= without) | no (the rocks 8 and 11) |
| plateau byte 17 | `7e4023875584c02ee9cd8438679291bf` | `77cb415fe4132720d497853d441f4805` | `../critic/keys/cast_2c_pf_plateau_hb17.json` (= without) | no (the rocks 8 and 11) |
| interruption T = 170 | `637edb089d5f26ce0832dc63818cce1a` | `6db56749fdfaa087352b0f5735fc95b0` | - | no (the edge rock in the air is cancelled: one more sound 45, death at T + 47) |
| interruption T = 190 | `8631569d884a60589cc87bfd8d602888` | `419fbaba230d0b1d3c589c21ab978e15` | - | no |

The unmodified-binary side reproduces the filed keys byte for byte in every row where there is one. The three casts with the patch equal `../rockfix/MANIFEST.md5` (and `lecture/h4-rev/h4c2/keys_rockfix/MANIFEST.md5`).
