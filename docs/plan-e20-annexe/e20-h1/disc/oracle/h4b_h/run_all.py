"""run_all.py - rerun every model/vector generator (seed 20261018) and the empty-memory replay; output is the content of ../out/run_summary.txt.
Reads ALUN_CD.EXE (path in exelib.py/lib.py/tools.py) and the corpus (data-extracted, read only); writes only ../vec and ../out. Run from a COPY: python -B run_all.py"""
import subprocess, sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
steps = [['b1_push.py', '3000', '20261018'], ['b1b_physpass.py', '600', '20261018'], ['b2_slots.py', '600', '20261018'], ['b3_query.py', '1500', '20261018'],
         ['b3_firstpick.py'], ['b4_tables.py'], ['b5_camera_shake.py', '1500', '20261018'], ['b7_corpus.py'], ['replay_all.py']]
for s in steps:
    print('#####', ' '.join(s), flush=True)
    r = subprocess.run([sys.executable, '-B'] + s, cwd=HERE, capture_output=True, text=True)
    print(r.stdout + r.stderr, flush=True)
