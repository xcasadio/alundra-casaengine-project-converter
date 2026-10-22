"""Critic's own kill matrix: the tests as DEFINED in proposed/PositionMemoOpcodesTests.cs.txt (montage included: logicIsOther only in the V facts and H6),
run on my own model of the DLL (dllmodel-like, with the 6A-6D mutations of the proposal's spec written as the C# text reads, int32 wrap by hand).
Output: per mutant, the tests that go red. RB tests assert what the draft asserts (RB-55: as written; the others: the full port tuple of RealBytesExpected, upper bound)."""
import re, sys, csv, os, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dllmodel import load_codes, SIZES, sext16, s32, M32, Ent, St

P = sys.argv[1]   # prep folder


# ---------- mutable model ----------
class M:
    def __init__(self, mut=None):
        self.mut = mut


def cs_int_shift_right(x, n):   # arithmetic >> on int32
    return s32(x) >> n


def moved_at_least(mut, memo, pos, v):
    """C# helper as proposed, mutated per the spec text (M1..M7); returns bool or raises OverflowError (Math.Abs on MinValue)."""
    memo = s32(memo)
    pos = s32(pos)
    if mut == 'M6':
        dist = abs(memo - pos)       # long: Math.Abs((long)memo - position), later 'if (distance < 0)' never true
        thr = s32(((v[1] | (v[2] << 8)) << 16))
        return not (dist < thr)
    dist = s32(memo - pos)
    if mut == 'M7':
        pass                           # abs removed
    elif dist < 0:
        if mut == 'M1':
            if dist == -(1 << 31):
                raise OverflowError
            dist = abs(dist)
        else:
            dist = s32(-dist)
    thr = s32(((v[1] | (v[2] << 8)) << 16))
    if mut == 'M2':
        return not ((dist >> 16) < (v[1] | (v[2] << 8)))
    if mut == 'M3':
        return not ((dist & M32) < (thr & M32))
    if mut == 'M4':
        return not (dist <= thr)
    return not (dist < thr)


class Runner:
    def __init__(self, mut=None):
        self.mut = mut

    def fill(self, st):
        c = st.Codes
        if c is None or st.CodeIndex >= len(c):
            return [0xFF] + [0] * 9
        v = [0] * 10
        for i in range(min(10, len(c) - st.CodeIndex)):
            v[i] = c[st.CodeIndex + i]
        return v

    def dispatch(self, cmd, logic, owner, v, st):
        mut = self.mut
        if cmd == 0x01:
            return 1
        if cmd == 0x02:
            return sext16((v[2] << 8) | v[1])
        if cmd == 0x03:
            return sext16((v[2] << 8) | v[1]) if st.Result != 0 else 3
        if cmd == 0x04:
            return sext16((v[2] << 8) | v[1]) if st.Result == 0 else 3
        if cmd == 0x08:
            logic.TargetDirection = (logic.TargetDirection + v[1]) & 0x1f
            return 2
        if cmd == 0x0A:
            logic.TargetDirection = (logic.TargetDirection + 0x10) & 0x1f
            return 1
        if cmd == 0x1A:
            return 2
        if cmd == 0x1B:
            return 3
        if cmd == 0x2A:
            return 1
        if cmd in (0x30, 0x31):
            isset = False
            want = (cmd == 0x30)
            return sext16((v[4] << 8) | v[3]) if isset == want else 5
        if cmd == 0x37:
            wi = 5 if mut == 'M16' else 2
            if st.P[1] != st.CodeIndex:
                st.P[1] = st.CodeIndex
                st.P[wi] = 0
                return 0
            c = st.P[wi]
            st.P[wi] = c + 1
            return 2 if c >= v[1] else 0
        if cmd == 0x6E:
            st.Result = logic.ForceAdjusted
            return 1
        if cmd == 0x6F:
            st.Result = logic.Collided
            return 1
        if cmd == 0x78:
            st._34 = st.CodeIndex + 3
            return sext16((v[2] << 8) | v[1])
        if cmd == 0x7D:
            return st._34 - st.CodeIndex
        if cmd == 0x7F:
            return st._34 - st.CodeIndex if st.Result == 0 else 1
        if cmd == 0x89:
            return 9
        if cmd == 0x6A:
            src = owner if mut == 'M10' else logic
            st.P[2] = src.PosX
            if mut != 'M8y':
                st.P[3] = src.PosY
            if mut != 'M8':
                st.P[4] = src.PosZ
            if mut == 'M9':
                st.Result = 1
            return 2 if mut == 'M13' else 1
        if cmd in (0x6B, 0x6C, 0x6D):
            k = cmd - 0x6B
            src = owner if mut == 'M10' else logic
            pos = (src.PosX, src.PosY, src.PosZ)[k]
            mi = 2 + k
            if mut == 'M11' and cmd == 0x6D:
                mi = 3
            res = moved_at_least(mut, st.P[mi], pos, v)
            if mut == 'M5' and cmd == 0x6B:
                res = not res
            st.Result = 1 if res else 0
            return 1 if (mut == 'M12' and cmd == 0x6B) else 3
        raise Exception("model: opcode %x not modelled" % cmd)

    def run(self, owner, st, logic=None):
        logic = logic or owner
        ex = []
        it = 0
        while True:
            it += 1
            if it > 1024:
                ex.append(('LOOP',))
                return ex
            ci = st.CodeIndex
            v = self.fill(st)
            cmd = v[0]
            if cmd == 0xFF:
                ex.append((ci, 0xFF))
                return ex
            if cmd == 0:
                st.P[1] = 0
                if self.mut == 'M14':
                    st.P[2] = st.P[3] = st.P[4] = 0
                st.CodeIndex += 1
                ex.append((ci, 0))
                return ex
            if st.Codes is not None and (st.CodeIndex < 0 or st.CodeIndex >= len(st.Codes)):
                raise IndexError
            r = self.dispatch(cmd, logic, owner, v, st)
            ex.append((ci, cmd))
            if r == 0:
                return ex
            st.P[1] = 0
            st.CodeIndex += r


def init_event_data(mut, st):
    st.P[0] = st.CodeIndex
    if mut == 'M15':
        st.P[2] = st.P[3] = st.P[4] = 0


DECOY = (0x0DEC0114, 0x0DEC0118, 0x0DEC011C)


def montage(codes, result=7, ci=0, logic_other=False):
    owner = Ent()
    st = St(list(codes), ci, result)
    if logic_other:
        logic = Ent()
        owner.PosX, owner.PosY, owner.PosZ = DECOY
        return owner, logic, st
    return owner, owner, st


def place(logic, x, y, z):
    logic.PosX, logic.PosY, logic.PosZ = s32(x), s32(y), s32(z)


# ---------- the tests ----------
TESTS = collections.OrderedDict()


def test(name):
    def deco(f):
        TESTS[name] = f
        return f
    return deco


def run_tick(R, owner, logic, st):
    return R.run(owner, st, logic if logic is not owner else None)


# V facts: rows of the TSV, all with logicIsOther
ROWS = list(csv.DictReader(open(P + '/vectors/vectors-sm2.tsv', encoding='utf-8'), delimiter='\t'))


def make_v(opcode):
    def f(R):
        bad = 0
        for r in ROWS:
            if int(r['op'], 16) != opcode:
                continue
            instr = [int(x, 16) for x in r['instr'].split()] + [0xFF]
            owner, logic, st = montage(instr, s32(int(r['result_in'], 16)), 0, True)
            place(logic, int(r['pos_x'], 16), int(r['pos_y'], 16), int(r['pos_z'], 16))
            st.P[2], st.P[3], st.P[4] = s32(int(r['memo_p2'], 16)), s32(int(r['memo_p3'], 16)), s32(int(r['memo_p4'], 16))
            for i in (0, 5, 6, 7, 8, 9):
                st.P[i] = 0x5A5A0000 + i
            st._30, st._34 = 0x5A5A0030, 0x5A5A0034
            try:
                ex = R.run(owner, st, logic)
            except (OverflowError, IndexError):
                bad += 1
                continue
            ok = (st.Result == s32(int(r['result_out'], 16)) and (st.P[2], st.P[3], st.P[4]) == tuple(s32(int(r[k], 16)) for k in ('memo_p2_out', 'memo_p3_out', 'memo_p4_out'))
                  and st.CodeIndex == int(r['v0']) and st.P[1] == 0 and all(st.P[i] == 0x5A5A0000 + i for i in (0, 5, 6, 7, 8, 9)))
            # first trace record: Implemented / size = v0 -> encoded by the executed list (no UnknownSkipped in the model)
            if not ok:
                bad += 1
        return bad == 0
    return f


for nm, op in (('V6A', 0x6A), ('V6B', 0x6B), ('V6C', 0x6C), ('V6D', 0x6D)):
    TESTS[nm] = make_v(op)


def tick_expect_result(R, codes, setup, expected, ci_expected=3, result0=7, extra=None):
    owner, logic, st = montage(codes, result0, 0, False)
    setup(logic, st)
    run_tick(R, owner, logic, st)
    return st.Result == expected and st.CodeIndex == ci_expected


@test('H1')
def h1(R):
    owner, logic, st = montage([0x6A, 0xFF])
    place(logic, 0x180000, 0x280000, 0x300000)
    st.P[2], st.P[3], st.P[4] = 0x11111111, 0x22222222, 0x33333333
    R.run(owner, st)
    return (st.P[2], st.P[3], st.P[4]) == (0x180000, 0x280000, 0x300000) and st.Result == 7 and st.CodeIndex == 1


H23 = [
    ('H2X-a', 0x6B, 0x100000, 0x880000, 0x78, 0, 1), ('H2X-b', 0x6B, 0x100000, 0x87FFFF, 0x78, 0, 0), ('H2X-c', 0x6B, 0x100000, 0x880001, 0x78, 0, 1),
    ('H2Xm-a', 0x6B, 0x880000, 0x100000, 0x78, 0, 1), ('H2Xm-b', 0x6B, 0x880000, 0x100001, 0x78, 0, 0), ('H2Xm-c', 0x6B, 0x880000, 0x10FFFF, 0x78, 0, 0),
    ('H2Y-a', 0x6C, 0x2000000, 0x2100000, 0x10, 0, 1), ('H2Y-b', 0x6C, 0x2000000, 0x20FFFFF, 0x10, 0, 0),
    ('H2Z-a', 0x6D, 0x600000, 0x610000, 0x01, 0, 1), ('H2Z-b', 0x6D, 0x600000, 0x60FFFF, 0x01, 0, 0),
    ('H3a', 0x6B, 0x500000, 0x500000, 0x00, 0x80, 1), ('H3b', 0x6B, s32(0x80000000), 0, 0xFF, 0xFF, 0), ('H3c', 0x6B, s32(0x80000000), 0, 0x00, 0x80, 1),
    ('H4', 0x6B, 0x7FFFFFFF, s32(0xFFFFFFFF), 0x78, 0, 0),
]


def make_h23(op, memo, pos, lo, hi, exp):
    def f(R):
        owner, logic, st = montage([op, lo, hi, 0xFF])
        axis = op - 0x6B
        st.P[2 + axis] = s32(memo)
        place(logic, pos if axis == 0 else 0, pos if axis == 1 else 0, pos if axis == 2 else 0)
        try:
            R.run(owner, st)
        except (OverflowError, IndexError):
            return False
        return st.Result == exp and st.CodeIndex == 3
    return f


for (nm, op, memo, pos, lo, hi, exp) in H23:
    TESTS[nm] = make_h23(op, memo, pos, lo, hi, exp)


def make_h5(posz, exp):
    def f(R):
        owner, logic, st = montage([0x6D, 0x00, 0x01, 0xFF])
        place(logic, 0, 0, posz)
        try:
            R.run(owner, st)
        except (OverflowError, IndexError):
            return False
        return st.Result == exp
    return f


for posz, exp in ((0x00FFFFFF, 0), (0x01000000, 1), (0x01000001, 1)):
    TESTS['H5-%08X' % posz] = make_h5(posz, exp)


@test('H6')
def h6(R):
    owner, logic, st = montage([0x6A, 0x00, 0x6B, 0x10, 0x00, 0xFF], 7, 0, True)
    place(logic, 0x500000, 0x600000, 0x700000)
    R.run(owner, st, logic)
    if (st.P[2], st.P[3], st.P[4]) != (0x500000, 0x600000, 0x700000):
        return False
    place(logic, 0x600000, 0x600000, 0x700000)
    R.run(owner, st, logic)
    return st.Result == 1


def make_h7(moved, exp):
    def f(R):
        owner, logic, st = montage([0x6A, 0x00, 0x6B, 0x78, 0x00, 0xFF])
        place(logic, 64 << 16, 0, 0)
        R.run(owner, st)
        if st.CodeIndex != 2:
            return False
        place(logic, (64 + moved) << 16, 0, 0)
        R.run(owner, st)
        return st.Result == exp and st.P[2] == 0x400000 and st.CodeIndex == 5
    return f


TESTS['H7a'] = make_h7(120, 1)
TESTS['H7b'] = make_h7(119, 0)


@test('H8')
def h8(R):
    owner, logic, st = montage([0x6A, 0x00, 0x6B, 0x78, 0x00, 0xFF])
    place(logic, 64 << 16, 5 << 16, 6 << 16)
    R.run(owner, st)
    init_event_data(R.mut, st)
    return (st.P[2], st.P[3], st.P[4]) == (0x400000, 0x50000, 0x60000)


def make_h9(op, exp):
    def f(R):
        owner, logic, st = montage([0x6A, 0x37, 0x03, op, 0x10, 0x00, 0xFF])
        place(logic, 0x500000, 0x500000, 0x500000)
        for _ in range(5):
            try:
                R.run(owner, st)
            except (OverflowError, IndexError):
                return False
        return st.Result == exp and st.P[2] == 4 and st.CodeIndex == 6
    return f


TESTS['H9X'] = make_h9(0x6B, 1)
TESTS['H9Y'] = make_h9(0x6C, 0)
TESTS['H9Z'] = make_h9(0x6D, 0)

# RB tests: parse RealBytesExpected
txt = open(P + '/proposed/RealBytesExpected.txt', encoding='utf-8').read().split('\n')
scen = []
cur = None
for ln in txt:
    m = re.match(r'=== (\S+)\s+(\d+) C\[(\d+)\]', ln)
    if m:
        cur = {'name': m.group(1), 'map': int(m.group(2)), 'c': int(m.group(3)), 'ticks': []}
        scen.append(cur)
        continue
    m = re.match(r'\s+map (\d+), program C\[(\d+)\], entry (\d+), first call at CodeIndex (\d+), Result0 (\d+)', ln)
    if m:
        cur['start'] = int(m.group(4))
        cur['r0'] = int(m.group(5))
        continue
    m = re.match(r'\s+tick (\d+)\s+pos=\((0x[0-9A-Fa-f]+), (0x[0-9A-Fa-f]+), (0x[0-9A-Fa-f]+)\)', ln)
    if m:
        cur['ticks'].append({'pos': tuple(int(m.group(i), 16) for i in (2, 3, 4))})
        continue
    m = re.match(r'\s+port\s*: executed (.*?)\s+Result=(\d+) CodeIndex=(\d+) P2\.\.P4=\((0x[0-9A-Fa-f]+), (0x[0-9A-Fa-f]+), (0x[0-9A-Fa-f]+)\) TargetDirection=(\d+)', ln)
    if m:
        ex = [(int(t.split(':')[0]), int(t.split(':')[1], 16)) for t in m.group(1).split()]
        cur['ticks'][-1]['port'] = dict(ex=ex, res=int(m.group(2)), ci=int(m.group(3)), p=tuple(int(m.group(i), 16) for i in (4, 5, 6)), td=int(m.group(7)))


def make_rb(sc):
    def f(R):
        d = load_codes(sc['map'])
        owner, logic, st = montage(d['Codes'], sc['r0'], sc['start'], False)
        for tk in sc['ticks']:
            place(logic, *tk['pos'])
            try:
                ex = R.run(owner, st)
            except (OverflowError, IndexError):
                return False
            e = tk['port']
            got = dict(ex=ex, res=st.Result, ci=st.CodeIndex, p=tuple(x & M32 for x in st.P[2:5]), td=logic.TargetDirection)
            if got != e:
                return False
        return True
    return f


for sc in scen:
    TESTS[sc['name']] = make_rb(sc)

MUTS = ['M1', 'M2', 'M3', 'M4', 'M5', 'M6', 'M7', 'M8', 'M9', 'M10', 'M11', 'M12', 'M13', 'M14', 'M15', 'M16']


class R5(Runner):
    pass


def run_all(mut):
    R = Runner(mut)
    red = []
    for name, f in TESTS.items():
        try:
            ok = f(R)
        except Exception as e:
            ok = False
        if not ok:
            red.append(name)
    return red


if __name__ == '__main__':
    base = run_all(None)
    print('baseline red (must be empty):', base)
    # M5 (inverted): implement through mut flag in moved_at_least? do it here
    for mut in MUTS:
        if False:
            pass
        else:
            red = run_all(mut)
        print(mut, len(red), red)
