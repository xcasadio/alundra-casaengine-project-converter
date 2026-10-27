#!/bin/bash
# usage: replay_fix.sh <outdir>   (the rockfix keys, with and without the in-memory patch)
S=/c/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/h4c2-t0
O="$1"
mkdir -p "$O"
cd $S/wf
# with the patch
python -B cast_rich_fix.py --pf --fix --out "$O/cast_2c_pf_rockfix.json" > "$O/cast_2c_pf_rockfix.txt" 2>&1
for N in 16 17; do
  python -B my_plateau_fix.py --pf --fix --plateau-hb $N --plateau-col 7 --hero-x 100 --hero-y 100 --out "$O/cast_2c_pf_plateau_hb${N}_rockfix.json" > "$O/cast_2c_pf_plateau_hb${N}_rockfix.txt" 2>&1
done
python -B cast_rich_fix.py --pf --fix --hero-x 600 --hero-y 480 --out "$O/with_patch_mid_x600y480.json" > "$O/with_patch_mid_x600y480.txt" 2>&1
for T in 60 100 170 190; do
  python -B cancel_fix.py $T --fix --out "$O/with_patch_cancel_t$T.json" > "$O/with_patch_cancel_t$T.txt" 2>&1
done
# without the patch (same scripts, no --fix)
python -B cast_rich_fix.py --pf --out "$O/nopatch_cast_2c_pf.json" > "$O/nopatch_cast_2c_pf.txt" 2>&1
python -B cast_rich_fix.py --pf --hero-x 600 --hero-y 480 --out "$O/nopatch_mid_x600y480.json" > "$O/nopatch_mid_x600y480.txt" 2>&1
for N in 16 17; do
  python -B my_plateau_fix.py --pf --plateau-hb $N --plateau-col 7 --hero-x 100 --hero-y 100 --out "$O/nopatch_plateau_hb$N.json" > "$O/nopatch_plateau_hb$N.txt" 2>&1
done
for T in 60 100 170 190; do
  python -B cancel_fix.py $T --out "$O/nopatch_cancel_t$T.json" > "$O/nopatch_cancel_t$T.txt" 2>&1
done
cd "$O"
md5sum -- *.json *.txt > MANIFEST_all.md5
