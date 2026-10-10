"""h1.py - E14.a0b T1 (a): the shared helpers of the enemy AI, each run as REAL code of ALUN_CD.EXE (France) on random states, with the independent model (hmodel.py) as the predicted value.

Helpers (address: what it reads / writes; the C# port in Alundra/Scripts/AlundraEnemyHelpers.cs is compared with the OUTPUT OF THE EMULATED BINARY, row by row):
  relpos      0x8007FE20  reads +0x120 +0x124 +0x134 of the entity and of the hero (slot 0, 0x80127D30); writes six words at a1: [|dx|, |dy|, |dz|, dx, dy, dz]
  relpos2     0x8007FE8C  same, but the Z of the entity is +0x11C (PosZ) instead of its floor +0x134
  tap2        0x8008048C  TryAttackPlayer2(a0, rel, dir, a3, sp+0x10, sp+0x14): reads the six words, returns 0/1
  tapfront    0x800805C8  TryAttackPlayerFront(entity, rel, a2, a3, sp+0x10): the direction is the WORD at entity+0x98
  had         0x80080144  HandleAnimationDirection(entity, anim, dist): reads +0x134 +0x158..+0x164 +0x8C; on a turn writes +0x88, +0xBC +0xC0 +0xC4 +0xC8 +0xD4 +0xD8 (zero), +0x8C
  startwalk   0x8007FEF8  StartWalk(entity, anim, range, chance): the random word 0x80098708, +0x114 +0x118 of the entity and of the hero; writes +0x8C, +0x88, +0x282 (16 bits), the random word
  atan        0x8002E814  direction (0..31) of the vector (a0, a1)
  d1          0x8007D9A4  the Beannoid hit reaction, ComputeNewHp 0x8003A374 STUBBED (returns the chosen value): writes +0x277 (byte), +0x88, +0x8C
  d9          0x8007DE04  the Muruta hit reaction, same
Every case writes ALL the inputs of the helper; the machine reports a read of a byte nobody wrote (none allowed) and the set of entity bytes the call changed must be inside the declared output set.
"""
import gzip
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kworld as K
import world as W
from machine import Machine
import hmodel as H
from hmodel import s32, M32

SELF = W.ent(5)
HERO = W.ent(0)
REL = 0x801EA000
RNG = 0x80098708
STACK = 0x801FFF00            # the stack pointer of both interpreters' call(); the stack arguments are at +0x10 and +0x14
CNH = 0x8003A374              # ComputeNewHp, stubbed for d1 / d9
N_CASES = 10000
MARK_FORCE = 0x11111111       # the old value of the words a helper must zero: a zeroing is then visible
MARK_ANIM = 0x00007777
MARK_DIR = 0x00005555
MARK_HALF = 0x1234


PATCHES = []                  # (address, old word, new word) applied to every machine built (empty except in the mutation tests)


def apply_patches(m):
    for addr, old, new in PATCHES:
        cur = m.peek(addr)
        if cur != old:
            raise RuntimeError('word at %08X is %08X, expected %08X' % (addr, cur, old))
        m.poke(addr, new)


def new_machine():
    m = Machine()
    apply_patches(m)
    m.cnh_ret = 0
    m.cnh_calls = []

    def cnh(m2):
        m2.journal.append((0, 'cnh', m2.r[4] & M32))
        m2.r[2] = m2.cnh_ret
    m.hooks[CNH] = cnh
    return m


# ---- generators: (inputs...) from the Rng; every domain restriction is stated ----------------------------------------------------------
def near(rng, centre):
    return s32(centre + rng.pick((-2, -1, 0, 0, 1, 2)))


def small_or_big(rng, p_small=0.7, lo=-9, hi=70):
    if rng.chance(int(p_small * 100), 100):
        return rng.range(lo, hi)
    return rng.s32()


def gen_relpos(rng, z_is_pos):
    if rng.chance(55, 100):
        sx, sy, hx, hy = rng.range(0, 63), rng.range(0, 63), rng.range(0, 63), rng.range(0, 63)
        if rng.chance(30, 100):
            hx = sx
        if rng.chance(30, 100):
            hy = sy
        base = rng.range(0, 0x400000)
        hz = base
        sz = s32(base + rng.pick((0, 0, 1, -1, 0x10000, -0x10000, 0x100000, -0x100000, 0x7FFF, rng.range(-0x200000, 0x200000))))
    else:
        sx, sy, sz, hx, hy, hz = [rng.s32() for _ in range(6)]
    return [sx, sy, sz, hx, hy, hz]


def gen_rel(rng):
    """six words [|dx|, |dy|, |dz|, dx, dy, dz]: consistent (70%) or independent random words (30%)"""
    if rng.chance(70, 100):
        d = [small_or_big(rng, 0.8, -12, 12) for _ in range(3)]
        d = [s32(x) if abs(x) < 0x40000000 else s32(x >> 3) for x in d]
        return [H.absw(x) for x in d] + d
    return [small_or_big(rng, 0.6, -9, 20) for _ in range(6)]


def gen_threshold(rng, rel, k=None):
    """near |dx|, |dy|, |dz| or a free value, so that every comparison has its boundary cases"""
    if rng.chance(55, 100):
        return s32(rel[rng.range(0, 2)] + rng.pick((-1, 0, 0, 1)))
    return small_or_big(rng, 0.7, -3, 40)


def gen_tap2(rng):
    rel = gen_rel(rng)
    direction = rng.range(0, 3) if rng.chance(85, 100) else rng.range(0, 0x1FF)
    a3, d2, d3 = gen_threshold(rng, rel), gen_threshold(rng, rel), gen_threshold(rng, rel)
    return rel + [direction, a3, d2, d3]


def gen_tapfront(rng):
    rel = gen_rel(rng)
    box_dir = rng.range(0, 3) if rng.chance(85, 100) else small_or_big(rng, 0.5, -3, 9)
    a2, a3, d3 = gen_threshold(rng, rel), gen_threshold(rng, rel), gen_threshold(rng, rel)
    return [box_dir] + rel + [a2, a3, d3]


def gen_had(rng):
    floor = rng.range(0, 0x1000000)
    dist = rng.pick((0, 1, 0x1000, 0x10000, 0x20000, rng.range(0, 0x30000)))
    thr = dist + 1
    corners = []
    for _ in range(4):
        u = rng.below(100)
        if u < 60:
            corners.append(s32(floor - (thr + rng.range(-2, 2))))
        elif u < 80:
            corners.append(floor)
        else:
            corners.append(s32(floor + rng.range(-0x100000, 0x100000)))
    direction = rng.range(0, 31)
    anim = rng.range(0, 0x1FF)
    return [floor] + corners + [direction, anim, dist]


def gen_pos(rng):
    return rng.range(0, 1247) << 16, rng.range(0, 959) << 16


def gen_startwalk(rng):
    rng_word = rng.u32()
    sx, sy = gen_pos(rng)
    if rng.chance(4, 100):
        hx, hy = sx, sy
    else:
        hx, hy = gen_pos(rng)
    anim = rng.range(0, 0x1FF)
    u = rng.below(4)
    rng_range = (0, rng.range(1, 64), rng.range(-64, -1), rng.s32())[u]
    chance = rng.range(0, 0x1FF) if rng.chance(20, 100) else rng.range(0, 110)
    return [s32(rng_word), sx, sy, hx, hy, anim, rng_range, chance]


def gen_atan(rng):
    def comp():
        u = rng.below(100)
        if u < 6:
            return 0
        bits = rng.range(0, 30)
        v = rng.u32() >> (32 - bits - 1) if bits >= 0 else 0
        v = v & 0x7FFFFFFF
        return -v if rng.chance(50, 100) else v
    return [comp(), comp()]


def gen_hit(rng):
    sx, sy = gen_pos(rng)
    hx, hy = gen_pos(rng) if not rng.chance(4, 100) else (sx, sy)
    return [sx, sy, hx, hy, rng.range(0, 1), rng.range(0, 255)]


# ---- runners: write the inputs, call the REAL function through the interpreter under test, read the outputs ---------------------------------
def poke_entity(m, base, fields):
    for off, v in fields.items():
        if off == 0x277:
            m.poke8(base + off, v)
        elif off == 0x282:
            m.poke16(base + off, v)
        else:
            m.poke(base + off, v)


def snap(m, base):
    o = base & 0x1FFFFFFF
    return bytes(m.ram[o:o + W.STRIDE])


def changed(before, after):
    return {i for i in range(len(before)) if before[i] != after[i]}


def span(off, n=4):
    return set(range(off, off + n))


FORCES = (0xBC, 0xC0, 0xC4, 0xC8, 0xD4, 0xD8)
FORCE_BYTES = set().union(*[span(o) for o in FORCES])


def run_relpos(m, c, z_is_pos):
    sx, sy, sz, hx, hy, hz = c
    poke_entity(m, SELF, {0x120: sx, 0x124: sy, (0x11C if z_is_pos else 0x134): sz})
    poke_entity(m, HERO, {0x120: hx, 0x124: hy, 0x134: hz})
    b1, b2 = snap(m, SELF), snap(m, HERO)
    K.call(m, 0x8007FE8C if z_is_pos else 0x8007FE20, SELF, REL)
    out = [m.peeks(REL + 4 * i) for i in range(6)]
    ok = not changed(b1, snap(m, SELF)) and not changed(b2, snap(m, HERO))
    return out, ok


def run_tap2(m, c):
    poke_words(m, REL, c[0:6])
    direction, a3, d2, d3 = c[6:10]
    m.poke(STACK + 0x10, d2)
    m.poke(STACK + 0x14, d3)
    r = K.call(m, 0x8008048C, 0, REL, direction, a3)
    return [r & M32], True


def run_tapfront(m, c):
    poke_entity(m, SELF, {0x98: c[0], 0x9C: (c[0] + 1) & 3})      # +0x9C holds a decoy (the DLL's AnimationDirection): the helper must read +0x98 (BoxDirection)
    poke_words(m, REL, c[1:7])
    a2, a3, d3 = c[7:10]
    m.poke(STACK + 0x10, d3)
    b1 = snap(m, SELF)
    r = K.call(m, 0x800805C8, SELF, REL, a2, a3)
    return [r & M32], not changed(b1, snap(m, SELF))


def poke_words(m, base, words):
    for i, w in enumerate(words):
        m.poke(base + 4 * i, w)


def run_had(m, c):
    floor, c0, c1, c2, c3, direction, anim, dist = c
    fields = {0x134: floor, 0x158: c0, 0x15C: c1, 0x160: c2, 0x164: c3, 0x8C: direction, 0x88: MARK_ANIM}
    for o in FORCES:
        fields[o] = MARK_FORCE
    poke_entity(m, SELF, fields)
    b1 = snap(m, SELF)
    r = K.call(m, 0x80080144, SELF, anim, dist)
    after = snap(m, SELF)
    declared = span(0x88) | span(0x8C) | FORCE_BYTES
    out = [r & M32, m.peek(SELF + 0x88), m.peek(SELF + 0x8C)] + [m.peek(SELF + o) for o in FORCES]
    return [s32(x) for x in out], changed(b1, after) <= declared


def run_startwalk(m, c):
    rng_word, sx, sy, hx, hy, anim, rng_range, chance = c
    m.poke(RNG, rng_word)
    poke_entity(m, SELF, {0x114: sx, 0x118: sy, 0x8C: MARK_DIR, 0x88: MARK_ANIM, 0x282: MARK_HALF})
    poke_entity(m, HERO, {0x114: hx, 0x118: hy})
    b1, b2 = snap(m, SELF), snap(m, HERO)
    K.call(m, 0x8007FEF8, SELF, anim, rng_range, chance)
    declared = span(0x8C) | span(0x88) | span(0x282, 2)
    out = [m.peeks(RNG), m.peeks(SELF + 0x8C), m.peeks(SELF + 0x88), m.peek(SELF + 0x282) & 0xFFFF]
    return out, changed(b1, snap(m, SELF)) <= declared and not changed(b2, snap(m, HERO))


def run_atan(m, c):
    r = K.call(m, 0x8002E814, c[0], c[1])
    return [r & M32], True


def run_hit(m, c, addr):
    sx, sy, hx, hy, killed, old = c
    m.cnh_ret = killed
    del m.journal[:]
    poke_entity(m, SELF, {0x114: sx, 0x118: sy, 0x277: old, 0x88: MARK_ANIM, 0x8C: MARK_DIR})
    poke_entity(m, HERO, {0x114: hx, 0x118: hy})
    b1, b2 = snap(m, SELF), snap(m, HERO)
    K.call(m, addr, SELF)
    declared = {0x277} | span(0x88) | span(0x8C)
    ok = changed(b1, snap(m, SELF)) <= declared and not changed(b2, snap(m, HERO))
    ok = ok and [j for j in m.journal if j[1] == 'cnh'] == [(0, 'cnh', SELF)]       # ComputeNewHp(entity) called exactly once, with the entity
    return [m.peek(SELF + 0x277) & 0xFF, m.peeks(SELF + 0x88), m.peeks(SELF + 0x8C)], ok


# ---- the models as predicted values ---------------------------------------------------------------------------------------------------------
def model_relpos(c, z_is_pos):
    return H.relpos(*c)


def model_tap2(c):
    return [H.try_attack2(c[0:6], c[6], c[7], c[8], c[9])]


def model_tapfront(c):
    return [H.try_attack_front(c[0], c[1:7], c[7], c[8], c[9])]


def model_had(c):
    floor, c0, c1, c2, c3, direction, anim, dist = c
    res, na, nd = H.handle_animation_direction(floor, [c0, c1, c2, c3], direction, anim, dist)
    if res:
        return [1, na, nd] + [0] * 6
    return [0, MARK_ANIM, direction] + [s32(MARK_FORCE)] * 6


def model_startwalk(c):
    rng_word, sx, sy, hx, hy, anim, rng_range, chance = c
    state, direction, a, half = H.start_walk(rng_word & M32, sx, sy, hx, hy, anim, rng_range, chance)
    return [s32(state), direction, a, half if half is not None else MARK_HALF]


def model_atan(c):
    return [H.atan_dir(c[0], c[1])]


def model_hit(c, anim):
    sx, sy, hx, hy, killed, old = c
    return list(H.hit_reaction(sx, sy, hx, hy, killed, old, anim))


RANGES = {
    'relpos': ('relpos 0x8007FE20', 0x8007FE20, 0x8007FE8C), 'relpos2': ('relpos2 0x8007FE8C', 0x8007FE8C, 0x8007FEF8),
    'tap2': ('TryAttackPlayer2 0x8008048C', 0x8008048C, 0x800805C8), 'tapfront': ('TryAttackPlayerFront 0x800805C8', 0x800805C8, 0x80080704),
    'had': ('HandleAnimationDirection 0x80080144', 0x80080144, 0x80080260), 'startwalk': ('StartWalk 0x8007FEF8', 0x8007FEF8, 0x80080030),
    'atan': ('direction 0x8002E814', 0x8002E814, 0x8002E910), 'd1': ('D1 0x8007D9A4', 0x8007D9A4, 0x8007DA08), 'd9': ('D9 0x8007DE04', 0x8007DE04, 0x8007DE68),
}


# instructions no input can reach: the fall-through after the dispatch tests on a value already shown to be 0 or 1 (a u8 below 2 that is neither 0 nor 1), and on the flip index (0..3) none of the four tests matched
DEAD = {'tap2': {0x800804C4, 0x800804C8}, 'atan': {0x8002E8C0, 0x8002E8C4, 0x8002E8D8, 0x8002E8DC}}


def helpers():
    return [
        dict(name='relpos', gen=lambda r: gen_relpos(r, False), run=lambda m, c: run_relpos(m, c, False), model=lambda c: model_relpos(c, False),
             cols_in='self_tile_x self_tile_y self_floor hero_tile_x hero_tile_y hero_floor', cols_out='abs_x abs_y abs_z dx dy dz'),
        dict(name='relpos2', gen=lambda r: gen_relpos(r, True), run=lambda m, c: run_relpos(m, c, True), model=lambda c: model_relpos(c, True),
             cols_in='self_tile_x self_tile_y self_pos_z hero_tile_x hero_tile_y hero_floor', cols_out='abs_x abs_y abs_z dx dy dz'),
        dict(name='tap2', gen=gen_tap2, run=run_tap2, model=model_tap2,
             cols_in='r0 r1 r2 r3 r4 r5 direction a3 d2 d3', cols_out='result'),
        dict(name='tapfront', gen=gen_tapfront, run=run_tapfront, model=model_tapfront,
             cols_in='box_direction r0 r1 r2 r3 r4 r5 a2 a3 d3', cols_out='result'),
        dict(name='had', gen=gen_had, run=run_had, model=model_had,
             cols_in='floor corner0 corner1 corner2 corner3 target_direction anim dist',
             cols_out='result target_anim target_direction force_bc force_c0 force_c4 force_c8 force_d4 force_d8',
             note='old values before the call: +0x88 = %d, +0x8C = %d, the six forces = %d' % (MARK_ANIM, MARK_DIR, MARK_FORCE)),
        dict(name='startwalk', gen=gen_startwalk, run=run_startwalk, model=model_startwalk,
             cols_in='rng self_x self_y hero_x hero_y anim range chance', cols_out='rng_after target_direction target_anim half',
             note='old values before the call: +0x8C = %d, +0x88 = %d, +0x282 = %d' % (MARK_DIR, MARK_ANIM, MARK_HALF)),
        dict(name='atan', gen=gen_atan, run=run_atan, model=model_atan, cols_in='dx dy', cols_out='direction'),
        dict(name='d1', gen=gen_hit, run=lambda m, c: run_hit(m, c, 0x8007D9A4), model=lambda c: model_hit(c, 5),
             cols_in='self_x self_y hero_x hero_y killed old_bytes3', cols_out='bytes3 target_anim target_direction',
             note='ComputeNewHp stubbed: returns `killed`; old values before the call: +0x88 = %d, +0x8C = %d' % (MARK_ANIM, MARK_DIR)),
        dict(name='d9', gen=gen_hit, run=lambda m, c: run_hit(m, c, 0x8007DE04), model=lambda c: model_hit(c, 2),
             cols_in='self_x self_y hero_x hero_y killed old_bytes3', cols_out='bytes3 target_anim target_direction',
             note='ComputeNewHp stubbed: returns `killed`; old values before the call: +0x88 = %d, +0x8C = %d' % (MARK_ANIM, MARK_DIR)),
    ]


SEEDS = {'relpos': 0x14A01, 'relpos2': 0x14A02, 'tap2': 0x14A03, 'tapfront': 0x14A04, 'had': 0x14A05, 'startwalk': 0x14A06, 'atan': 0x14A07, 'd1': 0x14A08, 'd9': 0x14A09}


def cases_of(h, n=N_CASES):
    rng = H.Rng(SEEDS[h['name']])
    return [h['gen'](rng) for _ in range(n)]


def run_helper(h, engine, n=N_CASES, cases=None):
    """-> (rows [(inputs, outputs)], stats dict)"""
    K.use_engine(engine)
    m = new_machine()
    cases = cases or cases_of(h, n)
    rows = []
    bad_write = 0
    model_bad = []
    for i, c in enumerate(cases):
        out, ok = h['run'](m, c)
        if not ok:
            bad_write += 1
        want = h['model'](c)
        if [x & M32 for x in out] != [x & M32 for x in want]:
            model_bad.append((i, c, out, want))
        rows.append((c, out))
    unread = sorted(m.uninit_reads.items())
    K.use_engine('emu')
    return rows, dict(bad_write=bad_write, model_bad=model_bad, uninit=unread, cov=m.cov)


def text_of(h, rows):
    lines = ['# helper %s: %d states (generator seed 0x%X); each row = inputs then outputs, signed 32-bit decimal' % (h['name'], len(rows), SEEDS[h['name']]),
             '# columns in : %s' % h['cols_in'], '# columns out: %s' % h['cols_out']]
    if h.get('note'):
        lines.append('# note: %s' % h['note'])
    for c, o in rows:
        lines.append(' '.join(str(s32(x)) for x in c) + ' | ' + ' '.join(str(s32(x)) for x in o))
    return '\n'.join(lines) + '\n'


def write_gz(path, text):
    raw = text.encode('utf-8')
    with open(path, 'wb') as f:
        with gzip.GzipFile(filename='', mode='wb', fileobj=f, compresslevel=9, mtime=0) as g:
            g.write(raw)
    return raw


def run_all(outdir, n=N_CASES):
    """both interpreters on every helper; the output text must be identical; returns (summary lines, ok, texts)"""
    lines = []
    ok = True
    texts = {}
    covs = set()
    for h in helpers():
        rows, st = run_helper(h, 'emu', n)
        covs |= st['cov']
        rows2, st2 = run_helper(h, 'mips', n)
        t1, t2 = text_of(h, rows), text_of(h, rows2)
        same = t1 == t2
        good = same and not st['bad_write'] and not st['model_bad'] and not st['uninit'] and not st2['bad_write'] and not st2['uninit']
        ok &= good
        texts[h['name']] = t1
        distinct = len({tuple(o) for _, o in rows})
        name, a, b = RANGES[h['name']]
        miss_all = [pc for pc in range(a, b, 4) if pc not in st['cov']]
        dead = sorted(pc for pc in miss_all if pc in DEAD.get(h['name'], ()))
        miss = [pc for pc in miss_all if pc not in dead]
        covered = (b - a) // 4 - len(miss)
        extra = ''
        if h['name'] in ('tap2', 'tapfront'):
            extra = '   results 1: %d' % sum(1 for _, o in rows if o[0] == 1)
        if h['name'] == 'had':
            extra = '   turns: %d' % sum(1 for _, o in rows if o[0] == 1)
        lines.append('%-9s %6d states   emulator == independent model: %d / %d   second interpreter text identical: %s   write-set violations: %d   unwritten-byte reads: %d   distinct outputs: %d%s   instructions executed: %d of %d%s   %s' % (
            h['name'], len(rows), len(rows) - len(st['model_bad']), len(rows), 'yes' if same else 'NO', st['bad_write'] + st2['bad_write'], len(st['uninit']) + len(st2['uninit']), distinct, extra,
            covered, (b - a) // 4, (' (unreachable by construction: %s)' % ' '.join('%08X' % pc for pc in dead)) if dead else (' (NOT EXECUTED: %s)' % ' '.join('%08X' % pc for pc in miss)) if miss else '', 'PASS' if good and not miss else 'FAIL'))
        ok &= not miss
        for i, c, out, want in st['model_bad'][:5]:
            lines.append('   first differences: state %d in %s out %s model %s' % (i, c, [s32(x) for x in out], want))
    return lines, ok, texts, covs


if __name__ == '__main__':
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 300
    ls, ok, _, _ = run_all(None, n)
    print('\n'.join(ls))
    print('H1', 'PASS' if ok else 'FAIL')
