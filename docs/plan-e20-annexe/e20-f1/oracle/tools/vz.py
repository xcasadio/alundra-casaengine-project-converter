"""vz.py - independent harness (verification of the h1b2 binary surface).
Runs the REAL ALUN_CD.EXE code (France) in the f3-disc MIPS interpreter core (CPU only): real physics pass P 0x80038364,
real list builder 0x800384F4, real RunScript 0x8004205C with the real opcode handlers, real terrain probe 0x800370C4.
Only data setup (entities, cells, header, program bank) is written here. Nothing of the first discovery's zsim.py/mips.py/zmodel.py is used.
"""
import sys, os, struct
# h2 annex edit: emu.py and lib.py are archived next to vz.py (were the scratchpad folders f3-disc/binary and e19j-disc); SP removed.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu import Emu, s32

ENT0 = 0x80127D30
ESZ = 0x294
HDR = 0x80100000      # map header buffer (G u16 at +4, V u16 at +6)
CELLS = 0x80101000    # cell grid base ; cell (col,row) at CELLS + 0x604 + row*0x1A0 + col*8
CODE = 0x80140000     # program code buffer (Codes[] of the map, byte per byte)
RECBUF = 0x80150000   # fake entity records (0x20 bytes each)
TABBUF = 0x80160000   # program tables (u16 offsets per kind)
U = 65536

F = dict(status=0x10, f20=0x20, attach=0x28, rec=0x44, recidx=0x48, flags=0x6c, anim=0xb4, forceZ=0xb8, forceX=0xbc, forceY=0xc0,
         imp=0xf8, posx=0x114, posy=0x118, posz=0x11c, rider=0x12c, floor=0x134, terr=0x138, adj=0x13c, contact=0x140, onground=0x144,
         moved=0x168, mx=0x1d8, my=0x1dc, mz=0x1e0, ox=0x1e4, oy=0x1e8, oz=0x1ec, w=0x1f0, h=0x1f4, d=0x1f8, ffz=0xec,
         logic=0x230, slot=0x4c, st=0x234, kindmark=0x22c, evslot=0x228)

def eaddr(i):
    return ENT0 + ESZ * i


class World:
    def __init__(self, G=128, V=4096):
        m = Emu(); self.m = m
        m.hooks[0x800831E0] = lambda mm: (_ for _ in ()).throw(RuntimeError("BIOS B(0x38) reached (retail error routine)"))
        m.ww(0x800E4334, HDR); m.wh(HDR + 4, G); m.wh(HDR + 6, V)
        m.ww(0x800DD86C, CELLS)
        for x in range(0, 52 * 24):                      # lookup 0x80127168 as filled at 0x8003B198-0x8003B1D4
            m.wh(0x80127168 + 2 * x, x // 24)
        self.maxent = 0
        self.nrec = 0
        # program bank (bit 0x80 set) at 0x80126E70: +0x30 code base, +0x34+4k tables for kind k
        m.ww(0x80126E70 + 0x30, CODE)

    # ---------- map data ----------
    def load_cells(self, cells, W=52, H=60):
        m = self.m
        for row in range(H):
            for col in range(W):
                i = row * W + col
                o = CELLS + 0x604 + row * 0x1A0 + col * 8
                m.wb(o, cells["walkability"][i]); m.wb(o + 1, cells["ground_property"][i])
                m.wb(o + 2, cells["slope"][i]); m.wb(o + 3, cells["height"][i])

    def flat(self, hbyte, W=52, H=60):
        m = self.m
        for row in range(H):
            for col in range(W):
                o = CELLS + 0x604 + row * 0x1A0 + col * 8
                m.wb(o, 0); m.wb(o + 1, 0); m.wb(o + 2, 4); m.wb(o + 3, hbyte)

    def load_code(self, codes, tables):
        """codes: list of ints (the events.json Codes), tables: {'A':[...],...} offsets into codes"""
        m = self.m
        for i, b in enumerate(codes): m.wb(CODE + i, b)
        for k, K in enumerate("ABCDEF"):
            t = tables[K]
            for i, off in enumerate(t): m.wh(TABBUF + 0x100 * k + 2 * i, off)
            m.ww(0x80126E70 + 0x34 + 4 * k, TABBUF + 0x100 * k)

    # ---------- entities ----------
    def add(self, idx, flags, pos, size=(0, 0, 0), off=(0, 0, 0), height=0, recidx=None, progC=0, status=2, anim=0, forceZ=0):
        m = self.m; e = eaddr(idx)
        for o in range(0, ESZ, 4): m.ww(e + o, 0)
        m.ww(e + F['status'], status)
        m.ww(e + F['flags'], flags)
        m.ww(e + F['anim'], anim)
        for ax, v in zip(('posx', 'posy', 'posz'), pos): m.ww(e + F[ax], v & 0xFFFFFFFF)
        # the REAL box initialiser 0x80039C40(entity, offX, offY, offZ, [sp+0x10]=sizeX, [sp+0x14]=sizeY, [sp+0x18]=sizeZ):
        # writes the mods (+0x1E4..), the map-bound words (+0xFC..+0x10C) and the sizes (+0x1F0.. = size<<16 - 1, or 0)
        sp = 0x801FFF00
        m.ww(sp + 0x10, size[0]); m.ww(sp + 0x14, size[1]); m.ww(sp + 0x18, size[2])
        m.call(0x80039C40, e, off[0] & 0xFFFFFFFF, off[1] & 0xFFFFFFFF, off[2] & 0xFFFFFFFF)
        m.ww(e + F['forceZ'], forceZ & 0xFFFFFFFF)
        # record: byte 9 = Height ; program bytes 0xA..0xF
        r = RECBUF + 0x20 * self.nrec; self.nrec += 1
        for o in range(0x20): m.wb(r + o, 0)
        m.wb(r + 9, height)
        m.ww(e + F['rec'], r)
        m.ww(e + F['recidx'], idx if recidx is None else recidx)
        m.ww(e + F['logic'], e)                                  # init 0x80042028: logic = self
        m.ww(e + F['slot'] + 4 * 2, progC)                        # kind 2 (Tick) program index, as 0x8004201C copies it
        self.maxent = max(self.maxent, idx)
        m.ww(0x80134360, self.maxent)
        return e

    def noRecord(self, idx):
        self.m.ww(eaddr(idx) + F['rec'], 0)

    def rd(self, idx, name): return s32(self.m.rw(eaddr(idx) + F[name]))
    def wr(self, idx, name, v): self.m.ww(eaddr(idx) + F[name], v & 0xFFFFFFFF)

    # ---------- real code ----------
    def probe(self, idx):
        return s32(self.m.call(0x800370C4, eaddr(idx)))

    def build_lists(self):
        self.m.call(0x800384F4)

    def physics(self):
        self.m.call(0x80038364)

    def runscript(self, idx, kind=2):
        return self.m.call(0x8004205C, eaddr(idx), kind)

    def state(self, idx):
        e = eaddr(idx)
        return dict(PosZ=self.rd(idx, 'posz'), ForceZ=self.rd(idx, 'forceZ'), FFZ=self.rd(idx, 'ffz'), contact=self.rd(idx, 'contact'),
                    onground=self.rd(idx, 'onground'), terr=self.rd(idx, 'terr'), floor=self.rd(idx, 'floor'), flags=self.m.rw(e + F['flags']))

    def tick(self, scripts=()):
        """one game tick in the binary order (0x8003B388): scripts (0x800386D0) -> lists (0x800384F4) -> [UpdateAnimation: none] -> physics P"""
        for idx in scripts: self.runscript(idx, 2)
        self.build_lists()
        self.physics()
