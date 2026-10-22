"""Independent differential check: REAL handlers (emu.py, MIPS-I interpreter copied from the e19sm folder) vs MY OWN python models,
written from my own disassembly reading (check-e19sm/listings/op_*.txt).  World generator is mine too (all words of the entity
region, state, pad, player stats and cell grid are randomised/patterned so that any unmodelled write is caught by a whole-region diff)."""
import random, struct, sys
import emu, lib
M32 = 0xFFFFFFFF
s32 = emu.s32
ENT0, STRIDE = 0x80127D30, 0x294
NADDR = 0x80134360
RES = 0x8013D8D8
STATE = 0x8013D9E8
CODE, PCP = 0x80150000, 0x80150100
PSP, PST = 0x80176318, 0x80176400
MAPP, MAPB = 0x800DD86C, 0x80180000
PAD = 0x80126F18
LO, HI = 0x80100000, 0x80190000      # diff window (code area below, stack above)
H = {0x3C: 0x8003E558, 0x3D: 0x8003E64C, 0x60: 0x8003F3F8, 0x61: 0x8003F488, 0x6A: 0x8003F8DC, 0x6B: 0x8003F908, 0x6C: 0x8003F94C,
     0x6D: 0x8003F990, 0x72: 0x8003FA24, 0x88: 0x8004011C, 0x96: 0x800409A8, 0x97: 0x80040A2C, 0x9E: 0x80040BB4, 0xA9: 0x80041174,
     0xAA: 0x80041200, 0xB2: 0x80041628, 0xB3: 0x80041750, 0xB4: 0x800417CC, 0xB5: 0x80041830, 0xB6: 0x80041894, 0xB7: 0x800418F8}
SZ = {0x3C: 7, 0x3D: 7, 0x60: 2, 0x61: 2, 0x6A: 1, 0x6B: 3, 0x6C: 3, 0x6D: 3, 0x72: 1, 0x88: 3, 0x96: 3, 0x97: 2, 0x9E: 6, 0xA9: 2,
      0xAA: 2, 0xB2: 3, 0xB3: 9, 0xB4: 2, 0xB5: 2, 0xB6: 2, 0xB7: 3}
OFS = dict(parent=0x0C, status=0x10, hp=0x14, hpmax=0x18, platform=0x28, ref=0x48, flags=0x6C, tanim=0x88, tdir=0x8C, anim=0xB4,
           forcez=0xB8, posx=0x114, posy=0x118, posz=0x11C, tx=0x120, ty=0x124, tz=0x128, riding=0x12C, xcoll=0x130, lasta=0x26C,
           lastd=0x270)


PATTERN = bytes(((x * 7 + 3) & 0xFF) for x in range(HI - LO))


def addr(i):
    return ENT0 + i * STRIDE


def mk():
    m = emu.Emu()
    m.hooks[0x8003C914] = lambda mm: None
    m.hooks[0x8003C840] = lambda mm: None
    return m


class W:
    pass


def gen(rng, op):
    w = W()
    w.N = rng.randint(0, 6)                    # N = index of last used slot (inclusive bound in the binary)
    w.E = []
    for i in range(10):                        # slots beyond N are filled too (must be ignored)
        d = {}
        for k in OFS:
            d[k] = rng.getrandbits(32)
        d['status'] = rng.choice([0, 1, 2, 3, 4, 1, 2, 3, 2, 2])
        d['flags'] = rng.choice([0, 0x80, 0x80, 0x100, 0x4100, 0x180, 0x1, 0x80 | (rng.getrandbits(32) & 0xFFFF0000), rng.getrandbits(32)])
        d['anim'] = rng.choice([0, 0x80, 0x40, 0xC0, rng.getrandbits(32)])
        d['platform'] = rng.choice([0, 0, 0, addr(rng.randrange(10))])
        for k in ('parent', 'riding', 'xcoll'):
            d[k] = rng.choice([0, addr(rng.randrange(10)), addr(rng.randrange(10)), rng.getrandbits(32)])
        d['ref'] = rng.choice([0, 1, 2, 3, 4, 5, 0x14, 0x7F, 0xFF, M32, rng.getrandbits(32)])
        d['forcez'] = rng.choice([0, 1, M32, 5, 0x80000000, 0x7FFFFFFF, rng.getrandbits(32)])
        d['hp'] = rng.choice([0, 1, 5, 50, 0xFFFFFFFF, rng.getrandbits(32) & 0xFF])
        d['hpmax'] = rng.choice([0, 1, 50, 60, 0xFFFFFFFF, rng.getrandbits(32) & 0xFF])
        d['tanim'] = rng.choice([0, 1, 2, 3, 0x41, 0xFF, rng.getrandbits(32) & 0x1FF])
        for k in ('tx', 'ty', 'tz'):
            d[k] = rng.choice([0, 1, 2, 5, 10, 20, M32, 0x80000000, rng.getrandbits(32) & 0x3F, rng.getrandbits(32) & 0xF, rng.getrandbits(32) & 0xF])
        for k in ('posx', 'posy', 'posz'):
            d[k] = rng.choice([0, 0x10000, 0x100000, rng.getrandbits(32), rng.getrandbits(26)])
        w.E.append(d)
    w.ref = rng.randrange(0, w.N + 1) if rng.random() < 0.9 else rng.randrange(0, 10)
    w.P = [rng.choice([0, rng.getrandbits(32), rng.getrandbits(24)]) for _ in range(3)]
    w.ps = dict(hp=rng.getrandbits(16), hpmax=rng.getrandbits(16),
                mp=rng.choice([0, 1, 3, 4, 0x7FFF, 0x8000, 0xFFFF, rng.getrandbits(16)]),
                mpmax=rng.choice([0, 1, 4, 0x8000, rng.getrandbits(16) & 7]), money=rng.getrandbits(16))
    w.cells = [rng.getrandbits(16) for _ in range(20000)]
    b = [op] + [rng.getrandbits(8) for _ in range(10)]
    if op in (0x60, 0x61, 0x88, 0x96, 0xA9, 0xAA, 0xB2, 0xB4, 0xB5, 0xB6, 0xB7):
        if rng.random() < 0.2:
            b[1] = rng.choice([0, 1, 2, 3, 4, 5, 0x14, 0x7F])
        else:
            b[1] = 0x80 | rng.randrange(12)
        if op == 0xB2:
            b[2] = (0x80 | rng.randrange(12)) if rng.random() < 0.8 else rng.randrange(8)
        if op == 0xB7:
            b[2] = rng.choice([0, 1, 2, 3, 0x41, 0xFF, rng.getrandbits(8)])
        if op == 0x88:
            b[2] = rng.choice([0, 1, 2, 3, 4, 5, 9, rng.getrandbits(8)])
        if op == 0x96:
            b[1] = rng.choice([b[1], rng.getrandbits(8), 0x80, 0x81])
    if op in (0x3C, 0x3D):
        wide = rng.random() < 0.5
        for k in (1, 3, 5):
            lo = rng.randint(0, 12) if not wide else rng.randint(0, 2)
            b[k] = lo
            b[k + 1] = lo + (rng.randint(0, 12) if not wide else rng.randint(30, 200))
        if rng.random() < 0.3:
            b[2] = rng.getrandbits(8)
    if op == 0x9E:
        b[1] = rng.randrange(0, 52)
        b[2] = rng.randrange(0, 60)
        b[3] = rng.choice([0, 1, 2, 3, 5, 12])
        b[4] = rng.choice([0, 1, 2, 3, 5, 12])
        b[5] = rng.randrange(0, 20)
    if op in (0x6B, 0x6C, 0x6D):
        b[1] = rng.getrandbits(8)
        b[2] = rng.choice([0, 0, 1, 2, 0x7F, 0x80, 0xFF, rng.getrandbits(8)])
    w.b = b
    return w


def load(m, w):
    base = LO & 0x1FFFFFFF
    m.ram[base:base + (HI - LO)] = PATTERN
    m.ww(NADDR, w.N)
    for i, d in enumerate(w.E):
        for o in range(0, STRIDE, 4):
            if o not in OFS.values():
                m.ww(addr(i) + o, (0xA5000000 | (i << 16) | o))
        for k, o in OFS.items():
            m.ww(addr(i) + o, d[k])
    for j in range(0x48):
        m.ww(RES - 4 + 4 * j, 0xDEADBEEF)
    for off in range(0, 0x40, 4):
        m.ww(STATE + off, 0xC0DE0000 | off)
    m.ww(STATE + 0xC, w.P[0])
    m.ww(STATE + 0x10, w.P[1])
    m.ww(STATE + 0x14, w.P[2])
    m.ww(PSP, PST)
    p = w.ps
    for k, o in (('hp', 0), ('hpmax', 2), ('mp', 4), ('mpmax', 6), ('money', 8)):
        m.wh(PST + o, p[k])
    for off in range(0, 0x20, 4):
        m.ww(PAD + off, 0xB0B00000 | off)
    m.ww(MAPP, MAPB)
    for j, v in enumerate(w.cells):
        m.wh(MAPB + 2 * j, v)
    for i, byte in enumerate(w.b + [0] * 4):
        m.wb(CODE + i, byte)
    m.ww(PCP, CODE)


# ---------------- my models ----------------
def act(d):
    return 1 <= d['status'] <= 3


def search(w, t):
    """list of slot indices (buffer order), per my reading of 0x8003C954 (inclusive 0..N / 1..N loops)"""
    N, S = w.N, w.E[w.ref]
    if (t & 0x80) == 0:
        if not act(S):
            return []
        return [i for i in range(1, N + 1) if w.E[i]['ref'] == t]
    ft = t & 0x7F
    if ft == 0:
        return [w.ref]
    if ft == 1:
        return [0]
    if ft == 2:
        return [i for i in range(0, N + 1) if act(w.E[i])]
    if ft == 3:
        return [i for i in range(1, N + 1) if act(w.E[i])]
    if ft == 4:
        return [i for i in range(0, N + 1) if act(S) and (w.E[i]['flags'] & 0x80) and not (w.E[i]['anim'] & 0x80) and w.E[i]['platform'] == 0]
    out = []
    for i in range(1, N + 1):
        c = w.E[i]
        if not act(c):
            continue
        if ft == 5 and S['riding'] == addr(i):
            out.append(i)
        elif ft == 6 and c['riding'] == addr(w.ref):
            out.append(i)
        elif ft == 7 and S['xcoll'] == addr(i):
            out.append(i)
        elif ft == 8 and c['xcoll'] == addr(w.ref):
            out.append(i)
        elif ft == 9 and c['parent'] == addr(w.ref):
            out.append(i)
        elif ft == 10 and S['parent'] == addr(i):
            out.append(i)
        elif ft == 11 and c['platform'] != 0:
            out.append(i)
    return out


def expect(w, op):
    b = w.b
    E = w.E
    wr = {}

    def W_(a, v):
        wr[('w', a)] = v & M32

    def buf(lst):
        for j, i in enumerate(lst):
            W_(RES + 4 * j, addr(i))
    R = None
    sz = SZ[op]
    if op in (0x3C, 0x3D):
        R = 0
        for i in range(0, w.N + 1):
            c = E[i]
            if not act(c):
                continue
            if op == 0x3C and not ((c['anim'] & 0x80) and not (c['flags'] & 0x80) and c['platform'] == 0):
                continue
            if b[1] <= s32(c['tx']) <= b[2] and b[3] <= s32(c['ty']) <= b[4] and b[5] <= s32(c['tz']) <= b[6]:
                R = 1
                break
    elif op in (0x60, 0x61, 0xA9, 0xAA, 0xB7, 0xB4, 0xB5, 0xB6, 0x88, 0x96):
        res = search(w, b[1])
        buf(res)
        S = E[w.ref]
        if op == 0x60:
            R = 1 if any(E[i]['riding'] == addr(w.ref) for i in res) else 0
        if op == 0x61:
            R = 1 if any(S['riding'] == addr(i) for i in res) else 0
        if op == 0xA9:
            R = 1 if any(S['xcoll'] == addr(i) for i in res) else 0
        if op == 0xAA:
            R = 1 if any(E[i]['xcoll'] == addr(w.ref) for i in res) else 0
        if op == 0xB7:
            R = 1 if any(E[i]['tanim'] == b[2] for i in res) else 0
        if op in (0xB4, 0xB5, 0xB6):
            if not res:
                R = 0
            else:
                f = s32(E[res[0]]['forcez'])
                R = int({0xB4: f > 0, 0xB5: f < 0, 0xB6: f == 0}[op])
        if op == 0x88:
            val = lib.u32(0x80023D2C + 4 * b[2])
            for i in res:
                W_(addr(i) + 0x10, val)
        if op == 0x96:
            for i in res:
                hp = s32(s32(E[i]['hp']) + b[1])
                hm = s32(E[i]['hpmax'])
                if hm < hp:
                    hp = hm
                W_(addr(i) + 0x14, hp)
    elif op in (0x6B, 0x6C, 0x6D):
        k = {0x6B: ('posx', 0), 0x6C: ('posy', 1), 0x6D: ('posz', 2)}[op]
        d = s32(w.P[k[1]] - E[w.ref][k[0]])
        if d < 0:
            d = s32(-d)
        thr = s32(((b[1] | (b[2] << 8)) << 16) & M32)
        R = 0 if d < thr else 1
    elif op == 0x6A:
        W_(STATE + 0xC, E[w.ref]['posx'])
        W_(STATE + 0x10, E[w.ref]['posy'])
        W_(STATE + 0x14, E[w.ref]['posz'])
    elif op == 0x72:
        W_(addr(w.ref) + 0x26C, E[w.ref]['tanim'])
        W_(addr(w.ref) + 0x270, E[w.ref]['tdir'])
    elif op == 0x97:
        mp = w.ps['mp'] if w.ps['mp'] < 0x8000 else w.ps['mp'] - 0x10000
        v = mp + b[1]
        mpmax = w.ps['mpmax'] if w.ps['mpmax'] < 0x8000 else w.ps['mpmax'] - 0x10000
        if mpmax < v:
            new = w.ps['mpmax']
        elif v >= 0:
            new = v & 0xFFFF
        else:
            new = 0
        wr[('h', PST + 4)] = new
    elif op == 0x9E:
        x, y, ww, hh, mn = b[1], b[2], b[3], b[4], b[5]
        cnt = 0
        for r in range(hh):
            for c in range(ww):
                a = MAPB + 0x604 + ((y + r) * 52 + x + c) * 8
                j = (a - MAPB) // 2
                if w.cells[j] & 2:
                    cnt += 1
        R = 0 if cnt < mn else 1
    elif op == 0xB2:
        r1 = search(w, b[1])
        buf(r1)
        if not r1:
            R = 0
        else:
            copy = [addr(i) for i in r1]
            r2 = search(w, b[2])
            buf(r2)
            R = 1 if any(addr(i) in copy for i in r2) else 0
    elif op == 0xB3:
        wr[('h', PAD + 0x10)] = b[1] | (b[2] << 8)
        wr[('h', PAD + 0x12)] = b[3] | (b[4] << 8)
        wr[('h', PAD + 0x14)] = b[5] | (b[6] << 8)
        wr[('h', PAD + 0x16)] = b[7] | (b[8] << 8)
    if R is not None:
        W_(STATE + 0x2C, R)
    return sz, wr


def run(op, seed, n):
    bad = 0
    hist = {}
    for t in range(n):
        rng = random.Random((seed << 20) ^ (op << 12) ^ t)
        w = gen(rng, op)
        m = mk()
        load(m, w)
        lo = LO & 0x1FFFFFFF
        before = bytes(m.ram[lo:lo + (HI - LO)])
        v0 = m.call(H[op], addr(w.ref), addr(0), PCP, STATE)
        after = bytes(m.ram[lo:lo + (HI - LO)])
        sz, wr = expect(w, op)
        exp = bytearray(before)
        for (kind, a), v in wr.items():
            o = (a & 0x1FFFFFFF) - lo
            if kind == 'w':
                struct.pack_into('<I', exp, o, v & M32)
            else:
                struct.pack_into('<H', exp, o, v & 0xFFFF)
        okv = (v0 & M32) == sz
        if after != bytes(exp) or not okv:
            bad += 1
            if bad <= 3:
                diffs = [(hex(LO + i), before[i], after[i], exp[i]) for i in range(len(after)) if after[i] != exp[i]][:12]
                print('MISMATCH op %02X t=%d v0=%d exp %d b=%s ref=%d N=%d diffs=%s' % (op, t, v0, sz, w.b[:8], w.ref, w.N, diffs))
        r = m.rw(STATE + 0x2C)
        key = r if r < 5 else 'sentinel'
        hist[key] = hist.get(key, 0) + 1
    return bad, hist


if __name__ == '__main__':
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 7
    for op in sorted(H):
        bad, hist = run(op, seed, n)
        print('op %02X: %d runs, %d mismatches, Result word hist %s' % (op, n, bad, dict(sorted(hist.items(), key=lambda x: str(x[0])))))
