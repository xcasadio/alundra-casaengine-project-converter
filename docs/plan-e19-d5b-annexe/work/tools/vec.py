"""vec.py - compares every model of models.py with the REAL code of ALUN_CD.EXE, on random single-call vectors (e14b1 scratch).
Usage: python -B vec.py [N]   (N vectors per handler, default 3000). Exit code 0 iff every comparison matches.
Also writes the outcome to vec_report.txt (counts, coverage of the branches by the vectors, mismatches)."""
import random
import sys
from collections import Counter

import models as M
from h import World, Ent, Rng, ENT0, STRIDE, M32, s32

N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
SELF, TOUCHER = 5, 3
ATTACK_REC = 0x80141000       # the toucher's attack record (byte 0 = attribute, byte 1 = power)
REL_BUF = 0x80141100

ADDR = {
    'A0': 0x8006174C, 'C15': 0x80069C84, 'C16': 0x80069F44, 'C17': 0x8006A29C, 'C18': 0x8006A564, 'C19': 0x8006A974,
    'D5': 0x8007DBE0, 'D6': 0x8007DCD8, 'D11': 0x8007DEE8, 'D12': 0x8007DF4C,
}

import emu as _emu
COV = set()
_orig_exec1 = _emu.Emu.exec1


def _exec1(self, pc, wd):
    COV.add(pc)
    return _orig_exec1(self, pc, wd)


_emu.Emu.exec1 = _exec1
w = World()
gdt = lambda dx, dy: w.call(0x8002E814, dx & M32, dy & M32)

rnd = random.Random(20261017)
cov = {}


def pick(lst):
    return lst[rnd.randrange(len(lst))]


def mk_hero():
    h = Ent()
    h.set32(M.POSX, rnd.randrange(0, 60 << 16) if rnd.random() < 0.5 else (20 * 24) << 16)
    h.set32(M.POSY, rnd.randrange(0, 40 << 16) if rnd.random() < 0.5 else (20 * 16) << 16)
    h.set32(M.TILEX, 20)
    h.set32(M.TILEY, 20)
    h.set32(M.FLOOR, pick([0, 0x100000, 0x200000, 0x500000, 0x700000]))
    h.set32(M.POSZ, h.s32(M.FLOOR))
    return h


def mk_self(hero, ctype, a_choices):
    e = Ent()
    e.set32(M.TYPE, ctype)
    e.set32(M.TANIM, pick(a_choices))
    e.set16(M.AI1, pick([0, 0, 1, 1, 2, 3, 5, 0x14, 0x1E, 0x3C, 0x78, 0xF0, 0xFFFF, rnd.randrange(0, 300)]))
    e.set16(M.AI0, rnd.randrange(0, 0x10000) if rnd.random() < 0.2 else 0)
    e.set8(M.B0, pick([0, 0, 0, 1, 2, 3, 4, 10, 255]))
    e.set8(M.B1, pick([0, 0, 1, 2, 3, 4, 5, 255]))
    e.set8(M.B2, rnd.randrange(256))
    e.set8(M.B3, pick([0, 0, 1]))
    e.set32(M.TDIR, rnd.randrange(32))
    e.set32(M.CDIR, rnd.randrange(32))
    e.set32(M.CANIM, rnd.randrange(12))
    e.set32(M.ADIR, rnd.randrange(4))
    e.set32(M.ANIMEND, pick([0, 1]))
    e.set32(M.FORCEADJ, pick([0, 1]))
    e.set32(M.ONGROUND, pick([0, 1]))
    e.set32(M.FLAGS, rnd.randrange(1 << 20) & ~0x40)
    e.set32(M.W28C, pick([0, 1, 6, 7, 8, 15, 16, rnd.randrange(0, 100), 0xFFFFFFFF]))
    for o in (M.TFX, M.TFY, M.FX, M.FY, M.FSX, M.FSY):
        e.set32(o, rnd.randrange(-5000, 5000))
    e.set32(M.POSX, rnd.randrange(0, 60 << 16))
    e.set32(M.POSY, rnd.randrange(0, 40 << 16))
    # tile offsets: near the hero most of the time so that the windows are hit
    if rnd.random() < 0.5:
        e.set32(M.TILEX, 20 + rnd.randrange(-3, 4))
        e.set32(M.TILEY, 20 + rnd.randrange(-3, 4))
    elif rnd.random() < 0.6:
        e.set32(M.TILEX, 20 + rnd.randrange(-8, 9))
        e.set32(M.TILEY, 20 + rnd.randrange(-8, 9))
    else:
        e.set32(M.TILEX, rnd.randrange(0, 52))
        e.set32(M.TILEY, rnd.randrange(0, 60))
    f = hero.s32(M.FLOOR) + pick([0, 0, 0, 0x100000, -0x100000, 0x200000, 0x300000, -0x300000, rnd.randrange(-0x400000, 0x400000)])
    e.set32(M.FLOOR, f)
    e.set32(M.POSZ, f + pick([0, 0, 0x100000, 0x200000, 0x280000, 0x500000, -0x100000, rnd.randrange(-0x600000, 0x900000)]))
    if ctype == 455 and rnd.random() < 0.7:     # the wasp's hover level around its own height
        e.set16(M.AI0, (e.s32(M.POSZ) >> 16) + rnd.randrange(-100, 101))
    # boundary values of the height windows (0x100000 of C19/C17, 0x180000 and 0x200000 of C18, the +-0x500000 band of C18)
    EDGES = [0, 0xFFFFF, 0x100000, 0x100001, 0x180000, 0x180001, 0x1FFFFF, 0x200000, 0x200001, -0x100000, -0x100001, -0x200000, -0x200001]
    if rnd.random() < 0.3:
        if rnd.random() < 0.5:
            e.set32(M.FLOOR, hero.s32(M.FLOOR) + pick(EDGES))
            e.set32(M.POSZ, hero.s32(M.FLOOR) + pick(EDGES))
        else:
            t_edge = pick([0x4FFFFF, 0x500000, 0x4FFFFE, -0x4FFFFF, -0x500000, -0x500001, 0, 1, -1])
            ai0 = rnd.randrange(0, 0x10000)
            e.set16(M.AI0, ai0)
            e.set32(M.POSZ, (t_edge + (ai0 << 16)) & 0xFFFFFFFF)
    if rnd.random() < 0.3:      # the hero inside the front window of the entity (TryAttackPlayerFront success paths)
        d = rnd.randrange(4)
        e.set32(M.ADIR, d)
        lat = rnd.randrange(-2, 3)
        fwd = rnd.randrange(0, 4)
        # case 0: dy<=0 (hero below), 1: dy>=0, 2: dx>=0, 3: dx<=0   (dx = self - hero)
        dxy = {0: (lat, -fwd), 1: (lat, fwd), 2: (fwd, lat), 3: (-fwd, lat)}[d]
        e.set32(M.TILEX, hero.s32(M.TILEX) + dxy[0])
        e.set32(M.TILEY, hero.s32(M.TILEY) + dxy[1])
        e.set32(M.FLOOR, hero.s32(M.FLOOR))
        e.set32(M.POSZ, hero.s32(M.FLOOR) + (0 if ctype != 455 else pick([0, 0x100000, 0x180000, 0x200000])))
        f = hero.s32(M.FLOOR)
        e.set16(M.AI1, pick([0, 0, 1, 2]))
        e.set8(M.B0, pick([0, 0, 0, 1]))
    for o in M.MH:
        e.set32(o, f - pick([0, 0, 0, 1, 2, 0x100000, 0x200000, 0x500000, -0x100000, rnd.randrange(-0x300000, 0x600000)]))
    return e


def run_real(addr, e, hero, rng_state, stack=(), a1=None, toucher=None, hp_result=0, mapc=0):
    w.j.clear()
    w.put(0, hero)
    w.put(SELF, e)
    if toucher is not None:
        w.put(TOUCHER, toucher)
    w.set_rng(rng_state)
    w.set_mapc(mapc)
    w.hp_result = hp_result
    args = [ENT0 + SELF * STRIDE]
    if a1 is not None:
        args.extend(a1)
    v0 = w.call(addr, *args, stack=stack)
    return w.get(SELF), w.get(0), w.get_rng(), list(w.j), v0


def check(name, real, model_e, model_rng, model_j, hero, hero_before, vecid, extra=''):
    e_real, h_real, rng_real, j_real, _ = real
    bad = []
    if bytes(e_real.raw) != bytes(model_e.raw):
        diffs = [(o, e_real.u32(o), model_e.u32(o)) for o in range(0, STRIDE, 4) if e_real.u32(o) != model_e.u32(o)]
        bad.append('entity %s' % diffs[:6])
    if bytes(h_real.raw) != bytes(hero_before.raw):
        bad.append('hero changed')
    if rng_real != model_rng.s:
        bad.append('rng %08x vs %08x (draws %s)' % (rng_real, model_rng.s, model_rng.draws))
    if j_real != model_j:
        bad.append('journal %s vs %s' % (j_real, model_j))
    return bad


def tally(name, key):
    cov.setdefault(name, Counter())[key] += 1


# ---- helpers: relpos x2, TryAttackPlayerFront, CanMoveForward, StartFlying --------------------------------------------------------
results = {}
mism = []


def record(name, bad, vecid):
    results.setdefault(name, [0, 0])
    results[name][0] += 1
    if bad:
        results[name][1] += 1
        if len(mism) < 40:
            mism.append((name, vecid, bad))


for i in range(N):
    hero = mk_hero()
    hero_before = hero.copy()
    e = mk_self(hero, 392, list(range(0, 12)))
    seed = rnd.randrange(1 << 32)
    # relpos 0x8007FE20 and 0x8007FE8C
    for nm, addr, fz in (('relpos', 0x8007FE20, False), ('relposZ', 0x8007FE8C, True)):
        real = run_real(addr, e, hero, seed, a1=[REL_BUF])
        buf = [w.m.rw(REL_BUF + 4 * k) for k in range(6)]
        want = [x & M32 for x in M.relpos(e, hero, fz)]
        record(nm, [] if buf == want else ['rel %s vs %s' % (buf, want)], i)
    # TryAttackPlayerFront 0x800805C8
    rel = M.relpos(e, hero)
    for k in range(6):
        w.m.ww(REL_BUF + 4 * k, rel[k])
    near, far, zmax = pick([(2, 6, 0x100000), (2, 2, 0), (3, 3, 0), (1, 4, 0x200000), (rnd.randrange(0, 5), rnd.randrange(0, 8), rnd.randrange(0, 0x400000))])
    w.j.clear()
    w.put(0, hero)
    w.put(SELF, e)
    w.set_rng(seed)
    v0 = w.call(0x800805C8, ENT0 + SELF * STRIDE, REL_BUF, near, far, stack=(zmax,))
    want = int(M.try_attack_front(e, rel, near, far, zmax))
    record('TryAttackPlayerFront', [] if (v0 & M32) == want else ['v0 %d vs %d' % (v0, want)], i)
    tally('TryAttackPlayerFront', want)
    # CanMoveForward 0x80080260
    dist = pick([0, 0, 0, 1, 0x100000, 0x400000, rnd.randrange(-5, 0x300000)])
    w.put(SELF, e)
    v0 = w.call(0x80080260, ENT0 + SELF * STRIDE, dist & M32)
    want = M.can_move_forward(e, dist)
    record('CanMoveForward', [] if (v0 & M32) == want else ['v0 %d vs %d (dist %d dir %d)' % (v0, want, dist, e.s32(M.TDIR))], i)
    tally('CanMoveForward', want)
    # StartFlying 0x8007FEF8
    anim, rg, ch = pick([(1, 0x78, 0x46), (1, 0x3C, 0x14), (rnd.randrange(0, 20), rnd.randrange(0, 400), rnd.randrange(0, 150)), (2, 0, 50)])
    real = run_real(0x8007FEF8, e, hero, seed, a1=[anim, rg, ch])
    me = e.copy()
    mr = Rng(seed)
    M.start_flying(me, hero, anim, rg, ch, mr, gdt)
    record('StartFlying', check('StartFlying', real, me, mr, [], hero, hero_before, i), i)
    tally('StartFlying', len(mr.draws))

# exhaustive CanMoveForward: the 16 corner patterns x 32 directions x 3 distances
bad = 0
cnt = 0
for pat in range(16):
    for d in range(32):
        for dist in (0, 1, 0x100000):
            e = Ent()
            e.set32(M.FLOOR, 0x300000)
            e.set32(M.TDIR, d)
            thr = dist + 1
            for k in range(4):
                # corner k "lower than the floor by more than thr" when bit set: height = floor - (thr + 1 + 5); else floor - (thr - 1 ... )
                e.set32(M.MH[k], 0x300000 - (thr + 6) if (pat >> k) & 1 else 0x300000 - max(thr - 1, 0))
            w.put(SELF, e)
            v0 = w.call(0x80080260, ENT0 + SELF * STRIDE, dist)
            want = M.can_move_forward(e, dist)
            cnt += 1
            if (v0 & M32) != want:
                bad += 1
                if len(mism) < 40:
                    mism.append(('CanMoveForward-exhaustive', (pat, d, dist), ['%d vs %d' % (v0, want)]))
results['CanMoveForward-exhaustive'] = [cnt, bad]

# ---- handlers ------------------------------------------------------------------------------------------------------------------------------
HANDLERS = {
    'C15': (M.c15, 338, list(range(0, 8)) + [0, 1, 3, 5, 6]),
    'C16': (M.c16, 341, list(range(0, 8)) + [0, 1, 3, 5, 6]),
    'C17': (M.c17, 344, list(range(0, 9)) + [0, 1, 1, 6]),
    'C18': (M.c18, 455, list(range(0, 8)) + [0, 1, 2, 3, 5]),
    'C19': (M.c19, 392, list(range(0, 10)) + [0, 1, 5, 7]),
}
for name, (fn, ctype, anims) in HANDLERS.items():
    for i in range(N * 3):
        hero = mk_hero()
        hero_before = hero.copy()
        e = mk_self(hero, ctype, anims)
        seed = rnd.randrange(1 << 32)
        mapc = rnd.randrange(0, 20)
        real = run_real(ADDR[name], e, hero, seed, mapc=mapc)
        me = e.copy()
        mr = Rng(seed)
        mj = []
        fn(me, hero, mr, mj, gdt, mapc)
        record(name, check(name, real, me, mr, mj, hero, hero_before, i), i)
        tally(name, 'a%d' % e.u32(M.TANIM) + ('>%d' % me.u32(M.TANIM) if me.u32(M.TANIM) != e.u32(M.TANIM) else '') + ' draws%d' % len(mr.draws))

# A0
for i in range(N):
    hero = mk_hero()
    e = mk_self(hero, 455, [0])
    e.set32(M.POSZ, rnd.randrange(-(1 << 31), 1 << 31))
    real = run_real(ADDR['A0'], e, hero, 1)
    me = e.copy()
    M.a0(me)
    record('A0', check('A0', real, me, Rng(1), [], hero, hero.copy(), i), i)

# D handlers
for name, ctype_choices in (('D5', [344, 425, 404, 454, 461, 467]), ('D6', [392]), ('D11', [455]), ('D12', [338, 341])):
    for i in range(N * 3):
        hero = mk_hero()
        hero_before = hero.copy()
        ctype = pick(ctype_choices)
        e = mk_self(hero, ctype, list(range(0, 9)))
        # the toucher: a fake entity whose CurAttack points to the 2-byte record
        t = Ent()
        t.set32(M.CURATTACK, ATTACK_REC)
        nib = pick([0, 1, 2, 3, 4, 5, 15, rnd.randrange(16)])
        w.m.wb(ATTACK_REC, nib | (0x80 if rnd.random() < 0.5 else 0))
        w.m.wb(ATTACK_REC + 1, rnd.randrange(1, 9))
        e.set32(M.TOUCH, ENT0 + TOUCHER * STRIDE)
        seed = rnd.randrange(1 << 32)
        hp = pick([0, 1])
        real = run_real(ADDR[name], e, hero, seed, toucher=t, hp_result=hp)
        me = e.copy()
        mr = Rng(seed)
        mj = []

        def cnh(ent, _hp=hp, _mj=mj):
            _mj.append(('cnh', SELF))
            return _hp

        if name == 'D5':
            M.d5(me, hero, mr, mj, gdt, cnh, nib)
        elif name == 'D12':
            M.d12(me, hero, mr, mj, gdt, cnh, nib)
        elif name == 'D6':
            M.hit_common(me, hero, 6, cnh, gdt)
        else:
            M.hit_common(me, hero, 4, cnh, gdt)
        bad = check(name, real, me, mr, mj, hero, hero_before, i)
        record(name, bad, i)
        tally(name, 't%d nib%d a%d>%d draws%d' % (ctype, nib, e.u32(M.TANIM), me.u32(M.TANIM), len(mr.draws)) if name in ('D5', 'D12') else 'a%d draws%d' % (e.u32(M.TANIM), len(mr.draws)))

import dis2
for nm, a in ADDR.items():
    addrs, _, _ = dis2.discover(a)
    hit = sum(1 for x in addrs if x in COV)
    lines_cov = '%s: %d/%d instructions executed (%.1f%%); not executed: %s' % (nm, hit, len(addrs), 100.0 * hit / len(addrs), ' '.join('%08X' % x for x in addrs if x not in COV)[:400])
    cov.setdefault('INSTR', Counter())[lines_cov] += 1
for nm, a in (('relpos', 0x8007FE20), ('relposZ', 0x8007FE8C), ('TryAttackPlayerFront', 0x800805C8), ('CanMoveForward', 0x80080260), ('StartFlying', 0x8007FEF8), ('GetDirectionToTarget', 0x8002E814)):
    addrs, _, _ = dis2.discover(a)
    hit = sum(1 for x in addrs if x in COV)
    cov.setdefault('INSTR', Counter())['%s: %d/%d instructions executed (%.1f%%); not executed: %s' % (nm, hit, len(addrs), 100.0 * hit / len(addrs), ' '.join('%08X' % x for x in addrs if x not in COV)[:300])] += 1
# ---- report ----------------------------------------------------------------------------------------------------------------------------------------
lines = ['vectors per handler: %d (helpers), %d (handlers)' % (N, N * 3)]
ok = True
for name, (n, b) in results.items():
    lines.append('%-28s %6d vectors, %d mismatches' % (name, n, b))
    ok = ok and b == 0
for k_ in sorted(cov.get('INSTR', {})):
    lines.append('INSTR ' + k_)
cov.pop('INSTR', None)
for name, c in cov.items():
    keys = sorted(c.items(), key=lambda kv: str(kv[0]))
    lines.append('coverage %s: %d distinct keys; %s' % (name, len(keys), ', '.join('%s:%d' % kv for kv in keys[:60])))
for m_ in mism:
    lines.append('MISMATCH %s' % (m_,))
open('vec_report.txt', 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
print('\n'.join(lines[:30]))
print('ALL MATCH' if ok else 'MISMATCHES: see vec_report.txt')
sys.exit(0 if ok else 1)
