"""Differential oracle for the E19.sm opcodes: the REAL handlers of ALUN_CD.EXE (France) run in a minimal MIPS-I interpreter
(emu.py, copied from docs/plan-e14-annexe/e14a/oracle-a1/tools, read-only use of the EXE) against a Python model written from the
disassembly, on random states.  The REAL search function 0x8003C954 runs too (only its id validator 0x8003C914 is stubbed: its result
is discarded by every caller).  Nothing is written outside this folder.

Usage: python -B oracle.py [N_PER_OPCODE] [SEED]
"""
import os, random, struct, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import emu

M32 = 0xFFFFFFFF
s32 = emu.s32
ENT0 = 0x80127D30
STRIDE = 0x294
NADDR = 0x80134360
RES = 0x8013D8D8            # first result word (index 1 of the 1-based buffer at 0x8013D8D4)
STATE = 0x8013D9E8
CODE = 0x80150000
PCP = 0x80150100
PSTATS_PTR = 0x80176318
PSTATS = 0x80176400
MAPPTR = 0x800DD86C
MAPBASE = 0x80160000
PAD = 0x80126F18
TABLE = 0x80098FAC

OFF = dict(parent=0x0C, status=0x10, hp=0x14, hpmax=0x18, platform=0x28, ref=0x48, flags=0x6C, tanim=0x88, tdir=0x8C,
           anim=0xB4, forcez=0xB8, posx=0x114, posy=0x118, posz=0x11C, tilex=0x120, tiley=0x124, tilez=0x128,
           riding=0x12C, xcoll=0x130, lasta=0x26C, lastd=0x270)


def ent_addr(i):
    return ENT0 + i * STRIDE


class World:
    """A random world: slots 0..n-1 (slot 0 is the hero), all fields as plain python ints (unsigned 32-bit)."""

    def __init__(self, rng, realistic_hero_ref=True):
        self.n = rng.randint(2, 8)
        self.e = []
        for i in range(self.n):
            d = {}
            d['parent'] = 0
            d['status'] = rng.choice([0, 1, 2, 2, 2, 3, 3, 4, 5]) if i else rng.choice([2, 2, 2, 3])
            d['hp'] = rng.choice([0, 1, 5, 10, 30, 50, 120])
            d['hpmax'] = rng.choice([1, 5, 10, 30, 50, 60])
            d['platform'] = 0
            d['ref'] = (rng.choice([0, 1, 2, 3, 4, 5, 6, 7, 8, 0x14, 0x7F, M32]) if i or not realistic_hero_ref else M32)
            d['flags'] = rng.choice([0, 0x80, 0x80, 0x100, 0x4100, 0x180, 0x1])
            d['tanim'] = rng.choice([0, 1, 2, 3, 9, 0x41, 0xFF])
            d['tdir'] = rng.randint(0, 31)
            d['anim'] = rng.choice([0, 0, 0x80, 0x40, 0xC0])
            d['forcez'] = rng.choice([0, 0, 1, -1, 5, -5, 0x10000, -0x10000, 0x7FFFFFFF, -0x80000000]) & M32
            d['posx'] = rng.randint(0, 0x1E00000) & M32
            d['posy'] = rng.randint(0, 0x2000000) & M32
            d['posz'] = rng.choice([0, 0x10000, 0x100000, 0x400001, 0x1FFFFFF])
            d['tilex'] = rng.randint(0, 20)
            d['tiley'] = rng.randint(0, 20)
            d['tilez'] = rng.randint(0, 12)
            d['riding'] = 0
            d['xcoll'] = 0
            d['lasta'] = rng.choice([0, 7, 0x1234])
            d['lastd'] = rng.choice([0, 3, 0x1F])
            self.e.append(d)
        for i, d in enumerate(self.e):
            for k in ('parent', 'platform', 'riding', 'xcoll'):
                if rng.random() < 0.45:
                    d[k] = ent_addr(rng.randrange(self.n))
        self.pstats = dict(hp=rng.randint(0, 50), hpmax=rng.randint(1, 50), mp=rng.randint(0, 4), mpmax=rng.randint(0, 4),
                           money=rng.randint(0, 9999))
        self.rng = rng

    def write(self, m):
        for off in range(0, 10 * STRIDE, 4):
            m.ww(ENT0 + off, 0)
        m.ww(NADDR, self.n - 1)
        for i, d in enumerate(self.e):
            for k, o in OFF.items():
                m.ww(ent_addr(i) + o, d[k] & M32)
        for off in range(0, 0x40, 4):
            m.ww(STATE + off, 0)
        for off in range(0, 0x120, 4):
            m.ww(RES - 4 + off, 0xDEADBEEF)
        m.ww(PSTATS_PTR, PSTATS)
        p = self.pstats
        m.wh(PSTATS + 0, p['hp']); m.wh(PSTATS + 2, p['hpmax']); m.wh(PSTATS + 4, p['mp']); m.wh(PSTATS + 6, p['mpmax']); m.wh(PSTATS + 8, p['money'])
        for off in range(0, 0x20, 4):
            m.ww(PAD + off, 0)


def make_machine():
    m = emu.Emu()
    m.hooks[0x8003C914] = lambda mm: None      # CheckEntityRecord wrapper: discarded result, message only
    m.hooks[0x8003C840] = lambda mm: None      # debug message printer (illegal search type path): no effect on the result
    return m


# ------------------------------------------------------------------------------------------------ models (python, from the disassembly)
def active(d):
    return 1 <= d['status'] <= 3


def model_search(w, self_i, t):
    """0x8003C954, from the disassembly: list of slot indices in buffer order (index 1, 2, ... of the 1-based buffer)."""
    N = w.n
    S = w.e[self_i]
    if (t & 0x80) == 0:
        if not active(S):
            return []
        return [i for i in range(1, N) if w.e[i]['ref'] == t]
    ft = t & 0x7F
    if ft == 0:
        return [self_i]
    if ft == 1:
        return [0]
    a = ent_addr(self_i)
    out = []
    if ft == 2:
        return [i for i in range(0, N) if active(w.e[i])]
    if ft == 3:
        return [i for i in range(1, N) if active(w.e[i])]
    if ft == 4:
        for i in range(0, N):
            c = w.e[i]
            if active(S) and (c['flags'] & 0x80) and not (c['anim'] & 0x80) and c['platform'] == 0:
                out.append(i)
        return out
    for i in range(1, N):
        c = w.e[i]
        if not active(c):
            continue
        ca = ent_addr(i)
        if ft == 5 and S['riding'] == ca:
            out.append(i)
        elif ft == 6 and c['riding'] == a:
            out.append(i)
        elif ft == 7 and S['xcoll'] == ca:
            out.append(i)
        elif ft == 8 and c['xcoll'] == a:
            out.append(i)
        elif ft == 9 and c['parent'] == a:
            out.append(i)
        elif ft == 10 and S['parent'] == ca:
            out.append(i)
        elif ft == 11 and c['platform'] != 0:
            out.append(i)
    return out


def dll_search(w, self_i, t):
    """Python translation of Alundra/Scripts/EntitySearchService.cs (GetMatchingEntitiesBySearchType): candidates are the spawned
    entities (the hero included), in list order; the player is slot 0."""
    S = w.e[self_i]
    pl = 0
    sp = list(range(w.n))
    out = []
    if (t & 0x80) == 0:
        if active(S):
            for c in sp:
                if w.e[c]['ref'] == t:
                    out.append(c)
        return out
    ft = t & 0x7F
    a = ent_addr(self_i)
    if ft == 0:
        return [self_i]
    if ft == 1:
        return [pl]
    for c in sp:
        d = w.e[c]
        ca = ent_addr(c)
        if ft == 2 and active(d):
            out.append(c)
        elif ft == 3 and active(d) and c != pl:
            out.append(c)
        elif ft == 4 and active(S) and (d['flags'] & 0x80) and not (d['anim'] & 0x80) and d['platform'] == 0:
            out.append(c)
        elif ft == 5 and c != pl and active(d) and S['riding'] == ca:
            out.append(c)
        elif ft == 6 and c != pl and active(d) and d['riding'] == a:
            out.append(c)
        elif ft == 7 and c != pl and active(d) and S['xcoll'] == ca:
            out.append(c)
        elif ft == 8 and c != pl and active(d) and d['xcoll'] == a:
            out.append(c)
        elif ft == 9 and c != pl and active(d) and d['parent'] == a:
            out.append(c)
        elif ft == 10 and c != pl and active(d) and S['parent'] == ca:
            out.append(c)
        elif ft == 11 and c != pl and active(d) and d['platform'] != 0:
            out.append(c)
    return out


def rev(lst):
    return list(reversed(lst))


class Exp:
    """expected outcome: v0, Result (None = not written), and word writes {addr: value}"""

    def __init__(self, v0, result=None, writes=None):
        self.v0 = v0
        self.result = result
        self.writes = writes or {}


def abs32(x):
    x = s32(x)
    return s32(-x) if x < 0 else x


MODELS = {}


def model(op):
    def deco(f):
        MODELS[op] = f
        return f
    return deco


@model(0x60)
def m60(w, si, b, search):
    ms = search(w, si, b[1])
    me = ent_addr(si)
    return Exp(2, 1 if any(w.e[i]['riding'] == me for i in ms) else 0)


@model(0x61)
def m61(w, si, b, search):
    ms = search(w, si, b[1])
    return Exp(2, 1 if any(w.e[si]['riding'] == ent_addr(i) for i in ms) else 0)


def area(w, b, need_flags):
    for i in range(w.n):
        c = w.e[i]
        if not active(c):
            continue
        if need_flags and not ((c['flags'] & 0x80) and not (c['anim'] & 0x80) and c['platform'] == 0):
            continue
        if b[1] <= s32(c['tilex']) <= b[2] and b[3] <= s32(c['tiley']) <= b[4] and b[5] <= s32(c['tilez']) <= b[6]:
            return 1
    return 0


@model(0x3C)
def m3c(w, si, b, search):
    return Exp(7, area(w, b, True))


@model(0x3D)
def m3d(w, si, b, search):
    return Exp(7, area(w, b, False))


@model(0x6A)
def m6a(w, si, b, search):
    S = w.e[si]
    return Exp(1, None, {STATE + 0xC: S['posx'], STATE + 0x10: S['posy'], STATE + 0x14: S['posz']})


def axis(w, si, b, which):
    S = w.e[si]
    memo = w.memo[which]
    d = abs32((memo - S[('posx', 'posy', 'posz')[which]]) & M32)
    thr = s32(((b[1] | (b[2] << 8)) << 16) & M32)
    return Exp(3, 0 if d < thr else 1)


@model(0x6B)
def m6b(w, si, b, search):
    return axis(w, si, b, 0)


@model(0x6C)
def m6c(w, si, b, search):
    return axis(w, si, b, 1)


@model(0x6D)
def m6d(w, si, b, search):
    return axis(w, si, b, 2)


@model(0x72)
def m72(w, si, b, search):
    S = w.e[si]
    a = ent_addr(si)
    return Exp(1, None, {a + OFF['lasta']: S['tanim'], a + OFF['lastd']: S['tdir']})


STATUS_TABLE = None


@model(0x88)
def m88(w, si, b, search):
    ms = search(w, si, b[1])
    val = STATUS_TABLE(b[2])
    return Exp(3, None, {ent_addr(i) + OFF['status']: val for i in ms})


@model(0x96)
def m96(w, si, b, search):
    ms = search(w, si, b[1])
    wr = {}
    for i in ms:
        c = w.e[i]
        hp = s32(c['hp'] + b[1])
        if s32(c['hpmax']) < hp:
            hp = s32(c['hpmax'])
        wr[ent_addr(i) + OFF['hp']] = hp & M32
    return Exp(3, None, wr)


@model(0x97)
def m97(w, si, b, search):
    p = w.pstats
    new = p['mp'] + b[1]
    if p['mpmax'] < new:
        v = p['mpmax']
    elif new < 0:
        v = 0
    else:
        v = new
    return Exp(2, None, {'mp': v})


@model(0xA9)
def ma9(w, si, b, search):
    ms = search(w, si, b[1])
    return Exp(2, 1 if any(w.e[si]['xcoll'] == ent_addr(i) for i in ms) else 0)


@model(0xAA)
def maa(w, si, b, search):
    ms = search(w, si, b[1])
    return Exp(2, 1 if any(w.e[i]['xcoll'] == ent_addr(si) for i in ms) else 0)


@model(0xB2)
def mb2(w, si, b, search):
    g1 = search(w, si, b[1])
    if not g1:
        return Exp(3, 0)
    g2 = search(w, si, b[2])
    return Exp(3, 1 if set(g1) & set(g2) else 0)


@model(0xB4)
def mb4(w, si, b, search):
    ms = search(w, si, b[1])
    return Exp(2, 1 if ms and s32(w.e[ms[0]]['forcez']) > 0 else 0)


@model(0xB5)
def mb5(w, si, b, search):
    ms = search(w, si, b[1])
    return Exp(2, 1 if ms and s32(w.e[ms[0]]['forcez']) < 0 else 0)


@model(0xB6)
def mb6(w, si, b, search):
    ms = search(w, si, b[1])
    return Exp(2, 1 if ms and w.e[ms[0]]['forcez'] == 0 else 0)


@model(0xB7)
def mb7(w, si, b, search):
    ms = search(w, si, b[1])
    return Exp(3, 1 if any(w.e[i]['tanim'] == b[2] for i in ms) else 0)


@model(0xB3)
def mb3(w, si, b, search):
    return Exp(9, None, {PAD + 0x10: b[1] | (b[2] << 8), PAD + 0x12: b[3] | (b[4] << 8), PAD + 0x14: b[5] | (b[6] << 8), PAD + 0x16: b[7] | (b[8] << 8)})


@model(0x9E)
def m9e(w, si, b, search):
    cnt = 0
    for row in range(b[4]):
        for col in range(b[3]):
            if w.cells.get((b[2] + row, b[1] + col), 0) & 2:
                cnt += 1
    return Exp(6, 0 if cnt < b[5] else 1)



# ---- the decompilation's behaviour (GameEngine.cs:1934-2109 and EntityEventHandlers.cs bodies of docs/plan-e19-sm-annexe/decomp-unported.txt)
def decomp_search(w, self_i, t):
    """GetMatchingEntityBySearchType of the decompilation: the loops of the group types are `i < g_numberOfEntities` (exclusive), the raw
    branch is `<=` (inclusive). g_numberOfEntities is read as the binary's 0x80134360 (the highest slot index)."""
    N = w.n - 1
    S = w.e[self_i]
    if (t & 0x80) == 0:
        return model_search(w, self_i, t)
    ft = t & 0x7F
    if ft in (0, 1) or ft >= 12:
        return model_search(w, self_i, t)
    full = model_search(w, self_i, t)
    return [i for i in full if i < N]


DECOMP = {}
DECOMP_ACTIVE = {}


def dmodel(op):
    def deco(f):
        DECOMP[op] = f
        return f
    return deco


@dmodel(0x3C)
def d3c(w, si, b, search):
    for i in range(0, w.n - 1):
        c = w.e[i]
        if active(c) and (c['anim'] & 0x80) != 0 and (c['flags'] & 0x80) == 0 and c['platform'] == 0:
            if b[1] <= s32(c['tilex']) <= b[2] and b[3] <= s32(c['tiley']) <= b[4] and b[5] <= s32(c['tilez']) <= b[6]:
                return Exp(7, 1)
    return Exp(7, 0)


@dmodel(0x3D)
def d3d(w, si, b, search):
    for i in range(0, w.n - 1):
        c = w.e[i]
        if active(c) and b[1] <= s32(c['tilex']) <= b[2] and b[3] <= s32(c['tiley']) <= b[4] and b[5] <= s32(c['tilez']) <= b[6]:
            return Exp(7, 1)
    return Exp(7, 0)


@dmodel(0x97)
def d97(w, si, b, search):
    # PlayerManager.SpendMoney(v1): SetMoney(Money - v1), clamp [0, 9999]
    return Exp(2, None, {'money': max(0, min(9999, w.pstats['money'] - b[1]))})


@dmodel(0x9E)
def d9e(w, si, b, search):
    # the decompilation takes the row's first tile once and never advances along x
    cnt = 0
    for row in range(b[4]):
        t = w.cells.get((b[2] + row, b[1]), 0)
        for col in range(b[3]):
            if t & 2:
                cnt += 1
    return Exp(6, 0 if cnt < b[5] else 1)


SIZES = {0x60: 2, 0x61: 2, 0x3C: 7, 0x3D: 7, 0x6A: 1, 0x6B: 3, 0x6C: 3, 0x6D: 3, 0x72: 1, 0x88: 3, 0x96: 3, 0x97: 2, 0x9E: 6, 0xA9: 2,
         0xAA: 2, 0xB2: 3, 0xB3: 9, 0xB4: 2, 0xB5: 2, 0xB6: 2, 0xB7: 3}


def run_one(m, w, op, si, b, search):
    """Run the handler of `op` in the emulator and compare with the model. Returns None when equal, else a description."""
    w.write(m)
    # position memo (Parameters[2..4]) and Result sentinel
    w.memo = [random_u32(w.rng) for _ in range(3)]
    for k in range(3):
        m.ww(STATE + 0xC + 4 * k, w.memo[k])
    sentinel = 0x5A5A5A5A
    m.ww(STATE + 0x2C, sentinel)
    # map cells (0x9E): 8-byte cells, 52 per row, base + 0x604
    w.cells = {}
    if op == 0x9E:
        m.ww(MAPPTR, MAPBASE)
        for r in range(0, 30):
            for c in range(0, 30):
                v = w.rng.choice([0, 1, 2, 3, 0x102, 0x2])
                w.cells[(r, c)] = v
                m.wh(MAPBASE + 0x604 + (r * 52 + c) * 8, v)
    code = bytes(b) + bytes(10 - len(b))
    for i, x in enumerate(code):
        m.wb(CODE + i, x)
    m.ww(PCP, CODE)
    before = bytes(m.ram[(ENT0 & 0x1FFFFFFF):(ENT0 & 0x1FFFFFFF) + 10 * STRIDE])
    sbefore = bytes(m.ram[(STATE & 0x1FFFFFFF):(STATE & 0x1FFFFFFF) + 0x40])
    padbefore = bytes(m.ram[(PAD & 0x1FFFFFFF):(PAD & 0x1FFFFFFF) + 0x20])
    psbefore = bytes(m.ram[(PSTATS & 0x1FFFFFFF):(PSTATS & 0x1FFFFFFF) + 0x10])
    handler = m.rw(TABLE + 4 * op)
    v0 = m.call(handler, ent_addr(si), ent_addr(si), PCP, STATE)
    exp = (DECOMP_ACTIVE.get(op) or MODELS[op])(w, si, b, search)
    errs = []
    if v0 != exp.v0:
        errs.append(f"v0 {v0} != {exp.v0}")
    res = m.rw(STATE + 0x2C)
    if exp.result is None:
        if res != sentinel:
            errs.append(f"Result written ({res}) but the model writes none")
    elif res != exp.result:
        errs.append(f"Result {res if res != sentinel else 'unwritten'} != {exp.result}")
    # memory diffs
    after = bytes(m.ram[(ENT0 & 0x1FFFFFFF):(ENT0 & 0x1FFFFFFF) + 10 * STRIDE])
    got = {}
    for off in range(0, len(before), 4):
        if before[off:off + 4] != after[off:off + 4]:
            got[ENT0 + off] = struct.unpack_from('<I', after, off)[0]
    safter = bytes(m.ram[(STATE & 0x1FFFFFFF):(STATE & 0x1FFFFFFF) + 0x40])
    for off in range(0, 0x40, 4):
        if off == 0x2C:
            continue
        if sbefore[off:off + 4] != safter[off:off + 4]:
            got[STATE + off] = struct.unpack_from('<I', safter, off)[0]
    padafter = bytes(m.ram[(PAD & 0x1FFFFFFF):(PAD & 0x1FFFFFFF) + 0x20])
    for off in range(0, 0x20, 2):
        if padbefore[off:off + 2] != padafter[off:off + 2]:
            got[PAD + off] = struct.unpack_from('<H', padafter, off)[0]
    psafter = bytes(m.ram[(PSTATS & 0x1FFFFFFF):(PSTATS & 0x1FFFFFFF) + 0x10])
    ps_changed = {}
    for off in range(0, 0x10, 2):
        if psbefore[off:off + 2] != psafter[off:off + 2]:
            ps_changed[off] = struct.unpack_from('<H', psafter, off)[0]
    # expected
    want = {}
    for a, v in exp.writes.items():
        if a in ('mp', 'money'):
            continue
        cur = (struct.unpack_from('<I', before, a - ENT0)[0] if ENT0 <= a < ENT0 + len(before) else
               struct.unpack_from('<I', sbefore, a - STATE)[0] if STATE <= a < STATE + 0x40 else
               struct.unpack_from('<H', padbefore, a - PAD)[0] if PAD <= a < PAD + 0x20 else None)
        if cur is None or (v & (0xFFFF if PAD <= a < PAD + 0x20 else M32)) != cur:
            want[a] = v & (0xFFFF if PAD <= a < PAD + 0x20 else M32)
    if got != want:
        errs.append("memory writes differ: got %s want %s" % ({hex(k): hex(v) for k, v in got.items()}, {hex(k): hex(v) for k, v in want.items()}))
    if op == 0x97 and 'mp' in exp.writes:
        wantmp = exp.writes['mp']
        gotmp = ps_changed.get(4, w.pstats['mp'])
        if gotmp != wantmp or (set(ps_changed) - {4}):
            errs.append(f"MP got {ps_changed} want {wantmp}")
    elif op == 0x97 and 'money' in exp.writes:
        gotmoney = ps_changed.get(8, w.pstats['money'])
        if gotmoney != exp.writes['money'] or (set(ps_changed) - {8}):
            errs.append(f"money (decompilation model) got {ps_changed} want {exp.writes['money']}")
    elif ps_changed:
        errs.append(f"player stats written {ps_changed}")
    return "; ".join(errs) if errs else None


def random_u32(rng):
    return rng.choice([0, 0x10000, 0x400000, rng.randint(0, M32), rng.randint(0, 0x2000000)])


def operand_bytes(rng, op, w):
    b = [op] + [0] * 9
    n = SIZES[op]
    search_ops = (0x60, 0x61, 0x88, 0x96, 0xA9, 0xAA, 0xB2, 0xB4, 0xB5, 0xB6, 0xB7)
    def stype():
        return rng.choice([rng.randint(0, 8), 0x14, 0x7F, 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0xFF, rng.randint(0, 255)])
    for i in range(1, 10):
        b[i] = rng.randint(0, 255)
    if op in search_ops:
        b[1] = stype()
    if op == 0xB2:
        b[2] = stype()
    if op == 0x88:
        b[2] = rng.choice([0, 1, 2, 2, 3, 4, 5])
    if op in (0x3C, 0x3D):
        j = rng.randrange(w.n)
        c = w.e[j]
        for i, key in ((1, 'tilex'), (3, 'tiley'), (5, 'tilez')):
            if rng.random() < 0.75:
                lo = max(0, c[key] - rng.randint(0, 2)); hi = c[key] + rng.randint(0, 2)
            else:
                lo = rng.randint(0, 22); hi = lo + rng.randint(0, 8)
            b[i] = lo; b[i + 1] = hi
        if rng.random() < 0.1:
            b[1], b[2] = b[2], b[1]
    if op == 0xB7 and rng.random() < 0.6:
        b[2] = w.e[rng.randrange(w.n)]['tanim'] & 0xFF
    if op in (0x6B, 0x6C, 0x6D):
        b[1] = rng.choice([0, 1, 2, 0x10, 0x78, 0xFF, rng.randint(0, 255)]); b[2] = rng.choice([0, 0, 0, 1, 0x7F, 0x80, 0xFF])
    if op == 0x96:
        b[1] = rng.choice([0x80, 0x81, 0x82, 0x83, 0x8B, 0x84, 0x00, 0x01, 0x04, 0x14, 0xFF])
    if op == 0x97:
        b[1] = rng.choice([0, 1, 4, 10, 0x32, 0xFF, rng.randint(0, 255)])
    if op == 0x9E:
        b[1] = rng.randint(0, 20); b[2] = rng.randint(0, 20); b[3] = rng.randint(0, 6); b[4] = rng.randint(0, 6); b[5] = rng.randint(0, 12)
    return b[:max(n, 1)] + [0] * (10 - max(n, 1))


def main():
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 2000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    mode = sys.argv[3] if len(sys.argv) > 3 else 'binary'
    search_fn = model_search
    if mode == 'decomp':
        DECOMP_ACTIVE.update(DECOMP)
        search_fn = decomp_search
    print("mode:", mode)
    rng = random.Random(seed)
    m = make_machine()
    global STATUS_TABLE
    STATUS_TABLE = lambda idx: m.rw(0x80023D2C + 4 * idx)
    total = bad = 0
    for op in sorted(MODELS):
        nbad = 0
        first = None
        branch = {}
        for it in range(N):
            w = World(rng)
            si = rng.randrange(w.n)
            b = operand_bytes(rng, op, w)
            err = run_one(m, w, op, si, b, search_fn)
            total += 1
            e_ = (DECOMP_ACTIVE.get(op) or MODELS[op])(w, si, b, search_fn)
            branch[e_.result] = branch.get(e_.result, 0) + 1
            if err:
                nbad += 1
                if first is None:
                    first = (it, si, b[:SIZES[op]], err)
        bad += nbad
        print(f"op {op:#04x} handler {m.rw(TABLE + 4 * op):#010x}: {N} random states, {nbad} mismatches, Result histogram {dict(sorted(branch.items(), key=str))}" + (f"  FIRST: {first}" if first else ""))
    print(f"TOTAL {total} runs, {bad} mismatches")

    # DLL search service vs binary search (random worlds; real hero ref = -1)
    print("\nEntitySearchService.cs (python translation) vs the binary search model, every search type:")
    rng2 = random.Random(seed + 100)
    diffs = {}
    cnt = {}
    for it in range(N * 6):
        w = World(rng2)
        si = rng2.randrange(w.n)
        t = rng2.choice(list(range(0, 16)) + list(range(0x80, 0x90)) + [0xFF, 0x7F])
        a = model_search(w, si, t)
        b2 = dll_search(w, si, t)
        k = t if t < 0x80 else (t & 0x7F) | 0x100
        cnt[k] = cnt.get(k, 0) + 1
        if sorted(a) != sorted(b2) and not (t & 0x80 == 0x80 and (t & 0x7F) >= 12):
            diffs.setdefault(k, []).append((si, t, a, b2))
        elif a != b2 and not (t & 0x80 == 0x80 and (t & 0x7F) >= 12):
            diffs.setdefault(('order', k), []).append((si, t, a, b2))
    for k in sorted(cnt, key=str):
        d = diffs.get(k, [])
        print(f"  type {k if k < 0x100 else hex(k & 0xFF)} ({'raw id' if k < 0x100 else 'group'}): {cnt[k]} worlds, {len(d)} set differences" + (f", e.g. {d[0]}" if d else ""))
    for k in diffs:
        if isinstance(k, tuple):
            print("  order-only differences:", k, len(diffs[k]), diffs[k][0])


if __name__ == '__main__':
    main()
