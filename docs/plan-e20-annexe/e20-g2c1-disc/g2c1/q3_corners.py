"""q3_corners.py - Q-G2C-3: WHERE do the four corner pointers (+0x148..+0x154) that C4 reads point, relative to the entity's final position of the tick?
REAL code of ALUN_CD.EXE (France) in the g1a/g2c oracle machine (copied under ./work/orc). Harness writes: pad words, cell field, object placement (as c4_scen).
Diagnostic only: at the end of every tick the harness calls the real sampler 0x800370C4 on the object (it rewrites only +0x148..+0x154 and +0x158..+0x164, see the
disassembly), reads the 'current position' pointers, and restores the eight words, so the simulation is not disturbed.
Run: python -B -I -X utf8 q3_corners.py [scenario ...]        scenarios: wallrise, wall1001, ground"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c4_scen as S
import gl
import k0lib as K
import world as W
from machine import M32

TILEBASE_PTR = 0x800DD86C


def decode(m, ptr, base):
    d = ptr - base - 0x604
    return (d % 416 // 8, d // 416)


def word16(m, ptr):
    return m.peek(ptr) & 0xFFFF


def post_factory(c, rec):
    e = K.ent(c)

    def post(m, t, r):
        base = m.peek(TILEBASE_PTR)
        ptrs = [m.peek(e + 0x148 + 4 * i) for i in range(4)]
        lev = [m.peeks(e + 0x158 + 4 * i) for i in range(4)]
        saved = ptrs + [m.peek(e + 0x158 + 4 * i) for i in range(4)]
        m.call(0x800370C4, e)
        cur = [m.peek(e + 0x148 + 4 * i) for i in range(4)]
        for i in range(4):
            m.poke(e + 0x148 + 4 * i, saved[i])
            m.poke(e + 0x158 + 4 * i, saved[4 + i])
        rec[t] = dict(
            pos=(m.peeks(e + 0x114) / 65536, m.peeks(e + 0x118) / 65536, m.peeks(e + 0x11C) / 65536),
            adj=m.peeks(e + 0x13C), ground=m.peeks(e + 0x144), anim=m.peeks(e + 0x88), status=m.peeks(e + 0x10), carrier=m.peek(e + 0x28),
            last=[decode(m, p, base) for p in ptrs], lastw=[hex(word16(m, p)) for p in ptrs],
            cur=[decode(m, p, base) for p in cur], same=(ptrs == cur))
    return post


def run(name, cells, hold, lo, hi, label):
    mapname, sprite, extra = S.OBJ[name]
    keep = {S.C4} | set(extra)
    m = gl.world(keep=keep, cells=cells, hero_px=(100, 100))
    S.widen(mapname, sprite)
    c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, hold | gl.SQUARE), (60, 0)]
    pads, nn = gl.pad_script(seq)
    rec = {}
    rows = gl.run(m, nn, pads, crates=[c], post=post_factory(c, rec))
    print('== %s : %s ==' % (label, name))
    for t in range(lo, hi + 1):
        d = rec[t]
        print('t%03d pos=(%.1f,%.1f,%.1f) adj=%d ground=%d anim=%d status=%d carrier=%X | pointers LAST(cells %s words %s) CURRENT(cells %s) %s' % (
            t, d['pos'][0], d['pos'][1], d['pos'][2], d['adj'], d['ground'], d['anim'], d['status'], d['carrier'], d['last'], d['lastw'], d['cur'], 'SAME' if d['same'] else 'DIFFER'))
    n_same = sum(1 for t in range(61, nn + 1) if rec[t]['same'])
    n_diff = [t for t in range(61, nn + 1) if not rec[t]['same']]
    print('   ticks 61..%d: pointers == current-position pointers on %d ticks ; differ on %d ticks: %s' % (nn, n_same, len(n_diff), n_diff[:30]))
    blocked = [t for t in range(61, nn + 1) if rec[t]['adj'] != 0]
    print('   ticks with ForceAdjusted != 0: %s ; of those, pointers differ from the current position on: %s' % (blocked[:30], [t for t in blocked if not rec[t]['same']][:30]))


if __name__ == '__main__':
    which = sys.argv[1:] or ['wallrise', 'wall1001', 'ground']
    if 'wallrise' in which:
        run('tonneau34', lambda cc, rr: dict(wk=0x40) if cc == 5 else None, gl.RIGHT, 58, 70, 'wall in column 5, thrown east (contact while rising)')
    if 'wall1001' in which:
        run('tonneau34', lambda cc, rr: dict(wk=0x01, gp=0x10) if 6 <= cc <= 9 else None, gl.RIGHT, 78, 112, 'cells 0x1001 in columns 6 to 9')
    if 'ground' in which:
        run('tonneau34', lambda cc, rr: dict(wk=0x40) if cc == 7 else None, gl.RIGHT, 78, 90, 'wall in column 7, thrown east (contact while falling / on the ground)')
