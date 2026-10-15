"""run_e0a.py - E20.e0a1 oracle: the REAL SpawnWarpEntity 0x80039F58 (-> 0x80039B28 page lookup, 0x80039AD0 FindFreeEntitySlot, 0x80039D04 InitializeEntity, 0x80038AB4 first animation switch,
0x80032A40 creation roll) of ALUN_CD.EXE (France) in the emulated machine of the E14/E20 oracles, with BOTH sprite pages installed (espawn.py).

Run from the repository root:   python -B docs/plan-e20-annexe/e20-e0a/oracle/run_e0a.py      (about 1 minute; exit code 0 = every check of out/summary.txt is PASS)
Writes only into out/ next to this file: summary.txt, manifest.txt (md5 of every other file; two runs are identical), v1.json, v2.json, v3.json, v4-pairs.txt, v5.json, v6-map14.txt, v6.json (E20.e0a3a, birth tick), v7.json, sounds.txt,
two-interpreters.txt, coverage.txt.  mutants.py (same folder) writes mutations.txt.

Vector files: one JSON object per line.  Positions x, y are in pixels (the binary receives them shifted by 16), z is the RAW 16.16 argument of the binary (the frame of the binary: the DLL frame is this minus 1, ADR-0026),
dir is the direction word (0, 8, 0x10, 0x18).  Nothing here knows an expected value of the DLL: every number is read from the machine's RAM."""
import os
import sys
import json
import hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import espawn as E
from machine import M32
import lib

OUT = os.path.join(HERE, 'out')
SEED = 0x12345678
HERO = E.ent(0)
DIRS = [0x10, 0, 8, 0x18]
AT9 = os.path.join(E.REPO, 'docs', 'plan-e20-annexe', 'e20-b', 'oracle', 'out', 'at9')
MAP_INDICES = os.path.join(E.REPO, 'alundra-project', 'Data', 'map-sprite-indices.json')
SPRITE_RECORDS = os.path.join(E.REPO, 'alundra-project', 'Data', 'sprite-records.json')
EXPECT_SHA1_INDICES = '63a3e6189d1f1375232bb2c54e1d1dcb385e360d'


def s32(v):
    return v - (1 << 32) if v & 0x80000000 else v


# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
# one creation
# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
def projection(w, p, aux_slot=None):
    """the fields the DLL compares (decoded names), read from the machine's RAM"""
    s = w.snap(p)
    m = w.m

    def who(slot):
        if slot is None:
            return 'none'
        if slot == 0:
            return 'hero'
        if slot == aux_slot:
            return 'parent'
        return 'slot%d' % slot

    hdr = m.peek(p + 0x64)
    return {
        'slot': E.slot_of(p), 'serial': s['serial'], 'status': s['status'], 'hp': s['hp'], 'hpmax': s['hpmax'], 'ref': s['ref48'], 'id': s['id68'], 'flags': s['flags6C'],
        'sprog': [s['cA'], s['cB'], s['cC'], s['cD'], s['cE'], s['cF']], 'prog': [s['prog4c_%d' % k] for k in range(6)], 'anim_t': s['anim_t'], 'dir_t': s['dir_t'],
        'px': s['px'], 'py': s['py'], 'pz': s['pz'], 'modx': s['modx'], 'mody': s['mody'], 'modz': s['modz'],
        'mx': s['bodyoff'], 'my': s['bodyoffy'], 'mz': s['bodyoffz'], 'w': s['bodysx'], 'h': s['bodysy'], 'd': s['bodysz'],
        'tilex': s['tilex120'], 'tiley': s['tiley124'], 'terrain': s['terrain138'], 'floor': s['floor134'], 'corners': [s['mh0'], s['mh1'], s['mh2'], s['mh3']],
        'pal': s['pal1b0'], 'sheet': s['sheet1b4'], 'root': who(s['root']), 'parent': who(s['parent']), 'rec44': s['rec44'], 'loot': [s['loot_item'], s['loot_flag']],
        'has_attack': 1 if s['atk1c8'] else 0, 'has_box': 1 if s['box1d4'] else 0, 'anim_flags': s['animflags_b4'], 'contents': m.peek(hdr + 0x1C) >> 24,
    }


def spawn_one(w, spec):
    """spec: page, index, x, y (px), z (raw), dir, parent in none / hero / spawned.  Returns the vector (spec + results); the entities it created are wiped afterwards."""
    m = w.m
    created = []
    aux_slot = None
    parent_ptr = {'none': 0, 'hero': HERO}.get(spec['parent'])
    if spec['parent'] == 'spawned':
        a = w.spawn(HERO, 0, 3, 150, 100, 0, 0x10)
        assert a, 'the parent of a vector could not be created'
        created.append(a)
        aux_slot = E.slot_of(a)
        parent_ptr = a
    m.poke(E.RNG, SEED)
    n_j = len(m.journal)
    n_p = len(w.printed)
    serial0, count0 = m.peek(E.SERIAL), m.peeks(E.SLOTCOUNT)
    p = w.spawn(parent_ptr, spec['page'], spec['index'], spec['x'], spec['y'], spec['z'], spec['dir'])
    v = dict(spec)
    v['ret'] = E.slot_of(p) if p else 0
    v['prints'] = [[a, b] for a, b in w.printed[n_p:]]
    v['rng_out'] = m.peek(E.RNG)
    v['draws'] = w.draws(SEED, m.peek(E.RNG)) if p else 0
    v['sounds'] = [j[2] for j in m.journal[n_j:] if j[1] == 'sound']
    v['serial_moved'] = int(m.peek(E.SERIAL) != serial0)
    v['count_moved'] = int(m.peeks(E.SLOTCOUNT) != count0)
    if p:
        import hashlib as h
        v['sha1'] = h.sha1(w.image(p)).hexdigest()[:12]
        v['p'] = projection(w, p, aux_slot)
        v['rolls'] = int((v['p']['contents'] & 0x80) != 0)
        created.append(p)
    for q in reversed(created):
        w.wipe(q)
    return v


def dump_lines(path, items):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('[\n' + ',\n'.join(json.dumps(v, sort_keys=True, separators=(',', ':')) for v in items) + '\n]\n')


# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
# the validity sweep (V4)
# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
def sweep(w):
    """483 maps x 256 indices of the real call on the map page.  Returns sets per map, image identity per number, source map (lowest) per number, mismatches against the non-null records"""
    m = w.m
    sets = {}
    imgs = {}
    source = {}
    mism = []
    pairs = 0
    for n in range(483):
        w.swap_map(n)
        ok = []
        for i in range(256):
            m.poke(E.RNG, SEED)
            p = w.spawn(HERO, 1, i, 150, 100, 0, 0x10)
            if p:
                ok.append(i)
                im = bytearray(w.image(p))
                im[0:8] = bytes(8)
                for o in (0x64, 0x9C, 0xA0, 0xA4, 0x194, 0x1D4):       # the index word, the serial and the pointers into the installed record (harness addresses)
                    im[o:o + 4] = bytes(4)
                imgs.setdefault(i, set()).add(hashlib.sha1(bytes(im)).hexdigest()[:10])
                source.setdefault(i, n)
                w.wipe(p)
        sets[n] = ok
        pairs += len(ok)
        if ok != sorted(w.rec_m):
            mism.append(n)
    return sets, imgs, source, mism, pairs


def probes(w):
    """the invalid-index probes: return, error prints, and whether the serial, the slot count word and the generator moved"""
    m = w.m
    out = []
    w.swap_map(14)
    # a valid-looking pointer (a real record) stands where a wrong index would read the table: at 256, at 300 and just before the table (index -1), on both pages, so that the range checks of 0x80039B28 are
    # the only thing that rejects those indices (with a zero word beyond the table, a missing check would still end in the same error)
    for ptrs, rec in ((E.GPTRS, w.rec_g[3]), (E.MPTRS, w.rec_m[sorted(w.rec_m)[0]])):
        for k in (256, 300, -1):
            m.poke(ptrs + 4 * k, rec)
    nullg = [i for i in range(256) if i not in w.rec_g][:3]
    nullm = [i for i in range(256) if i not in w.rec_m][:3]
    # the entries below the count that are not "-1": 0 and a small positive word (neither can be the pointer of a record: the rule of 0x80039B8C-0x80039BA0 is "0x80000000..0x801FFFFF")
    m.poke(E.GPTRS + 4 * nullg[1], 0)
    m.poke(E.GPTRS + 4 * nullg[2], 0x1000)
    m.poke(E.MPTRS + 4 * nullm[1], 0)
    m.poke(E.MPTRS + 4 * nullm[2], 0x1000)
    jobs = [(0, nullg[0], 'null entry (-1)'), (0, nullg[1], 'entry 0'), (0, nullg[2], 'entry 0x1000')]
    jobs += [(0, 256, '256 (a record pointer stands there)'), (0, 300, '300 (idem)'), (0, 0xFFFFFFFF, '-1 (idem just before the table)'), (0, 0x80000000, '0x80000000')]
    jobs += [(1, 213, 'not valid on map 14'), (1, nullm[0], 'null entry (-1)'), (1, nullm[1], 'entry 0'), (1, nullm[2], 'entry 0x1000'), (1, 256, '256 (idem)'), (1, 0xFFFFFFFF, '-1 (idem)')]
    for page, idx, why in jobs:
        m.poke(E.RNG, SEED)
        n_p = len(w.printed)
        serial0, count0 = m.peek(E.SERIAL), m.peeks(E.SLOTCOUNT)
        p = w.spawn(HERO, page, idx, 150, 100, 0, 0x10)
        out.append({'page': page, 'index': idx, 'why': why, 'ret': E.slot_of(p) if p else 0, 'prints': [[a, b] for a, b in w.printed[n_p:]],
                    'serial_moved': int(m.peek(E.SERIAL) != serial0), 'count_moved': int(m.peeks(E.SLOTCOUNT) != count0), 'rng_moved': int(m.peek(E.RNG) != SEED)})
        if p:
            w.wipe(p)
    return out


# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
# V5': the slot table
# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
def slot_table(make):
    """the slot model on fresh worlds: first free slot, a slot of status 4 stays busy until the slot pass, the table fills at the hero + 63 (the 64th and the 65th call return 0 with a message each), lowest free slot first"""
    r = {}
    w = make(0)
    m = w.m
    ps = [w.spawn(HERO, 0, 3, 150, 100, 0, 0x10) for _ in range(5)]
    r['five'] = {'slots': [E.slot_of(p) for p in ps], 'count_word': m.peeks(E.SLOTCOUNT), 'serials': [m.peek(p + 4) for p in ps]}
    m.poke(ps[1] + 0x10, 4)
    p6 = w.spawn(HERO, 0, 3, 150, 100, 0, 0x10)
    r['busy_status4'] = {'slot_of_spawn_before_pass': E.slot_of(p6)}
    m.call(0x80038634)
    r['busy_status4'].update({'status_after_pass': m.peek(ps[1] + 0x10), 'index_word_after_pass': m.peek(ps[1]), 'ref_after_pass': m.peeks(ps[1] + 0x48), 'count_word': m.peeks(E.SLOTCOUNT)})
    p7 = w.spawn(HERO, 0, 4, 150, 100, 0, 0x10)
    r['reuse_after_pass'] = {'slot': E.slot_of(p7), 'serial': m.peek(p7 + 4), 'previous_serial': m.peek(p6 + 4)}
    # the whole table, from an empty one
    w = make(0)
    m = w.m
    accepted = []
    while len(accepted) < 80:
        q = w.spawn(HERO, 0, 3, 150, 100, 0, 0x10)
        if q == 0:
            break
        accepted.append(E.slot_of(q))
    serial0, count0, rng0 = m.peek(E.SERIAL), m.peeks(E.SLOTCOUNT), m.peek(E.RNG)
    n_p = len(w.printed)
    live = sum(1 for i in range(64) if m.peek(E.ent(i) + 0x10))
    q64 = w.spawn(HERO, 0, 3, 150, 100, 0, 0x10)
    q65 = w.spawn(HERO, 0, 3, 150, 100, 0, 0x10)
    r['full'] = {'accepted': len(accepted), 'slots_first_last': [accepted[0], accepted[-1]], 'live_slots_with_the_hero': live, 'refused_64th': q64, 'refused_65th': q65,
                 'prints_of_the_two_refusals': [[a, b] for a, b in w.printed[n_p:]], 'serial_moved_by_refusals': int(m.peek(E.SERIAL) != serial0), 'count_word': m.peeks(E.SLOTCOUNT),
                 'count_moved_by_refusals': int(m.peeks(E.SLOTCOUNT) != count0), 'rng_moved_by_refusals': int(m.peek(E.RNG) != rng0)}
    # E20.e0a2 T1 (line 1): an invalid index with the table full: the page lookup refuses first, only its own message ("Race", 0x800239CC at 0x80039BB8), never the message of the full table (0x800239B0)
    null_index = [i for i in range(256) if i not in w.rec_g][0]
    n_p, serial0, count0, rng0 = len(w.printed), m.peek(E.SERIAL), m.peeks(E.SLOTCOUNT), m.peek(E.RNG)
    qi = w.spawn(HERO, 0, null_index, 150, 100, 0, 0x10)
    r['invalid_full'] = {'ret': E.slot_of(qi) if qi else 0, 'index': null_index, 'prints': [[a, b] for a, b in w.printed[n_p:]], 'serial_moved': int(m.peek(E.SERIAL) != serial0),
                         'count_moved': int(m.peeks(E.SLOTCOUNT) != count0), 'rng_moved': int(m.peek(E.RNG) != rng0)}
    for s in (40, 9, 21):
        m.poke(E.ent(s) + 0x10, 4)
    m.call(0x80038634)
    r['lowest_first'] = [E.slot_of(w.spawn(HERO, 0, 3, 150, 100, 0, 0x10)) for _ in range(3)]
    r['recycled_loop2'] = recycled_loop2(make(0))
    return r


CTAB = 0x800C4F34
FAKE_C = 0x8008FF00
A0_HANDLER = 0x8006174C


def recycled_loop2(w):
    """E20.e0a2 T1 (line 2): an apparition made in loop 2 of the events pass (the C handler of a status-2 entity, as in the E20.e0a probe P3) takes a slot that the slot pass of the SAME tick has just recycled: five entities in the slots 1 to 5,
    the slots 2 and 4 dead (status 4) since the tick before, the slot pass frees both, the spawner (slot 5) spawns in loop 2 -> slot 2; a second apparition after the pass takes slot 4, a third slot 6"""
    m = w.m
    m.hooks[0x80031B50] = lambda mm: None            # MovePlayer: nothing
    m.hooks[A0_HANDLER] = lambda mm: mm.r.__setitem__(2, 0)
    ps = [w.spawn(HERO, 0, 3, 150, 100, 0, 0x10) for _ in range(5)]
    spawner = ps[4]
    m.poke(spawner + 0x10, 2)                        # status 2, C code 200
    m.poke(spawner + 0x78, 200)
    m.poke(spawner + 0x80, 0)
    m.poke(spawner + 0x6C, 0)
    m.poke(CTAB + 4 * 200, FAKE_C)
    box = {}

    def handler(mm):
        if (mm.r[4] & M32) == spawner and 'p' not in box:
            sp = (mm.r[29] - 0x800) & ~7
            mm.poke(sp + 0x10, 100 << 16)
            mm.poke(sp + 0x14, 40 << 16)
            mm.poke(sp + 0x18, 0x10)
            box['p'] = lib_side_call(mm, sp)
        mm.r[2] = 0
    m.hooks[FAKE_C] = handler
    for dead in (ps[1], ps[3]):
        m.poke(dead + 0x10, 4)
    out = {'slots_before': [E.slot_of(p) for p in ps], 'dead_before': [2, 4]}
    m.call(0x80038634, limit=5_000_000)
    out['status_after_slot_pass'] = [m.peek(p + 0x10) for p in ps]
    out['serial_before'] = m.peek(E.SERIAL)
    m.call(0x800386D0, limit=5_000_000)
    p = box.get('p')
    out['spawned_in_loop2_slot'] = E.slot_of(p) if p else 0
    out['status_of_spawned_after_events'] = m.peek(p + 0x10) if p else None
    out['second_after_pass'] = E.slot_of(w.spawn(HERO, 0, 3, 150, 100, 0, 0x10))
    out['third_after_pass'] = E.slot_of(w.spawn(HERO, 0, 3, 150, 100, 0, 0x10))
    return out


def lib_side_call(mm, sp):
    import c1world as C
    return C.side_call(mm, 0x80039F58, (HERO, 0, 3, 190 << 16), sp)


def dirty_slot(w):
    """a free slot (status 0) that is not the template: +0x44, +0x48, +0x3C, +0x40 hold leftovers; the creation must overwrite +0x44 and +0x48 (and the roll +0x3C/+0x40 if the sprite rolls)"""
    m = w.m
    s1 = E.ent(1)
    m.poke(s1 + 0x44, 0x1234)
    m.poke(s1 + 0x48, 7)
    m.poke(s1 + 0x3C, 0x55)
    m.poke(s1 + 0x40, 0x66)
    v = spawn_one(w, {'id': 'dirty', 'page': 0, 'index': 3, 'x': 150, 'y': 100, 'z': 0, 'dir': 0x10, 'parent': 'hero', 'world': 'flat', 'map': None})
    return v


# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
# the vector sets
# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
def v1_vectors(w):
    return [spawn_one(w, {'id': 'g%d' % i, 'page': 0, 'index': i, 'x': 150, 'y': 100, 'z': 0, 'dir': 0x10, 'parent': 'hero', 'world': 'flat', 'map': None}) for i in sorted(w.rec_g)]


def v2_vectors(w, source):
    out = []
    cur = None
    for i in sorted(source, key=lambda k: (source[k], k)):
        if cur != source[i]:
            w.swap_map(source[i])
            cur = source[i]
        v = spawn_one(w, {'id': 'm%d' % i, 'page': 1, 'index': i, 'x': 150, 'y': 100, 'z': 0, 'dir': 0x10, 'parent': 'hero', 'world': 'flat', 'map': source[i]})
        out.append(v)
    out.sort(key=lambda v: v['index'])
    return out


PX = 1 << 16


def v3_vectors(make_world, source):
    """the geometry vectors: the ten hero projectile types and the items 1 and 2 at several Z, the four directions, the three parents, the prefabs with OffsetZ != 0 above the ground, a raised terrain (32 px)"""
    base = [(0, k) for k in (3, 4, 7, 11, 12, 14, 15, 27, 28, 29, 1, 2)]
    zs = [0, 1, 16 * PX, -5 * PX]
    parents = ['hero', 'none', 'spawned', 'hero']
    specs = []
    k = 0
    for bi, (page, idx) in enumerate(base):
        for zi, z in enumerate(zs):
            specs.append(('flat', page, idx, z, DIRS[(zi + bi) % 4], parents[(bi + zi) % 4]))
            k += 1
    for idx in (20, 64, 242):
        for z in (0, 30 * PX, 50 * PX):
            specs.append(('flat', 0, idx, z, DIRS[(idx + z // PX) % 4], 'hero'))
    for idx in (153, 251, 194, 199, 223, 142, 215, 84, 108, 47):
        for z in (0, 40 * PX):
            specs.append(('flat', 1, idx, z, DIRS[(idx + z // PX) % 4], 'hero'))
    for bi, (page, idx) in enumerate(base):
        for z in (0, 16 * PX):
            specs.append(('raised', page, idx, z, DIRS[(bi + z // PX) % 4], parents[bi % 4]))
    for page, idx in ((0, 20), (0, 64), (0, 242), (1, 153)):
        for z in (0, 40 * PX):
            specs.append(('raised', page, idx, z, 0x10, 'hero'))
    out = []
    worlds = {'flat': make_world(0), 'raised': make_world(2)}
    for n, (wk, page, idx, z, d, par) in enumerate(specs):
        w = worlds[wk]
        src = source[idx] if page == 1 else None
        if page == 1:
            w.swap_map(src)
        out.append(spawn_one(w, {'id': 'x%03d' % n, 'page': page, 'index': idx, 'x': 150, 'y': 100, 'z': z, 'dir': d, 'parent': par, 'world': wk, 'map': src}))
    return out


def v7_vectors(w):
    """the spawn calls the AT9 oracle traces journal at the real sites (hero shot, types 3, 4, 11, 14, on the ground and in the air): replayed with their own arguments"""
    calls = []
    for name in sorted(os.listdir(AT9)):
        if not name.endswith('.machine.txt'):
            continue
        for line in open(os.path.join(AT9, name), encoding='utf-8'):
            if "'spawn_39F58'" in line:
                tick = int(line[1:line.index(',')])
                args = [int(x) for x in line[line.index('[') + 1:line.index(']')].split(',')]
                calls.append((name[:-len('.machine.txt')], tick, args))
    out = []
    for name, tick, a in calls:
        assert a[0] == HERO and a[3] % PX == 0 and a[4] % PX == 0, (name, a)
        v = spawn_one(w, {'id': 'a9-%s' % name, 'trace': name, 'tick': tick, 'page': a[1], 'index': a[2], 'x': a[3] // PX, 'y': a[4] // PX, 'z': s32(a[5]), 'dir': a[6], 'parent': 'hero', 'world': 'flat', 'map': None})
        out.append(v)
    return out


# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
# the second interpreter
# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
def two_interpreters(make_world):
    import interp2

    def run(use2):
        w = make_world(0)
        m = w.m
        out = []
        jobs = [(0, i) for i in sorted(w.rec_g)] + [(1, i) for i in sorted(w.rec_m)] + [(0, 5), (0, 256), (1, 213)]
        for page, i in jobs:
            sp = 0x801FFF00
            m.poke(sp + 0x10, 100 << 16)
            m.poke(sp + 0x14, 0)
            m.poke(sp + 0x18, 0x10)
            m.poke(E.RNG, SEED)
            if use2:
                p = interp2.call2(m, 0x80039F58, HERO, page, i, 150 << 16) & M32
            else:
                p = m.call(0x80039F58, HERO, page, i, 150 << 16, limit=5_000_000) & M32
            if p:
                out.append((page, i, E.slot_of(p), hashlib.sha1(w.image(p)).hexdigest()[:12], m.peek(E.RNG)))
                w.wipe(p)
            else:
                out.append((page, i, None, None, m.peek(E.RNG)))
        return out

    a = run(False)
    b = run(True)
    return len(a), a == b


# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
# coverage
# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
# the half of InitializeEntity that stores the argument of a registered record into +0x48 (0x80039D74-0x80039D7C, reached only when a3 != 0): SpawnWarpEntity passes a3 = zero at 0x8003A010, so it is not part of this oracle
UNREACHABLE = {0x80039D74, 0x80039D78, 0x80039D7C}
FUNCS = [('SpawnWarpEntity', 0x80039F58), ('page lookup', 0x80039B28), ('FindFreeEntitySlot', 0x80039AD0), ('InitializeEntity', 0x80039D04)]


def func_range(entry):
    ins = lib.dis(entry, 400)
    maxt = entry
    for i in ins:
        mn, op = i.mnemonic, i.op_str
        if mn in ('beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'b'):
            try:
                t = int(op.split(',')[-1].strip(), 16)
            except ValueError:
                t = 0
            if t > i.address:
                maxt = max(maxt, t)
        if mn == 'j':
            try:
                t = int(op.strip(), 16)
            except ValueError:
                t = 0
            if t >= entry:
                maxt = max(maxt, t)
        if mn == 'jr' and op.strip() == '$ra' and i.address >= maxt - 4:
            return entry, i.address + 8
    return entry, entry + 4 * len(ins)


def coverage_text(cov):
    lines = []
    ok = True
    for name, entry in FUNCS:
        a, b = func_range(entry)
        words = list(range(a, b, 4))
        miss = [x for x in words if x not in cov]
        rng = []
        for x in miss:
            if rng and x == rng[-1][1] + 4:
                rng[-1][1] = x
            else:
                rng.append([x, x])
        lines.append('%-20s %08X-%08X  %3d of %3d instructions executed ; not executed: %s' % (name, a, b - 4, len(words) - len(miss), len(words), ' '.join('%08X-%08X' % (p, q) for p, q in rng) or 'none'))
        ok = ok and set(miss) <= UNREACHABLE
    return lines, ok


# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
WORLDS = []


def make_world(raised=0, map_no=14):
    w = E.World(map_no=map_no, raised=raised)
    WORLDS.append(w)
    return w


def battery(make):
    """everything except the 483-map sweep and the interpreter comparison: the signature the mutants are measured on.  Returns the dict of result lists."""
    w = make(0)
    r = {}
    r['v1'] = v1_vectors(w)
    # the validity sets are needed to find the source map of each number of the map page: the non-null records of the export (the sweep proves their equality with the real call)
    source = {}
    for n in range(483):
        for i, rec in enumerate(E.mapjson(n)['SpriteRecords'][:256]):
            if rec:
                source.setdefault(i, n)
    r['source'] = source
    r['v2'] = v2_vectors(make(0), source)
    r['v3'] = v3_vectors(make, source)
    r['v5'] = slot_table(make)
    r['v5dirty'] = dirty_slot(make(0))
    r['probes'] = probes(make(0))
    r['v7'] = v7_vectors(make(0))
    return r


def signature(r):
    return hashlib.sha1(json.dumps({k: r[k] for k in r if k != 'source'}, sort_keys=True).encode()).hexdigest()


def main():
    os.makedirs(OUT, exist_ok=True)
    checks = []

    def check(name, ok, detail):
        checks.append((name, ok, detail))
        print('%s %s: %s' % ('PASS' if ok else 'FAIL', name, detail), flush=True)

    # --- V4: the sweep (also the source of the map page validity) -------------------------------------------------------------------------------------------------------------------
    w4 = make_world(0)
    sets, imgs, source, mism, pairs = sweep(w4)
    cov = set(w4.m.cov)
    empty = sum(1 for v in sets.values() if not v)
    check('V4 pairs', pairs == 2507 and not mism, '%d valid (map, index) pairs on 483 maps x 256 indices; maps whose set differs from the non-null SpriteRecords of the export: %d' % (pairs, len(mism)))
    check('V4 empty maps', empty == 65, '%d maps without any valid index' % empty)
    check('V4 max per map', max(len(v) for v in sets.values()) <= 22, 'at most %d valid indices on one map' % max(len(v) for v in sets.values()))
    check('V4 distinct numbers', len(imgs) == 244, '%d distinct numbers valid on at least one map' % len(imgs))
    check('V4 one image per number', all(len(v) == 1 for v in imgs.values()), 'numbers with a single creation image on every map where they are valid: %d of %d' % (sum(1 for v in imgs.values() if len(v) == 1), len(imgs)))
    raw = open(MAP_INDICES, 'rb').read()
    sha = hashlib.sha1(raw).hexdigest()
    exported = json.loads(raw.decode('utf-8'))
    same = all(exported.get(str(n)) == sets[n] for n in range(483)) and len(exported) == 483
    check('V4 equals Data/map-sprite-indices.json', same and sha == EXPECT_SHA1_INDICES, 'the sets of the real call equal the exported file on 483 maps ; SHA-1 %s (%d bytes)' % (sha, len(raw)))
    with open(os.path.join(OUT, 'v4-pairs.txt'), 'w', encoding='utf-8', newline='\n') as f:
        for n in range(483):
            f.write('%d:%s\n' % (n, ','.join(str(i) for i in sets[n])))
    with open(os.path.join(OUT, 'v6-map14.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# V6\': the numbers of the map page that are valid on map 14 (the full interface path of the DLL runs on map 14); the global page is valid on every map (151 numbers)\n')
        f.write('map 14 count %d\n' % len(sets[14]))
        f.write('map 14 indices %s\n' % ','.join(str(i) for i in sets[14]))

    # --- the other vectors ---------------------------------------------------------------------------------------------------------------------------------------------------------
    r = battery(make_world)
    mapped = {k: v for k, v in source.items()}
    check('source map of every number', r['source'] == mapped, '%d numbers, source = the lowest map where the number is valid (equal between the sweep and the export)' % len(mapped))
    v1, v2, v3, v7 = r['v1'], r['v2'], r['v3'], r['v7']
    check('V1 global page', len(v1) == 151 and all(v['ret'] for v in v1), '%d of 151 indices of the global page spawn (parent hero, flat, direction 0x10, Z 0)' % sum(1 for v in v1 if v['ret']))
    check('V2 map page', len(v2) == 244 and all(v['ret'] for v in v2), '%d of 244 numbers of the map page spawn (one vector per number, source map recorded)' % sum(1 for v in v2 if v['ret']))
    check('V1/V2 palette and sheet', all((v['p']['pal'], v['p']['sheet']) == (0xB, 0x60) for v in v1) and all((v['p']['pal'], v['p']['sheet']) == (0, 0x20) for v in v2), 'global page: palette 0xB, sheet 0x60; map page: palette 0, sheet 0x20')
    check('V1/V2 identifier', all(v['p']['id'] == v['index'] for v in v1) and all(v['p']['id'] == v['index'] + 0x100 for v in v2), '+0x68 = index on the global page, index + 0x100 on the map page')
    check('V1/V2 roll', sum(v['rolls'] for v in v1) == 6 and sum(v['rolls'] for v in v2) == 48 and all((v['draws'] > 0) == bool(v['rolls']) for v in v1 + v2), 'creation roll (Contents & 0x80): %d on the global page, %d on the map page ; a vector draws exactly when it rolls' % (sum(v['rolls'] for v in v1), sum(v['rolls'] for v in v2)))
    snd = [(v['page'], v['index'], [x for x in v['sounds'] if x]) for v in v1 + v2 if any(v['sounds'])]
    check('V1/V2 creation sound', sum(1 for s in snd if s[0] == 0) == 7 and sum(1 for s in snd if s[0] == 1) == 9 and sum(1 for s in snd if s[0] == 1 and any(x & 0x100 for x in s[2])) == 3, 'creation sound: %d prefabs of the global page, %d numbers of the map page (%d with the 0x100 flag)' % (sum(1 for s in snd if s[0] == 0), sum(1 for s in snd if s[0] == 1), sum(1 for s in snd if s[0] == 1 and any(x & 0x100 for x in s[2]))))
    with open(os.path.join(OUT, 'sounds.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# sound requests journalled by the creation (page, index, sound ids): the sound of the animation 0 (0x800490FC, from the first switch 0x80038AB4)\n')
        for page, idx, sd in snd:
            f.write('page %d index %d sounds %s\n' % (page, idx, ' '.join('0x%X' % x for x in sd)))
    check('V3 geometry', len(v3) >= 40 and all(v['ret'] for v in v3), '%d geometry vectors, all accepted' % len(v3))
    check('V7 AT9 sites', len(v7) == 8 and all(v['ret'] for v in v7), '%d of 8 spawn calls of the AT9 traces replayed with their own arguments (types %s)' % (sum(1 for v in v7 if v['ret']), sorted(set(v['index'] for v in v7))))
    t5 = r['v5']
    check('V5 slots', t5['five']['slots'] == [1, 2, 3, 4, 5] and t5['five']['count_word'] == 5, 'five spawns: slots %s, slot count word %d' % (t5['five']['slots'], t5['five']['count_word']))
    check('V5 status 4 stays busy', t5['busy_status4']['slot_of_spawn_before_pass'] == 6 and t5['busy_status4']['status_after_pass'] == 0 and t5['busy_status4']['ref_after_pass'] == -1 and t5['reuse_after_pass']['slot'] == 2, 'a slot of status 4 is busy until the slot pass (spawn went to %d), then free with +0x48 = -1 and its index word restored (%d); next spawn takes slot %d' % (t5['busy_status4']['slot_of_spawn_before_pass'], t5['busy_status4']['index_word_after_pass'], t5['reuse_after_pass']['slot']))
    f5 = t5['full']
    check('V5 cap 63', f5['accepted'] == 63 and f5['live_slots_with_the_hero'] == 64 and f5['refused_64th'] == 0 and f5['refused_65th'] == 0 and len(f5['prints_of_the_two_refusals']) == 2
          and not f5['serial_moved_by_refusals'] and not f5['count_moved_by_refusals'] and not f5['rng_moved_by_refusals'],
          'from an empty table %d apparitions are accepted (slots %d to %d, the hero makes 64 live), the 64th and the 65th return 0 with one message each (%s), serial, slot count word and generator unchanged' % (f5['accepted'], f5['slots_first_last'][0], f5['slots_first_last'][1], [hex(x[0]) for x in f5['prints_of_the_two_refusals']]))
    check('V5 lowest first', t5['lowest_first'] == [9, 21, 40], 'after freeing 40, 9, 21: next three spawns went to %s' % t5['lowest_first'])
    inv = t5['invalid_full']
    check('V5 invalid index with a full table', inv['ret'] == 0 and inv['prints'] == [[0x80039BB8, 0x800239CC]] and not inv['serial_moved'] and not inv['count_moved'] and not inv['rng_moved'],
          'table full (the hero + 63), a null entry of the global page (%d): return 0, only the message of the page lookup (%s, "Race" 0x800239CC), never the message of the full table (0x800239B0); serial, slot count word and generator unchanged' % (inv['index'], [hex(x[0]) for x in inv['prints']]))
    l2 = t5['recycled_loop2']
    check('V5 loop 2 in a recycled slot', l2['slots_before'] == [1, 2, 3, 4, 5] and l2['status_after_slot_pass'][1] == 0 and l2['status_after_slot_pass'][3] == 0 and l2['spawned_in_loop2_slot'] == 2
          and l2['status_of_spawned_after_events'] == 1 and l2['second_after_pass'] == 4 and l2['third_after_pass'] == 6,
          'slots 2 and 4 dead since the tick before, freed by the slot pass of the tick; an apparition in loop 2 takes slot %d (state %s after the events pass), the next ones go to %d then %d' % (l2['spawned_in_loop2_slot'], l2['status_of_spawned_after_events'], l2['second_after_pass'], l2['third_after_pass']))
    dv = r['v5dirty']
    check('V5 dirty slot', dv['p']['ref'] == -1 and dv['p']['rec44'] == 0 and dv['ret'] == 1, 'a dirty free slot (+0x44, +0x48, +0x3C, +0x40 leftovers): the creation writes +0x44 = %d and +0x48 = %d ; loot words %s' % (dv['p']['rec44'], dv['p']['ref'], dv['p']['loot']))
    pr = r['probes']
    one_msg = all(len(p['prints']) == 1 and p['prints'][0][0] == 0x80039BB8 and p['ret'] == 0 and not p['serial_moved'] and not p['count_moved'] and not p['rng_moved'] for p in pr)
    check('V4 probes', one_msg, '%d invalid indices (null entry, 256, 300, -1, 0x80000000, not valid on this map): return 0, exactly one message at 0x80039BB8, serial, slot count word and generator unchanged' % len(pr))
    dump_lines(os.path.join(OUT, 'v1.json'), v1)
    dump_lines(os.path.join(OUT, 'v2.json'), v2)
    dump_lines(os.path.join(OUT, 'v3.json'), v3)
    dump_lines(os.path.join(OUT, 'v7.json'), v7)
    with open(os.path.join(OUT, 'v5.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'table': t5, 'dirty': dv, 'probes': pr}, f, sort_keys=True, indent=1)
        f.write('\n')

    # --- V6: what a fresh entity runs in its birth tick (E20.e0a3a; probe p3_tick.py, driver v6.py) ---------------------------------------------------------------------------------------------
    import v6
    rows6 = v6.run()
    kinds = dict((r['id'], (r['kind'], v6.classify(r))) for r in rows6)
    same_kind = all(k == (c if c != 'loot_full' else 'birth_full') for k, c in ((v6.classify(r), r['kind']) for r in rows6))
    check('V6 birth tick', len(rows6) == 12 and all(r['spawned'] for r in rows6) and same_kind,
          '12 lines BT-M1..BT-L3 of the real passes: birth in MovePlayer = full participation (A once in tick 1, status 2, in the lists), loop 2 with trigger 0 = A twice (status 1, out of the lists in tick 1), loop 2 with trigger -1 and loop 1 on a lower slot = postponed by one tick, loop 1 on a higher slot = full participation; measured %s' % sorted(kinds.items()))
    with open(os.path.join(OUT, 'v6.json'), 'w', encoding='utf-8', newline=chr(10)) as f:
        for r in rows6:
            f.write(json.dumps(r, sort_keys=True) + chr(10))

    # --- coverage of the four functions (every machine of the run) ---------------------------------------------------------------------------------------------------------------------
    for wk in WORLDS:
        cov |= set(wk.m.cov)
    lines, covered = coverage_text(cov)
    with open(os.path.join(OUT, 'coverage.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# instruction coverage of the functions the vectors exercise (Machine.cov: every executed PC of the sweep world and of the full-table world)\n' + '\n'.join(lines) + '\n')
    check('coverage', covered, 'every instruction of SpawnWarpEntity, the page lookup and FindFreeEntitySlot is executed, and of InitializeEntity all but the 3 of the registered-record half (0x80039D74-0x80039D7C, unreachable with a3 = 0) (coverage.txt)')

    # --- the second interpreter -----------------------------------------------------------------------------------------------------------------------------------------------------
    n_calls, same2 = two_interpreters(make_world)
    check('second interpreter', same2 and n_calls >= 159, 'emu.py and the independent mips.py: %d calls, identical return slot, image hash and generator word: %s' % (n_calls, same2))
    with open(os.path.join(OUT, 'two-interpreters.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('emu.py vs mips.py (interp2.call2): %d calls (151 global + %d map-14 page + 3 invalid) ; identical (return slot, image sha1, generator word): %s\n' % (n_calls, n_calls - 151 - 3, same2))
    check('uninitialised reads', not w4.m.uninit_reads, '%d reads of bytes nobody wrote during the sweep' % len(w4.m.uninit_reads))

    ok = all(c[1] for c in checks)
    with open(os.path.join(OUT, 'summary.txt'), 'w', encoding='utf-8', newline='\n') as f:
        for name, good, detail in checks:
            f.write('%s %s: %s\n' % ('PASS' if good else 'FAIL', name, detail))
        f.write('%s\n' % ('ALL PASS' if ok else 'FAILED'))
        f.write('vector signature %s\n' % signature(r))
    write_manifest()
    print('ALL PASS' if ok else 'FAILED')
    return 0 if ok else 1


def write_manifest():
    names = sorted(n for n in os.listdir(OUT) if n not in ('manifest.txt', 'mutations.txt') and os.path.isfile(os.path.join(OUT, n)))
    with open(os.path.join(OUT, 'manifest.txt'), 'w', encoding='utf-8', newline='\n') as f:
        for n in names:
            f.write('%s  %s\n' % (hashlib.md5(open(os.path.join(OUT, n), 'rb').read()).hexdigest(), n))


if __name__ == '__main__':
    sys.exit(main())
