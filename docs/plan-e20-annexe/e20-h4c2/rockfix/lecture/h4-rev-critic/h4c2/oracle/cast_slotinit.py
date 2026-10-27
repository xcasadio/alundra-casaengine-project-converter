"""cast_slotinit.py - copy of the trace_cast scenario with the map-load slot initialisation of the binary (0x8003B24C-0x8003B2CC: every one of the 64 slots
holds its own index in +0), which the oracle's setup leaves at 0 for the never-used slots.  Compares the births of a cast with and without it.

usage: python -B cast_slotinit.py ITEM [--no-init]"""
import sys
import json
import spellworld as S
import trace_cast as T
import world as W

ITEM = int(sys.argv[1], 0)
INIT = '--no-init' not in sys.argv

orig_make = S.make


def make(*a, **k):
    m, STATS, placed = orig_make(*a, **k)
    if INIT:
        for i in range(64):
            m.poke(S.ENT0 + S.STRIDE * i, i)
    return m, STATS, placed


S.make = make
m, STATS, births, timeline, mp0, mp1 = T.run(ITEM, mp=4, mpmax=4, ticks=600)
T.summarize(ITEM, births, timeline, m, mp0, mp1, 600)
out = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else None
if out:
    json.dump(dict(item=ITEM, timeline=timeline, births={('%d_%d' % k): dict(type=v['type'], birth=v['birth'], last=v['last_seen'], track=v['track']) for k, v in births.items()}), open(out, 'w'))
