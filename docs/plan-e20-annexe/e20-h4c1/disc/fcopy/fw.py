"""fw.py - my own harness over the f-projectiles oracle (scratch copy in src/, never run in place).

What it adds to src/pworld.py (nothing else is changed; the MIPS machine and the real code are the same):
  * the effect stubs 0x8003BDD8 (CreateEffect) and 0x8003BE74 (attach effect) hand back a DISTINCT zeroed record per call (src had one shared dummy
    slot), so that the velocity words the handlers write into it afterwards (+0x54, +0x58, +0x5C) can be read at the end of the tick;
  * the RNG word [0x80098708] is compared before/after every tick and the number of LCG steps is reported (draws);
  * a text dump with every tick of the tracked slots, the effects (with velocities), the sounds and the draws.
Nothing here knows an expected value.
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'src'))
import pworld as P
import world as W
import c1world as C
from machine import M32, s32

ENT0, STRIDE = W.ENT0, W.STRIDE
RNG = 0x80098708
K, ADD = 0x7D2B89DD, 0xE06A02E7
FXREC = 0x801F0000
FXSTRIDE = 0x100


def lcg(s):
    return (s * K + ADD) & M32


def draws(s0, s1, limit=64):
    s = s0
    for k in range(limit + 1):
        if s == s1:
            return k
        s = lcg(s)
    return None


def install_fx(m):
    m.fxrecs = []                     # (tick, kind, args, addr) for the current tick

    def mk(kind):
        def f(m2):
            sp = m2.r[29]
            args = [m2.r[4], m2.r[5], m2.r[6], m2.r[7]] + [m2.peek(sp + 0x10 + 4 * i) for i in range(6)]
            addr = FXREC + FXSTRIDE * len(m2.fxrecs)
            m2.zero(addr, FXSTRIDE)
            m2.fxrecs.append((m2.tick_no, kind, [x & M32 for x in args], addr))
            m2.journal.append((m2.tick_no, 'effect_3BDD8' if kind == 'fx' else 'effect_3BE74', [x & M32 for x in args]))
            m2.r[2] = addr
        return f
    m.hooks[0x8003BDD8] = mk('fx')
    m.hooks[0x8003BE74] = mk('attach')


def slotname(m, a):
    if ENT0 <= a < ENT0 + 64 * STRIDE:
        return 'slot%d' % ((a - ENT0) // STRIDE)
    return hex(a)


def fx_text(m, rec):
    tick, kind, a, addr = rec
    if kind == 'fx':
        # CreateEffect(a0=0, id, dir, x, [sp+0x10]=y, [sp+0x14]=z)
        s = 'fx(id=%d,dir=%d,x=%.3f,y=%.3f,z=%.3f,v=(%.4f,%.4f,%.4f))' % (
            a[1], a[2], s32(a[3]) / 65536, s32(a[4]) / 65536, s32(a[5]) / 65536,
            m.peeks(addr + 0x54) / 65536, m.peeks(addr + 0x58) / 65536, m.peeks(addr + 0x5C) / 65536)
        return s
    return 'attach(id=%d,target=%s,mode=%d)' % (a[1], slotname(m, a[3]), s32(a[4]))


def run(spec):
    """spec as pworld.run, plus: setup (list of fn(m) applied before tick 1, after P.setup), track (slots to snapshot; None = all active)"""
    m, meta = P.setup(item=spec.get('item'), hero_px=spec.get('hero_px', (100, 100)), direction=spec.get('direction', 24), targets=spec.get('targets', ()),
                      cells=spec.get('cells'), seed=spec.get('seed', 0x12345678), types=spec.get('types', P.PROJECTILE_TYPES), keep=spec.get('keep', P.REAL),
                      target_kind=spec.get('target_kind'))
    install_fx(m)
    for fn in spec.get('init', []):
        fn(m)
    pad = W.Pad()
    rows = []
    holds = spec.get('holds', {})
    after = spec.get('after', 0)
    for t in range(1, spec['n'] + 1):
        m.tick_no = t
        m.fxrecs = []
        j0 = len(m.journal)
        rng0 = m.peek(RNG)
        hold = holds.get(t, after)
        (spec.get('ticker') or W.tick)(m, pad, hold)
        for fn in spec.get('after_tick', []):
            fn(m, t)
        act = P.active_slots(m)
        rng1 = m.peek(RNG)
        jl = [P.fmt_j(j) for j in m.journal[j0:] if not j[1].startswith('pass_') and j[1] not in ('effect_3BDD8', 'effect_3BE74')]
        r = dict(tick=t, hold=hold, rng0=rng0, rng1=rng1, draws=draws(rng0, rng1), rec_ptr=m.peek(0x80127110), charge=m.peeks(0x80126FFC),
                 slots={s: P.snap(m, s) for s in act}, journal=jl, effects=[fx_text(m, rec) for rec in m.fxrecs])
        rows.append(r)
    return dict(spec=spec, meta=meta, rows=rows, m=m)


def fv(c, v):
    if v is None:
        return 'None'
    if c in ('px', 'py', 'pz', 'hbx', 'hby', 'hbz'):
        return '%.4f' % (v / 65536)
    if c in ('flags6C', 'animflags_B4'):
        return '%X' % v
    if c in ('bw', 'bd', 'bh'):
        return '%d' % ((v + 1) / 65536) if v else '0'
    if c == 'box1d4':
        return '1' if v else '0'
    return str(v)


DEFAULT_COLS = ['status', 'prog228', 'anim_t', 'anim_c', 'dir_t', 'adir', 'cnt1c', 'px', 'py', 'pz', 'fx', 'fy', 'fz', 'adj_13C', 'coll_140', 'ground_144', 'terr_138',
                'slope_18C', 'reset_AC', 'hitc', 'touch_slot', 'c1d0', 'flags6C', 'atk_attr', 'atk_pow', 'box1d4', 'hbx', 'hby', 'hbz', 'bw', 'bd', 'bh']


def dump(res, path, slots, cols=None, first=1, last=None, only_changes=False, title=''):
    cols = cols or DEFAULT_COLS
    out = []
    if title:
        out.append('# ' + title)
    out.append('# columns: ' + ' '.join(cols) + ' | rng-draws | effects | sounds/handlers/other journal')
    out.append('# px,py,pz,hbx,hby,hbz in pixels (16.16 raw / 65536); bw,bd,bh in pixels (size field + 1) / 65536; fx,fy,fz raw forces (16.16: 65536 = 1 px/tick)')
    prev = {}
    for r in res['rows']:
        if r['tick'] < first or (last and r['tick'] > last):
            continue
        for s in slots:
            d = r['slots'].get(s)
            if d is None:
                if prev.get(s) is not None or not only_changes:
                    out.append('t%03d slot%d absent' % (r['tick'], s))
                prev[s] = None
                continue
            line = ' '.join('%s=%s' % (c, fv(c, d[c])) for c in cols)
            if only_changes:
                key = tuple(d[c] for c in cols if c not in ('cnt1c',))
                if prev.get(s) == key and not r['journal'] and not r['effects'] and not r['draws']:
                    continue
                prev[s] = key
            out.append('t%03d slot%d %s' % (r['tick'], s, line))
        extra = []
        if r['draws']:
            extra.append('RNG+%d' % r['draws'])
        extra += r['effects']
        extra += [j for j in r['journal'] if not j.startswith('stub')]
        if extra:
            out.append('t%03d   >> %s' % (r['tick'], ' ; '.join(extra)))
    open(path, 'w').write('\n'.join(out) + '\n')
    return '\n'.join(out)


def wall_col(c, wk=0x40, hb=0):
    def cells(col, row):
        if col == c:
            return (hb, 0, wk, 0)
        return (0, 0, 0, 0)
    return cells


def cells_fn(spec_list, default=(0, 0, 0, 0)):
    """spec_list: list of (colmin, colmax, rowmin, rowmax, (hb, sl, wk, gp))"""
    def cells(col, row):
        for c0, c1, r0, r1, val in spec_list:
            if c0 <= col <= c1 and r0 <= row <= r1:
                return val
        return default
    return cells


def dump2(res, path, slots, cols=None, first=1, last=None, title='', rel=None):
    """compressed timeline: a row is printed when a non-position column changes, the (dpx,dpy) step changes, or something happened (effect, draw, sound, ...).
       rel = tick to print as t+0 for the first slot (the spawn tick S); None = absolute only."""
    cols = cols or DEFAULT_COLS
    quiet = {'px', 'py', 'hbx', 'hby', 'cnt1c'}
    out = []
    if title:
        out.append('# ' + title)
    out.append('# columns: ' + ' '.join(cols))
    out.append('# px,py,pz,hbx,hby,hbz in pixels; bw,bd,bh box size in pixels; fx,fy,fz = ForceX/Y/Z raw 16.16 (65536 = 1 px/tick); flags6C hex')
    prev = {}
    skipped = {}
    for r in res['rows']:
        if r['tick'] < first or (last and r['tick'] > last):
            continue
        extra = []
        if r['draws']:
            extra.append('RNG+%d' % r['draws'])
        extra += r['effects']
        extra += [j for j in r['journal'] if not j.startswith('stub')]
        any_extra = bool(extra)
        for s in slots:
            d = r['slots'].get(s)
            if d is None:
                if prev.get(s, 'x') is not None:
                    out.append('t%03d slot%d absent' % (r['tick'], s))
                prev[s] = None
                continue
            key = tuple(d[c] for c in cols if c not in quiet)
            p = prev.get(s)
            step = None
            if p is not None:
                step = (d['px'] - p[1]['px'], d['py'] - p[1]['py'])
            show = p is None or key != p[0] or step != p[2] or any_extra
            if show:
                if skipped.get(s):
                    out.append('      ... %d ticks uniform (previous step repeated)' % skipped[s])
                    skipped[s] = 0
                line = ' '.join('%s=%s' % (c, fv(c, d[c])) for c in cols)
                tag = ('t%03d' % r['tick']) + ((' (S%+d)' % (r['tick'] - rel)) if rel is not None else '')
                out.append('%s slot%d %s' % (tag, s, line))
            else:
                skipped[s] = skipped.get(s, 0) + 1
            prev[s] = (key, d, step if step is not None else (None, None))
        if any_extra:
            out.append('t%03d   >> %s' % (r['tick'], ' ; '.join(extra)))
    for s, n in skipped.items():
        if n:
            out.append('      ... %d ticks uniform (previous step repeated)' % n)
    open(path, 'w').write('\n'.join(out) + '\n')
    return '\n'.join(out)
