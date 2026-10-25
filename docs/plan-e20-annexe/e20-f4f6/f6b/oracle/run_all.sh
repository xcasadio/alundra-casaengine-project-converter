#!/bin/sh
# E20.f6b T0: the complete oracle run, from a COPY of the annex root (it rewrites the ball keys of bin/out in the copy only, and writes f6b/oracle/out): sh f6b/oracle/run_all.sh (foreground, about three minutes).
# Order: the frozen replay of the balls and of the flames (run_balls.py, run_balls_extra.py, run_balls_mixed.py, run_pointblank.py, run_flames.py, run_flames_noA.py), the new keys and the keys with the step by image (run_f6b.py), the mutants (mutants_f6b.py), the table of the plan checked against the keys (check_plan_values.py).
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
cd "$HERE/../../bin"
python -B run_balls.py
python -B run_balls_extra.py
python -B run_balls_mixed.py
python -B run_pointblank.py
python -B run_flames.py
python -B run_flames_noA.py
cd "$HERE"
python -B run_f6b.py
python -B mutants_f6b.py
python -B check_plan_values.py > out/check_plan_values.txt
