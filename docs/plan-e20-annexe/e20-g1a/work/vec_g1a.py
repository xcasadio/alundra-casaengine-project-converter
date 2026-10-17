"""vec_g1a.py - compares the models of g1a_models.py with the REAL code of ALUN_CD.EXE (France) on random single-call vectors.
Usage: python -B vec_g1a.py [N] [mutant_word_addr new_word]     N vectors per function (default 3000). Exit 0 iff every comparison matches.
A binary one-word mutant (second form) patches the interpreter's RAM image before the run: the vectors must then report mismatches (sensitivity check)."""
import random
import sys
from collections import Counter

import g1a_models as M
from h import World, Ent, ENT0, STRIDE, M32, s32
import emu as _emu

N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
MUT = None
if len(sys.argv) >= 4:
    MUT = (int(sys.argv[2], 16), int(sys.argv[3], 16))

COV = set()
_orig = _emu.Emu.exec1


def _exec1(self, pc, wd):
    COV.add(pc)
    return _orig(self, pc, wd)


_emu.Emu.exec1 = _exec1

w = World()
m = w.m
if MUT:
    m.ww(MUT[0], MUT[1])

# optional golden export: GOLDEN=path python -B vec_g1a.py N  -> JSON of (inputs, outputs) read from the REAL code, every K-th vector of each function
import os, json
GOLD_PATH = os.environ.get('GOLDEN')
GOLD = {}
GOFFS = [0x10, 0x14, 0x20, 0x24, 0x28, 0x2C, 0x30, 0x34, 0x38, 0x68, 0x6C, 0x88, 0x8C, 0x98, 0xAC, 0xB0, 0x114, 0x118, 0x11C, 0x12C, 0x130, 0x134, 0x138, 0x13C, 0x140, 0x144, 0x168, 0x180, 0x184, 0x1B8, 0x1C4, 0x1D8, 0x1DC, 0x1E0, 0x1E4, 0x1E8, 0x1EC, 0x228]
GADDR = {0x8002F854: 'StepTowards', 0x8002EDBC: 'TryLift', 0x8002EEAC: 'PlayerTryAction', 0x8002F884: 'UpdatePlayerCarriedEntity', 0x8006B848: 'C23', 0x8007ED30: 'E2', 0x8003A774: 'DestroyEntity1', 0x80037E34: 'MoveEntityCarriedBranch'}
GCOUNT = {}
GKEEP = int(os.environ.get('GOLDEN_EVERY', '40'))


def gsnap():
    ents = {}
    for sl in range(0, 10):
        a = ENT0 + sl * STRIDE
        ents[str(sl)] = {('0x%X' % o): m.rw(a + o) for o in GOFFS}
    return dict(entities=ents, ctr=m.rw(M.CTR), nslot=m.rw(M.NSLOT), padedge=m.rh(M.PADEDGE), has3b=HAS3B['v'], sheet_attack=m.rb(0x80141000 + 0xE))


if GOLD_PATH:
    _orig_call = w.call

    def _rec_call(addr, *args, stack=()):
        name = GADDR.get(addr)
        keep = False
        if name:
            GCOUNT[name] = GCOUNT.get(name, 0) + 1
            keep = GCOUNT[name] % GKEEP == 0
        if keep:
            pre = gsnap(); w.j.clear(); JOURNAL.clear()
        r = _orig_call(addr, *args, stack=stack)
        if keep:
            post = gsnap()
            pre_nz = {sl: {o: v for o, v in d.items() if v} for sl, d in pre['entities'].items()}
            pre_nz = {sl: d for sl, d in pre_nz.items() if d}
            changed = {sl: {o: v for o, v in d.items() if v != pre['entities'][sl][o]} for sl, d in post['entities'].items()}
            changed = {sl: d for sl, d in changed.items() if d}
            GOLD.setdefault(name, []).append(dict(args=[a & M32 for a in args], pre=dict(entities=pre_nz, ctr=pre['ctr'], nslot=pre['nslot'], padedge=pre['padedge'], has3b=pre['has3b'], sheet_attack=pre['sheet_attack']),
                                                 post_changed=dict(entities=changed, ctr=post['ctr']), journal=[list(x) for x in w.j] + [list(x) for x in JOURNAL], ret=r & M32))
        return r
    w.call = _rec_call

# stubs --------------------------------------------------------------------------------------------------------------------------------
HAS3B = {'v': 0}
JOURNAL = []


def getnum(mm):
    JOURNAL.append(('item', mm.r[4] & M32))
    mm.r[2] = HAS3B['v']


m.hooks[0x8004E428] = getnum

# addresses
ADDR = dict(step=0x8002F854, trylift=0x8002EDBC, pta=0x8002EEAC, upce=0x8002F884, c23=0x8006B848, e2=0x8007ED30, destroy1=0x8003A774, moveent=0x80037E34)
RANGES = dict(step=(0x8002F854, 0x8002F884), trylift=(0x8002EDBC, 0x8002EEAC), pta=(0x8002EEAC, 0x8002EFE0), upce=(0x8002F884, 0x8002FB14),
              c23=(0x8006B848, 0x8006B8CC), e2=(0x8007ED30, 0x8007EDA0), destroy1=(0x8003A774, 0x8003A7B0), moveent=(0x80037E34, 0x80037F28))

rnd = random.Random(20261017)
mism = Counter()
total = Counter()
examples = {}
TABLE = [m.rw(0x80027400 + 4 * i) for i in range(4)]
SHEET = 0x80141000
LINKBLOB = 0x80141400


def pick(l):
    return l[rnd.randrange(len(l))]


def rs32():
    return rnd.randrange(-(1 << 31), 1 << 31)


def snapshot_slots(slots):
    return {s: bytes(w.get(s).raw) for s in slots}


def compare(name, before_desc, real, model, extra=''):
    total[name] += 1
    if real != model:
        mism[name] += 1
        if name not in examples:
            examples[name] = (before_desc, real, model)


def diff_ents(a, b):
    out = []
    for s in sorted(set(a) | set(b)):
        x, y = a.get(s), b.get(s)
        if x != y:
            offs = [i for i in range(len(x)) if x[i] != y[i]]
            out.append((s, offs[:12]))
    return out


def reset_globals():
    m.ww(M.CTR, 0)
    m.wh(M.PADEDGE, 0)
    m.wh(M.PADHOLD, 0)
    m.ww(M.NSLOT, 0)
    for i in range(16):
        m.wb(SHEET + i, 0)
    JOURNAL.clear()
    w.j.clear()


def mk_hero(kind='free'):
    h = Ent()
    h.set32(M.STATUS, 2)
    h.set32(M.POSX, rnd.randrange(0, 400 << 16))
    h.set32(M.POSY, rnd.randrange(0, 300 << 16))
    h.set32(M.POSZ, pick([0, 0x10000, rnd.randrange(0, 0x400000)]))
    h.set32(M.GROUND, pick([0, 1, 1, 7]))
    h.set32(M.DIR, rnd.randrange(32))
    h.set32(M.FACE, rnd.randrange(4))
    h.set32(0x180, rnd.randrange(1 << 16) | pick([0, 0x2000, 0x4000, 0x6000]))
    h.set32(M.ANIM, rnd.randrange(0x50))
    h.set32(M.OFFX, rs32()); h.set32(M.OFFY, rs32()); h.set32(M.OFFZ, rs32())
    for o in (0x134, 0x138, 0x13C, 0x140, 0x16C):
        h.set32(o, rnd.randrange(1 << 32))
    return h


def mk_obj(near=None):
    e = Ent()
    e.set32(M.STATUS, pick([0, 1, 2, 2, 3, 3, 4]))
    e.set32(M.FLAGS, rnd.randrange(1 << 24) if rnd.random() < 0.5 else (0x3323E4 | (rnd.randrange(4) << 9)) & M32)
    e.set32(M.POSX, rnd.randrange(0, 400 << 16))
    e.set32(M.POSY, rnd.randrange(0, 300 << 16))
    e.set32(M.POSZ, rnd.randrange(0, 0x600000))
    e.set32(M.SUPPORT, pick([0, ENT0, ENT0, ENT0 + STRIDE * 3, rnd.randrange(1 << 32)]))
    e.set32(M.T20, pick([0, 0, 0, 1, 5]))
    for o in (M.ANIM, M.DIR, M.FACE, 0x130, 0x134, 0x138, 0x13C, 0x140, 0x16C, M.T24):
        e.set32(o, rnd.randrange(1 << 32) if rnd.random() < 0.7 else 0)
    return e


# ---- V1: StepTowards ------------------------------------------------------------------------------------------------------------------
def v_step():
    for i in range(N * 4):
        cur = rs32() if rnd.random() < 0.5 else rnd.randrange(-0x400000, 0x400000)
        step = pick([0x10000, 0x20000, 0x10000, rnd.randrange(0, 1 << 32), 1, 0])
        r = rnd.random()
        if r < 0.3:
            tgt = cur + pick([0, 1, -1, step - 1, step, step + 1, -(step - 1), -step, -(step + 1)])
        elif r < 0.5:
            tgt = pick([0, 0x200000])
        else:
            tgt = rs32() if rnd.random() < 0.3 else cur + rnd.randrange(-0x400000, 0x400000)
        cur &= M32; tgt &= M32
        real = w.call(ADDR['step'], cur, tgt, step) & M32
        model = M.step_towards(cur, tgt, step)
        compare('step', (hex(cur), hex(tgt), hex(step)), real, model)


# ---- V2: TryLift --------------------------------------------------------------------------------------------------------------------
def v_trylift():
    for i in range(N * 2):
        reset_globals()
        hero = mk_hero(); obj = mk_obj()
        hero.set32(M.CARRIED, pick([0, 0]))
        HAS3B['v'] = pick([0, 1, 5])
        w.put(0, hero); w.put(3, obj)
        before = (hero.copy(), obj.copy())
        # real
        ret = w.call(ADDR['trylift'], ENT0 + 3 * STRIDE) & M32
        real = (ret, bytes(w.get(0).raw), bytes(w.get(3).raw), tuple(JOURNAL))
        # model
        mh, mo = hero.copy(), obj.copy()
        j = []
        mr = M.try_lift(mh, mo, ENT0 + 3 * STRIDE, bool(HAS3B['v']), j)
        model = (mr, bytes(mh.raw), bytes(mo.raw), tuple(j))
        compare('trylift', (hex(obj.u32(M.FLAGS)), HAS3B['v']), real, model)
        JOURNAL.clear()


# ---- V3: PlayerTryAction ------------------------------------------------------------------------------------------------------------
def v_pta():
    for i in range(N * 2):
        reset_globals()
        ns = rnd.randrange(0, 7)
        nslot = pick([ns, ns, -1])
        hero = mk_hero()
        hero.set32(M.CARRIED, pick([0, 0, 0, ENT0 + STRIDE * 2]))
        ents = {0: hero}
        for s in range(1, 9):
            o = mk_obj()
            o.set32(M.FLAGS, (o.u32(M.FLAGS) & ~0x600) | (rnd.randrange(4) << 9))
            if rnd.random() < 0.5:
                o.set32(M.SUPPORT, ENT0)
            ents[s] = o
        if rnd.random() < 0.55:
            c = rnd.randrange(1, 9)
            hero.set32(M.CONTACT, ENT0 + c * STRIDE)
        edge = pick([0, 0x80, 0x80, 0x40, 0xC0, rnd.randrange(1 << 16)])
        HAS3B['v'] = pick([0, 1])
        for s, e in ents.items():
            w.put(s, e)
        m.ww(M.NSLOT, nslot & M32)
        m.wh(M.PADEDGE, edge)
        ret = w.call(ADDR['pta']) & M32
        real = (ret, tuple(bytes(w.get(s).raw) for s in range(9)), tuple(JOURNAL))
        mdl = {s: e.copy() for s, e in ents.items()}
        j = []
        mr = M.player_try_action(mdl, nslot, edge, bool(HAS3B['v']), j)
        model = (mr, tuple(bytes(mdl[s].raw) for s in range(9)), tuple(j))
        compare('pta', (nslot, hex(edge), hex(hero.u32(M.CONTACT)), HAS3B['v']), real, model)
        JOURNAL.clear()


# ---- V4: UpdatePlayerCarriedEntity ------------------------------------------------------------------------------------------------
def v_upce():
    for i in range(N * 5):
        reset_globals()
        hero = mk_hero(); obj = mk_obj()
        mode = pick([0, 0, 1, 1, 2, 2, 3, 0xFFFFFFFF, 7])
        carried = pick([True, True, True, True, False])
        sheet_attack = pick([0, 1, 1, 5, 255])
        ctr = pick([0, 1, 4, 5, 6, 100, 0xFFFFFFFF, rnd.randrange(0, 12)])
        # offset geometry: positions near thresholds
        hx, hy, hz = hero.u32(M.POSX), hero.u32(M.POSY), hero.u32(M.POSZ)
        mag = pick([0, 1, 0xFFFF, 0x10000, 0x10001, 0x1FFFF, 0x20000, 0x20001, 0x150000, 0x200000, 0x200001, rnd.randrange(0, 0x400000)])

        def comp(base):
            v = pick([0, mag, -mag, base, rnd.randrange(-0x400000, 0x400000), 0x10000, -0x10000])
            return v

        dx, dy = comp(0), comp(0)
        dz = pick([0x200000, 0x200000 + mag, 0x200000 - mag, comp(0), 0, 0x1FFFF, -0x1FFFF, 0x20000, rnd.randrange(-0x400000, 0x600000)])
        if rnd.random() < 0.25:   # equality m == e
            e_ = abs(dz - 0x200000)
            dx = pick([e_, -e_]); dy = pick([0, e_ // 2, -e_ // 2, e_])
        obj.set32(M.POSX, (hx + dx) & M32); obj.set32(M.POSY, (hy + dy) & M32); obj.set32(M.POSZ, (hz + dz) & M32)
        obj.set32(M.CARRIER, pick([ENT0, ENT0, 0]))
        obj.set32(M.SHEET, SHEET)
        m.wb(SHEET + 0xE, sheet_attack)
        hero.set32(M.CARRIED, (ENT0 + 3 * STRIDE) if carried else 0)
        m.ww(M.CTR, ctr)
        w.put(0, hero); w.put(3, obj)
        ret = w.call(ADDR['upce'], mode & M32)
        real = (bytes(w.get(0).raw), bytes(w.get(3).raw), m.rw(M.CTR), tuple(w.j))
        mh, mo = hero.copy(), obj.copy()
        mem = {'ctr': ctr}
        j = []
        M.upce(s32(mode), mh, mo if carried else None, sheet_attack, mem, j)
        model = (bytes(mh.raw), bytes(mo.raw), mem['ctr'], tuple(w.j))
        compare('upce', (mode, carried, sheet_attack, hex(ctr), (dx, dy, dz)), real, model)
        w.j.clear()


# ---- V5: C23 ---------------------------------------------------------------------------------------------------------------------------
def v_c23():
    for i in range(N * 2):
        reset_globals()
        hero = mk_hero(); obj = mk_obj()
        obj.set32(M.CARRIER, pick([0, ENT0, ENT0]))
        t = pick([0, 1, 2, 3, -1, 4, -2, 0, 1, 2, 3, -1])
        obj.set32(M.T24, t & M32)
        hero.set32(M.CARRIED, pick([0, ENT0 + 3 * STRIDE]))
        w.put(0, hero); w.put(3, obj)
        w.call(ADDR['c23'], ENT0 + 3 * STRIDE)
        real = (bytes(w.get(0).raw), bytes(w.get(3).raw))
        mh, mo = hero.copy(), obj.copy()
        M.c23(mo, ENT0 + 3 * STRIDE, mh, lambda t: m.rw(0x80027400 + 4 * t), [])
        model = (bytes(mh.raw), bytes(mo.raw))
        compare('c23', (t, hex(obj.u32(M.CARRIER))), real, model)


# ---- V6: E2 ----------------------------------------------------------------------------------------------------------------------------
def v_e2():
    for i in range(N * 2):
        reset_globals()
        obj = mk_obj()
        obj.set32(M.ANIM, pick([0, 1, 2, 2, 3, 4]))
        obj.set32(M.ANIMEND, pick([0, 1, 1, 2]))
        obj.set32(M.VRAM184, pick([0, 4, 0xFFFF, 0, rnd.randrange(1 << 32)]))
        w.put(3, obj)
        w.call(ADDR['e2'], ENT0 + 3 * STRIDE)
        real = (bytes(w.get(3).raw), tuple(w.j))
        mo = obj.copy(); j = []
        M.e2(mo, 3, j)
        model = (bytes(mo.raw), tuple(j))
        compare('e2', (obj.u32(M.ANIM), obj.u32(M.ANIMEND), hex(obj.u32(M.VRAM184))), real, model)
        w.j.clear()


# ---- V7: MoveEntity carried branch -----------------------------------------------------------------------------------------------------
def v_move():
    # the non-carried path of MoveEntity calls 0x800375E0 and 0x80037730: stubbed (they return 0); the recursion into the carrier (moved flag 0) runs that path
    for a in (0x800375E0, 0x80037730):
        def sh(mm):
            mm.r[2] = 0
        m.hooks[a] = sh
    for i in range(N):
        reset_globals()
        carrier = mk_hero(); e = mk_obj()
        carrier.set32(M.MOVED, pick([0, 1]))
        carrier.set32(0x134, rs32() & M32); carrier.set32(0x138, rs32() & M32)
        carrier.set32(M.CARRIER, 0)
        carrier.set32(0x130, rnd.randrange(1 << 32))
        e.set32(M.CARRIER, ENT0 + 5 * STRIDE)
        for o in (0x1E4, 0x1E8, 0x1EC):
            e.set32(o, rnd.randrange(-0x100000, 0x100000) & M32)
        w.put(5, carrier)
        w.put(2, e)
        w.call(ADDR['moveent'], ENT0 + 2 * STRIDE)
        real = (bytes(w.get(2).raw), bytes(w.get(5).raw))
        me = e.copy(); mc = carrier.copy()
        j = []
        M.move_entity_carried(me, mc, j, bool(carrier.u32(M.MOVED)))
        if j and j[0] == ('recurse',):
            mc.set32(M.MOVED, 1)       # the recursion sets the carrier's moved flag, then runs the stubs and stores their result in +0x130
            mc.set32(0x130, 0)
        model = (bytes(me.raw), bytes(mc.raw))
        compare('moveent', (carrier.u32(M.MOVED),), real, model)
    for a in (0x800375E0, 0x80037730):
        del m.hooks[a]


def v_destroy1():
    for i in range(N):
        reset_globals()
        e = mk_obj(); hero = mk_hero()
        e.set32(M.CARRIER, pick([0, ENT0]))
        linked = rnd.random() < 0.5
        e.set32(M.LINK, (ENT0 + 7 * STRIDE) if linked else 0)
        link = mk_obj(); link.set32(0x68, rnd.randrange(1, 500))
        hero.set32(M.CARRIED, ENT0 + 3 * STRIDE)
        w.put(0, hero); w.put(3, e); w.put(7, link)
        w.call(ADDR['destroy1'], ENT0 + 3 * STRIDE)
        real = (bytes(w.get(0).raw), bytes(w.get(3).raw), bytes(w.get(7).raw))
        mh, me, ml = hero.copy(), e.copy(), link.copy()
        me.link_obj = ml
        M.destroy1(me, mh)
        model = (bytes(mh.raw), bytes(me.raw), bytes(ml.raw))
        compare('destroy1', (hex(e.u32(M.CARRIER)),), real, model)


FUNCS = [('step', v_step), ('trylift', v_trylift), ('pta', v_pta), ('upce', v_upce), ('c23', v_c23), ('e2', v_e2), ('moveent', v_move), ('destroy1', v_destroy1)]

if __name__ == '__main__':
    only = None
    for name, f in FUNCS:
        COV.clear()
        f()
        a, b = RANGES[name]
        n_all = (b - a) // 4
        hit = sum(1 for pc in COV if a <= pc < b)
        missing = [pc for pc in range(a, b, 4) if pc not in COV]
        print('%-9s %6d vectors  %4d mismatches   coverage %d/%d instructions' % (name, total[name], mism[name], hit, n_all))
        if missing:
            print('          uncovered:', ' '.join('%08x' % x for x in missing[:30]), '...' if len(missing) > 30 else '')
        if name in examples:
            print('   first mismatch:', examples[name][0])
            r, mo = examples[name][1], examples[name][2]
            print('   real :', str(r)[:300])
            print('   model:', str(mo)[:300])
    bad = sum(mism.values())
    print('TOTAL mismatches', bad)
    if GOLD_PATH:
        json.dump(GOLD, open(GOLD_PATH, 'w'))
        print('golden vectors written:', {k: len(v) for k, v in GOLD.items()})
    sys.exit(1 if bad else 0)
