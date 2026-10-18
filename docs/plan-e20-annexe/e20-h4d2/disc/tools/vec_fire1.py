"""vec_fire1.py - h4d2 discovery (read-only): replayable vectors (every data word READ and WRITTEN, format of e20-h4b/bin/vec/README.txt) of the fire-1 functions, recorded on the REAL MIPS code of ALUN_CD.EXE
(France) by the TM machine of e20-h4b/bin/h (copied here as ../vech), and compared with MY OWN models (f1model.py).

  driver  0x80035A84   (guard 0x800351B8 and cancel 0x800350C0 -> 0x80034D2C -> DestroyEntityNoFx 0x8003A774 are the REAL code ; SpawnWarpEntity 0x80039F58 and CreateEffectEntity 0x8003BDD8 are
                        journalling stubs that return a chosen pointer: the vector carries stub_spawn_ret = {type: pointer} and the journal of their arguments)
  cancel  0x80034D2C   (called directly)
  C63     0x8007AC60   (the REAL query 0x8003AF70 + sort 0x8003AE04 and GetDirectionToTarget 0x8002E814 inside ; the turn is checked on a 32 x 32 grid)
  E3      0x8007EDA0   (DestroyEntity 0x8003A59C stubbed: journal)

usage: python -B vec_fire1.py OUTDIR [seed]
It also replays EVERY vector file from an EMPTY memory (vtrace.replay, hooks re-installed from the vector) and prints the model-versus-binary result and the coverage."""
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'vech'))
sys.path.insert(0, HERE)
import vtrace as V
from machine import s32, M32
import f1model as F

OUT = sys.argv[1]
SEED = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
os.makedirs(OUT, exist_ok=True)
ENT0, STRIDE = F.ENT0, F.STRIDE
HERO = F.HERO
ent = F.ent
SHEET = 0x801E7000
EFFECT_RET = 0x801E8000
FRAME_LO, FRAME_HI = 0x801FF000, 0x801FFF00      # the callee's own stack frame AND the frames of its callees (the recursion of the sort): scratch, not part of the contract
V.FRAME_LO = FRAME_LO


class View:
    """a state view over the machine BEFORE the real call: reads memory, writes into an overlay; this is what the models use"""

    def __init__(self, m):
        self.m = m
        self.o = {}
        self.j = []
        self.query_calls = []

    def b(self, a):
        a &= M32
        return self.o[a] if a in self.o else self.m.ram[a & 0x1FFFFFFF]

    def g(self, a):
        return self.b(a) | self.b(a + 1) << 8 | self.b(a + 2) << 16 | self.b(a + 3) << 24

    def p(self, a, v):
        for i in range(4):
            self.o[(a + i) & M32] = (v >> (8 * i)) & 0xFF


def hook_stubs(m):
    cfg = {'spawn': {}, 'destroy_ret': 0}
    m.cfg = cfg

    def spawn(mm):
        sp = mm.r[29]
        args = [mm.r[4], mm.r[5], mm.r[6], mm.r[7]] + [mm.peek(sp + 0x10), mm.peek(sp + 0x14), mm.peek(sp + 0x18)]
        mm.journal.append((0, 'spawn_39F58', [x & M32 for x in args]))
        mm.r[2] = cfg['spawn'].get(args[2] & M32, 0)

    def effect(mm):
        sp = mm.r[29]
        args = [mm.r[4], mm.r[5], mm.r[6], mm.r[7]] + [mm.peek(sp + 0x10), mm.peek(sp + 0x14)]
        mm.journal.append((0, 'effect_3BDD8', [x & M32 for x in args]))
        mm.r[2] = EFFECT_RET

    def destroy(mm):
        mm.journal.append((0, 'destroy_3A59C', [mm.r[4] & M32, mm.r[5] & M32]))
        mm.r[2] = 0
    m.hooks[0x80039F58] = spawn
    m.hooks[0x8003BDD8] = effect
    m.hooks[0x8003A59C] = destroy


def replay_hooks(vec):
    h = {}
    sr = {int(k): v for k, v in vec.get('stub_spawn_ret', {}).items()}

    def spawn(mm):
        mm.r[2] = sr.get(mm.r[6] & M32, 0)

    def effect(mm):
        mm.r[2] = EFFECT_RET

    def destroy(mm):
        mm.r[2] = 0
    h[0x80039F58] = spawn
    h[0x8003BDD8] = effect
    h[0x8003A59C] = destroy
    return h


def norm_journal(js):
    out = []
    for j in js:
        k = j[1]
        a = list(j[2])
        if k == 'spawn_39F58':
            out.append(('spawn', a[0], a[1], a[2], a[3], a[4], a[5], a[6]))
        elif k == 'effect_3BDD8':
            out.append(('effect', a[0], a[1], a[2], a[3], a[4], a[5]))
        elif k == 'destroy_3A59C':
            out.append(('destroy', a[0], a[1]))
    return out


def norm_model_journal(js):
    out = []
    for j in js:
        j = list(j)
        if j[0] == 'spawn':
            out.append(tuple(j))
        elif j[0] == 'effect':
            out.append(tuple(j))
        elif j[0] == 'destroy':
            out.append(('destroy', j[1], j[2]))
    return out


def bytemap(writes):
    d = {}
    for a, n, v in writes:
        if FRAME_LO <= a < FRAME_HI:
            continue
        for i in range(n):
            d[(a + i) & M32] = (v >> (8 * i)) & 0xFF
    return d


def compare(view, vec, model_ret=None):
    """model overlay + journal + return versus the recorded real writes / journal / v0.  returns an error text or None"""
    real = bytemap(vec['writes'])
    if view.o != real:
        only_m = sorted(set(view.o) - set(real))[:4]
        only_r = sorted(set(real) - set(view.o))[:4]
        diff = [hex(a) for a in sorted(set(view.o) & set(real)) if view.o[a] != real[a]][:4]
        return 'writes differ: model-only %s real-only %s value-diff %s' % ([hex(x) for x in only_m], [hex(x) for x in only_r], diff)
    if model_ret is not None and ('ret' in vec) and (vec['ret'] & M32) != (model_ret & M32):
        return 'ret real %#x model %#x' % (vec['ret'], model_ret & M32)
    if norm_model_journal(view.j) != norm_journal(vec.get('journal', [])):
        return 'journal differs: model %s real %s' % (norm_model_journal(view.j), norm_journal(vec.get('journal', [])))
    return None


def prep_entities(m, R, count, kinds=(0x14, 0x17, 0x15, 0x18, 0x10, 0x113), status=(2, 2, 2, 3, 3, 4, 1, 0)):
    m.poke(F.COUNT, count)
    for i in range(1, 64):
        e = ent(i)
        if i <= max(count, 0) + 2:
            m.poke(e + 0x10, R.choice(status))
            m.poke(e + 0x68, R.choice(kinds))
            m.poke(e + 0x20, R.choice([0, 0, 0, 3]))
            m.poke(e + 0x114, R.randrange(0, 1200 << 16) & M32)
            m.poke(e + 0x118, R.randrange(0, 900 << 16) & M32)
            m.poke(e + 0x11C, R.randrange(0, 200 << 16) & M32)
            m.poke(e + 4, 100 + i)
            m.poke(e + 0x1B8, ent(R.randrange(1, 64)) if R.random() < 0.25 else 0)
            m.poke(e + 0x28, ent(R.randrange(1, 64)) if R.random() < 0.2 else 0)
        else:
            m.poke(e + 0x10, 0)
            m.poke(e + 0x68, R.choice(kinds))
            m.poke(e + 0x1B8, 0)
            m.poke(e + 0x28, 0)


def run_one(m, fn, args, model_fn, spawn_cfg, extra=None):
    """model on a view, then the real code; returns (vec, err)"""
    view = View(m)
    m.cfg['spawn'] = dict(spawn_cfg)
    mret = model_fn(view)
    vec = V.run(m, fn, args)
    if spawn_cfg:
        vec['stub_spawn_ret'] = {str(k): v for k, v in spawn_cfg.items()}
    if extra:
        vec.update(extra)
    err = compare(view, vec, mret)
    return vec, err, view


# ============================================================ the driver 0x80035A84 ============================================================
def gen_driver(R, n_random):
    m = V.TM()
    hook_stubs(m)
    vecs, errs = [], []
    cases = []
    base = dict(phase=0, counter=0, anim=0x33, slope=0, inuse=0x2F, spawn_col=True, spawn_ball=True, ident_same=True, ptr_slot=5, count=4)
    # a deterministic grid over the decisions
    for phase in (-1, 0, 1, 2, 3, 0x7FFFFFFF):
        for anim in (0x31, 0x32, 0x33, 0x34, 0):
            for slope in (0, 4):
                for inuse in (0x2F, 0, 0x2B):
                    c = dict(base, phase=phase, anim=anim, slope=slope, inuse=inuse)
                    for sc in (True, False):
                        for sb in (True, False):
                            for same in (True, False):
                                for ctr in (0, 3, 8, 16, 33):
                                    if phase not in (0, 1) and (not sc or not sb or not same or ctr != 8):
                                        continue
                                    if phase == 0 and (not same or sb is False and sc is False):
                                        continue
                                    cases.append(dict(c, spawn_col=sc, spawn_ball=sb, ident_same=same, counter=ctr))
    for k in range(n_random):
        cases.append(dict(phase=R.choice([0, 1, 1, 1, 2, 3, -1]), counter=R.choice([0, 1, 7, 8, 9, 15, 16, 24, 31, 32, 33, 40, 0x7FFFFFF8, 0x7FFFFFFF, 0x80000000, 0xFFFFFFF8, R.getrandbits(32)]),
                          anim=R.choice([0x32, 0x33, 0x33, 0x34, 0x31, 0, R.getrandbits(8)]), slope=R.choice([0, 0, 0, 4, 1, 4]), inuse=R.choice([0x2F, 0x2F, 0, 0x2B]),
                          spawn_col=R.random() < 0.85, spawn_ball=R.random() < 0.85, ident_same=R.random() < 0.7, ptr_slot=R.randrange(1, 12), count=R.choice([0, 1, 2, 3, 6, 11, 40])))
    for n, c in enumerate(cases):
        prep_entities(m, R, c['count'])
        ph = c['phase'] & M32
        m.poke(F.PHASE, ph)
        m.poke(F.COUNTER, c['counter'])
        m.poke(F.INUSE, c['inuse'])
        m.poke(HERO + 0x88, c['anim'])
        m.poke(HERO + 0x18C, c['slope'])
        m.poke(HERO + 0x114, R.randrange(0, 1200 << 16) & M32)
        m.poke(HERO + 0x118, R.randrange(0, 900 << 16) & M32)
        m.poke(HERO + 0x11C, R.choice([1, 2, 0x10001, R.getrandbits(24)]))
        m.poke(F.RNG, R.choice([0x12345678, 0, 1, M32, R.getrandbits(32), R.getrandbits(32)]))
        pe = ent(c['ptr_slot'])
        m.poke(pe + 4, 5000 + c['ptr_slot'])
        m.poke(F.PTR, pe)
        m.poke(F.IDENT, 5000 + c['ptr_slot'] if c['ident_same'] else R.choice([0, 1, 4999 + c['ptr_slot'], 6000]))
        col, ball = ent(13), ent(14)               # the entities the stub hands back (fresh slots)
        m.poke(col + 4, R.choice([7, 0, 0x7FFFFFFF, 9999]))
        m.poke(ball + 4, 8000)
        m.poke(ball + 0xB8, 0x7777)
        m.poke(ball + 0x274, 0x55)
        m.poke(ball + 0x278, 0x66)
        cfg = {0x14: col if c['spawn_col'] else 0, 0x17: ball if c['spawn_ball'] else 0}

        def model(view, c=c):
            return F.driver(view, lambda t: cfg[t])
        vec, err, view = run_one(m, 0x80035A84, (), model, cfg, extra=dict(id=n, case=c))
        if err:
            errs.append((n, c, err))
        vecs.append(vec)
    return vecs, errs, m


# ============================================================ the cancel 0x80034D2C ============================================================
def gen_cancel(R, n):
    m = V.TM()
    hook_stubs(m)
    vecs, errs = [], []
    for k in range(n):
        count = R.choice([0, 1, 2, 3, 5, 8, 11, 20, 63, -1])
        prep_entities(m, R, count if count >= 0 else 0)
        if count < 0:
            m.poke(F.COUNT, M32)
        view = View(m)
        m.cfg['spawn'] = {}
        F.cancel_fire1(view)
        vec = V.run(m, 0x80034D2C, ())
        err = compare(view, vec, None)
        vec['id'] = k
        if err:
            errs.append((k, err))
        vecs.append(vec)
    return vecs, errs, m


# ============================================================ E3 ============================================================
def gen_e3(R):
    m = V.TM()
    hook_stubs(m)
    vecs, errs = [], []
    n = 0
    for ta in (0, 1, 2, 5, 0x31, 0xFFFFFFFF):
        for ae in (0, 1, 2, 0xFFFFFFFF):
            for fl in (0x102074, 0x132114, 0x102054, 0x0, M32, 0x30, 0x40, 0x10, 0x20, 0x100000, 0x8000FFFF, R.getrandbits(32)):
                e = ent(3)
                m.poke(e + 0x88, ta)
                m.poke(e + 0xAC, ae)
                m.poke(e + 0x6C, fl)
                view = View(m)
                F.e3(view, e)
                vec = V.run(m, 0x8007EDA0, (e,))
                err = compare(view, vec, None)
                vec['id'] = n
                n += 1
                if err:
                    errs.append((n, err))
                vecs.append(vec)
    return vecs, errs, m


# ============================================================ C63 0x8007AC60 ============================================================
def build_sheets(m, R):
    for b in range(0, 16 * 0x20):
        m.poke8(SHEET + b, 0)
    for k in range(16):
        for t in range(0, 14):
            m.poke8(SHEET + 0x20 * k + 2 + t, R.choice([0x10, 0x10, 0x20, 0x80, 0xC0, 0x40, 0x00, 0x8F, 0x90]))


def dir_grid(m):
    """a (dx, dy) in 16.16 units for each of the 32 directions, found by calling the REAL GetDirectionToTarget 0x8002E814"""
    found = {}
    for r in (40, 90, 160):
        for dxp in range(-r, r + 1, 3):
            for dyp in range(-r, r + 1, 3):
                if dxp == 0 and dyp == 0:
                    continue
                d = m.call(0x8002E814, (dxp << 16) & M32, (dyp << 16) & M32) & M32
                found.setdefault(d, (dxp << 16, dyp << 16))
    return found


def gen_c63(R, n_random, mode='binary'):
    m = V.TM()
    hook_stubs(m)
    vecs, errs = [], []
    dg = dir_grid(m)
    dirto = lambda dx, dy: m.call(0x8002E814, dx, dy) & M32
    build_sheets(m, R)

    def world(attacker_slot, target_slot, tgt_alive=True):
        a0 = ent(attacker_slot)
        return a0
    n = 0
    # (1) the turn grid: the ball has a LIVE target (so the query is not called): 32 x 32 current x target directions
    tg = ent(9)
    for cur in range(32):
        for tdir, (dx, dy) in sorted(dg.items()):
            a0 = ent(2)
            prep_entities(m, R, 6)
            m.poke(F.COUNT, 6)
            m.poke(a0 + 0x274, 0)
            m.poke(a0 + 0x278, tg)
            m.poke(a0 + 0x27C, 4242)
            m.poke(tg + 0x10, 2)
            m.poke(tg + 4, 4242)
            m.poke(a0 + 0x114, 300 << 16)
            m.poke(a0 + 0x118, 300 << 16)
            m.poke(tg + 0x114, (300 << 16) + dx)
            m.poke(tg + 0x118, (300 << 16) + dy)
            m.poke(a0 + 0x8C, cur)
            m.poke(a0 + 0xB8, 0x1234)
            view = View(m)
            F.c63(view, a0, dirto)
            vec = V.run(m, 0x8007AC60, (a0,))
            err = compare(view, vec, None)
            vec['id'] = n
            vec['kind'] = 'turn'
            vec['cur'] = cur
            vec['target_dir'] = tdir
            n += 1
            if err:
                errs.append((n, 'turn', cur, tdir, err))
            vecs.append(vec)
    # (2) random worlds: the countdown, no target (query + RNG), a live / dead / swapped target
    for k in range(n_random):
        a_slot = R.randrange(1, 40)
        a0 = ent(a_slot)
        count = R.choice([0, 1, 2, 3, 5, 7, 9, 12, 20, 40, 63])
        prep_entities(m, R, count)
        build_sheets(m, R)
        hx, hy = R.randrange(100 << 16, 300 << 16), R.randrange(100 << 16, 300 << 16)
        m.poke(a0 + 0x114, hx)
        m.poke(a0 + 0x118, hy)
        m.poke(a0 + 0x8C, R.randrange(0, 32))
        m.poke(a0 + 0xB8, 0x1234)
        for i in range(1, 64):                              # the query world, as e20-h4b/bin/h/b3_query.py builds it: statuses, filters, sheets, positions around the ball
            e = ent(i)
            if e == a0:
                continue
            m.poke(e + 0x10, R.choice([2, 2, 2, 3, 3, 0, 1, 4]))
            m.poke(e + 0x20, 0 if R.random() < 0.9 else 7)
            m.poke(e + 0x6C, R.choice([1, 1, 1, 0, 5, 9, 0x101, 4, 0x100, 0x84, 2]))
            m.poke(e + 0x1D0, 0 if R.random() < 0.9 else 1)
            m.poke(e + 0x1CC, 0 if R.random() < 0.9 else 1)
            m.poke(e + 0xB4, 0 if R.random() < 0.9 else 0x40)
            m.poke(e + 0x1C4, 0 if R.random() < 0.05 else SHEET + 0x20 * R.randrange(16))
            ox = R.choice([0, 8, 16, 24, 32, -8, -16, 40, 64, 160, 161, -200])
            oy = R.choice([0, 8, 16, 24, 32, -8, -16, 40, 64, 0, 1])
            m.poke(e + 0x114, (hx + (ox << 16) + R.choice([0, 0, 0, 0x4000])) & M32)
            m.poke(e + 0x118, (hy + (oy << 16)) & M32)
        mode_t = R.choice(['delay', 'notarget', 'notarget', 'notarget', 'live', 'dead', 'swapped', 'noattack'])
        m.poke(a0 + 0x274, R.choice([1, 2, 3, 16, 0x7FFFFFFF, 0xFFFFFFFF]) if mode_t == 'delay' else 0)
        tslot = R.randrange(1, 60)
        tgp = ent(tslot) if tslot != a_slot else ent(((tslot + 1) % 62) + 1)
        m.poke(a0 + 0x278, tgp if mode_t in ('live', 'dead', 'swapped') else 0)
        m.poke(a0 + 0x27C, 777)
        m.poke(tgp + 4, 777 if mode_t in ('live', 'dead') else 778)
        m.poke(tgp + 0x10, {'dead': R.choice([0, 1, 3, 4, 5]), 'live': 2}.get(mode_t, 2))
        m.poke(tgp + 0x114, (hx + R.choice([-90, -40, -3, 0, 5, 40, 90, 200]) * 65536 + R.choice([0, 0x4000])) & M32)
        m.poke(tgp + 0x118, (hy + R.choice([-90, -40, -3, 0, 5, 40, 90, 200]) * 65536) & M32)
        atk = 0x801E7800
        m.poke8(atk, R.choice([0x0A, 0x0A, 0x0A, 0x8A, 0x1A, 0x09, 0x0B]))
        m.poke(a0 + 0x1C8, atk)
        for q in range(64):
            m.poke(F.PTRS + 4 * q, 0xDEAD0000 + q)
            m.poke(F.KEYS + 4 * q, 0xBEEF0000 + q)
        m.poke(0x801FFF10, F.KEYS)
        m.poke(F.RNG, R.choice([0x12345678, R.getrandbits(32), R.getrandbits(32), 0, M32]))
        view = View(m)
        F.c63(view, a0, dirto, mode='binary')
        vec = V.run(m, 0x8007AC60, (a0,))
        err = compare(view, vec, None)
        vec['id'] = n
        vec['kind'] = 'random:' + mode_t
        n += 1
        if err:
            errs.append((n, mode_t, err))
        vecs.append(vec)
    return vecs, errs, m


def coverage(m, lo, hi):
    pcs = set(range(lo, hi, 4))
    return len(pcs & set(m.cov)), len(pcs)


def flag_binary_only(vecs):
    """a vector whose real run read a NON-ZERO +0x1B8 (attached active effect) or +0x28 (carrier) of an entity that DestroyEntityNoFx (0x8003A774) handled exercises two writes (+0x68 of the effect, +0x2C of the carrier)
    that the DLL's single-argument DestroyEntity does not port (named stubs E20.e / E20.g): binary_only = true, the adapter of a C# test skips it (the world keys never reach it: the column and the balls have neither)"""
    n = 0
    watch = {}
    for i in range(0, 64):
        watch[F.ent(i) + 0x1B8] = True
        watch[F.ent(i) + 0x28] = True
    for v in vecs:
        v['binary_only'] = any(a in watch and val != 0 for a, sz, val in v['reads'])
        n += v['binary_only']
    return n


def save(name, fn, note, vecs):
    nb = flag_binary_only(vecs)
    print('  binary_only vectors (child / carrier pointers, not expressible in the DLL):', nb, 'of', len(vecs))
    path = os.path.join(OUT, name)
    json.dump({'function': fn, 'note': note, 'vectors': vecs}, open(path, 'w'), sort_keys=True, separators=(',', ':'))
    return path


def replay_all(path):
    d = json.load(open(path))
    bad = 0
    for v in d['vectors']:
        ok, msg = V.replay(v, hooks=replay_hooks(v))
        if not ok:
            bad += 1
            if bad <= 3:
                print('  REPLAY FAIL', os.path.basename(path), v.get('id'), msg)
    print('  replay from an EMPTY memory: %d/%d vectors reproduce the writes and the return value' % (len(d['vectors']) - bad, len(d['vectors'])))
    return bad


def main():
    R = random.Random(SEED)
    total_err = 0
    print('== driver 0x80035A84')
    vecs, errs, m = gen_driver(R, 1500)
    p = save('vec_fire1_driver_80035A84.json', '0x80035A84', 'fire-1 driver; SpawnWarpEntity 0x80039F58 / CreateEffectEntity 0x8003BDD8 are journalling stubs (stub_spawn_ret = {type: pointer}); guard, general cancel, fire-1 cancel and DestroyEntityNoFx are the real code', vecs)
    print('  vectors', len(vecs), 'model mismatches', len(errs), errs[:2])
    print('  coverage driver %d/%d guard %d/%d cancel %d/%d destroyNoFx %d/%d' % (coverage(m, 0x80035A84, 0x80035C64) + coverage(m, 0x800351B8, 0x80035204) + coverage(m, 0x80034D2C, 0x80034E08) + coverage(m, 0x8003A774, 0x8003A7B0)))
    miss = sorted(set(range(0x80035A84, 0x80035C64, 4)) - set(m.cov))
    print('  driver words never executed:', [hex(a) for a in miss])
    total_err += len(errs) + replay_all(p)
    print('== cancel 0x80034D2C')
    vecs, errs, m = gen_cancel(R, 400)
    p = save('vec_cancel_fire1_80034D2C.json', '0x80034D2C', 'fire-1 cancel called directly; CreateEffectEntity stubbed (journal); DestroyEntityNoFx real', vecs)
    print('  vectors', len(vecs), 'model mismatches', len(errs), errs[:2])
    print('  coverage cancel %d/%d destroyNoFx %d/%d' % (coverage(m, 0x80034D2C, 0x80034E08) + coverage(m, 0x8003A774, 0x8003A7B0)))
    total_err += len(errs) + replay_all(p)
    print('== E3 0x8007EDA0')
    vecs, errs, m = gen_e3(R)
    p = save('vec_e3_8007EDA0.json', '0x8007EDA0', 'E3; DestroyEntity 0x8003A59C stubbed (journal)', vecs)
    print('  vectors', len(vecs), 'model mismatches', len(errs), errs[:2])
    print('  coverage E3 %d/%d' % coverage(m, 0x8007EDA0, 0x8007EE04))
    total_err += len(errs) + replay_all(p)
    print('== C63 0x8007AC60')
    vecs, errs, m = gen_c63(R, 2500)
    p = save('vec_c63_8007AC60.json', '0x8007AC60', 'C63 (ball); the target query 0x8003AF70 + sort 0x8003AE04 and GetDirectionToTarget 0x8002E814 are real code; kind turn = the 32 x 32 grid of current x target directions', vecs)
    print('  vectors', len(vecs), 'model mismatches', len(errs), errs[:2])
    print('  coverage C63 %d/%d query %d/%d sort %d/%d dirto %d/%d' % (coverage(m, 0x8007AC60, 0x8007ADDC) + coverage(m, 0x8003AF70, 0x8003B0E8) + coverage(m, 0x8003AE04, 0x8003AF70) + coverage(m, 0x8002E814, 0x8002E8A0)))
    miss = sorted(set(range(0x8007AC60, 0x8007ADDC, 4)) - set(m.cov))
    print('  C63 words never executed:', [hex(a) for a in miss])
    total_err += len(errs) + replay_all(p)
    print('TOTAL problems', total_err)
    sys.exit(1 if total_err else 0)


if __name__ == '__main__':
    main()
