"""h3w.py - shared helpers of the h3 reading: the h4 spell world (real passes, real SpawnWarpEntity over the global sprite page), a compact tracer that prints CHANGES only."""
import sys
import spellworld as S
import world as W
from machine import s32, M32

HERO = W.ent(0)
FIELDS = [('st', 0x10, 1), ('ty', 0x68, 1), ('fl', 0x6C, 0), ('t24', 0x24, 1), ('c28', 0x28, 0), ('c2c', 0x2C, 0), ('ta', 0x88, 1), ('ca', 0x90, 1), ('dir', 0x8C, 1), ('aend', 0xAC, 1),
          ('b274', 0x274, 1), ('b278', 0x278, 1), ('gr', 0x144, 1), ('adj', 0x13C, 1), ('hitc', 0x220, 1), ('touch', 0x224, 0), ('hp', 0x14, 1), ('inv1CC', 0x1CC, 1), ('inv1D0', 0x1D0, 1),
          ('sup', 0x12C, 0), ('box', 0x1D4, 0), ('cur', 0x1C8, 0)]
POS = [('x', 0x114), ('y', 0x118), ('z', 0x11C)]


def snap(m, i):
    e = W.ent(i)
    d = {n: (m.peeks(e + o) if s else m.peek(e + o)) for n, o, s in FIELDS}
    d.update({n: m.peeks(e + o) for n, o in POS})
    d['fx'] = m.peeks(e + 0xC4); d['fy'] = m.peeks(e + 0xC8); d['fz'] = m.peeks(e + 0xB8)
    return d


def fmtv(k, v):
    if k in ('x', 'y', 'z'):
        return '%.3f' % (v / 65536)
    if k in ('fl', 'c28', 'c2c', 'sup', 'touch', 'cur', 'box'):
        return '%#x' % v
    return str(v)


def show(d, keys=None):
    return ' '.join('%s=%s' % (k, fmtv(k, v)) for k, v in d.items() if keys is None or k in keys)


def run_trace(m, nt, holds, slots=(1,), pos_every=0, hero_keys=('ta', 'ca', 'c2c', 'dir', 'x', 'y', 'z'), skip_keys=('b274',), out=print, pre=None, post=None):
    """one line per tick where any non-position field of the hero or a slot changed (and positions each pos_every ticks)"""
    pad = W.Pad()
    prev = {}
    for t in range(1, nt + 1):
        hold = holds(t) if callable(holds) else holds.get(t, 0)
        m.tick_no = t
        if pre:
            pre(m, t)
        j0 = len(m.journal)
        W.tick(m, pad, hold, check_pad=False)
        if post:
            post(m, t)
        js = [j for j in m.journal[j0:] if not j[1].startswith('pass_')]
        parts = []
        for i in (0,) + tuple(slots):
            d = snap(m, i)
            p = prev.get(i)
            chg = []
            for k, v in d.items():
                if k in ('x', 'y', 'z', 'fx', 'fy', 'fz', 'b274') or (i == 0 and k not in hero_keys):
                    continue
                if p is None or p[k] != v:
                    chg.append('%s=%s' % (k, fmtv(k, v)))
            if p is not None and any(p[k] != d[k] for k in ('x', 'y', 'z')) and (pos_every and t % pos_every == 0):
                chg.append('pos=(%s,%s,%s)' % (fmtv('x', d['x']), fmtv('y', d['y']), fmtv('z', d['z'])))
            if p is None and i != 0:
                chg.append('pos=(%s,%s,%s)' % (fmtv('x', d['x']), fmtv('y', d['y']), fmtv('z', d['z'])))
            prev[i] = d
            if chg:
                parts.append(('H' if i == 0 else 'E%d' % i) + ' ' + ' '.join(chg))
        if parts or js:
            out('t%03d %s%s' % (t, ' | '.join(parts), (' || J ' + ' '.join(str(j[1:]) for j in js)) if js else ''))
