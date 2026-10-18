"""replay_all.py - replay EVERY vector file of ../vec from an EMPTY memory (vtrace.SparseRun: instruction fetch from the EXE image, every DATA read must be listed in the vector;
the writes and the return value must be reproduced bit for bit).  usage: python -B replay_all.py"""
import glob, json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import vtrace as V
from machine import M32
VEC = os.path.join(HERE, '..', 'vec')
tot = bad = 0
for f in sorted(glob.glob(os.path.join(VEC, 'vec_*.json'))):
    d = json.load(open(f))
    nb = 0
    for v in d['vectors']:
        hooks = {}
        if v['fn'].lower() == '0x8007f30c':
            hooks[0x8003A59C] = lambda m: None
        if v['fn'].lower() == '0x80035c64':
            ret = v['stub_spawn_ret']
            hooks[0x80039F58] = (lambda r: (lambda m: m.r.__setitem__(2, r)))(ret)
        ok, msg = V.replay(v, hooks=hooks)
        if not ok:
            nb += 1
            if nb <= 2:
                print('  FAIL', os.path.basename(f), v.get('id'), msg)
    tot += len(d['vectors']); bad += nb
    print('%-34s %-12s %4d vectors  reads=%d words  writes=%d words  replay: %s' % (os.path.basename(f), d['function'], len(d['vectors']),
          sum(len(v['reads']) for v in d['vectors']), sum(len(v['writes']) for v in d['vectors']), 'all ok' if nb == 0 else '%d FAIL' % nb))
print('TOTAL %d vectors, %d failures' % (tot, bad))
sys.exit(1 if bad else 0)
