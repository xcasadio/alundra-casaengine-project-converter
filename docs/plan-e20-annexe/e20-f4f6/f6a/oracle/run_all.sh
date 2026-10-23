#!/bin/sh
# E20.f6a T0: the complete oracle run, from a COPY of the annex root (it rewrites the ball keys of bin/out in the copy only, and writes f6a/oracle/out): sh f6a/oracle/run_all.sh (foreground, under a minute).
# Order: the frozen replay of the balls (run_balls.py, run_balls_mixed.py, run_flames.py), the new keys of f6a, the mutants of the tile-attribute pass 0x80038064.
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
cd "$HERE/../../bin"
python -B run_balls.py
python -B run_balls_mixed.py
python -B run_flames.py
cd "$HERE"
python -B run_f6a.py
python -B mutants_f6a.py
