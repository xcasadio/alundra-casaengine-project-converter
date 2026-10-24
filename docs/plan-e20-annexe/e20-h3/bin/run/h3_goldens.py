"""h3_goldens.py - reference vectors for a C# replay: the REAL code of the bomb / beans functions run on random worlds (same generator as h3_vec.py), inputs and outputs as SPARSE word lists.
For each vector: function name, address, `self` (entity pointer for the handlers), `in` = the words of the compared regions (+ cells, attack records) that differ from the zeroed baseline BEFORE the call,
`out` = the words that CHANGED during the call, `ret` = v0 for the functions that return one, `journal` = the stubbed calls in order (spawn / fx / sound / destroy / cate), `spawn_results` and `fx_results`
(the pointers the stubs hand back, in order).  Addresses are the binary's virtual addresses; words are little-endian u32.  The slot k of the entity table is 0x80127D30 + 0x294 * k.
usage: python -B h3_goldens.py [per_function=40] [seed=20261018] -> ../results/goldens_h3.json"""
import json
import random
import sys
import h3_vec as V
import h4_models as M
from machine import s32, M32

REGIONS = V.COMPARE + [(V.CELLS, V.CELLS + 8 * V.N_CELLS), (V.ATK_BASE, V.ATK_BASE + 64)]


def words(ram, regions):
    out = {}
    for a, b in regions:
        o0, o1 = a & 0x1FFFFFFF, b & 0x1FFFFFFF
        for o in range(o0, o1, 4):
            out[o] = int.from_bytes(ram[o:o + 4], 'little')
    return out


def main(per=40, seed=20261018):
    rnd = random.Random(seed)
    m, STATS, ctx = V.build()
    snapshot = (bytes(m.ram), bytes(m.init))
    base = words(snapshot[0], REGIONS)
    res = []
    for name in V.ADDR:
        for k in range(per):
            m.ram[:] = snapshot[0]
            m.init[:] = snapshot[1]
            s, sres, fres, nent = V.fill_world(m, rnd, name)
            before = words(m.ram, REGIONS)
            ctx.j = []
            ctx.spawn_results = list(sres)
            ctx.fx_results = list(fres)
            handler = name in ('C62', 'D30', 'E12', 'C73', 'E21')
            if handler:
                m.call(V.ADDR[name], s, limit=3_000_000)
                ret = None
            else:
                ret = s32(m.call(V.ADDR[name], limit=3_000_000))
                if name.startswith('cancel') or name == 'dispatch':
                    ret = None
            after = words(m.ram, REGIONS)
            res.append(dict(fn=name, addr='%08X' % V.ADDR[name], self=('%08X' % s) if handler else None,
                            inn={'%08X' % (0x80000000 | o): v for o, v in before.items() if v != base[o]},
                            out={'%08X' % (0x80000000 | o): v for o, v in after.items() if v != before[o]},
                            ret=ret, journal=[list(j) for j in ctx.j], spawn_results=['%08X' % x for x in sres[:6]], fx_results=['%08X' % x for x in fres[:12]]))
    json.dump(dict(note=__doc__.split('\n')[0], seed=seed, vectors=res), open('../results/goldens_h3.json', 'w'), separators=(',', ':'))
    print('vectors', len(res), 'bytes', len(json.dumps(res)))


if __name__ == '__main__':
    per = int(sys.argv[1]) if len(sys.argv) > 1 else 40
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    main(per, seed)
