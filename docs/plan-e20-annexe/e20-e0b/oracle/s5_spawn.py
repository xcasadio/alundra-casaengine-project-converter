"""s5_spawn.py - the loot roll through the REAL SpawnWarpEntity 0x80039F58 -> InitializeEntity 0x80039D04 -> 0x80032A40 (record-less path), for every sprite number whose header
Contents is nonzero (6 on the global page, 48 on the map page) plus control numbers with Contents 0, with three RNG words each.  The model (rows of the table) is checked against the
machine: item, draws, RNG word out.  Output: out/spawn_vectors.json."""
import sys
import json
sys.path.insert(0, 'tools')
sys.path.insert(0, '.')
import espawn as X
from machine import M32, s32

TAB = 0x80028E0C


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


def find_seed(n1):
    g = 0x2468ACE1
    while True:
        g = lcg(g)
        if (lcg(g) >> 28) == n1:
            return g


w = X.World(map_no=14)
m = w.m
rows = None


def rb(a):
    return m.peek(a & ~3) >> (8 * (a & 3)) & 0xFF


rows = [[rb(TAB + 16 * r + c) for c in range(16)] for r in range(128)]


def model(code, s):
    n = 0
    while True:
        if code >= 0x100:
            return 0, s, n
        if code & 0x80:
            s = lcg(s)
            n += 1
            code = rows[code & 0x7F][s >> 28]
        else:
            return (code if code < 0x62 else 0), s, n


# which numbers: contents per number and the lowest map where valid
D = X.DATA
import glob, re, os
g = json.load(open(D + '/map_alundra.json', encoding='utf-8-sig'))['SpriteInfo']['SpriteRecords']
glob_nums = [i for i, r in enumerate(g) if r]
glob_contents = {i: g[i]['Header']['Contents'] for i in glob_nums}
lowest = {}
contents_map = {}
for p in glob.glob(D + '/map_*.json'):
    mo = re.match(r'map_(\d+)\.json', os.path.basename(p))
    if not mo:
        continue
    n = int(mo.group(1))
    sr = (json.load(open(p, encoding='utf-8-sig')).get('SpriteInfo') or {}).get('SpriteRecords') or []
    for i, r in enumerate(sr):
        if r:
            if i not in lowest or n < lowest[i]:
                lowest[i] = n
                contents_map[i] = r['Header']['Contents']
map_nums = sorted(lowest)
print('global numbers', len(glob_nums), 'map numbers', len(map_nums))

seeds = [('S0', 0x12345678), ('N3', find_seed(3)), ('N15', find_seed(15))]
vec = []
bad = 0
hero = X.ent(0)
# control numbers: contents 0
controls_g = [3, 4, 7, 0xB]
controls_m = [79, 136, 144]
targets = [(0, i) for i in glob_nums if glob_contents[i]] + [(1, i) for i in map_nums if contents_map[i]]
targets += [(0, i) for i in controls_g if i in glob_nums] + [(1, i) for i in controls_m if i in lowest]
cur_map = 14
for page, idx in targets:
    if page == 1 and lowest[idx] != cur_map:
        w.swap_map(lowest[idx])
        cur_map = lowest[idx]
    code = glob_contents[idx] if page == 0 else contents_map[idx]
    for name, seed in seeds:
        m.poke(X.RNG, seed)
        p = w.spawn(hero, page, idx, 150, 100, 0, 0x10)
        assert p, (page, idx)
        item, st = s32(m.peek(p + 0x3C)), m.peek(X.RNG)
        flag = s32(m.peek(p + 0x40))
        want_item, want_st, want_n = model(code, seed)
        ok = (item == want_item and st == want_st and flag == 0)
        if not ok:
            bad += 1
            print('MISMATCH page %d idx %d code %02X seed %s: real item %d st %08X flag %d / model item %d st %08X' % (page, idx, code, name, item, st, flag, want_item, want_st))
        vec.append(dict(page=page, index=idx, id68=m.peek(p + 0x68), header_contents=code, seed_name=name, rng_in=seed, item=item, flag40=flag, rng_out=st, draws=want_n, rec44=m.peek(p + 0x44), ref48=s32(m.peek(p + 0x48))))
        w.wipe(p)
json.dump(vec, open('out/spawn_vectors.json', 'w'))
print('spawn vectors', len(vec), 'mismatches with the table model:', bad)
# human table
seen = set()
for v in vec:
    k = (v['page'], v['index'])
    if k in seen:
        continue
    seen.add(k)
    row = [x for x in vec if (x['page'], x['index']) == k]
    print('page %d #%3d (id68 0x%03X) Contents 0x%02X  ' % (k[0], k[1], row[0]['id68'], row[0]['header_contents']) + '  '.join('%s: item %2d draws %d out %08X' % (r['seed_name'], r['item'], r['draws'], r['rng_out']) for r in row))
