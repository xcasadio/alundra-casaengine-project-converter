"""gen_h1.py - E20.h1 discovery: generates the replayable vectors of the five start functions of the instant items, of the six heal functions and of the start dispatcher,
by running the REAL code of ALUN_CD.EXE (France) on synthetic heroes.  Read-only on the binary and the repository; writes only next to itself (./vec/).

usage: python -B gen_h1.py            (deterministic: fixed seeds; two runs give byte-identical files)

Vector format: docs/plan-e20-annexe/e20-h4b/bin/vec/README.txt, plus
  "stubs":  {"0x....": [return values in order]}   the callees replaced by journalled hooks (h1rig.STUBS); everything else runs for real
  "calls":  [["0x....", [arguments]], ...]           the calls the stubs saw, in order (register arguments, then for 0x8003BDD8 the two stack arguments y and z)
  "class":  "game" (an input the game can produce) | "synthetic" (robustness: an input the game cannot produce, listed so that every instruction is covered)
  "label":  what the vector is.
"""
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from h1rig import *          # noqa: E402,F403
import vtrace as V           # noqa: E402
from machine import M32      # noqa: E402

OUT = os.path.join(HERE, 'vec')
os.makedirs(OUT, exist_ok=True)

# ---- the functions -------------------------------------------------------------------------------------------------------------------------------------------
HEALS = {            # fn: (name, effect creations when everything is obtained)
    0x80032E50: ('herb_heal', 1),
    0x80032F84: ('water1_heal', 4),
    0x800330FC: ('spotion_heal', 4),
    0x80033274: ('lpotion_heal', 8),
    0x800336F0: ('elixir_heal', 8),
    0x80033A2C: ('cureale_heal', 16),
}
STARTS = {           # fn: (name, item id, heal fn)
    0x80034680: ('herb_start', 0x24, 0x80032E50),
    0x800346F0: ('lpotion_start', 0x25, 0x80033274),
    0x80034760: ('elixir_start', 0x26, 0x800336F0),
    0x800347D4: ('cureale_start', 0x27, 0x80033A2C),
    0x80034870: ('spotion_start', 0x29, 0x800330FC),
}
DISPATCH = 0x8003499C

MIPS_MASK = M32


def new_rig(stubs):
    m = V.TM()
    m.hooks.clear()                       # the default stubs of machine.py are not wanted: only the vector's own stubs
    m.watch_uninit = True
    calls = []
    pending = install_stubs(m, stubs, calls, init_effect=True)
    return m, calls, pending


def base_memory(m, stats=None, inv_counts=None, inv_size=0x63):
    """the pointers and tables every function reads through"""
    m.poke(STATS_PTR, STATS)
    m.poke(INV_PTR, INV)
    m.poke(INV_COUNT, inv_size)
    if stats is not None:
        mp, mpmax = stats
        m.poke16(STATS + 4, mp & 0xFFFF)
        m.poke16(STATS + 6, mpmax & 0xFFFF)
    if inv_counts:
        for item, cnt in inv_counts.items():
            m.poke16(INV + 4 * item + 2, cnt & 0xFFFF)


def set_hero(m, base, hp, hpmax, pos=None):
    m.poke(base + 0x14, hp & M32)
    m.poke(base + 0x18, hpmax & M32)
    if pos is not None:
        m.poke(HERO + 0x114, pos[0] & M32)
        m.poke(HERO + 0x118, pos[1] & M32)
        m.poke(HERO + 0x11C, pos[2] & M32)


def prepare_effects(m, rets, rnd):
    """nothing to prepare: the effect stub initialises each object it hands out as InitEffect 0x8003BB14 does (h1rig.install_stubs, init_effect)"""
    return None


def finish(m, calls, pending, fn, args, cls, label, stubs):
    vec = V.run(m, fn, args)
    assert not m.uninit_reads, ('uninitialised read', label, sorted(m.uninit_reads)[:3])
    # trim the unused tail of every stub list: the vector lists exactly what was consumed
    used = {}
    for a, rets in stubs.items():
        n = len(rets) - len(pending[a])
        used[a] = rets[:n]
    vec['stubs'] = {"0x%08x" % a: [x & M32 for x in r] for a, r in used.items() if r}
    vec['calls'] = calls
    vec['class'] = cls
    vec['label'] = label
    vec['_cov'] = sorted(m.cov)          # removed before writing (kept in memory for the statistics)
    return vec


def eff_rets(rnd, n, pattern):
    """the n returns of the effect stub: 'all' (every creation obtained), 'none' (pool full), 'first_null', 'last_null', 'random'"""
    ptrs = [EFF0 + 0x100 * k for k in range(n)]
    if pattern == 'all':
        return ptrs
    if pattern == 'none':
        return [0] * n
    if pattern == 'first_null':
        return [0] + ptrs[1:]
    if pattern == 'last_null':
        return ptrs[:-1] + [0]
    if pattern == 'alt':
        return [p if k % 2 == 0 else 0 for k, p in enumerate(ptrs)]
    if pattern == 'alt2':
        return [p if k % 2 == 1 else 0 for k, p in enumerate(ptrs)]
    return [p if rnd.random() < 0.6 else 0 for p in ptrs]


SEEDS = [0x12345678, 0, 1, 0xFFFFFFFF, 0x80000000, 0x7FFFFFFF, 0xE06A02E7, 0x7D2B89DD]
POS = [(100 << 16, 100 << 16, 0x10000), (0, 0, 0), (0x7FFFFFFF, 0x80000000, 0xFFFFFFFF), (0xFFFF0000, 0x00010000, 0x00AB0000)]

HPMAXES_GAME = [1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 18, 19, 20, 21, 22, 23, 24, 25, 26, 30, 49, 50, 51]
HPMAXES_SYNTH = [0, -1, -3, -4, -5, -8, 100, 0x7FFFFFFF]


def hp_cases(hpmax, rnd):
    """hit points around the interesting values of one maximum: the sums that reach, miss or pass the maximum for a heal of 5, 10, a quarter or a half"""
    c = {hpmax - 1, hpmax - 2, hpmax - 4, hpmax - 5, hpmax - 6, hpmax - 9, hpmax - 10, hpmax - 11, hpmax - hpmax // 4, hpmax - hpmax // 2, 0, 1}
    c = {x for x in c if x >= 0}
    c |= {hpmax, hpmax + 1}            # at max: the heal itself is still defined (the start refuses before)
    return sorted(c)


def gen_heals(rnd):
    out = {fn: [] for fn in HEALS}
    for fn, (name, n_eff) in HEALS.items():
        item = {0x80032E50: 0x24, 0x80032F84: 0x2D, 0x800330FC: 0x29, 0x80033274: 0x25, 0x800336F0: 0x26, 0x80033A2C: 0x27}[fn]
        k = 0
        for hpmax in HPMAXES_GAME + HPMAXES_SYNTH:
            in_game = hpmax in HPMAXES_GAME
            hps = hp_cases(hpmax, rnd) if in_game else sorted({hpmax - 1, hpmax // 2, 0, 1, -1, hpmax, hpmax - 7})
            for hp in hps:
                pat = ['all', 'none', 'first_null', 'last_null', 'alt', 'alt2', 'random', 'random', 'all'][k % 9]
                pos = POS[k % len(POS)]
                seed = SEEDS[k % len(SEEDS)] if k % 3 else rnd.getrandbits(32)
                rets = eff_rets(rnd, n_eff, pat)
                stubs = {0x8003BDD8: rets}
                m, calls, pending = new_rig(stubs)
                base_memory(m, stats=(rnd.randint(-2, 6), rnd.randint(-2, 6)))
                set_hero(m, HERO, hp, hpmax, pos)
                m.poke(RNG, seed)
                prepare_effects(m, rets, rnd)
                cl = 'game' if (in_game and 0 <= hp <= hpmax) else 'synthetic'
                out[fn].append(finish(m, calls, pending, fn, [HERO, item], cl, '%s hp=%d hpmax=%d effects=%s' % (name, hp, hpmax, pat), stubs))
                k += 1
        # overflow of the 32-bit sum (Hp + heal wraps negative): synthetic
        for hp, hpmax in ((0x7FFFFFFE, 0x7FFFFFFF), (0x7FFFFFF0, 100), (0x7FFFFFFF, 1000), (0x80000000, 5), (-5, 20), (-100, 30), (-1, 3)):
            rets = eff_rets(rnd, n_eff, 'all')
            stubs = {0x8003BDD8: rets}
            m, calls, pending = new_rig(stubs)
            base_memory(m, stats=(1, 4))
            set_hero(m, HERO, hp, hpmax, POS[0])
            m.poke(RNG, rnd.getrandbits(32))
            prepare_effects(m, rets, rnd)
            out[fn].append(finish(m, calls, pending, fn, [HERO, item], 'synthetic', '%s wrap hp=%d hpmax=%d' % (name, hp, hpmax), stubs))
        # a0 differs from the hero literal: the life is read and written through a0, the position through the literal address
        for hp, hpmax in ((3, 10), (7, 12), (1, 50)):
            rets = eff_rets(rnd, n_eff, 'all')
            stubs = {0x8003BDD8: rets}
            m, calls, pending = new_rig(stubs)
            base_memory(m, stats=(1, 4))
            set_hero(m, HERO2, hp, hpmax)
            set_hero(m, HERO, 5, 6, POS[1])          # the hero literal: another life, ignored by the heal of a0
            m.poke(RNG, rnd.getrandbits(32))
            prepare_effects(m, rets, rnd)
            out[fn].append(finish(m, calls, pending, fn, [HERO2, item], 'synthetic', '%s a0=other entity hp=%d hpmax=%d' % (name, hp, hpmax), stubs))
    return out


HP_SET = [(1, 10), (4, 10), (9, 10), (10, 10), (11, 10), (3, 12), (11, 12), (12, 12), (17, 24), (23, 24), (24, 24), (25, 50), (49, 50), (50, 50), (51, 51), (0, 10), (0, 1), (1, 1),
          (2, 3), (3, 3), (0, 0), (5, 4), (5, 20), (15, 20), (9, 19), (10, 21), (-1, 10), (3, -2), (-3, -1), (0x7FFFFFFF, 5), (-5, 3)]
MP_SET = [(0, 4), (1, 4), (3, 4), (4, 4), (2, 3), (3, 3), (0, 0), (0, 1), (1, 1), (0, 2), (4, 3), (-1, 4), (5, 4), (3, -2), (-3, -1), (0x7FFF, 4), (-0x8000, 4)]


def one_start(rnd, fn, name, item, n_eff, hp, hpmax, mp, mpmax, count, pat, pos, seed, label_extra=''):
    rets = eff_rets(rnd, n_eff, pat)
    stubs = {0x800490FC: [0], 0x8003BDD8: rets}
    m, calls, pending = new_rig(stubs)
    base_memory(m, stats=(mp, mpmax), inv_counts={item: count})
    for other in rnd.sample([i for i in range(0x63) if i != item], 4):       # other inventory entries: random, they must stay untouched
        m.poke16(INV + 4 * other + 2, rnd.randint(0, 99))
    set_hero(m, HERO, hp, hpmax, pos)
    m.poke(RNG, seed)
    prepare_effects(m, rets, rnd)
    cl = 'game'
    if hp < 0 or hp > hpmax or mp < 0 or mp > mpmax or mpmax > 4 or hpmax > 51 or hpmax < 1 or count <= 0 or count > 99:
        cl = 'synthetic'
    return finish(m, calls, pending, fn, [item], cl, '%s hp=%d/%d mp=%d/%d count=%d effects=%s%s' % (name, hp, hpmax, mp, mpmax, count, pat, label_extra), stubs)


def gen_starts(rnd):
    out = {fn: [] for fn in STARTS}
    for fn, (name, item, heal) in STARTS.items():
        n_eff = HEALS[heal][1]
        k = 0
        if fn == 0x80034760:                                  # the elixir reads only the magic
            combos = [(rnd.choice(HP_SET), mp) for mp in MP_SET]
            combos += [(rnd.choice(HP_SET), mp) for mp in MP_SET]
        elif fn == 0x800347D4:                                # the ale reads the life first, then the magic
            combos = [(hp, mp) for i, hp in enumerate(HP_SET) for j, mp in enumerate(MP_SET) if (i + j) % 3 == 0]
        else:                                                 # the herb and the potions read only the life
            combos = [(hp, rnd.choice(MP_SET)) for hp in HP_SET] + [(hp, rnd.choice(MP_SET)) for hp in HP_SET]
        for (hp, hpmax), (mp, mpmax) in combos:
            count = [1, 2, 9, 99, 1, 3, 5][k % 7]
            pat = ['all', 'all', 'none', 'random', 'first_null', 'alt', 'last_null', 'alt2'][k % 8]
            pos = POS[k % len(POS)]
            seed = SEEDS[k % len(SEEDS)] if k % 3 else rnd.getrandbits(32)
            out[fn].append(one_start(rnd, fn, name, item, n_eff, hp, hpmax, mp, mpmax, count, pat, pos, seed))
            k += 1
        # item counts: 0 (UseItem answers -1 and writes nothing), the largest, a negative one
        for count in (0, 1, 2, 0x7FFF, 0xFFFF, 0x8000):
            out[fn].append(one_start(rnd, fn, name, item, n_eff, 3, 10, 1, 4, count, 'all', POS[0], rnd.getrandbits(32), ' (count probe)'))
        # the item argument is the inventory index UseItem decrements: another index (synthetic: the dispatcher only calls with the item's own id)
        for other_item in (0x00, 0x62, 0x30, -1):
            rets = eff_rets(rnd, n_eff, 'all')
            stubs = {0x800490FC: [0], 0x8003BDD8: rets}
            if other_item < 0:
                stubs[0x8004DB90] = [0]
            m, calls, pending = new_rig(stubs)
            base_memory(m, stats=(1, 4), inv_counts={other_item: 5, item: 7})
            set_hero(m, HERO, 3, 10, POS[0])
            m.poke(RNG, rnd.getrandbits(32))
            prepare_effects(m, rets, rnd)
            out[fn].append(finish(m, calls, pending, fn, [other_item], 'synthetic', '%s called with item argument %#x' % (name, other_item), stubs))
        # the inventory table has fewer entries than the item index: UseItem prints its error and answers 0 (the start ignores the answer): synthetic, the game's table has 0x63 entries
        for size in (item, 0, 0x10):
            rets = eff_rets(rnd, n_eff, 'all')
            stubs = {0x800490FC: [0], 0x8003BDD8: rets, 0x8004DB90: [0]}
            m, calls, pending = new_rig(stubs)
            base_memory(m, stats=(1, 4), inv_counts={item: 4}, inv_size=size)
            set_hero(m, HERO, 3, 10, POS[0])
            m.poke(RNG, rnd.getrandbits(32))
            prepare_effects(m, rets, rnd)
            out[fn].append(finish(m, calls, pending, fn, [item], 'synthetic', '%s inventory size %#x (UseItem error path)' % (name, size), stubs))
    return out


def gen_dispatch(rnd):
    out = []
    items_instant = [0x24, 0x25, 0x26, 0x27, 0x29]
    other_starts = {0x1F: 0x80034224, 0x20: 0x80034320, 0x23: 0x8003453C, **{i: 0x800348E0 for i in range(0x2B, 0x33)}}
    holes = [0x21, 0x22, 0x28, 0x2A]
    s0s = [-1, 0, 1, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32, 0x33, 0x34, 0x40, 0x61, 0x62, 0x63, 0x7F, 0x80,
            0x7FFFFFFF, 0x80000000, 0xFFFFFFFE]
    k = 0
    for s0 in s0s:
        for cur_mode in ('free', 'same', 'other'):
            if cur_mode == 'same' and s0 in (-1, 0, 0xFFFFFFFE, 0x80000000):
                pass
            cur = {'free': 0, 'same': s0 & M32, 'other': rnd.choice([0x2B, 0x23, 0x1F, 0x32, 0x01, 0x7FFFFFFF, 0xFFFFFFFF])}[cur_mode]
            if cur_mode == 'other' and cur == (s0 & M32):
                cur = 0x2C
            # the start the dispatcher reaches, if any
            sidx = s0 & M32
            target = None
            if sidx < 0x62:
                idx = sidx - 0x1F
                if 0 <= idx < 0x14:
                    if sidx in items_instant:
                        target = {0x24: 0x80034680, 0x25: 0x800346F0, 0x26: 0x80034760, 0x27: 0x800347D4, 0x29: 0x80034870}[sidx]
                    elif sidx in other_starts:
                        target = other_starts[sidx]
            for r in ([0, 1, 2, 0xFFFFFFFF] if (target in (0x80034224, 0x80034320, 0x8003453C, 0x800348E0)) else [0]):
                stubs = {0x800490FC: [0], 0x8004E0F8: [s0 & M32]}
                if target is not None:
                    stubs[target] = [r]
                m, calls, pending = new_rig(stubs)
                base_memory(m)
                m.poke(IN_USE, cur)
                m.poke(COUNTER, rnd.getrandbits(32))      # old counter and phase: the dispatcher zeroes them only when the start answers 0
                m.poke(PHASE, rnd.getrandbits(32))
                label = 'dispatch s0=%#x in_use=%#x start_ret=%s' % (s0 & M32, cur, r if target else '-')
                vec = finish(m, calls, pending, DISPATCH, [], 'game' if (0 <= s0 < 0x80) else 'synthetic', label, stubs)
                out.append(vec)
                k += 1
    return out


def gen_tryuse(rnd):
    out = []
    pressed_set = [0, 0x20, 0x10, 0x30, 0x40, 0x80, 0xFFFF, 0xFFDF, 0x0020 | 0x800, 0x1000, 0x2000, 0x21]
    ctrl_set = [0, 1, 2, 4, 8, 0x10, 0x20, 0x34, 0x40, 0x48, 0x80, 0x100, 0x80000000, 0xFFFFFFFF]
    for pressed in pressed_set:
        for ctrl in ctrl_set:
            for ret in ([0, 1, 2] if (pressed & 0x20 and ctrl == 0) else [1]):
                stubs = {0x800490FC: [0]}
                if (pressed & 0x20) and ctrl == 0:
                    stubs[0x8003499C] = [ret]
                m, calls, pending = new_rig(stubs)
                m.poke16(0x80126F2A, pressed)
                m.poke(0x800DC4B8, ctrl)
                out.append(finish(m, calls, pending, 0x8002ED64, [], 'game' if pressed < 0x1000 and ctrl in (0, 1, 2, 4, 8, 0x10, 0x20, 0x34, 0x40, 0x48, 0x80, 0x100) else 'synthetic',
                                  'tryuseitem pressed=%#x ctrl=%#x start_ret=%d' % (pressed, ctrl, ret), stubs))
    return out


def dump(name, fn, vecs):
    cov = set()
    for v in vecs:
        cov |= set(v.pop('_cov'))
    for i, v in enumerate(vecs):
        v['id'] = i
        v['fn'] = '0x%08x' % fn if 'fn' not in v else v['fn']
    path = os.path.join(OUT, 'vec_%s_%08X.json' % (name, fn))
    with open(path, 'w') as f:
        json.dump({'function': '0x%08x' % fn, 'name': name, 'vectors': vecs}, f, separators=(',', ':'), sort_keys=False)
    return cov, len(vecs)


def main():
    rnd = random.Random(0x4831)
    summary = {}
    heals = gen_heals(rnd)
    for fn, vecs in heals.items():
        cov, n = dump(HEALS[fn][0], fn, vecs)
        summary[fn] = (HEALS[fn][0], n, len(cov))
    starts = gen_starts(rnd)
    for fn, vecs in starts.items():
        cov, n = dump(STARTS[fn][0], fn, vecs)
        summary[fn] = (STARTS[fn][0], n, len(cov))
    tu = gen_tryuse(rnd)
    cov, n = dump('tryuseitem', 0x8002ED64, tu)
    summary[0x8002ED64] = ('tryuseitem', n, len(cov))
    disp = gen_dispatch(rnd)
    cov, n = dump('dispatch', DISPATCH, disp)
    summary[DISPATCH] = ('dispatch', n, len(cov))
    for fn, (name, n, c) in sorted(summary.items()):
        print('%#010x %-14s %5d vectors, %4d distinct pcs executed' % (fn, name, n, c))


if __name__ == '__main__':
    main()
