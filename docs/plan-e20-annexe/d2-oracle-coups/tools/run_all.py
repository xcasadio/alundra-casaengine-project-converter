"""run_all.py - D2: rebuilds every file of out/ and exits 0 only if: the six validation cases PASS, the 3000 random sets agree with the model, the two interpreters
agree, and every mutation is caught.   usage: python -B run_all.py [nsets]   (about 30 s)"""
import contextlib
import io
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'out')
sys.path.insert(0, HERE)
import hitworld as H
import balance_enc as B
import cases as C
import random_sets as R
import diff2_hits as D2
import mutations as MU
import dump_dis


def main():
    nsets = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
    os.makedirs(OUT, exist_ok=True)
    log = []
    rc = 0

    n, bad, tbad, lj, ld = B.check_against_disc()
    line = 'balance.json encoded: %d records, %d differ from the disc DATA/BALANCE.BIN, %d table entries differ, length %d vs %d' % (n, len(bad), len(tbad), lj, ld)
    open(os.path.join(OUT, 'balance-encoding.txt'), 'w').write(line + '\n')
    log.append(line)
    rc |= bool(bad or tbad or lj != ld)

    dump_dis.main(OUT)

    W = H.World()
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        r1 = C.main(OUT, W)
    log.append(buf.getvalue().rstrip())
    rc |= r1
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        r2 = R.main(OUT, nsets, R.SEED, W)
    log.append(buf.getvalue().rstrip().splitlines()[0])
    rc |= r2
    cov = R.coverage(W)
    open(os.path.join(OUT, 'coverage.txt'), 'w').write(cov)
    log.append(cov.rstrip())
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        r3 = D2.main(OUT, 300)
    log.append(buf.getvalue().rstrip())
    rc |= r3
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        r4 = MU.main(OUT)
    log.append('mutations: %s' % ('all caught' if r4 == 0 else 'SOME MISSED'))
    rc |= r4
    open(os.path.join(OUT, 'run.log'), 'w').write('\n'.join(log) + '\n')
    print('\n'.join(log))
    print('EXIT', int(bool(rc)))
    return int(bool(rc))


if __name__ == '__main__':
    sys.exit(main())
