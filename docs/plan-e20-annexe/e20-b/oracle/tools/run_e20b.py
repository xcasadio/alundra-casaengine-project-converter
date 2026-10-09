"""run_e20b.py - E20.b: everything, in order.   usage: python -B tools/run_e20b.py     (about 3 minutes)

 1. D1 re-validation with this folder's copy of the tools: S-A and S-B must still equal the annex field by field (tools/run_all.py -> out/d1/, exit code 0 = PASS)
 2. the attack scenarios AT1-AT11, X1 and their two-interpreter comparison (run_attacks.py -> out/)
 3. the mutation tests of the attack oracle (mutations_attack.py -> out/mutations.txt)
Exit code 0 only if all three succeed.
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'out')
rc = 0
for label, args in (('D1 re-validation (S-A, S-B)', [os.path.join(HERE, 'run_all.py')]),
                    ('attack scenarios + two-interpreter comparison', [os.path.join(HERE, 'run_attacks.py'), OUT]),
                    ('attack mutation tests', [os.path.join(HERE, 'mutations_attack.py'), OUT])):
    print('== ' + label)
    sys.stdout.flush()
    r = subprocess.run([sys.executable, '-B'] + args).returncode
    print('   exit code', r)
    rc |= (r != 0)
sys.exit(rc)
