"""Prints, for each hand test, the observation under the port and under 'today' (the values written in the plan)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import killmatrix as K, mini_prog as P
for name, codes, ticks, params, result0 in K.cases():
    for mode in ('port', 'today'):
        res = P.simulate_codes(mode, codes, 0, None, result0, ticks, params=params)
        last = res[-1][:5]
        print('%-86s [%-5s] last tick: Result=%d CodeIndex=%d P2..P4=(%s)  all-ticks-results=%s' % (
            name, mode, last[1], last[2], ','.join('%08X' % (m & 0xFFFFFFFF) for m in last[3]), [r[1] for r in res]))
