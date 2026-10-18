"""wind1_replay.py - h4e1 discovery: replay a wind-1 vector file from an EMPTY memory (vtrace.SparseRun: instruction fetch from the EXE image, every DATA read must be listed in the vector; the writes and v0
must be reproduced bit for bit), with the same two replay hooks as the recording (SpawnWarpEntity returns stub_spawn_ret, DestroyEntityNoFx is a no-op).
usage (from a copy): python -B wind1_replay.py VECFILE"""
import json
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4b_h'))
import vtrace as V
d = json.load(open(sys.argv[1]))
bad = 0
for v in d['vectors']:
    hooks = {0x80039F58: (lambda r: (lambda m: m.r.__setitem__(2, r)))(v['stub_spawn_ret']), 0x8003A774: (lambda m: m.r.__setitem__(2, 0))}
    ok, msg = V.replay(v, hooks=hooks)
    if not ok:
        bad += 1
        if bad <= 3:
            print('FAIL', v['id'], msg)
print('%d vectors, reads=%d words, writes=%d words, replay: %s' % (len(d['vectors']), sum(len(v['reads']) for v in d['vectors']), sum(len(v['writes']) for v in d['vectors']), 'all ok' if not bad else '%d FAIL' % bad))
sys.exit(1 if bad else 0)
