"""g4b_lib.py - E20.g4b T0 (a): helpers shared by the binary scenarios of the barricade by LIFTING, on the REAL code of ALUN_CD.EXE (France), my392 harness (the 12 records of map 392).
A scripted player reads the hero's state in the machine before each tick and chooses the pad word; the harness writes nothing after the start (position/height of the start only)."""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import my392 as G

U = 65536
W = G.gl.W
K = G.K
HERO = K.ent(0)


class Sim:
    """one world, one pad, a tick loop reading the machine; rows of the per-tick state"""

    def __init__(self, hero_xy, direction, x_raw=None, y_raw=None, z=None):
        self.m, self.S = G.world((int(hero_xy[0]), int(hero_xy[1])), direction, xraw=x_raw, yraw=y_raw)
        self.slot_rec = {v: k for k, v in self.S.items()}
        self.addr = {r: K.ent(s) for r, s in self.S.items()}
        self.rec_of = {K.ent(s): r for r, s in self.S.items()}
        if z is not None:
            self.m.poke(HERO + 0x11C, int(z * U) & 0xFFFFFFFF)
        self.pad = W.Pad()
        self.t = 0
        self.rows = []

    # ---- reads
    def hero(self):
        m, h = self.m, HERO
        return dict(x=m.peeks(h + 0x114) / U, y=m.peeks(h + 0x118) / U, z=m.peeks(h + 0x11C) / U, anim=m.peeks(h + 0x88), ground=m.peeks(h + 0x144),
                    xcoll=self.rec_of.get(m.peek(h + 0x130), ('other' if m.peek(h + 0x130) else None)), carried=self.rec_of.get(m.peek(h + 0x2C), ('other' if m.peek(h + 0x2C) else None)),
                    support=self.rec_of.get(m.peek(h + 0x12C), ('other' if m.peek(h + 0x12C) else None)))

    def crate(self, rec):
        m, e = self.m, self.addr[rec]
        return dict(x=m.peeks(e + 0x114) / U, y=m.peeks(e + 0x118) / U, z=m.peeks(e + 0x11C) / U, status=m.peeks(e + 0x10), anim=m.peeks(e + 0x88),
                    support=self.rec_of.get(m.peek(e + 0x12C), ('other' if m.peek(e + 0x12C) else None)), ground=m.peeks(e + 0x144), carried_by=m.peek(e + 0x28))

    def tick(self, hold):
        self.t += 1
        self.m.tick_no = self.t
        W.tick(self.m, self.pad, hold)
        h = self.hero()
        self.rows.append(dict(t=self.t, hold=hold, **h))
        return h


def fmt_row(r, crates=''):
    return 't%03d pad=%04X x=%9.4f y=%9.4f z=%8.3f anim=%2d gr=%d xcoll=%s carried=%s sup=%s %s' % (
        r['t'], r['hold'], r['x'], r['y'], r['z'], r['anim'], r['ground'], r['xcoll'] or '-', r['carried'] or '-', r['support'] or '-', crates)


def write(name, lines):
    path = os.path.join(G.OUT, name)
    open(path, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    return path
