#!/bin/bash
# E20.g1d T0 (c). usage: run_all.sh <work dir>  (a scratch directory outside the repository)
# Copies the three oracles (never edited) into <work dir>/orc, runs the four scripts twice each, compares the two runs byte for byte.
set -e
REPO=D:/development/repo/alundra-casaengine-project-converter
A=$REPO/docs/plan-e20-annexe
W=$1
mkdir -p $W/orc
rm -rf $W/orc/oracle_h3 $W/orc/oracle_h3bin $W/orc/oracle_g1a
cp -r $A/e20-h3/critic/run $W/orc/oracle_h3
cp -r $A/e20-h3/bin/run $W/orc/oracle_h3bin
cp -r $A/e20-g1a/work/orc $W/orc/oracle_g1a
find $W/orc -name __pycache__ -prune -exec rm -rf {} +
export G1D_ORC=$W/orc
export PYTHONDONTWRITEBYTECODE=1
export PYTHONPATH=$A/e20-g1d/keys
for s in bomb_keys bomb_keys2 crate_keys throw_probe; do
  for run in 1 2; do
    python -B $A/e20-g1d/keys/$s.py > $W/$s.run$run.txt 2> $W/$s.run$run.err
  done
  cmp $W/$s.run1.txt $W/$s.run2.txt && echo "$s: two runs identical, $(wc -l < $W/$s.run1.txt) lines"
done
