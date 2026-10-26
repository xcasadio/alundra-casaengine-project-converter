"""s1_drop_cycle.py - read-only replay (copy of the g2c oracle machine, ../replay/work/orc): ONE water drop (map-page sprite 135 of map 92, record 15 = the placement WITHOUT a scripted program, so the
native header handlers run), REAL A19 (0x800619D0, via the real status machine: status 1 -> slot A -> status 2) and REAL C71 (0x8007B834), REAL physics, REAL animation.
Harness writes (documented): the drop's position (x=300, y=100 px, away from the hero at 100,100) and its Z = ground + 160 px (zraw below), +0x54 = 0x00B1 (a stand-in for a scripted C program index, the
byte A19 must copy to +0x275); nothing else.  The scripted program itself is not run (RunScript is a journalling stub in this machine): see s1b for the 0x37/0x40 model.
Run: python -B -I -X utf8 s1_drop_cycle.py [n]"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'replay', 'work', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, os.path.join(HERE, 'replay'))
import gl
import k0lib as K
import c4_scen as S
from machine import M32

A19, C71 = 0x800619D0, 0x8007B834


def add_obj(m, xpx, ypx, mapname, sprite, status, zraw_fn, rec_addr=None, ent_addr=None, ptr_addr=None):
    """as gl.add_crate, but with the status the spawner leaves (1 = Loaded -> the real pass runs slot A) and a Z chosen by zraw_fn(T)"""
    kw = {}
    if rec_addr:
        kw = dict(rec_addr=rec_addr, ent_addr=ent_addr, ptr_addr=ptr_addr)
    slots, info = K.spawn_record_entities(m, mapname, sprite, [(10, 11, 0)], slot_status=status, **kw)
    s = slots[0]
    e = K.ent(s)
    m.poke(e + 0x114, (xpx * 65536) & M32)
    m.poke(e + 0x118, (ypx * 65536) & M32)
    m.poke(e + 0x1D8, (xpx * 65536 + m.peeks(e + 0x1E4)) & M32)
    m.poke(e + 0x1DC, (ypx * 65536 + m.peeks(e + 0x1E8)) & M32)
    T = m.call(0x800370C4, e)
    m.poke(e + 0x138, T)
    zraw = zraw_fn(T)
    m.poke(e + 0x11C, zraw & M32)
    m.poke(e + 0x1E0, (zraw + m.peeks(e + 0x1EC)) & M32)
    return s


def fields(m, e):
    return dict(status=m.peek(e + 0x10), st274=m.peek(e + 0x274) & 0xFF, b275=0,
                c282=m.peek(e + 0x282) & 0xFFFF, anim=m.peek(e + 0x88), AC=m.peek(e + 0xAC), flags=m.peek(e + 0x6C),
                z=m.peeks(e + 0x11C) / 65536, ground=m.peek(e + 0x144), collz=m.peek(e + 0x140), support=m.peek(e + 0x12C),
                p54=m.peek(e + 0x54), fz=m.peeks(e + 0xB8), animcur=m.peek(e + 0x90))


def rd8(m, a):
    return (m.peek(a & ~3) >> (8 * (a & 3))) & 0xFF


def run(n=420, zup=160, p54=0xB1, xpx=300, waitN=None):
    S.widen('map_92', 135)
    m = gl.world(keep={A19, C71})
    d = add_obj(m, xpx, 100, 'map_92', 135, 1, lambda T: T + 1 + zup * 65536, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    e = K.ent(d)
    m.poke(e + 0x54, p54)          # harness: stand-in for the scripted program index (A19 copies its low byte to +0x275)
    pads, nn = gl.pad_script([(n, 0)])
    rows = []
    sc = {'cnt': 0, 'seen': 0}

    def pre(mm, t):
        sc['seen'] = mm.peek(e + 0x54) & 0x7F    # was the entity scripted when its C pass of this tick ran?

    def post(mm, t, r):
        f = fields(mm, e)
        f['b275'] = rd8(mm, e + 0x275)
        f['b274'] = rd8(mm, e + 0x274)
        f['j'] = [x for x in r['journal'] if not x.startswith('stub')]
        rows.append((t, f))
        # harness model of the scripted program `37 [N] ; 40 [2, 0] ; FF` (EventProgramRunner.Wait: 1st call suspends, then N more calls, the (N+2)th call advances and runs 0x40): +0x54 := 0
        if waitN is not None and sc['seen']:
            sc['cnt'] += 1
            if sc['cnt'] == waitN + 2:
                mm.poke(e + 0x54, 0)
                sc['cnt'] = 0
                f['j'].append('SCRIPT:40[2,0] (harness model)')

    gl.run(m, nn, pads, crates=[d], post=post, pre=pre)
    return m, d, rows


if __name__ == '__main__':
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 420
    wn = int(sys.argv[2]) if len(sys.argv) > 2 else None
    m, d, rows = run(n, p54=(0xB1 if wn is not None else 0), waitN=wn)
    prev = None
    keys = ('status', 'b274', 'b275', 'anim', 'AC', 'flags', 'ground', 'collz', 'support', 'p54')
    for t, f in rows:
        cur = tuple(f[k] for k in keys)
        zc = round(f['z'], 2)
        if cur != prev or f['j']:
            print('t%03d status=%d b274=%d b275=0x%02X c282=%d anim=%d AC=%d flags=0x%X ground=%d collz=%d support=0x%X p54=0x%X fz=%d z=%.2f  %s' % (
                t, f['status'], f['b274'], f['b275'], f['c282'], f['anim'], f['AC'], f['flags'], f['ground'], f['collz'], f['support'], f['p54'], f['fz'], f['z'], f['j'] or ''))
            prev = cur
