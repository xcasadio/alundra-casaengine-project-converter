"""scen_lib.py - critic's scenario helpers (own code; infra = copied oracle: Machine, World, spellworld.make)."""
import spellworld as S
import world as W
from machine import s32, M32

HERO = W.ent(0)
ITEMVAR = 0x80127164

def mk(item=0x23, **kw):
    m, STATS, placed = S.make(item=item, **kw)
    return m, STATS

def ents(m):
    out = []
    for i in range(1, 64):
        e = W.ent(i)
        if m.peeks(e + 0x10) or m.peek(e + 0x68):
            out.append(i)
    return out

def snap(m, i):
    e = W.ent(i)
    g = m.peeks
    return dict(i=i, st=g(e+0x10), sp=g(e+0x68), tanim=g(e+0x88), canim=g(e+0x90), fuse=g(e+0x274), x=g(e+0x114)/65536, y=g(e+0x118)/65536,
                z=g(e+0x11C)/65536, hp=g(e+0x14), end=g(e+0xAC), fl=m.peek(e+0x6C), link=m.peek(e+0x28), mode=g(e+0x24), gnd=g(e+0x144))

def run(m, nticks, hold_fn, watch=None, quiet=False, on_tick=None):
    """returns list of per-tick records (tick, hold, hero snapshot, entity snapshots, new journal)"""
    pad = W.Pad()
    log = []
    for t in range(1, nticks + 1):
        m.tick_no = t
        j0 = len(m.journal)
        W.tick(m, pad, hold_fn(t), check_pad=False)
        rec = dict(t=t, hold=hold_fn(t), hero=snap(m, 0), link=m.peek(HERO + 0x2C), item=m.peek(ITEMVAR),
                   ents={i: snap(m, i) for i in ents(m)}, jr=[x for x in m.journal[j0:] if x[1] not in ('pass_38E84_effects_stub',)])
        log.append(rec)
        if on_tick:
            on_tick(m, t, rec)
    return log
