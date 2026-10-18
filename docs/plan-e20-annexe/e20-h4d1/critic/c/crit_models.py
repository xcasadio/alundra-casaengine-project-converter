"""crit_models.py - the critic's own Python models of the fire-2 driver 0x80035C64, the general cancel path
0x800350C0 -> 0x80034E08, the guard 0x800351B8 and E28 0x8007FB58, written from the critic's own disassembly
reading (see report), then FUZZED against the REAL code run by cmipsmini (own interpreter).
Run: python -B crit_models.py [N]
"""
import random
import sys
from cmipsmini import Mem, CPU, s32, M32

HERO = 0x80127D30
STRIDE = 0x294
BLOCK = 0x800E4338
BUF = 0x80127138
PTR = 0x80127124
IDENT = 0x80127128
PHASE = 0x80127160
COUNTER = 0x8012715C
ITEM = 0x80127164
COUNT = 0x80134360
SEED = 0x80098708
TABX = [0, -543, -768, -543, 0, 543, 768, 543]
TABY = [512, 362, 0, -362, -512, -362, 0, 362]


def ent(i):
    return HERO + STRIDE * i


class St:
    def __init__(self):
        self.block = [0] * 9
        self.buf = [0] * 9
        self.ptr = 0
        self.ptr_id = 0       # +4 of the entity designated by ptr
        self.ident = 0
        self.phase = 0
        self.counter = 0
        self.item = 0
        self.hero_anim = 0
        self.slope = 0
        self.hx = self.hy = self.hz = 0
        self.ents = {}
        self.count = 0
        self.spawn_result = 0
        self.spawn_id = 0
        self.spawn_calls = []


# ------------------------------------------------------------------------------------------ the critic's models
def model_cancel_fire2(s):                       # 0x80034E08
    s.block = list(s.buf)
    for i in range(1, s.count + 1):              # slots 1..count INCLUSIVE
        e = s.ents.get(i)
        if e is not None and e['type'] == 0x15 and e['status'] == 2:
            e['status'] = 3
            e['blk'] = 0


def model_general_cancel(s):                     # 0x800350C0, item 0x30 only
    if s.item == 0:
        return
    if s.item == 0x30:
        model_cancel_fire2(s)
    s.item = 0


def model_guard(s):                              # 0x800351B8
    if s.slope == 4 or s.hero_anim - 0x32 not in (0, 1):
        model_general_cancel(s)
        return 1
    return 0


def model_driver(s):                             # 0x80035C64
    if s.phase == 1:
        return phase1(s)
    if s.phase == 0:
        return phase0(s)
    if s.phase == 2:
        return 1 if s.hero_anim != 0x34 else 0
    return 0                                     # negative or >= 3


def phase0(s):
    if model_guard(s) != 0:
        return 1
    if s.hero_anim != 0x33:
        return 0
    s.spawn_calls.append((HERO, 0, 0x15, s.hx, s.hy, s.hz, 0))
    s.ptr = s.spawn_result
    if s.spawn_result == 0:
        return 1
    s.ptr_id = s.spawn_id
    s.buf = list(s.block)
    s.phase += 1
    s.ident = s.ptr_id
    return phase1(s)


def phase1(s):
    if model_guard(s) != 0:
        s.block = list(s.buf)
        return 1
    if s.ptr_id != s.ident:
        s.block = list(s.buf)
        s.hero_anim = 0x34
        s.phase += 1
        return 0
    c = s.counter
    s.block[0] = 1
    s.block[2] = s.block[4] = c >> 4             # LimitY, SpeedY
    s.block[1] = s.block[3] = c >> 6             # LimitX, SpeedX
    return 0


# ------------------------------------------------------------------------------------------ interpreter side
def load(s, mem):
    for i, v in enumerate(s.block):
        mem.w32(BLOCK + 4 * i, v)
    for i, v in enumerate(s.buf):
        mem.w32(BUF + 4 * i, v)
    mem.w32(PTR, s.ptr)
    mem.w32(IDENT, s.ident)
    mem.w32(PHASE, s.phase)
    mem.w32(COUNTER, s.counter)
    mem.w32(ITEM, s.item)
    mem.w32(HERO + 0x88, s.hero_anim)
    mem.w32(HERO + 0x18C, s.slope)
    mem.w32(HERO + 0x114, s.hx)
    mem.w32(HERO + 0x118, s.hy)
    mem.w32(HERO + 0x11C, s.hz)
    mem.w32(COUNT, s.count)
    for i, e in s.ents.items():
        mem.w32(ent(i) + 0x10, e['status'])
        mem.w32(ent(i) + 0x68, e['type'])
        mem.w32(ent(i) + 0x20, e['blk'])
    if s.ptr:
        mem.w32(s.ptr + 4, s.ptr_id)


def snapshot(mem, s):
    return dict(block=[s32(mem.r32(BLOCK + 4 * i)) for i in range(9)], buf=[s32(mem.r32(BUF + 4 * i)) for i in range(9)],
                ptr=mem.r32(PTR), ident=mem.r32(IDENT), phase=s32(mem.r32(PHASE)), item=mem.r32(ITEM), anim=mem.r32(HERO + 0x88),
                ents={i: (mem.r32(ent(i) + 0x10), s32(mem.r32(ent(i) + 0x20))) for i in s.ents})


def snap_model(s):
    return dict(block=[s32(x) for x in s.block], buf=[s32(x) for x in s.buf], ptr=s.ptr, ident=s.ident, phase=s.phase, item=s.item,
                anim=s.hero_anim, ents={i: (e['status'], e['blk']) for i, e in s.ents.items()})


def rand_state(rng):
    s = St()
    s.block = [rng.choice([0, 1, rng.randint(-5, 300)]) for _ in range(9)]
    s.buf = [rng.choice([0, rng.randint(-5, 300)]) for _ in range(9)]
    s.phase = rng.choice([-1, 0, 0, 1, 1, 1, 2, 2, 3, 5])
    s.counter = rng.choice([0, 1, 15, 16, 31, 32, 63, 64, rng.randint(0, 400), rng.randint(0, 0x7FFFFFFF), rng.randint(-1000, -1)])
    s.item = 0x30
    s.hero_anim = rng.choice([0x32, 0x33, 0x33, 0x34, 0, 1, 0x31, 0x2D])
    s.slope = rng.choice([0, 0, 4, 2, 1])
    s.hx, s.hy, s.hz = (rng.randint(-2**31, 2**31 - 1) for _ in range(3))
    s.count = rng.randint(0, 5)
    for i in range(1, 8):
        if rng.random() < 0.7:
            s.ents[i] = dict(status=rng.choice([0, 1, 2, 2, 3, 4]), type=rng.choice([0x15, 0x15, 0x14, 0x16, 0x115, 0x11, 0]), blk=rng.choice([0, 5, -3]))
    s.spawn_result = rng.choice([0, 0x80128000 + STRIDE * 5, 0x80128000 + STRIDE * 6])
    s.spawn_id = rng.randint(1, 0xFFFF)
    s.ptr = (s.spawn_result or 0x80128000 + STRIDE * 6) if rng.random() < 0.5 else 0x80128000 + STRIDE * 5
    s.ptr_id = rng.randint(1, 0xFFFF)
    s.ident = s.ptr_id if rng.random() < 0.5 else rng.randint(1, 0xFFFF)
    return s


def clone(s):
    m = St()
    for k, v in s.__dict__.items():
        if k == 'ents':
            m.ents = {i: dict(e) for i, e in v.items()}
        elif isinstance(v, list):
            setattr(m, k, list(v))
        else:
            setattr(m, k, v)
    return m


def run_driver_case(s):
    mem = Mem()
    load(s, mem)
    cpu = CPU(mem)

    def spawn_stub(c):
        c.r[2] = s.spawn_result
        if s.spawn_result:
            mem.w32(s.spawn_result + 4, s.spawn_id)
    cpu.stubs[0x80039f58] = spawn_stub
    ret = cpu.run(0x80035C64)
    real = snapshot(mem, s)
    real_calls = [c for c in cpu.calls if c[0] == 0x80039f58]
    m = clone(s)
    mret = model_driver(m)
    return ret, real, real_calls, mret, snap_model(m), m.spawn_calls


def fuzz_driver(n, seed):
    rng = random.Random(seed)
    bad = 0
    cover = {}
    for k in range(n):
        s = rand_state(rng)
        ret, real, rc, mret, mod, mcalls = run_driver_case(s)
        key = (s.phase if s.phase in (0, 1, 2) else 'other', 'slope4' if s.slope == 4 else 'ok', hex(s.hero_anim) if s.hero_anim in (0x32, 0x33, 0x34) else 'otheranim',
               'spawn' if s.spawn_result else 'nospawn', mret)
        cover[key] = cover.get(key, 0) + 1
        ok = (ret == mret) and real == mod
        if ok and mcalls:
            c = rc[0] if len(rc) == 1 else None
            ok = c is not None and c[1][0] == HERO and c[1][1] == 0 and c[1][2] == 0x15 and c[1][3] == (s.hx & M32) and c[2] == (s.hy & M32) and c[3] == (s.hz & M32) and c[4] == 0
        elif ok:
            ok = len(rc) == 0
        if not ok:
            bad += 1
            if bad <= 5:
                print('MISMATCH', k, 'ret', ret, mret)
                for kk in real:
                    if real[kk] != mod[kk]:
                        print('  ', kk, 'real', real[kk], 'model', mod[kk])
                print('  case', {a: getattr(s, a) for a in ('phase', 'counter', 'hero_anim', 'slope', 'spawn_result', 'ptr', 'ident', 'ptr_id', 'spawn_id', 'count')})
    return bad, cover


def model_e28(seed, x, y, z):
    calls = []
    s = seed
    for k in range(8):
        s = (s * 0x7D2B89DD + 0xE06A02E7) & M32
        r = ((s * 0x1801) >> 32) + 0x320
        calls.append((0, 19, 2, (x + TABX[k] * r) & M32, (y + TABY[k] * r) & M32, z & M32))
    return calls, s


def fuzz_e28(n, seed):
    rng = random.Random(seed)
    bad = 0
    for k in range(n):
        sd = rng.randint(0, M32)
        x, y, z = (rng.randint(-2**31, 2**31 - 1) for _ in range(3))
        mem = Mem()
        ee = 0x80128000
        mem.w32(SEED, sd)
        mem.w32(ee + 0x114, x); mem.w32(ee + 0x118, y); mem.w32(ee + 0x11C, z)
        cpu = CPU(mem)
        cpu.stubs[0x8003bdd8] = lambda c: c.r.__setitem__(2, 1)
        cpu.stubs[0x8003a59c] = lambda c: c.r.__setitem__(2, 0)
        cpu.run(0x8007FB58, args=(ee,))
        eff = [(c[1][0], c[1][1], c[1][2], c[1][3], c[2], c[3]) for c in cpu.calls if c[0] == 0x8003bdd8]
        des = [(c[1][0], s32(c[1][1])) for c in cpu.calls if c[0] == 0x8003a59c]
        mcalls, msd = model_e28(sd, x, y, z)
        okc = eff == mcalls and mem.r32(SEED) == msd and des == [(ee, -1)] and len(cpu.calls) == 9
        if not okc:
            bad += 1
            if bad < 4:
                print('E28 MISMATCH', k, eff[:2], mcalls[:2], hex(mem.r32(SEED)), hex(msd), des)
    return bad


if __name__ == '__main__':
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
    bad, cover = fuzz_driver(n, 20261018)
    print('driver fuzz', n, 'cases, mismatches', bad)
    for k in sorted(cover, key=str):
        print('  ', k, cover[k])
    print('E28 fuzz mismatches', fuzz_e28(n, 777))
