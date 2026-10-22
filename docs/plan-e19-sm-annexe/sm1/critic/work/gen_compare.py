"""Critic: MY OWN generator. Random worlds -> (a) the real handlers on my interpreter, (b) my model of the DLL in the 'prod' montage
(hero in SpawnedEntities slot 0 and PlayerEntity) and the 'ctx' montage (hero only PlayerEntity). Independent of the preparation's vectors."""
import sys, random, json, collections
sys.path.insert(0, 'work')
from mymips import *
import mymodel as M
import replay_binary_lib as RB

INT_MIN, INT_MAX = -2**31, 2**31 - 1
TILES = [-1, 0, 1, 2, 3, 5, 7, 10, 11, 12, 20, 127, 128, 255, 256, 257, 261, 65536, 65541, INT_MIN, INT_MAX, -256, -255]
REFS = [0, 1, 2, 3, 5, 7, 9, 25, 127, 128, 255, 261, 0x1000005, -1, -2]
OPS = (0x60, 0x61, 0x3C, 0x3D)


def rnd_ptr(r, n, me):
    x = r.random()
    if x < 0.35:
        return None
    if x < 0.85:
        return r.randrange(0, n + 1)
    return 'foreign'


def make_world(r, op):
    n = r.choice([1, 2, 3, 4, 5, 6, 9])
    slots = []
    for i in range(n + 1):
        if i == 0:
            slots.append(dict(status=r.choice([2, 2, 2, 1, 3, 0, 4]), ref=-1,
                              flags=r.choice([0x80, 0x31184, 0x6000C0, r.getrandbits(32)]),
                              anim=r.choice([0, 0, 0x80, 4]),
                              tile=[r.choice(TILES + list(range(0, 21))) for _ in range(3)],
                              riding=rnd_ptr(r, n, i), xcoll=rnd_ptr(r, n, i), parent=rnd_ptr(r, n, i), platform=rnd_ptr(r, n, i) if r.random() < 0.3 else None, blocked=False))
            continue
        slots.append(dict(status=r.choice([2, 2, 2, 1, 3, 0, 4]),
                          ref=r.choice(REFS),
                          flags=r.choice([0x80, 0x00, 0x80, r.getrandbits(32), r.getrandbits(32) | 0x80, r.getrandbits(32) & ~0x80, 0x332384, 0xFFFFFF7F, 0x80000000]),
                          anim=r.choice([0, 0, 0, 0x80, 4, r.getrandbits(32) - 2**31, r.getrandbits(32) | 0x80, r.getrandbits(32) & ~0x80]),
                          tile=[r.choice(TILES + list(range(0, 21))) for _ in range(3)],
                          riding=rnd_ptr(r, n, i), xcoll=rnd_ptr(r, n, i), parent=rnd_ptr(r, n, i),
                          platform=(rnd_ptr(r, n, i) if r.random() < 0.3 else None), blocked=r.random() < 0.2))
    logic = r.randrange(0, n + 1)
    anon = [i for i in range(1, n + 1) if r.random() < 0.1 and i != logic]
    for s_ in slots:  # nobody can point at a bare proxy (no Entity behind it): such a pointer is 'foreign'
        for f_ in ('riding', 'xcoll', 'parent', 'platform'):
            if s_[f_] in anon:
                s_[f_] = 'foreign'
    if op in (0x60, 0x61):
        refs_present = [s['ref'] for s in slots[1:] if 0 <= s['ref'] < 128]
        x = r.random()
        if x < 0.6 and refs_present:
            v1 = r.choice(refs_present)
        elif x < 0.75:
            v1 = r.randrange(0, 128)
        else:
            v1 = 0x80 + r.randrange(0, 12)
        code = [op, v1]
    else:
        b = []
        for _ in range(3):
            lo = r.choice([0, 1, 2, 3, 5, 7, 10, 11, 12, 20, 127, 128, 200, 255, r.randrange(256)])
            hi = r.choice([lo, lo, lo + 1, 255, r.randrange(256)]) & 0xFF if r.random() < 0.9 else r.randrange(256)
            b += [lo, hi]
        # bias the bounds around the tile values of the world
        if r.random() < 0.5:
            t = [s['tile'] for s in slots]
            c = r.choice(t)
            b = []
            for k in range(3):
                tv = c[k]
                if 0 <= tv <= 255:
                    b += [max(0, tv - r.choice([0, 0, 1])), min(255, tv + r.choice([0, 0, 1]))]
                else:
                    b += [r.randrange(256), r.randrange(256)]
        code = [op] + b
    return dict(op=op, n=n, slots=slots, logic=logic, anon=anon, code=code)


def to_bin_vector(w):
    return dict(op=w['op'], n=w['n'], slots=w['slots'], logic=w['logic'], code=w['code'])


def dll_world(w, montage):
    n = w['n']
    proxies = []
    for i, s in enumerate(w['slots']):
        ctx = None if i in w['anon'] else 'auto'
        proxies.append(M.P(i, s['status'], s['ref'], s['flags'], to_s32(s['anim']), [to_s32(t) for t in s['tile']],
                           blocked=s['blocked'], ctx=ctx, is_player=(i == 0)))
    foreign_proxy = M.P(99, 2, 77, 0, 0, [0, 0, 0], ctx='F')
    for i, s in enumerate(w['slots']):
        p = proxies[i]
        def idty(x):
            if x is None:
                return None
            if x == 'foreign':
                return 'FOREIGN'
            return proxies[x].ctx
        p.riding = idty(s['riding'])
        p.parent = idty(s['parent'])
        p.platform = idty(s['platform'])
        x = s['xcoll']
        p.xcoll = None if x is None else (foreign_proxy if x == 'foreign' else proxies[x])
    player = proxies[0]
    spawned = proxies if montage == 'prod' else proxies[1:]
    return proxies, spawned, player


def to_s32(x):
    x &= 0xFFFFFFFF
    return x - 2**32 if x & 0x80000000 else x


def main(seed, count):
    r = random.Random(seed)
    stats = collections.Counter()
    diffs = collections.defaultdict(list)
    for k in range(count):
        op = OPS[k % 4]
        w = make_world(r, op)
        v = to_bin_vector(w)
        res, v0, bad, above = RB.run(v, True)
        res2, _, _, _ = RB.run(v, False)
        if res != res2 or bad or above:
            stats['bin_unstable'] += 1
            diffs['bin_unstable'].append(w)
        proxies, spawned, player = dll_world(w, 'prod')
        mp = M.run(op, proxies[w['logic']], w['code'], spawned, player)
        proxies2, spawned2, player2 = dll_world(w, 'ctx')
        mc = M.run(op, proxies2[w['logic']], w['code'], spawned2, player2)
        stats[(hex(op), 'res', res)] += 1
        if mp != res:
            stats[(hex(op), 'prod_diff')] += 1
            diffs[(hex(op), 'prod')].append(w)
        if mc != res:
            stats[(hex(op), 'ctx_diff')] += 1
            diffs[(hex(op), 'ctx')].append(w)
    return stats, diffs


if __name__ == '__main__':
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    count = int(sys.argv[3]) if len(sys.argv) > 3 else 20000
    stats, diffs = main(seed, count)
    for k in sorted(stats, key=str):
        print(k, stats[k])
    for k, ws in diffs.items():
        print("DIFFS", k, len(ws))
        for w in ws[:3]:
            print("   ", json.dumps(w)[:600])
