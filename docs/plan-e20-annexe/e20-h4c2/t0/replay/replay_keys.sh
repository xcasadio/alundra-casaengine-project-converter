#!/bin/bash
# usage: replay_keys.sh <outdir>   (runs the h4c2 key scripts from the scratch copies; writes only under <outdir>)
S=/c/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/h4c2-t0
O="$1"
mkdir -p "$O/disc" "$O/critic"
cd $S/wd
python -B cast_rich.py --pf --out "$O/disc/cast_2c_pf.json" > "$O/disc/cast_2c_pf.stdout" 2>&1
for T in 100 60; do
  python -B cancel_pf.py $T --out "$O/disc/cancel_2c_pf_t$T.json" > "$O/disc/cancel_2c_pf_t$T.txt" 2>&1
done
for I in 0 30; do
  python -B targets_pf.py 0x2C --slotinit --pf --inv $I --out "$O/disc/targets_2c_pf_inv$I.json" > "$O/disc/targets_2c_pf_inv$I.txt" 2>&1
done
python -B driver_keys.py "$O/disc/driver_2c_cases.json" > "$O/disc/driver_2c_cases.stdout" 2>&1
cd $S/wc
python -B cast_rich.py --pf --hero-x 600 --hero-y 480 --out "$O/critic/cast_2c_pf_mid_x600y480.json" > "$O/critic/cast_2c_pf_mid_x600y480.txt" 2>&1
for N in 16 17; do
  python -B my_plateau.py --pf --plateau-hb $N --plateau-col 7 --hero-x 100 --hero-y 100 --out "$O/critic/cast_2c_pf_plateau_hb$N.json" > "$O/critic/cast_2c_pf_plateau_hb$N.txt" 2>&1
done
for T in 100 60; do
  python -B my_cancel_snd.py $T > "$O/critic/cancel_2c_pf_t${T}_sounds.txt" 2>&1
done
cd "$O"
( cd disc; md5sum -- *.json *.txt ) > manifest_disc.txt
( cd critic; md5sum -- *.json *.txt ) > manifest_critic.txt
