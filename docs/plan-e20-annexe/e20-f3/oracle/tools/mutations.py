"""mutations.py - D1: the comparison must be able to FAIL.  Each mutation changes one thing of the oracle (a pass left out, a constant, a starting value)
and the two scenarios are compared with the annex again.  Expected: the mutations that change the hero's trace FAIL, the ones that do not (pass
0x80038634, idle ticks before S-A, a held Cross) stay identical to the baseline trace.
writes out/mutations.txt"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import oracle
import compare
import world as W

KEYS = ('anim_cur', 'force_x', 'force_y', 'force_z', 'pos_x', 'pos_y', 'pos_z', 'on_ground', 'coll_z')


def trace_sig(res):
    m, meta, pre, rows, warm = res
    return [tuple(r[k] for k in KEYS) for r in rows], tuple(pre[k] for k in KEYS if k not in ('pos_x', 'pos_y'))


def run(fn, **kw):
    res = fn(**kw)
    m, meta, pre, rows, warm = res
    return res


def set_ent(off, val):
    def f(m):
        m.poke(W.ent(0) + off, val)
    return f


MUTATIONS = [
    ('baseline', dict()),
    ('skip pass 0x80038634 (entity slots)', dict(skip=(0x80038634,))),
    ('skip pass 0x80038998 (counters)', dict(skip=(0x80038998,))),
    ('skip pass 0x800384F4 (lists)', dict(skip=(0x800384F4,))),
    ('skip pass 0x80038E18 (animation)', dict(skip=(0x80038E18,))),
    ('skip pass 0x80038364 (physics)', dict(skip=(0x80038364,))),
    ('skip pass 0x800386D0 (events: MovePlayer)', dict(skip=(0x800386D0,))),
    ('gravity 100 instead of 128', dict(gravity=100)),
    ('ZViscosity 512 instead of 4096', dict(visc=512)),
    ('hero HP 0 (+0x14 = 0): death branch', dict(mutate=set_ent(0x14, 0))),
    ('hero +0x20 = 1: early exit', dict(mutate=set_ent(0x20, 1))),
    ('hero +0x1CC = 5 (invulnerability counter)', dict(mutate=set_ent(0x1CC, 5))),
    ('[0x800DC4B8] = 0x20: locked branch', dict(mutate=lambda m: m.poke(0x800DC4B8, 0x20))),
    ('Cross held on every tick (no new edge after tick 1)', dict(cross_held=True)),
    ('start direction 8 (left) instead of 24', dict(direction=8)),
]


def main(outdir):
    annex = compare.parse_annex()
    base = {}
    out = ['# D1 mutation tests: does the comparison with the annex notice a change of the oracle?',
           '# columns: mutation | S-A verdict (differences) | S-B verdict (differences) | trace identical to baseline (compared fields)']
    for label, kw in MUTATIONS:
        cells = []
        same = []
        for name, fn in (('S-A', oracle.scenario_A), ('S-B', oracle.scenario_B)):
            kws = dict(kw)
            if name == 'S-B' and 'idle_warm' in kws:
                del kws['idle_warm']
            try:
                res = fn(**kws)
                sig = trace_sig(res)
                m, meta, pre, rows, warm = res
                v, lines = compare.compare_rows(name, rows, pre, annex)
                nd = sum(int(l.split('DIFF')[0] != l and 1) for l in lines)
                diffs = [l for l in lines if 'VERDICT' in l][0].split(',')[1].strip()
                cells.append('%s (%s)' % (v, diffs))
                if label == 'baseline':
                    base[name] = sig
                same.append('same' if sig == base.get(name) else 'differs')
            except Exception as e:                       # a mutation may break the run itself (e.g. the warm-up never reaches Moving)
                cells.append('RUN ERROR: %s' % str(e)[:60])
                same.append('n/a')
        out.append('%-58s | S-A %-28s | S-B %-28s | %s' % (label, cells[0], cells[1], '/'.join(same)))
    # extra informational runs that must stay equal to the annex
    for label, fn, kw in (('S-A after 3 idle ticks (hero at rest before tick 1)', oracle.scenario_A, dict(idle_warm=3)),):
        res = fn(**kw)
        m, meta, pre, rows, warm = res
        v, lines = compare.compare_rows('S-A', rows, pre, annex)
        out.append('%-58s | S-A %-28s' % (label, [l for l in lines if 'VERDICT' in l][0].strip()))
    # start positions other than (100, 100): the annex must still be reproduced (the deltas do not depend on the cell the hero stands in)
    for (x, y) in ((101, 203), (333, 500), (23, 17), (700, 300)):
        oracle.START_X, oracle.START_Y = x, y
        vs = []
        for name, fn in (('S-A', oracle.scenario_A), ('S-B', oracle.scenario_B)):
            m, meta, pre, rows, warm = fn()
            v, lines = compare.compare_rows(name, rows, pre, annex)
            vs.append('%s %s' % (name, v))
        out.append('%-58s | %s' % ('start position (%d, %d) px' % (x, y), ' | '.join(vs)))
    oracle.START_X, oracle.START_Y = 100, 100
    open(os.path.join(outdir, 'mutations.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    print('\n'.join(out))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
