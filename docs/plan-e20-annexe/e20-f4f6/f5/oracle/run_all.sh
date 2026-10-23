#!/bin/sh
# E20.f5 T0: the complete oracle run, from a COPY of the annex root (it writes f5/oracle/out under that copy): sh f5/oracle/run_all.sh
# (foreground, about one minute). Order: the frozen replay of the arrows (the keys of ../../bin/out are rewritten in the copy only, for the manifest), the 12 starts of the wall, the new keys and the keys with the step by image, the mutants.
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
cd "$HERE/../../bin"
python -B run_arrows.py
python -B run_arrows_extra.py
python -B run_pointblank.py
python -B sweep_wall.py
cd "$HERE"
python -B run_f5.py
python -B mutants_f5.py
