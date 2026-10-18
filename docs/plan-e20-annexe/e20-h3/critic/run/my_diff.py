"""my_diff.py - REAL code vs MY models on random structured states.  usage: python -B my_diff.py [N] [seed] [name ...]"""
import random
import sys
import time
from machine import M32, s32
from my_rig import Rig, Mem, diffs, ent, H, STRIDE, RNG, ITEMVAR, EFFSLOT, CELLS, ATKAREA
import my_models as MM

FUNCS = {
    'start_beans': (0x80034224, 0x80034320),
    'start_bomb': (0x8003453C, 0x80034680),
    'drv_beans': (0x80035204, 0x80035260),
    'drv_bomb': (0x800352C4, 0x80035320),
    'cancel_beans': (0x80034ACC, 0x80034B54),
    'cancel_bomb': (0x80034B54, 0x80034BDC),
    'dispatch': (0x800350C0, 0x800351B8),
    'C62': (0x8007A978, 0x8007AC60),
    'D30': (0x8007E79C, 0x8007E7E4),
    'E12': (0x8007F27C, 0x8007F30C),
    'C73': (0x8007BB30, 0x8007BB9C),
    'E21': (0x8007F7CC, 0x8007F878),
}


def pick(rnd, *pools):
    return rnd.choice(pools[0]) if len(pools) == 1 else rnd.choice(rnd.choice(pools))


def gen(rig, rnd):
    m = rig.m
    slots = [ent(i) for i in range(64)]
    kinds = [0, 1, 2, 3, 0x199, 0x99, 5]
    # cell words for the pointers
    nowall = rnd.random() < 0.4            # 40 %: no cell under the four corners carries the wall pattern 0x1001 (the 'clear the animation' path of C62)
    for k in range(64):
        a = CELLS + 2 * k
        v = rnd.choice([0x1001, 0x1001, 0x0001, 0x1000, 0x3001, 0x1003, 0x0000, 0xFFFF, rnd.getrandbits(16)])
        if nowall and (v & 0x1001) == 0x1001: v = 0x0001
        m.poke16(a, v)
    for k in range(32):
        m.poke8(ATKAREA + k, rnd.choice([4, 6, 10, 0x14, 0x84, 0x8A, 1, 2, 3, 5, 7, 8, 9, 11, 12, 0x0F, rnd.getrandbits(8)]))
    for i in range(64):
        e = slots[i]
        W = lambda off, v: m.poke(e + off, v & M32)
        W(0x68, rnd.choice(kinds))
        W(0x10, rnd.choice([0, 1, 2, 3, 4]))
        W(0x24, rnd.choice([0, 0, 0, 1, 2, 3, M32]))
        W(0x28, rnd.choice([0, 0, H, rnd.choice(slots)]))
        W(0x2C, rnd.choice([0, 0, rnd.choice(slots)]))
        W(0x88, rnd.choice([0, 1, 2, 5, 0xB, 0xC]))
        W(0x8C, rnd.randrange(32))
        W(0xAC, rnd.choice([0, 1, 1, 2]))
        W(0xB8, rnd.choice([0, 1, -1, 0x8000, -0x8000, 0x98000, -0x30000, rnd.getrandbits(32)]))
        W(0x12C, rnd.choice([0, 0, rnd.choice(slots)]))
        W(0x13C, rnd.choice([0, 1, 0xFF, rnd.getrandbits(32)]))
        W(0x144, rnd.choice([0, 1, 1]))
        for j in range(4):
            W(0x148 + 4 * j, CELLS + 2 * rnd.randrange(64))
        W(0x158, rnd.choice([0, CELLS + 2 * rnd.randrange(64)]))
        W(0x184, rnd.choice([0, 4, 1, 5, 0x80, 0x84, rnd.getrandbits(32)]))
        W(0x18C, rnd.choice([0, 1, 2, 3, 4, 4, 5, 8, rnd.getrandbits(32)]))
        W(0x1C8, ATKAREA + 2 * rnd.randrange(16))
        W(0x220, rnd.choice([0, 0, 1, 3]))
        W(0x224, rnd.choice([0, rnd.choice(slots)]))
        W(0x274, rnd.choice([0, 1, 2, 7, 8, 9, 15, 16, 55, 56, 57, 59, 60, 61, 64, 240, M32, M32 - 7, M32 - 8, rnd.getrandbits(32), rnd.randrange(0, 300)]))
        W(0x278, rnd.choice([0, 0, 1, rnd.getrandbits(32)]))
        W(0x6C, rnd.getrandbits(32))
        W(0x30, rnd.getrandbits(32)); W(0x34, rnd.getrandbits(32)); W(0x38, rnd.getrandbits(32))
        W(0x114, rnd.getrandbits(32)); W(0x118, rnd.getrandbits(32)); W(0x11C, rnd.getrandbits(32))
    # the hero slot (slot 0) is a normal slot but the +0x2C link and a few fields are read at H
    m.poke(ent(64) + 0x68, rnd.choice([0, 2, 2, 1]))
    m.poke(ITEMVAR, rnd.choice([0, 0, 0x1F, 0x23, 0x23, 0x1F, 0x20, 0x24, 0x80, 0x1E, 0x40, 0x32, 0x33, 0x34, 0x62, 0x7FFFFFFF, M32]))
    m.poke(RNG, rnd.getrandbits(32))
    rig.spawn_ret = rnd.choice([0, slots[rnd.randrange(1, 64)], slots[rnd.randrange(1, 64)]])
    rig.eff_ret = rnd.choice([0, EFFSLOT, EFFSLOT])
    m.zero(EFFSLOT, 0x300)
    for a in range(0x801FFE00, 0x801FFF80, 4):          # stack garbage: a word the code does not write stays random (sensitivity of the argument stores)
        m.poke(a, rnd.getrandbits(32))


def friendly(rig, rnd):
    """bias the state so that the starts / drivers reach their deep paths: hero link empty, slope != 4, no item in progress, 0..4 slots of kind 2"""
    m = rig.m
    m.poke(H + 0x2C, rnd.choice([0, 0, 0, 0, rnd.choice([ent(i) for i in range(1, 64)])]))
    m.poke(H + 0x18C, rnd.choice([0, 1, 2, 3, 5, 6, 4]))
    m.poke(ITEMVAR, rnd.choice([0, 0, 0, 0, 0x23, 0x1F, 0x20]))
    n2 = rnd.choice([0, 1, 2, 2, 3, 3, 4, 8])
    for i in range(64):
        k = m.peek(ent(i) + 0x68)
        if k == 2:
            m.poke(ent(i) + 0x68, rnd.choice([0, 1, 3]))
    idx = list(range(0, 64))
    rnd.shuffle(idx)
    for i in idx[:n2]:
        m.poke(ent(i) + 0x68, 2)
    c = m.peek(H + 0x2C)
    if c and rnd.random() < 0.8:
        m.poke(c + 0x68, rnd.choice([1, 2]))
        m.poke(c + 0x24, rnd.choice([0, 0, 0, 1]))


def model_for(name, M, J, rig, E):
    if name == 'start_beans': return MM.start(M, J, rig, 1)
    if name == 'start_bomb': return MM.start(M, J, rig, 2)
    if name == 'drv_beans': return MM.driver(M, J, rig, 1)
    if name == 'drv_bomb': return MM.driver(M, J, rig, 2)
    if name == 'cancel_beans': return MM.cancel(M, J, rig, 1)
    if name == 'cancel_bomb': return MM.cancel(M, J, rig, 2)
    if name == 'dispatch': return MM.dispatch(M, J, rig)
    return getattr(MM, name)(M, J, rig, E)


def main(N, seed, names):
    rig = Rig()
    rig.freeze()
    rnd = random.Random(seed)
    m = rig.m
    tot = {}
    for name in names:
        addr, end = FUNCS[name]
        bad = 0
        cov = set()
        t0 = time.time()
        for n in range(N):
            rig.reset()
            gen(rig, rnd)
            if name in ('start_beans', 'start_bomb', 'drv_beans', 'drv_bomb', 'cancel_beans', 'cancel_bomb', 'dispatch') and rnd.random() < 0.85:
                friendly(rig, rnd)
            E = rnd.choice([ent(rnd.randrange(1, 64))] * 8 + [H])
            argE = E
            m.cov.clear()
            before = bytes(m.ram)
            M = Mem(before)
            Jm = []
            retm = model_for(name, M, Jm, rig, E)
            rig.J = []
            if name in ('start_beans', 'start_bomb', 'drv_beans', 'drv_bomb', 'cancel_beans', 'cancel_bomb', 'dispatch'):
                ret = m.call(addr)
            else:
                ret = m.call(addr, E)
            Jr = list(rig.J)
            after = bytes(m.ram)
            d_real = diffs(before, after)
            d_mod = {a: v for a, v in M.changed(before).items() if (a & 0x1FFFFFFF) < 0x1FE000}
            cov |= {p for p in m.cov if addr <= p < end}
            okret = (retm is None) or ((ret & M32) == (retm & M32))
            if name in ('dispatch',):
                okret = True
            if not (okret and Jr == Jm and d_real == d_mod):
                bad += 1
                if bad <= 3:
                    print('MISMATCH', name, 'n', n, 'ret real', hex(ret & M32), 'model', retm, '\n  J real', Jr, '\n  J mod ', Jm,
                          '\n  mem only real', {hex(a): v for a, v in d_real.items() if d_mod.get(a) != v}, '\n  mem only model', {hex(a): v for a, v in d_mod.items() if d_real.get(a) != v})
        total_ins = (end - addr) // 4
        # coverage over instructions of the function proper (stop at first jr ra + slot is not needed: report raw over the range)
        print('%-13s vectors %5d  mismatches %4d  covered %3d/%3d instrs  (%.1fs)' % (name, N, bad, len(cov), total_ins, time.time() - t0))
        tot[name] = (bad, len(cov), total_ins)
    return sum(v[0] for v in tot.values())


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 2000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    names = sys.argv[3:] or list(FUNCS)
    sys.exit(1 if main(N, seed, names) else 0)
