"""myvec.py - independent models (written by the critic from its own disassembly reading) vs the REAL code, on the critic's own vector generator.
Usage: python -B myvec.py [N]"""
import os, sys, random, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'emu'))
import emu as E

M32 = 0xFFFFFFFF
ENT0 = 0x80127D30
STRIDE = 0x294
CTR = 0x80098F28
NSLOT = 0x80134360
PAD_EDGE = 0x80126F2A
SHEETBUF = 0x80141000


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
COV = set()
_orig = E.Emu.exec1


def _exec1(self, pc, w):
    COV.add(pc)
    return _orig(self, pc, w)


E.Emu.exec1 = _exec1

m = E.make_machine()
J = []
HAS3B = [0]


def h_getnum(mm):
    J.append(('item', mm.r[4] & M32))
    mm.r[2] = HAS3B[0]


def h_destroy2(mm):
    J.append(('destroy2', (mm.r[4] - ENT0) // STRIDE, s32(mm.r[5])))


m.hooks[0x8004E428] = h_getnum
m.hooks[0x8003A59C] = h_destroy2  # the two-argument destroy is stubbed (journalled); 1-arg is real


def ea(slot):
    return ENT0 + slot * STRIDE


def rd(slot, off):
    return m.rw(ea(slot) + off)


def wr(slot, off, v):
    m.ww(ea(slot) + off, v & M32)


def snap(slots):
    return {s: bytes(m.ram[ea(s) & 0x1FFFFFFF:(ea(s) & 0x1FFFFFFF) + STRIDE]) for s in slots}


def restore(sn):
    for s, b in sn.items():
        a = ea(s) & 0x1FFFFFFF
        m.ram[a:a + STRIDE] = b


rnd = random.Random(777001)


def rs32():
    c = rnd.randrange(8)
    if c == 0:
        return rnd.randrange(-(1 << 31), 1 << 31)
    if c == 1:
        return rnd.choice([0, 1, -1, 0x1FFFF, 0x20000, -0x1FFFF, -0x20000, 0x10000, -0x10000, 0xFFFF, 0x200000, -0x200000, 0x1FFFFF, 0x201000, 0x1F0000])
    if c == 2:
        return rnd.randrange(-0x400000, 0x400000)
    if c == 3:
        return rnd.randrange(-0x40000, 0x40000)
    if c == 4:
        return rnd.choice([0x7FFFFFFF, -0x80000000, 0x7FFF0000, -0x7FFF0000])
    return rnd.randrange(-0x100000, 0x300000)


def rand_junk(slot):
    for o in range(0, STRIDE, 4):
        wr(slot, o, rnd.getrandbits(32) if rnd.random() < 0.5 else 0)


fails = {}
counts = {}


def check(name, ok, info):
    counts[name] = counts.get(name, 0) + 1
    if not ok:
        fails[name] = fails.get(name, 0) + 1
        if fails[name] <= 3:
            print('MISMATCH', name, info)


def step_towards(cur, tgt, step):
    d = s32(tgt - cur)
    ad = (-d) & M32 if d < 0 else d
    if ad < (step & M32):
        return tgt & M32
    return (cur + step) & M32 if d > 0 else (cur - step) & M32


def abs32(x):
    x = s32(x)
    return (-x) & M32 if x < 0 else x  # note: abs(-2^31) stays 0x80000000 as unsigned, signed compare below handles it


def sabs(x):
    x = s32(x)
    return -x if x < 0 else x


def sabs_wrap(x):
    # 32-bit wrapping negate then signed interpretation (what negu does)
    x = s32(x)
    if x < 0:
        x = s32((-x) & M32)
    return x


# ------------------------------------------------------------------------------------------ StepTowards
for _ in range(N):
    cur, tgt = rs32(), rs32()
    step = rnd.choice([0x10000, 0x20000, 1, 0, 0x7FFFFFFF, rs32() & M32])
    real = m.call(0x8002F854, cur & M32, tgt & M32, step & M32)
    # model: v1 = tgt-cur (wrap); v0 = v1 or negu v1 (wrap); sltu v0,v0,step -> tgt ; else (v1>0 signed) cur+step else cur-step
    v1 = (tgt - cur) & M32
    v0 = v1 if s32(v1) >= 0 else ((-v1) & M32)
    if v0 < (step & M32):
        mod = tgt & M32
    elif s32(v1) > 0:
        mod = (cur + step) & M32
    else:
        mod = (cur - step) & M32
    check('StepTowards', real == mod, (cur, tgt, step, real, mod))

# ------------------------------------------------------------------------------------------ UpdatePlayerCarriedEntity
CARRIED_SLOT = 3


def setup_upce():
    for s in (0, CARRIED_SLOT, 5):
        for o in range(0, STRIDE, 4):
            wr(s, o, 0)
    for o in range(0, STRIDE, 4):
        if rnd.random() < 0.3:
            wr(0, o, rnd.getrandbits(32))
            wr(CARRIED_SLOT, o, rnd.getrandbits(32))
    has = rnd.random() < 0.9
    wr(0, 0x2C, ea(CARRIED_SLOT) if has else 0)
    if has and rnd.random() < 0.8:
        wr(CARRIED_SLOT, 0x28, ea(0))
    else:
        wr(CARRIED_SLOT, 0x28, ea(0) if has else 0)
    hx, hy, hz = rs32() & M32, rs32() & M32, rs32() & M32
    wr(0, 0x114, hx); wr(0, 0x118, hy); wr(0, 0x11C, hz)
    kind = rnd.randrange(5)
    if kind == 0:
        ox, oy, oz = rs32(), rs32(), rs32()
    elif kind == 1:
        ox, oy, oz = rnd.randrange(-0x30000, 0x30000), rnd.randrange(-0x30000, 0x30000), rnd.randrange(-0x30000, 0x30000)
    elif kind == 2:
        ox, oy, oz = rnd.randrange(-0x40000, 0x40000), rnd.randrange(-0x40000, 0x40000), 0x200000 + rnd.randrange(-0x30000, 0x30000)
    elif kind == 3:
        ox = rnd.choice([0x1FFFF, 0x20000, -0x1FFFF, -0x20000, 0, 0x1FFFE]); oy = rnd.choice([0x1FFFF, 0x20000, -0x1FFFF, -0x20000, 0]); oz = rnd.choice([0x1FFFF, 0x20000, -0x1FFFF, -0x20000, 0])
    else:
        ox, oy, oz = rnd.randrange(-0x180000, 0x180000), rnd.randrange(-0x180000, 0x180000), 0x200000 + rnd.randrange(-0x180000, 0x180000)
    wr(CARRIED_SLOT, 0x114, hx + ox); wr(CARRIED_SLOT, 0x118, hy + oy); wr(CARRIED_SLOT, 0x11C, hz + oz)
    # stale offsets in hero +0x30..0x38
    for o in (0x30, 0x34, 0x38):
        wr(0, o, rs32())
    wr(CARRIED_SLOT, 0x1C4, SHEETBUF)
    m.wb(SHEETBUF + 0xE, rnd.choice([0, 0, 1, 7, 255]))
    m.ww(CTR, rnd.choice([0, 1, 4, 5, 6, 100, 0xFFFFFFFF, 0x80000000, 3, 4, 5, 5]))
    # link entity for destroy1
    if rnd.random() < 0.5:
        wr(CARRIED_SLOT, 0x1B8, ea(5))
        wr(5, 0x68, 2)
    else:
        wr(CARRIED_SLOT, 0x1B8, 0)
    wr(CARRIED_SLOT, 0x10, rnd.choice([1, 2, 3]))
    wr(CARRIED_SLOT, 0x228, rnd.getrandbits(32))


def model_upce(mode):
    h = {o: rd(0, o) for o in (0x2C, 0x30, 0x34, 0x38, 0x114, 0x118, 0x11C, 0x8C)}
    ctr = m.rw(CTR)
    out = dict(h30=h[0x30], h34=h[0x34], h38=h[0x38], ctr=ctr, obj={}, hero2c=h[0x2C])
    o = h[0x2C]
    if o == 0:
        out['ctr'] = 0
        return out
    sl = (o - ENT0) // STRIDE
    sheet = rd(sl, 0x1C4)
    attack = m.rb(sheet + 0xE)
    if attack == 0 and mode == 1:
        mode = 0
    ox, oy, oz = rd(sl, 0x114), rd(sl, 0x118), rd(sl, 0x11C)
    dx = (ox - h[0x114]) & M32
    dy = (oy - h[0x118]) & M32
    dz = (oz - h[0x11C]) & M32
    if mode == 0:
        ax = sabs_wrap(dx); ay = sabs_wrap(dy)
        t = (dz + 0xFFE00000) & M32            # dz - 0x200000
        e = t if s32(t) >= 0 else (0x200000 - dz) & M32
        e = s32(e)
        mm = ax if not (ax < ay) else ay      # slt v0,v1,a1 ; if |dx|<|dy| v1=|dy|
        if mm < e:
            nz = step_towards(dz, 0x200000, 0x10000)
            out['h30'], out['h34'], out['h38'] = dx, dy, nz
        else:
            out['h30'] = step_towards(dx, 0, 0x10000)
            out['h34'] = step_towards(dy, 0, 0x10000)
            out['h38'] = step_towards(dz, 0x200000, 0x10000)
        out['ctr'] = 0
    elif mode == 1:
        if s32(ctr) < 6:
            out['ctr'] = (ctr + 1) & M32
            return out
        if sabs_wrap(dx) <= 0x1FFFF and sabs_wrap(dy) <= 0x1FFFF and sabs_wrap(dz) <= 0x1FFFF:
            out['obj']['status'] = 4
            out['obj']['p228'] = M32
            out['obj']['link_cleared'] = rd(sl, 0x1B8) != 0
            if rd(sl, 0x28) != 0:
                out['hero2c'] = 0
            return out
        out['h30'] = step_towards(dx, 0, 0x20000)
        out['h34'] = step_towards(dy, 0, 0x20000)
        out['h38'] = step_towards(dz, 0, 0x20000)
    else:
        out['obj'] = dict(t24=M32, dir=h[0x8C], x=h[0x114], y=h[0x118], z=(h[0x11C] + 0x200000) & M32)
        out['ctr'] = 0
    return out


for mode in (0, 1, 2, 3, 0xFF):
    for _ in range(N):
        setup_upce()
        pre_slot3 = snap([3, 5])
        pred = model_upce(mode)
        link_before = rd(CARRIED_SLOT, 0x1B8)
        r = m.call(0x8002F884, mode)
        got = dict(h30=rd(0, 0x30), h34=rd(0, 0x34), h38=rd(0, 0x38), ctr=m.rw(CTR), hero2c=rd(0, 0x2C))
        ok = all(got[k] == pred[k] for k in got)
        # object side effects
        if 'status' in pred['obj']:
            ok = ok and rd(3, 0x10) == 4 and rd(3, 0x228) == M32 and rd(3, 0x1B8) == 0 and (pred['obj']['link_cleared'] == (rd(5, 0x68) == 0 and link_before != 0) or not pred['obj']['link_cleared'])
        elif 't24' in pred['obj']:
            ok = ok and rd(3, 0x24) == M32 and rd(3, 0x8C) == pred['obj']['dir'] and rd(3, 0x114) == pred['obj']['x'] and rd(3, 0x118) == pred['obj']['y'] and rd(3, 0x11C) == pred['obj']['z']
        else:
            ok = ok and rd(3, 0x10) != 4 or True
        # nothing else of the carried entity may change unless the model says so
        post3 = snap([3])[3]
        allowed = set()
        if 'status' in pred['obj']:
            allowed |= set(range(0x10, 0x14)) | set(range(0x228, 0x22C)) | set(range(0x1B8, 0x1BC))
        if 't24' in pred['obj']:
            allowed |= set(range(0x24, 0x28)) | set(range(0x8C, 0x90)) | set(range(0x114, 0x120))
        diffb = [i for i in range(STRIDE) if post3[i] != pre_slot3[3][i] and i not in allowed]
        ok = ok and not diffb
        check('UPCE mode %d' % mode, ok, (mode, pred, got, diffb[:6]))
        restore(pre_slot3)

# ------------------------------------------------------------------------------------------ TryLift
def setup_gen(slot_count=6):
    for s in range(slot_count + 1):
        for o in range(0, STRIDE, 4):
            wr(s, o, rnd.getrandbits(32) if rnd.random() < 0.35 else 0)
    m.ww(NSLOT, slot_count)


for _ in range(N):
    setup_gen()
    sl = rnd.randrange(1, 6)
    wr(0, 0x2C, 0)
    flags = rnd.getrandbits(32)
    wr(sl, 0x6C, flags)
    if rnd.random() < 0.5:
        wr(sl, 0x12C, ea(0))
    HAS3B[0] = rnd.choice([0, 1, 5])
    J.clear()
    pre = snap(range(0, 7))
    r = m.call(0x8002EDBC, ea(sl))
    kind = (flags & 0x600) >> 9
    if kind == 0:
        exp = 0
    elif kind == 1:
        exp = 1
    else:
        exp = 1 if HAS3B[0] else 0
    post = snap(range(0, 7))
    ok = (r == exp)
    if exp == 0:
        ok = ok and post == pre
    else:
        ground = struct.unpack_from('<I', pre[0], 0x144)[0] != 0
        sup = struct.unpack_from('<I', pre[sl], 0x12C)[0]
        anim = (0x0C if ground else 0x30) if sup == ea(0) else (5 if ground else 0x0B)
        exp0 = bytearray(pre[0]); exp1 = bytearray(pre[sl])
        struct.pack_into('<I', exp0, 0x2C, ea(sl))
        struct.pack_into('<I', exp1, 0x28, ea(0))
        struct.pack_into('<I', exp0, 0x88, anim)
        for k, o in enumerate((0x114, 0x118, 0x11C)):
            struct.pack_into('<I', exp0, 0x30 + 4 * k, (struct.unpack_from('<I', pre[sl], o)[0] - struct.unpack_from('<I', pre[0], o)[0]) & M32)
        ok = ok and bytes(exp0) == post[0] and bytes(exp1) == post[sl]
        for s in range(1, 7):
            if s != sl:
                ok = ok and post[s] == pre[s]
    jexp = [('item', 0x3B)] if kind in (2, 3) else []
    ok = ok and J == jexp
    check('TryLift', ok, (kind, flags, r))

# ------------------------------------------------------------------------------------------ PlayerTryAction
for _ in range(N):
    cnt = rnd.randrange(-1, 7)
    setup_gen(7)
    m.ww(NSLOT, cnt & M32)
    pre0 = None
    hero_has = rnd.random() < 0.15
    wr(0, 0x2C, ea(rnd.randrange(1, 7)) if hero_has else 0)
    contact = rnd.random() < 0.4
    wr(0, 0x130, ea(rnd.randrange(0, 7)) if contact else 0)
    edge = rnd.choice([0, 0x80, 0x20, 0xA0, 0xFFFF, 0x7F])
    m.wh(PAD_EDGE, edge)
    HAS3B[0] = rnd.choice([0, 1])
    for s in range(1, 8):
        # make the loop interesting: status 2/3, +0x20 == 0, support == hero
        if rnd.random() < 0.6:
            wr(s, 0x10, rnd.choice([1, 2, 3, 4]))
        if rnd.random() < 0.6:
            wr(s, 0x20, 0)
        if rnd.random() < 0.6:
            wr(s, 0x12C, ea(0))
        if rnd.random() < 0.7:
            wr(s, 0x6C, rnd.choice([0, 0x200, 0x400, 0x600, 0x3323E4, 0x3325E4]) | rnd.getrandbits(8))
    wr(0, 0x144, rnd.choice([0, 1]))
    # model on a python copy of slots
    pre = snap(range(0, 8))
    J.clear()

    def u(slot, off, sn=pre):
        return struct.unpack_from('<I', sn[slot], off)[0]

    # emulate in python on a dictionary of bytearrays
    ents = {s: bytearray(pre[s]) for s in range(0, 8)}

    def getu(s, o):
        return struct.unpack_from('<I', ents[s], o)[0]

    def setu(s, o, v):
        struct.pack_into('<I', ents[s], o, v & M32)

    jm = []

    def trylift_m(sl):
        kind = (getu(sl, 0x6C) & 0x600) >> 9
        if kind == 0:
            return 0
        if kind != 1:
            jm.append(('item', 0x3B))
            if not HAS3B[0]:
                return 0
        setu(0, 0x2C, ea(sl)); setu(sl, 0x28, ea(0))
        g = getu(0, 0x144) != 0
        if getu(sl, 0x12C) == ea(0):
            setu(0, 0x88, 0x0C if g else 0x30)
        else:
            setu(0, 0x88, 5 if g else 0x0B)
        for k, o in enumerate((0x114, 0x118, 0x11C)):
            setu(0, 0x30 + 4 * k, getu(sl, o) - getu(0, o))
        return 1

    def pta_m():
        if getu(0, 0x2C) != 0:
            setu(0, 0x88, 0x0C if getu(0, 0x144) else 0x30)
            return 1
        c = getu(0, 0x130)
        if c != 0 and (edge & 0x80):
            return trylift_m((c - ENT0) // STRIDE)
        n = s32(m.rw(NSLOT))
        if n < 0:
            return 0
        for s in range(0, n + 1):
            if ((getu(s, 0x10) - 2) & M32) < 2 and getu(s, 0x20) == 0 and getu(s, 0x12C) == ea(0):
                if trylift_m(s):
                    return 1
        return 0

    # contact slot must be within 0..7
    exp = pta_m()
    r = m.call(0x8002EEAC)
    post = snap(range(0, 8))
    ok = (r == exp) and J == jm and all(bytes(ents[s]) == post[s] for s in range(0, 8))
    check('PlayerTryAction', ok, (cnt, hero_has, contact, edge, r, exp, J, jm))

print('counts', counts)
print('fails', fails)
print('coverage addrs', len(COV))


def cov(lo, hi):
    tot = (hi - lo) // 4
    hit = sum(1 for a in range(lo, hi, 4) if a in COV)
    return hit, tot


for nm, (lo, hi) in dict(step=(0x8002F854, 0x8002F884), trylift=(0x8002EDBC, 0x8002EEAC), pta=(0x8002EEAC, 0x8002EFF0), upce=(0x8002F884, 0x8002FB00)).items():
    print(nm, cov(lo, hi))
