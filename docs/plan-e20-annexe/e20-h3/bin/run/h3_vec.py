"""h3_vec.py - the REAL code of the bomb and beans (starts, small drivers, cancels, C62, D30, E12, C73, E21) against the models of h3_models.py on random vectors.
After each call the written regions of RAM are compared BYTE FOR BYTE (the 64 entity slots, the globals page 0x80127100-0x80127200, the random word, the pools of fake
entities / effects), plus the return value and the journal of the stubbed calls (SpawnWarpEntity, CreateEffectEntity, PlaySoundEffect, DestroyEntity 2 args,
CheckAndTriggerTileEffect).  The 1-argument destroy 0x8003A774 and the cancel dispatcher 0x800350C0 stay REAL.

usage: python -B h3_vec.py [N] [seed] [name]     exit code 0 iff no mismatch.   Also prints the coverage of the instructions of each real function.
name in: start_beans start_bomb drv_beans drv_bomb cancel_beans cancel_bomb dispatch C62 D30 E12 C73 E21"""
import random
import sys
from collections import Counter
import spellworld as S
from machine import s32, M32
import h4_models as M
import h3_models as H
import h4_vec_drivers as D

HERO = M.HERO
STRIDE = M.STRIDE
CELLS = 0x801E5000         # synthetic tile cells (8 bytes each) the corner pointers of the bomb point to
N_CELLS = 16
ATK_BASE = 0x801E6000      # synthetic attack records (one byte attribute then power)
COMPARE = [(0x80127D30, 0x80127D30 + 64 * STRIDE), (0x80127100, 0x80127200), (0x80098700, 0x80098710), (D.SPAWN_POOL, D.SPAWN_POOL + D.N_SPAWN * STRIDE),
           (D.FX_POOL, D.FX_POOL + D.N_FX * 0x100), (0x80134360, 0x80134364)]
REGION = {
    'start_beans': (0x80034224, 0x80034320), 'start_bomb': (0x8003453C, 0x80034680), 'drv_beans': (0x80035204, 0x80035260), 'drv_bomb': (0x800352C4, 0x80035320),
    'cancel_beans': (0x80034ACC, 0x80034B54), 'cancel_bomb': (0x80034B54, 0x80034BDC), 'dispatch': (0x800350C0, 0x800351B8),
    'C62': (0x8007A978, 0x8007AC60), 'D30': (0x8007E79C, 0x8007E7E4), 'E12': (0x8007F27C, 0x8007F30C), 'C73': (0x8007BB30, 0x8007BB9C), 'E21': (0x8007F7CC, 0x8007F878),
}
ADDR = {'start_beans': 0x80034224, 'start_bomb': 0x8003453C, 'drv_beans': 0x80035204, 'drv_bomb': 0x800352C4, 'cancel_beans': 0x80034ACC, 'cancel_bomb': 0x80034B54,
        'dispatch': 0x800350C0, 'C62': 0x8007A978, 'D30': 0x8007E79C, 'E12': 0x8007F27C, 'C73': 0x8007BB30, 'E21': 0x8007F7CC}
MODEL = {'start_beans': H.start_beans, 'start_bomb': H.start_bomb, 'drv_beans': H.drv_beans, 'drv_bomb': H.drv_bomb, 'cancel_beans': H.cancel_beans, 'cancel_bomb': H.cancel_bomb,
         'dispatch': M.cancel, 'C62': H.c62, 'D30': H.d30, 'E12': H.e12, 'C73': H.c73, 'E21': H.e21}


def build():
    m, STATS, ctx = D.build_machine()

    def destroy(mm):
        ctx.j.append(('destroy', mm.r[4] & M32, mm.r[5] & M32))
        mm.r[2] = 0

    def cate(mm):
        ctx.j.append(('cate', mm.r[4] & M32))
        mm.r[2] = 0
    m.hooks[0x8003A59C] = destroy
    m.hooks[0x8003A7B0] = cate
    return m, STATS, ctx


def rnd_pos(rnd):
    return rnd.randrange(0, 1 << 32)


def fill_world(m, rnd, name):
    """random world: hero fields, 63 other slots, cells, attack records, pools.  Returns the self entity pointer (handlers) and the effect / spawn result lists."""
    for i in range(0, 64):
        e = M.ent(i)
        for off in range(0, STRIDE, 4):
            m.poke(e + off, 0)
    h = HERO
    for a in range(0x801FFE00, 0x801FFF00, 4):                 # stale stack words: a store that the real code forgets must show
        m.poke(a, rnd.getrandbits(32))
    for off in (0x180, 0x188, 0x190, 0x194, 0x198, 0x1CC, 0x1D0):    # fields next to the slope word, read by the neighbours of the tested code
        m.poke(h + off, rnd.getrandbits(32) if rnd.random() < 0.7 else 0)
    ready = name in ('start_beans', 'start_bomb') and rnd.random() < 0.65
    carried_choice = 'none' if ready else rnd.choice(['none', 'none', 'bean', 'bomb', 'other', 'any'])
    m.poke(h + 0x18C, rnd.choice([0, 1, 2, 3, 5]) if ready and rnd.random() < 0.9 else rnd.choice([0, 1, 2, 3, 4, 4, 5, 0xFFFFFFFF]))
    m.poke(INUSE_ADDR, 0 if ready and rnd.random() < 0.9 else rnd.choice([0, 0, 0, 0x1F, 0x23, 0x23, 0x2B, 0x30, 0x2C, 0x1E, 0x32, 0x33, 0x20, 0x21, 0x62]))
    m.poke(h + 0x144, rnd.choice([0, 1, 1]))
    for off in (0x114, 0x118, 0x11C):
        m.poke(h + off, rnd_pos(rnd) if rnd.random() < 0.7 else rnd.randrange(0, 1 << 24))
    m.poke(h + 0x88, rnd.choice([0, 1, 5, 0xB, 0x32]))
    for off in (0x30, 0x34, 0x38):
        m.poke(h + off, rnd_pos(rnd))
    n = rnd.choice([1, 2, 3, 4, 6, 8, 12])
    m.poke(0x80134360, n)
    m.poke(M.RNG, rnd.getrandbits(32))
    m.poke(0x80127160, rnd.choice([0, 3]))
    m.poke(0x8012715C, rnd.choice([0, 7]))
    # slots: types and statuses, with the bomb count near 2/3
    nb = rnd.choice([0, 1, 2, 2, 3, 3, 4]) if not ready else rnd.choice([0, 1, 2, 2, 2, 3])
    types = [2] * nb + [rnd.choice([0, 1, 0x102, 5, 0x11, 0x13]) for _ in range(n - nb if n > nb else 0)]
    rnd.shuffle(types)
    for i in range(1, n + 1):
        e = M.ent(i)
        m.poke(e + 0x10, rnd.choice([0, 1, 2, 2, 3, 4]))
        m.poke(e + 0x68, types[i - 1] if i - 1 < len(types) else 0)
        m.poke(e + 4, rnd.randrange(1, 1000))
        m.poke(e + 0x6C, rnd.getrandbits(32))
        for off in (0x114, 0x118, 0x11C):
            m.poke(e + off, rnd_pos(rnd))
    m.poke(M.ent(64) + 0x68, rnd.choice([0, 2, 2, 1]))                    # the word just after the table : a loop that runs one slot too long counts it
    # a bomb in a far slot (the count loop covers all 64 slots, whatever the last used index)
    if rnd.random() < 0.35:
        for far in rnd.sample([63, 62, 40, 33, 20, 16], rnd.choice([1, 1, 2])):
            m.poke(M.ent(far) + 0x68, 2)
    if name == 'start_bomb' and rnd.random() < 0.5:                         # directed: exactly two bombs in the middle of the table and a third at a boundary slot of the count loop
        for i in range(0, 65):
            m.poke(M.ent(i) + 0x68, 0)
        for far in rnd.sample(range(1, 41), 2):
            m.poke(M.ent(far) + 0x68, 2)
        m.poke(M.ent(rnd.choice([0, 62, 63, 64, 41, 61, 1])) + 0x68, 2)
    # the hero's own +0x68 sometimes counts as a bomb (the loop starts at slot 0)
    if rnd.random() < 0.08:
        m.poke(h + 0x68, 2)
    # cells: random u16 words, some with the pit pattern
    for k in range(N_CELLS):
        w = rnd.choice([0x1001, 0x0001, 0x1000, 0x1101, 0x0000, 0x0000, 0x0010, rnd.getrandbits(16)])
        m.poke16(CELLS + 8 * k, w)
    # attack records
    for k in range(16):
        m.poke8(ATK_BASE + 4 * k, rnd.choice([4, 6, 10, 0x84, 0x86, 0x8A, 1, 2, 3, 5, 7, 9, 0x0B, 0x14, 0x16, 0xFA]))
    # pools of results
    sres = [D.SPAWN_POOL + k * STRIDE if rnd.random() > 0.12 else 0 for k in range(D.N_SPAWN)]
    fres = [D.FX_POOL + k * 0x100 if rnd.random() > 0.12 else 0 for k in range(D.N_FX)]
    m.zero(D.SPAWN_POOL, D.N_SPAWN * STRIDE)
    m.zero(D.FX_POOL, D.N_FX * 0x100)
    for k in range(min(4, D.N_SPAWN)):                                  # the spawned entity: random position, like the real spawner leaves it
        sp = D.SPAWN_POOL + k * STRIDE
        for off in (0x114, 0x118, 0x11C):
            m.poke(sp + off, rnd_pos(rnd))
        m.poke(sp + 0x28, rnd.choice([0, 0x12345678]))
    carried = {'none': 0, 'bean': M.ent(1), 'bomb': M.ent(1), 'other': M.ent(rnd.randrange(1, n + 1)), 'any': M.ent(rnd.randrange(1, n + 1))}[carried_choice]
    if carried_choice == 'bean' and n >= 1:
        m.poke(M.ent(1) + 0x68, 1)
    if carried_choice == 'bomb' and n >= 1:
        m.poke(M.ent(1) + 0x68, 2)
    if carried_choice == 'other' and n >= 1:
        m.poke(carried + 0x68, rnd.choice([1, 2, 0x102, 7]))
    if carried_choice == 'any' and n >= 1:
        m.poke(carried + 0x68, rnd.choice([1, 2]))
        m.poke(carried + 0x24, rnd.choice([0, 0, 1, 2, 3, 0xFFFFFFFF]))
        m.poke(carried + 0x114, rnd_pos(rnd)); m.poke(carried + 0x118, rnd_pos(rnd)); m.poke(carried + 0x11C, rnd_pos(rnd))
    if carried_choice == 'bean' and n >= 1 or carried_choice == 'bomb' and n >= 1:
        m.poke(carried + 0x24, rnd.choice([0, 0, 0, 1, 2, 3, 0xFFFFFFFF]))
        m.poke(carried + 0x114, rnd_pos(rnd)); m.poke(carried + 0x118, rnd_pos(rnd)); m.poke(carried + 0x11C, rnd_pos(rnd))
        m.poke(carried + 0x1B8, rnd.choice([0, 0, D.FX_POOL + 0x80]))
        m.poke(carried + 0x28, rnd.choice([HERO, HERO, HERO, 0, M.ent(rnd.randrange(1, n + 1))]))
    m.poke(h + 0x2C, carried if n >= 1 else 0)
    # the self entity of a handler
    s = M.ent(rnd.randrange(1, n + 1))
    if name in ('C62', 'D30', 'E12', 'C73', 'E21'):
        m.poke(s + 0x10, rnd.choice([1, 2, 2, 2]))
        m.poke(s + 0x68, {'C62': 2, 'D30': 2, 'E12': 2, 'C73': 1, 'E21': 1}[name])
        m.poke(s + 0x184, rnd.choice([0, 0, 0, 0, 4, 5, 0x10, 8, 1, 0x11, 2]))
        m.poke(s + 0x88, rnd.choice([0, 1, 2, 3, 0xFFFFFFFF]))
        m.poke(s + 0xAC, rnd.choice([0, 1, 1, 2]))
        m.poke(s + 0x24, rnd.choice([0, 0, 1, 2, 3, 0xFFFFFFFF]))
        m.poke(s + 0x28, rnd.choice([0, HERO, HERO, M.ent(rnd.randrange(1, n + 1))]))
        m.poke(s + 0x6C, rnd.choice([0x13234E, 0x13235E, 0x1323DE, 0x0, rnd.getrandbits(32)]))
        m.poke(s + 0x13C, rnd.choice([0, 0, 1, 2]))
        m.poke(s + 0x144, rnd.choice([0, 1]))
        m.poke(s + 0xB8, rnd.choice([0, 1, 0x10000, 0xFFFF0000, 0x80000, 0xFFF80000]))
        m.poke(s + 0x8C, rnd.randrange(0, 32))
        m.poke(s + 0x220, rnd.choice([0, 0, 1, 3]))
        m.poke(s + 0x278, rnd.choice([0, 0, 1, 5]))
        for i in range(4):
            m.poke(s + 0x148 + 4 * i, CELLS + 8 * rnd.randrange(N_CELLS))
        for off in (0x158, 0x15C, 0x160):                                 # the words that follow the four corner pointers: pointers to cells too, so that a loop that runs too long shows
            m.poke(s + off, CELLS + 8 * rnd.randrange(N_CELLS))
        m.poke(s + 0x1B8, 0)
        t = M.ent(rnd.randrange(1, n + 1))
        m.poke(s + 0x224, t if name == 'D30' else rnd.choice([0, t]))
        m.poke(t + 0x1C8, ATK_BASE + 4 * rnd.randrange(16))
        if name == 'C62' and rnd.random() < 0.6:                       # bias toward the free-flight branches (wall bounce, landing)
            m.poke(s + 0x28, 0)
            m.poke(s + 0x88, rnd.choice([1, 2, 3]))
            m.poke(s + 0x13C, rnd.choice([0, 1, 1, 1]))
            m.poke(s + 0x144, rnd.choice([0, 0, 1]))
            m.poke(s + 0xB8, rnd.choice([0x10000, 0x80000, 0x10000, 0, 0xFFF80000]))
            m.poke(s + 0x278, rnd.choice([0, 0, 0, 1]))
            m.poke(s + 0x184, 0)
        if name == 'C62':
            fuse = rnd.choice([0, 1, 2, 7, 8, 9, 15, 16, 17, 55, 56, 57, 58, 59, 60, 61, 63, 64, 120, 240, 0xFFFFFFFF, 0xFFFFFFF0, rnd.randrange(0, 250)])
            m.poke(s + 0x274, fuse)
        if name in ('E12', 'E21'):
            m.poke(s + 0x88, rnd.choice([0, 1, 1, 2]))
        if name == 'E21':
            sup = rnd.choice([0, 0, M.ent(rnd.randrange(1, n + 1))])
            m.poke(s + 0x12C, sup)
            if sup:
                m.poke(sup + 0x68, rnd.choice([0x199, 0x199, 0x99, 0x198, 0x19A, 5]))
                m.poke(sup + 0x88, rnd.choice([0, 0, 1, 2]))
    elif name in ('cancel_beans', 'cancel_bomb', 'dispatch', 'drv_beans', 'drv_bomb'):
        pass
    return s, sres, fres, n


INUSE_ADDR = M.INUSE


def main(N=2000, seed=20261018, names=None, mut=None):
    rnd = random.Random(seed)
    m, STATS, ctx = build()
    if mut:
        mut(m)
    snapshot = (bytes(m.ram), bytes(m.init))
    m.cov.clear()
    names = names or list(ADDR)
    mism = 0
    cnt = Counter()
    first = []
    for n_ in range(N):
        name = names[n_ % len(names)] if len(names) > 1 else names[0]
        name = rnd.choice(names) if len(names) > 1 else name
        m.ram[:] = snapshot[0]
        m.init[:] = snapshot[1]
        s, sres, fres, nent = fill_world(m, rnd, name)
        ram0 = bytes(m.ram)
        mem = M.Mem(ram0)
        env = H.Env3(mem, sres, fres, None)
        ctx.j = []
        ctx.spawn_results = list(sres)
        ctx.fx_results = list(fres)
        fn = MODEL[name]
        if name in ('C62', 'D30', 'E12', 'C73', 'E21'):
            fn(env, s)
            mres = None
            rres = m.call(ADDR[name], s, limit=3_000_000)
            rres = None
        else:
            mres = fn(env)
            rres = s32(m.call(ADDR[name], limit=3_000_000))
            if mres is None:
                rres = None
        diffs = []
        if mres != rres:
            diffs.append(('return', mres, rres))
        if ctx.j != env.j:
            diffs.append(('journal', env.j, ctx.j))
        for (a, b) in COMPARE:
            ra = m.ram[a & 0x1FFFFFFF:b & 0x1FFFFFFF]
            ma = mem.b[a & 0x1FFFFFFF:b & 0x1FFFFFFF]
            if ra != ma:
                for k in range(len(ra)):
                    if ra[k] != ma[k]:
                        diffs.append(('mem', hex(a + k), ma[k], ra[k]))
                        break
        key = name if mres is None else '%s result=%s' % (name, mres)
        cnt[key] += 1
        if diffs:
            mism += 1
            if len(first) < 6:
                first.append((name, diffs[:3]))
    return mism, cnt, first, set(m.cov)


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 2000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    names = [sys.argv[3]] if len(sys.argv) > 3 else None
    mism, cnt, first, cov = main(N, seed, names)
    print('vectors', N, 'seed', seed, 'mismatches', mism)
    for k, v in sorted(cnt.items()):
        print('  ', v, k)
    for f in first:
        print('MISMATCH', f)
    tot_hit = tot = 0
    for name in (names or REGION):
        a, b = REGION[name]
        hit = sum(1 for x in range(a, b, 4) if x in cov)
        tot_hit += hit; tot += (b - a) // 4
        miss = [('%08X' % x) for x in range(a, b, 4) if x not in cov]
        print('coverage %-13s %3d/%3d  never executed: %s' % (name, hit, (b - a) // 4, ' '.join(miss)))
    sys.exit(1 if mism else 0)
