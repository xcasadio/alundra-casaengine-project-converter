"""vec_families.py - the carried halves of C4 (0x80066984), C70 (0x8007B7B0) and C73 (0x8007BB30) against Python models, on the REAL code. Usage: python -B vec_families.py [N]"""
import random, sys
import g1a_models as M
from h import World, Ent, ENT0, STRIDE, M32, s32
N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
w = World(); m = w.m
rnd = random.Random(7)
mism = {}
tot = {}
def rt(name):
    tot[name] = tot.get(name, 0) + 1

def mk(carried):
    e = Ent()
    e.set32(M.STATUS, 2)
    e.set32(M.CARRIER, ENT0 if carried else 0)
    e.set32(M.T24, rnd.choice([0, 1, 2, 3, 0xFFFFFFFF, 4, 0xFFFFFFFE, 0, 1, 2, 3]) )
    e.set32(M.FLAGS, rnd.randrange(1 << 24))
    e.set32(0x80, rnd.choice([0, 1, 2, 2, 3]))
    for o in (M.ANIM, M.DIR, 0x130, 0x134):
        e.set32(o, rnd.randrange(1 << 32))
    return e

def hero():
    h = Ent(); h.set32(M.CARRIED, ENT0 + 3 * STRIDE)
    for o in (M.OFFX, M.OFFY, M.OFFZ, M.ANIM):
        h.set32(o, rnd.randrange(1 << 32))
    return h

def tbl(base):
    return lambda t: m.rw(base + 4 * t)

def c4(e, h):
    if e.u32(M.CARRIER) == 0:
        return
    t = e.s32(M.T24)
    if t == 0:
        e.set32(M.ANIM, 0)
        return
    if t == -1:
        e.set32(M.ANIM, 3)
        if e.u32(0x80) == 2:
            e.set32(M.FLAGS, ((e.u32(M.FLAGS) | 0x10) & ~0x80) & M32)
    else:
        e.set32(M.ANIM, tbl(0x800270C0)(t))
    e.set32(M.T24, 0)
    h.set32(M.CARRIED, 0)
    e.set32(M.CARRIER, 0)

def c70(e, h):
    if e.u32(M.CARRIER) == 0:
        return
    t = e.s32(M.T24)
    if t == 0:
        e.set32(M.ANIM, 0)
        return
    e.set32(M.ANIM, 3 if t == -1 else tbl(0x800281DC)(t))
    e.set32(M.T24, 0)
    h.set32(M.CARRIED, 0)
    e.set32(M.CARRIER, 0)
    e.set32(M.FLAGS, ((e.u32(M.FLAGS) | 0x34) & ~0x80) & M32)

def c73(e, h):
    if e.u32(M.CARRIER) == 0:
        return          # (the non-carried half is not modelled; carried=False vectors are excluded)
    if e.u32(M.T24) == 0:
        e.set32(M.ANIM, 0)
        return
    e.set32(M.ANIM, 2)
    e.set32(M.T24, 0)
    h.set32(M.CARRIED, 0)
    e.set32(M.CARRIER, 0)
    e.set32(M.FLAGS, (e.u32(M.FLAGS) | 0x10) & M32)

import emu as _emu
COV = set(); _o = _emu.Emu.exec1
def _x(self, pc, wd):
    COV.add(pc); return _o(self, pc, wd)
_emu.Emu.exec1 = _x
FAM = [('C4', 0x80066984, c4, (0x80066984, 0x80066a20)), ('C70', 0x8007B7B0, c70, (0x8007B7B0, 0x8007B82C)), ('C73', 0x8007BB30, c73, (0x8007BB30, 0x8007BB80))]
for name, addr, model, (lo, hi) in FAM:
    COV.clear()
    for i in range(N):
        e = mk(True); h = hero()
        w.put(0, h); w.put(3, e)
        w.call(addr, ENT0 + 3 * STRIDE)
        real = (bytes(w.get(0).raw), bytes(w.get(3).raw))
        me, mh = e.copy(), h.copy()
        model(me, mh)
        rt(name)
        if real != (bytes(mh.raw), bytes(me.raw)):
            mism[name] = mism.get(name, 0) + 1
    miss = [p for p in range(lo, hi, 4) if p not in COV]
    print('%-4s %6d vectors %4d mismatches  carried-half coverage %d/%d  uncovered %s' % (name, tot[name], mism.get(name, 0), (hi - lo) // 4 - len(miss), (hi - lo) // 4, ' '.join('%08x' % p for p in miss[:12])))
sys.exit(1 if mism else 0)
