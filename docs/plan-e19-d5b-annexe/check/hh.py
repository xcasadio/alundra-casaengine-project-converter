"""hh.py - my own harness (check-d5b): the REAL physics pass 0x80038364 of ALUN_CD.EXE (France) on the emu/ interpreter (copy of the e14b1 one,
NOT the mips.py of the d5b author). Memory image built from scratch from my own reading of the disassembly.
Slot 0 = hero (the hero branch of 0x80036828 is taken because the address equals 0x80127D30).
Binary convention: rest on a terrain T is T + 1.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "emu"))
import emu as emumod

M32 = 0xFFFFFFFF
U = 65536
ENT0 = 0x80127D30
STRIDE = 0x294
MAPHDR = 0x80180000
TILES = 0x80184000
ROW = 416


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def ent(i):
    return ENT0 + STRIDE * i


# field offsets (all read from the disassembly of this session)
O = dict(status=0x10, blocked=0x20, attach=0x28, flags=0x6C, animflags=0xB4, forcez=0xB8, izf=0xF8, finalz=0xEC,
         posx=0x114, posy=0x118, posz=0x11C, mx=0x1D8, my=0x1DC, mz=0x1E0, modx=0x1E4, mody=0x1E8, modz=0x1EC,
         w=0x1F0, h=0x1F4, d=0x1F8, ride=0x12C, floor=0x134, terrain=0x138, coll=0x140, adj=0x13C, ground=0x144,
         moved=0x168, finx=0xE4, finy=0xE8, adjx=0xDC, adjy=0xE0, logic=0x230)


class H:
    def __init__(self, gravity=128, visc=4096, cell_h=lambda c, r: 0):
        self.e = emumod.Emu()
        self.n = 0
        e = self.e
        e.ww(0x800E4334, MAPHDR)
        e.wh(MAPHDR + 4, gravity)
        e.wh(MAPHDR + 6, visc)
        e.wb(MAPHDR + 8, 0)
        e.ww(0x800DD86C, TILES)
        for x in range(1300):
            e.wh(0x80127168 + 2 * x, min(x // 24, 51))
        for row in range(60):
            for col in range(52):
                a = TILES + 0x604 + row * ROW + col * 8
                e.wh(a, 0)
                e.wb(a + 2, 0)
                e.wb(a + 3, cell_h(col, row))
        e.ww(0x80127000, 0)
        e.ww(0x80127164, 0)

    # ---------------------------------------------------------------- accessors
    def g(self, i, name):
        return s32(self.e.rw(ent(i) + O[name]))

    def p(self, i, name, v):
        self.e.ww(ent(i) + O[name], v & M32)

    # ---------------------------------------------------------------- entity
    def add(self, i, flags, off, size, pos, status=2, animflags=0, izf=0):
        e = self.e
        P = ent(i)
        ox, oy, oz = off
        sx, sy, sz = size
        e.ww(P + 0x10, status)
        e.ww(P + 0x6C, flags)
        e.ww(P + 0x108, (-(ox << 16)) & M32)
        e.ww(P + 0x10C, (-(oy << 16)) & M32)
        e.ww(P + 0x1E4, (ox << 16) & M32)
        e.ww(P + 0x1E8, (oy << 16) & M32)
        e.ww(P + 0x1EC, (oz << 16) & M32)
        e.ww(P + 0xFC, (0x4E00000 - ((ox + sx) << 16)) & M32)
        e.ww(P + 0x100, (0x3C00000 - ((oy + sy) << 16)) & M32)
        e.ww(P + 0x104, (0x7800000 - ((oz + sz) << 16)) & M32)
        e.ww(P + 0x1F0, ((sx << 16) - 1) & M32 if sx else 0)
        e.ww(P + 0x1F4, ((sy << 16) - 1) & M32 if sy else 0)
        e.ww(P + 0x1F8, ((sz << 16) - 1) & M32 if sz else 0)
        e.ww(P + 0x114, pos[0] & M32)
        e.ww(P + 0x118, pos[1] & M32)
        e.ww(P + 0x11C, pos[2] & M32)
        e.ww(P + 0x1D8, (pos[0] + (ox << 16)) & M32)
        e.ww(P + 0x1DC, (pos[1] + (oy << 16)) & M32)
        e.ww(P + 0x1E0, (pos[2] + (oz << 16)) & M32)
        e.ww(P + 0xF8, izf & M32)
        e.ww(P + 0xB4, animflags)
        e.ww(P + 0x230, P)
        self.n = max(self.n, i + 1)
        e.ww(0x80134360, self.n - 1)
        # InitializeEntity-like state: terrain probe, then the end pass (floor / IsOnGround)
        e.ww(P + 0x138, self._probe(P) & M32)
        self.lists()
        e.call(0x80038064, P)
        return P

    def _probe(self, P):
        v = self.e.call(0x800370C4, P)
        return v

    # ---------------------------------------------------------------- passes
    def lists(self):
        self.e.ww(0x80134360, self.n - 1)
        self.e.call(0x800384F4)

    def counters(self):
        for i in range(self.n):
            self.p(i, "ride", 0)

    def physics(self):
        self.e.call(0x80038364, limit=3_000_000)

    def tick(self):
        self.counters()
        self.lists()
        self.physics()

    def snap(self, i):
        r = self.g(i, "ride") & M32
        return dict(x=self.g(i, "posx"), y=self.g(i, "posy"), z=self.g(i, "posz"), fz=self.g(i, "forcez"), ffz=self.g(i, "finalz"),
                    ride=((r - ENT0) // STRIDE) if r else None, coll=self.g(i, "coll"), ground=self.g(i, "ground"), floor=self.g(i, "floor"),
                    terr=self.g(i, "terrain"), mz=self.g(i, "mz"), ffx=self.g(i, "finx"), ffy=self.g(i, "finy"), adjx=self.g(i, "adjx"),
                    adjy=self.g(i, "adjy"), moved=self.g(i, "moved"))


# object flavours (flags read from the report's constants only as data; meaning checked against the disassembly: 0x80 collidable, 0x100 gravity, 0x4000 no-riders)
HERO_FLAGS = 0x03118C
HERO_OFF, HERO_SIZE = (-10, -7, 0), (21, 15, 32)
PLAT_FLAGS = 0x0560C0
PLAT_OFF, PLAT_SIZE = (-24, -16, 0), (48, 32, 8)
