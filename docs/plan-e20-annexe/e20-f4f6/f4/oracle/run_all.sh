#!/bin/sh
# E20.f4 T0: the complete oracle run, from a COPY of the annex root (it writes bin/out and f4/oracle/out under that copy): sh f4/oracle/run_all.sh
# (foreground, about one minute). Order: the frozen replay, the 12 starts of the wall, the keys with the step by image, the mutants.
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
cd "$HERE/../../bin"
python -B run_charged.py
python -B sweep_wall_charged.py
cd "$HERE"
python -B run_charged_pf.py
python -B mutants_f4.py
