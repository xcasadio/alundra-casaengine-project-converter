"""probe_count.py - h4d2 discovery: at EVERY call of the target query 0x8003AF70 made by C63, records the stored count [0x80134360] and the on-demand highest occupied slot (status not 0 and not 4), and the
number of results; wraps the real function by single-stepping it (no model).  usage: python -B probe_count.py"""
import fire1_lib as L
from machine import M32

def make_probe(log):
    def patch(m):
        def wrapper(mm):
            saved_ra = mm.r[31]
            count = mm.peeks(L.COUNT)
            live = [i for i in range(1, 64) if mm.peeks(L.ent(i) + 0x10) not in (0, 4)]
            caller = (mm.r[4] - L.HERO) // L.STRIDE
            log.append(dict(t=mm.tick_no, caller=caller, count=count, ondemand=max(live) if live else 0, live=live))
            del mm.hooks[0x8003AF70]
            mm.pc = 0x8003AF70
            n = 0
            while mm.pc != saved_ra:
                mm.step(); n += 1
                if n > 200000:
                    raise RuntimeError('query did not return')
            mm.hooks[0x8003AF70] = wrapper
        m.hooks[0x8003AF70] = wrapper
    return patch

SC = [('cast', dict(ticks=200)), ('seedB', dict(ticks=200, seed=0xA5A5A5A5)), ('one', dict(ticks=200, targets=[(140, 100)])), ('three', dict(ticks=200, targets=[(140, 100), (100, 140), (180, 100)])),
      ('two_wrong_order', dict(ticks=200, targets=[(180, 100), (130, 100)])), ('topslot', dict(ticks=200, targets=[(1100, 900)] * 11 + [(140, 100)], remove={1: list(range(11))})),
      ('vanish75', dict(ticks=200, targets=[(140, 100)], remove={75: [0]})), ('table_one_free', dict(ticks=200, targets=[(1100, 900)] * 62))]
tot = bad = 0
for name, kw in SC:
    log = []
    res = L.run(patch=make_probe(log), **kw)
    diffs = [e for e in log if e['count'] != e['ondemand']]
    tot += len(log); bad += len(diffs)
    print('%-16s queries %3d  stored count != on-demand highest live slot in %d of them %s' % (name, len(log), len(diffs), [(e['t'], e['count'], e['ondemand']) for e in diffs[:6]]))
print('TOTAL queries', tot, 'differences', bad)
