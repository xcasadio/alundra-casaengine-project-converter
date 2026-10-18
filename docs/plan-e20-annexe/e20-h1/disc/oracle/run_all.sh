#!/bin/bash
# run_all.sh - E20.h1 discovery: regenerates and re-checks EVERYTHING of the oracle from this folder (a COPY of the harness: run it here, never in the repository).
# Needs python 3 with capstone and numpy; reads the binary, data-extracted/data/map_alundra.json and DATA/BALANCE.BIN (paths named in h4b_h/lib.py and h4o/world.py, h4o/spellworld.py), writes only here.
cd "$(dirname "$0")" || exit 1
mkdir -p out
python -B make_listings.py                      > out/listings_out.txt          2>&1
rm -f vec/*.json vec_ss/*.json
python -B gen_h1.py                             > out/gen_out.txt               2>&1
python -B gen_startselect.py                    > out/gen_startselect_out.txt   2>&1
python -B replay_h1.py                          > out/replay_out.txt            2>&1   # every vector from an EMPTY memory on the real code + the instruction coverage
python -B check_model.py                        > out/check_model_out.txt       2>&1   # my model vs the real code on every vector
python -B mutants_h1.py                         > out/mutants_out.txt           2>&1   # the mutants of my model
python -B check_startselect.py                  > out/check_startselect_out.txt 2>&1   # the second guard site, replay + model
python -B loaddelay_scan.py                     > out/loaddelay_out.txt         2>&1
python -B rw_classes.py                         > out/rw_classes_out.txt        2>&1
for it in 24 25 26 27 29; do python -B states_herb.py $it > out/states_item_$it.txt 2>&1; done     # from which of the 80 hero states Circle drinks each item
rm -rf run1 run2
python -B keys_h1.py run1                       > out/keys_run1_out.txt         2>&1
python -B keys_h1.py run2                       > out/keys_run2_out.txt         2>&1
if diff -q run1/keys/manifest.txt run2/keys/manifest.txt > /dev/null; then echo "world keys: two runs, IDENTICAL manifests" > out/keys_compare_out.txt; else echo "world keys: manifests DIFFER" > out/keys_compare_out.txt; fi
rm -rf keys && mv run1/keys keys && cp run2/keys/manifest.txt keys/manifest_run2.txt && cp keys_README.txt keys/README.txt && rm -rf run1 run2
find . -name __pycache__ -type d -prune -exec rm -rf {} +
sha1sum vec/*.json vec_ss/*.json keys/*.json keys/manifest*.txt > out/SHA1SUMS_vectors_keys.txt
echo done
