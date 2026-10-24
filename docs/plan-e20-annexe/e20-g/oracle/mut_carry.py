"""mut_carry.py - one-word mutants of the carry group of the REAL MovePlayer: sweep_carry.py must fail (mismatch) for each. Run from orc/."""
import subprocess, sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE); sys.path.insert(0, os.path.join(HERE, '..'))
from mut_g1a import mutants, dis
import exelib
RANGES = [(0x80031DF8, 0x80031E34), (0x800322B4, 0x800324B8), (0x80031D68, 0x80031D80)]
N = sys.argv[1] if len(sys.argv) > 1 else '1200'
tot = kill = 0
for a, b in RANGES:
    for addr in range(a, b, 4):
        w = exelib.u32(addr)
        for name, nw in mutants(addr, w):
            tot += 1
            try:
                r = subprocess.run([sys.executable, '-B', '-I', 'sweep_carry.py', N, '%08x' % addr, '%08x' % nw], capture_output=True, text=True, timeout=180, cwd=HERE)
                dead = r.returncode != 0
            except subprocess.TimeoutExpired:
                dead = True
            if dead: kill += 1
            else: print('SURVIVOR %08x %-16s %-28s -> %s' % (addr, name, dis(addr, w), dis(addr, nw)), flush=True)
print('carry-group mutants %d killed %d survivors %d' % (tot, kill, tot - kill))
