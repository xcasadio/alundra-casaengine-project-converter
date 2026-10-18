"""my_diff_pot.py - critic: MY model of the magic pot handlers A20 (0x800619DC) and C74 (0x8007BB9C) vs the real code."""
import random, sys
from machine import M32, s32
from my_rig import Rig, Mem, diffs, ent, H, RNG, EFFSLOT
import my_models as MM

def sra(x, n):
    return x >> n

def A20(M, J, ctx, E):
    M.ww(E + 0x274, M.rw(E + 0x11C))

def C74(M, J, ctx, E):
    anim = M.rws(E + 0x88)
    if (anim - 1) & M32 >= 5:
        return
    sup = M.rw(H + 0x12C)
    if anim == 1:
        if (M.rw(E + 0x1C) & 3) != 0:
            return
        eff = MM.effect(M, J, ctx, 14, M.rw(E + 0x114), M.rw(E + 0x118), M.rw(E + 0x11C))
        if eff == 0:
            return
        s1 = MM.lcg(M.rw(RNG)); s2 = MM.lcg(s1)
        M.ww(RNG, s2)
        M.ww(eff + 0x5C, 0x20000)
        M.ww(eff + 0x54, ((s1 * 0x30001) >> 32) + 0xFFFE8000)
        M.ww(eff + 0x58, ((s2 * 0x20001) >> 32) + 0xFFFF0000)
    elif anim == 2:
        M.ww(E + 0xB8, s32((M.rw(E + 0x274) - M.rw(E + 0x11C)) & M32) >> 1)
        if sup != E:
            return
        M.ww(E + 0xB8, 0xFFFF8000)
        M.ww(E + 0x88, 3)
    elif anim == 3:
        if sup == E:
            return
        M.ww(E + 0x88, 2)
    elif anim == 4:
        if sup == E:
            return
        if M.rws(H + 0xB8) > 0:
            M.ww(H + 0xB8, 0x98000)
            M.ww(E + 0x88, 5)
        else:
            M.ww(E + 0x88, 2)
    elif anim == 5:
        M.ww(E + 0xB8, s32((M.rw(E + 0x274) - M.rw(E + 0x11C)) & M32) >> 1)

def main(N, seed):
    rig = Rig(); rig.freeze()
    rnd = random.Random(seed); m = rig.m
    for name, addr, end, fn in (('A20', 0x800619DC, 0x800619E8, A20), ('C74', 0x8007BB9C, 0x8007BD8C, C74)):
        bad = 0; cov = set()
        for n in range(N):
            rig.reset()
            E = ent(rnd.randrange(1, 64))
            for off in (0x88, 0x1C, 0x274, 0x11C, 0x114, 0x118, 0xB8, 0x12C):
                pass
            m.poke(E + 0x88, rnd.choice([0, 1, 2, 3, 4, 5, 6, M32, 1, 2, 3, 4, 5]))
            m.poke(E + 0x1C, rnd.choice([0, 1, 2, 3, 4, 8, 12, rnd.getrandbits(32)]))
            m.poke(E + 0x274, rnd.getrandbits(32)); m.poke(E + 0x11C, rnd.getrandbits(32))
            m.poke(E + 0x114, rnd.getrandbits(32)); m.poke(E + 0x118, rnd.getrandbits(32))
            m.poke(E + 0xB8, rnd.getrandbits(32))
            m.poke(H + 0x12C, rnd.choice([0, E, E, ent(rnd.randrange(1, 64))]))
            m.poke(H + 0xB8, rnd.choice([0, 1, M32, 0x50000, rnd.getrandbits(32)]))
            m.poke(RNG, rnd.getrandbits(32))
            rig.eff_ret = rnd.choice([0, EFFSLOT, EFFSLOT]); m.zero(EFFSLOT, 0x300)
            before = bytes(m.ram); M = Mem(before); Jm = []
            fn(M, Jm, rig, E)
            m.cov.clear(); rig.J = []
            m.call(addr, E)
            Jr = list(rig.J); after = bytes(m.ram)
            dr = diffs(before, after); dm = {a: v for a, v in M.changed(before).items() if (a & 0x1FFFFFFF) < 0x1FE000}
            cov |= {p for p in m.cov if addr <= p < end}
            if not (Jr == Jm and dr == dm):
                bad += 1
                if bad <= 3: print('MISMATCH', name, n, Jr, Jm, {hex(a): v for a, v in dr.items() if dm.get(a) != v}, {hex(a): v for a, v in dm.items() if dr.get(a) != v})
        print('%-4s vectors %d mismatches %d covered %d/%d' % (name, N, bad, len(cov), (end - addr) // 4))
main(int(sys.argv[1]) if len(sys.argv) > 1 else 5000, 7)
