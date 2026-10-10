"""run_all.py - D1: rebuild every file of ../out.  usage: python -B tools/run_all.py [--full]
(--full adds the slow checks: the two-interpreter differential diff2.py, about 2 minutes, and explore.py, about 1 minute)
Exit code 0 = both scenarios equal to the annex field by field."""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
OUT = os.path.join(HERE, '..', 'out', 'd1')
os.makedirs(OUT, exist_ok=True)
import dump_dis, oracle, compare, startstate, pass38634, padcheck, mutations

import subprocess
dump_dis.main(OUT)
with open(os.path.join(OUT, 'run_update_animation.out'), 'w') as f:      # the copied E19 tool, import fixed; its output equals the E19 annex's
    subprocess.run([sys.executable, '-B', os.path.join(HERE, 'run_update_animation.py')], stdout=f, check=True)
oracle.main(OUT)
rc = compare.main(OUT)
startstate.main(OUT)
pass38634.main(OUT)
padcheck.main(OUT)
mutations.main(OUT)
import hazards
hazards.main(OUT)
if '--full' in sys.argv:
    import diff2, explore
    diff2.main(OUT)
    explore.main(OUT)
sys.exit(rc)
