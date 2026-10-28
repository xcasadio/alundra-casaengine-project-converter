#!/bin/bash
# usage: replay_keys.sh <rundir>  (runs the faithful key scripts of the h4d1 critic from the scratch copy; writes only under <rundir>)
W=/c/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/h4d1-t0
R="$1"
H=$W/critic_c/h4o
mkdir -p "$R/keys_faithful" "$R/p/q"
cd "$R/p/q"
export FAITHFUL=1
O=../../keys_faithful
K=$H/fire2_key_c.py
python -B $K --pf --ticks 200 --out $O/fire2_cast_pf.json > $O/fire2_cast_pf.txt 2>&1
python -B $K --pf --ticks 200 --block 1 5 6 7 8 3 -2 1 0 --out $O/fire2_cast_preset_block.json > $O/fire2_cast_preset_block.txt 2>&1
python -B $K --pf --ticks 200 --block 1 0 0 7 8 0 0 1 1 --out $O/fire2_cast_preset_limit0_pf.json > $O/fire2_cast_preset_limit0_pf.txt 2>&1
python -B $K --pf --hurt 170 --ticks 230 --out $O/fire2_hurt170.json > $O/fire2_hurt170.txt 2>&1
python -B $K --pf --hurt 20 --ticks 230 --out $O/fire2_hurt20.json > $O/fire2_hurt20.txt 2>&1
python -B $K --pf --hurt 60 --ticks 230 --out $O/fire2_hurt60.json > $O/fire2_hurt60.txt 2>&1
python -B $K --pf --hurt 20 --ticks 60 --block 1 5 6 7 8 3 -2 1 1 --buf 9 9 9 9 9 9 9 9 9 --out $O/fire2_hurt20_stale.json > $O/fire2_hurt20_stale.txt 2>&1
python -B $K --pf --hurt 20 --ticks 60 --block 1 5 6 7 8 3 -2 1 1 --out $O/fire2_hurt20_zerobuf.json > $O/fire2_hurt20_zerobuf.txt 2>&1
T=$H/fire2_targets_c.py
python -B $T 140 100 100 140 180 100 --pf --ticks 200 --out $O/fire2_targets_pf.json > $O/fire2_targets_pf.txt 2>&1
python -B $T 200 100 201 100 100 186 100 187 --pf --ticks 200 --out $O/fire2_targets_edge_pf.json > $O/fire2_targets_edge_pf.txt 2>&1
cd $O && sha1sum -b -- *.json *.txt > ../manifest.sha1
