# E20.h4c2 - rockfix: the "corrected" keys of D-H4C2-4 (INDICATIVE, NOT FAITHFUL TO THE BYTE)

**These keys are indicative, not faithful to the byte.** They do not come from `ALUN_CD.EXE` as it is: they come from the binary **modified in memory** by `fixpatch.py`, which models the author's answer to Q-H4C2-3 (B, D-E20-56): a rock that a wall or the
edge of the map holds in the air waits for the ground (the first pass of E13 - animation 1, flags, sound 45, camera kick - happens only when `+0x144 != 0`; in the air the status is put back to 2 and nothing else is written). They are an INFERENCE
(what the corrected game would do), the oracle of the original stays in `../disc/keys/` and `../critic/keys/` (the keys of the unmodified binary, which are also the pin of the test (e1)). Never use one of them as a statement about the binary.

Provenance: the read-only reading of 2026-10-25 for the revision 3 of `docs/plan-e20-heros.md` section 1.31.4 (scratchpad `t2-next/h4-rev/` and its adversarial critic `t2-next/h4-rev-critic/`), filed here by T0 (a2) as `lecture/h4-rev/` and `lecture/h4-rev-critic/`
(all files, `__pycache__` left out; the scripts read the binary at the absolute path they name, outside the repository, and write next to themselves: rerun them from a copy, never in place). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.

## Files

| File | What |
|---|---|
| `fixpatch.py` | the in-memory patch of E13 `0x8007F30C`: a code cave at `0x801F0000` called from `0x8007F348`; if `+0x144 == 0` the status (`+0x10`) is put back to 2 and the epilogue `0x8007F368` is taken; otherwise the original code runs |
| `cast_rich_fix.py`, `my_plateau_fix.py`, `cancel_fix.py` | the scripts that apply it (`--fix`) to the cast, the plateau casts and the interruption; without `--fix` they give the keys of the unmodified binary |
| `cast_2c_pf_rockfix.json` | the cast, hero (100, 100), flat field (the rock 11 slides along the edge, ground at 194, status 3 and kick at 195) |
| `cast_2c_pf_plateau_hb16_rockfix.json`, `cast_2c_pf_plateau_hb17_rockfix.json` | the same with a plateau of height byte 16 / 17 in the columns >= 7 (the rock 8 stays against the wall, status 3 and kick at 147 / destroyed rocks 9, 13, 14 in the byte 17) |
| `cancel_2c_pf_t170_rockfix.json`, `cancel_2c_pf_t170_rockfix_sounds.txt` | the interruption at T = 170 (the edge rock in the air is cancelled: one more sound 45; the stdout of `cancel_fix.py 170 --fix` carries the sound journal: tick 170, the sound 23 then two sounds 45) |
| `MANIFEST.md5` | md5 of the files above |
| `replay/` | the two replays of T0 (a2) (`MANIFEST_all_run1.md5`, `MANIFEST_all_run2.md5`, identical), with and without the patch |
| `proof.md` | the proof that `cast_2c_pf_mid_x600y480` and the interruptions T = 60 and T = 100 are identical with and without the patch |
| `lecture/` | the whole reading of 2026-10-25 (`h4-rev/`, `h4-rev-critic/`) |

## Replay (T0 (a2), 2026-10-27)

From copies (the oracle of `lecture/h4-rev/h4c2/oracle/` copied to a scratch folder, `python -B`), two runs:

```
python -B cast_rich_fix.py --pf --fix --out cast_2c_pf_rockfix.json
python -B my_plateau_fix.py --pf --fix --plateau-hb 16 --plateau-col 7 --hero-x 100 --hero-y 100 --out cast_2c_pf_plateau_hb16_rockfix.json      (same with 17)
python -B cancel_fix.py 170 --fix --out cancel_2c_pf_t170_rockfix.json  > cancel_2c_pf_t170_rockfix_sounds.txt
```

The two runs give the same manifest (`replay/MANIFEST_all_run1.md5` = `..._run2.md5`); the three casts match `lecture/h4-rev/h4c2/keys_rockfix/MANIFEST.md5` and the interruption matches `lecture/h4-rev/h4c2/out/cancel_t170_FIX.json` / `.txt` byte for byte.
