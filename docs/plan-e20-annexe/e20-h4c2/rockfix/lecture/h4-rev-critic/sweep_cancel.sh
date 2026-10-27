#!/bin/bash
S="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/h4-rev-critic"
cd "$S"
mkdir -p out/sweep
for T in $(seq 150 200); do
  python -I run.py h4c2/oracle cancel_fix.py $T --fix --out "$S/out/sweep/fix_$T.json" > "$S/out/sweep/fix_$T.txt" 2>&1
  python -I run.py h4c2/oracle cancel_pf.py $T --out "$S/out/sweep/base_$T.json" > "$S/out/sweep/base_$T.txt" 2>&1
done
echo done > out/sweep/DONE
