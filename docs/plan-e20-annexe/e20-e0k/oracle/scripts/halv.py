import sys, os
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import k0lib as K
from k0_arrows import wall_col
from k0_arrows2 import arrow_run
LIM = 349 * 65536
print('limit raw', LIM)
for x0, frac in ((100,0),(101,0),(102,0),(103,0),(100,1),(100,12345),(100,40000),(101,65535),(102,3),(100,196607)):
    m, s, init, rows = arrow_run(wall_col(15), typ=3, n=60, x=x0, x_frac=frac)
    out = []
    for r in rows:
        d = r['slots'][s]
        if d['px'] >= LIM - 400000:
            out.append('t%d:%+d%s' % (r['tick'], d['px'] - LIM, '*' if d['adj_13C'] else ''))
        if d['status'] != 2: 
            out.append('(status %d at t%d)' % (d['status'], r['tick'])); break
    print('x0=%d+%d: ' % (x0, frac) + ' '.join(out))
