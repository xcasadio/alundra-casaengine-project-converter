"""rgate.py - the D1 gate THROUGH the run-oracle harness.  Imports rworld / rscen (every import-time change: the dictionary entries hworld adds to c1world.KINDS and kworld.INV, the
module-level state of rscen), runs ONE run scenario first (to exercise the world.build_world monkey patch and the exec1 override), then the D1 scenarios S-A and S-B of tools/oracle.py
and the comparison with the annex (tools/compare.py, 216 values each).  Writes ../out/gate/.  Exit code 0 = both PASS and the two trace JSON files equal the ones the unmodified D1 tools wrote.
usage: python -B rgate.py"""
import hashlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rworld as R
import rscen
import world as W
import oracle
import compare

GATE = os.path.join(HERE, '..', 'out', 'gate')
D1OUT = os.path.join(HERE, '..', 'd1', 'out')


def md5(p):
    return hashlib.md5(open(p, 'rb').read()).hexdigest()


def main():
    os.makedirs(GATE, exist_ok=True)
    before = W.build_world
    res = R.run_r(dict(name='gate-warm', hero_px=(100, 100), n=40, pad=[(1, R.TRI | R.RIGHT), (20, R.TRI | R.RIGHT | R.SQU)],
                       layout=R.Layout().rect(14, 0, 15, 59, word=0x40), dash=[(1, 2)]))
    assert W.build_world is before, 'world.build_world was not restored'
    oracle.main(GATE)
    rc = compare.main(GATE)
    lines = ['run scenario executed first: %d ticks, anim timeline %s' % (len(res['rows']), R.timeline_str(res))]
    ok = rc == 0
    for n in ('S-A', 'S-B'):
        a, b = md5(os.path.join(GATE, n + '.trace.json')), md5(os.path.join(D1OUT, n + '.trace.json'))
        lines.append('%s.trace.json md5 %s, unmodified D1 tools %s: %s' % (n, a, b, 'EQUAL' if a == b else 'DIFFERENT'))
        ok = ok and a == b
    lines.append('compare.main exit code %d (0 = 216 values per scenario equal to the annex)' % rc)
    lines.append('GATE %s' % ('PASS' if ok else 'FAIL'))
    open(os.path.join(GATE, 'gate.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
