"""Own kill matrix: the tests of the preparation's draft (PositionMemoOpcodesTests.cs.txt), re-expressed from its text on my own interpreter model,
run against: the correct port, the skip by size ('today'), and the 17 mutants M1..M17 as DEFINED IN THE DRAFT'S MUTATION SPEC (one edit each).
A test is red when an assertion fails or an exception is raised (OverflowException for Math.Abs(int.MinValue))."""
import sys, csv, collections, re
sys.path.insert(0, 'tools')
from mycorpus import *
from my_rb import parse_expected

M32 = 0xFFFFFFFF
def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x

class Boom(Exception): pass

class Impl:
    def __init__(s, **kw):
        s.port = True            # False = skip by size (today)
        s.abs_mode = 'neg'       # neg | Math.Abs | none | long
        s.form = 'sig32'         # sig32 | pixels | unsigned | le
        s.invert = set()         # ops inverted
        s.forget_z = False
        s.save_result = None     # None | 0 | 1
        s.read_owner = False
        s.d_cell = 4
        s.ret6a = 1; s.ret6b = 3
        s.break_clears = False
        s.init_clears = False
        s.wait_cell = 2
        s.label6b = 'If entity x moved >= threshold from the saved position'
        s.__dict__.update(kw)

class Ent:
    def __init__(s): s.x = s.y = s.z = 0; s.dir = 0; s.anim = 0

class Mont:
    def __init__(s, impl, codes, result=7, ci=0, other=False, p0=None):
        s.i = impl; s.codes = codes; s.result = result; s.ci = ci
        s.P = [0] * 10; s.P[0] = ci if p0 is None else p0
        s._34 = 0
        s.logic = Ent(); s.owner = s.logic
        if other:
            s.owner = Ent(); s.owner.x, s.owner.y, s.owner.z = 0x0DEC0114, 0x0DEC0118, 0x0DEC011C
        s.trace = []
    def place(s, x, y, z): s.logic.x, s.logic.y, s.logic.z = x, y, z
    def tick(s): s.trace = run_call(s)

def moved(i, memo, pos, lo, hi):
    memo &= M32; pos &= M32
    if i.abs_mode == 'long':
        d = abs(s32(memo) - s32(pos))   # 64-bit, no wrap
        thr = s32(((lo | (hi << 8)) << 16))
        return not (d < thr)
    d = s32(memo - pos)
    if i.abs_mode == 'Math.Abs':
        if d == -(1 << 31): raise Boom('OverflowException')
        d = abs(d)
    elif i.abs_mode == 'neg':
        if d < 0: d = s32(-d)
    # none: keep d
    thr = s32(((lo | (hi << 8)) << 16))
    if i.form == 'pixels':
        return not ((d >> 16) < (lo | (hi << 8)))
    if i.form == 'unsigned':
        return not ((d & M32) < (thr & M32))
    if i.form == 'le':
        return not (d <= thr)
    return not (d < thr)

def run_call(m):
    i = m.i; C = m.codes; tr = []; it = 0
    ent = m.logic
    ownr = m.owner
    def byte(k): return C[k] if 0 <= k < len(C) else 0
    while True:
        it += 1
        if it > 400: raise Boom('loop guard')
        ci = m.ci
        v = [byte(ci + k) for k in range(10)] if ci < len(C) else [0xFF] + [0] * 9
        cmd = v[0]
        if cmd == 0xFF: tr.append((ci, cmd)); return tr
        if cmd == 0x00:
            m.P[1] = 0; m.ci += 1; tr.append((ci, 0))
            if i.break_clears: m.P[2] = m.P[3] = m.P[4] = 0
            return tr
        pos_src = ownr if i.read_owner else ent
        res = None
        if cmd == 0x02: res = s16(v[1] | (v[2] << 8))
        elif cmd == 0x03: res = s16(v[1] | (v[2] << 8)) if m.result != 0 else 3
        elif cmd == 0x04: res = s16(v[1] | (v[2] << 8)) if m.result == 0 else 3
        elif cmd == 0x08: ent.dir = (ent.dir + v[1]) & 0x1F; res = 2
        elif cmd == 0x0A: ent.dir = (ent.dir + 0x10) & 0x1F; res = 1
        elif cmd == 0x1A: res = 2
        elif cmd == 0x1B: res = 3
        elif cmd in (0x2A, 0x2B, 0x16, 0x17, 0x6F):
            if cmd == 0x6F: m.result = 0
            res = 1
        elif cmd == 0x30: res = 5
        elif cmd == 0x49: res = m.P[0] - m.ci
        elif cmd == 0x6E: m.result = 0; res = 1
        elif cmd == 0x78: m._34 = m.ci + 3; res = s16(v[1] | (v[2] << 8))
        elif cmd == 0x7D: res = m._34 - m.ci
        elif cmd == 0x7F: res = (m._34 - m.ci) if m.result == 0 else 1
        elif cmd == 0x89: res = 9
        elif cmd == 0x37:   # real Wait with optional mutated cell
            c = i.wait_cell
            if m.P[1] != m.ci:
                m.P[1] = m.ci; m.P[c] = 0; res = 0
            else:
                counter = m.P[c]; m.P[c] = counter + 1
                res = 2 if counter >= v[1] else 0
        elif cmd in (0x6A, 0x6B, 0x6C, 0x6D):
            if not i.port:
                res = {0x6A: 1, 0x6B: 3, 0x6C: 3, 0x6D: 3}[cmd]   # skip by size, trace UnknownSkipped
            elif cmd == 0x6A:
                m.P[2] = pos_src.x
                m.P[3] = pos_src.y
                if not i.forget_z: m.P[4] = pos_src.z
                if i.save_result is not None: m.result = i.save_result
                res = i.ret6a
            else:
                k = cmd - 0x6B
                cell = i.d_cell if cmd == 0x6D else 2 + k
                pos = (pos_src.x, pos_src.y, pos_src.z)[k]
                r = 1 if moved(i, m.P[cell], pos, v[1], v[2]) else 0
                if cmd in i.invert: r = 1 - r
                m.result = r
                res = i.ret6b if cmd == 0x6B else 3
        else: raise Exception('opcode not modelled %02X' % cmd)
        tr.append((ci, cmd))
        if res == 0: return tr
        m.P[1] = 0
        m.ci += res

# ---------------------------------------------------------------- tests
TESTS = []
def test(name):
    def deco(f): TESTS.append((name, f)); return f
    return deco
def eq(a, b, what=''):
    if a != b: raise AssertionError('%s: %r != %r' % (what, a, b))

# H1
@test('H1')
def _(i):
    m = Mont(i, [0x6A, 0xFF]); m.place(0x180000, 0x280000, 0x300000)
    m.P[2], m.P[3], m.P[4] = 0x11111111, 0x22222222, 0x33333333
    m.tick()
    eq((m.P[2], m.P[3], m.P[4]), (0x180000, 0x280000, 0x300000)); eq(m.result, 7); eq(m.ci, 1)

H234 = [(0x6B, 0x00100000, 0x00880000, 0x78, 0, 1), (0x6B, 0x00100000, 0x0087FFFF, 0x78, 0, 0), (0x6B, 0x00100000, 0x00880001, 0x78, 0, 1),
        (0x6B, 0x00880000, 0x00100000, 0x78, 0, 1), (0x6B, 0x00880000, 0x00100001, 0x78, 0, 0), (0x6B, 0x00880000, 0x0010FFFF, 0x78, 0, 0),
        (0x6C, 0x02000000, 0x02100000, 0x10, 0, 1), (0x6C, 0x02000000, 0x020FFFFF, 0x10, 0, 0),
        (0x6D, 0x00600000, 0x00610000, 0x01, 0, 1), (0x6D, 0x00600000, 0x0060FFFF, 0x01, 0, 0),
        (0x6B, 0x00500000, 0x00500000, 0x00, 0x80, 1), (0x6B, 0x80000000, 0x00000000, 0xFF, 0xFF, 0), (0x6B, 0x80000000, 0x00000000, 0x00, 0x80, 1),
        (0x6B, 0x7FFFFFFF, 0xFFFFFFFF, 0x78, 0, 0)]
NAMES234 = ['H2X-120', 'H2X-119.99', 'H2X+1', 'H2X<-120', 'H2X<-119.99a', 'H2X<-119.99b', 'H2Y-16', 'H2Y-15.99', 'H2Z-1', 'H2Z-0.99', 'H3a', 'H3b', 'H3c', 'H4']
for nm, (op, memo, pos, lo, hi, exp) in zip(NAMES234, H234):
    def mk(op=op, memo=memo, pos=pos, lo=lo, hi=hi, exp=exp):
        def f(i):
            m = Mont(i, [op, lo, hi, 0xFF]); ax = op - 0x6B
            m.P[2 + ax] = s32(memo)
            m.place(*[s32(pos) if a == ax else 0 for a in range(3)])
            m.tick(); eq(m.result, exp, 'Result'); eq(m.ci, 3)
        return f
    TESTS.append((nm, mk()))

for pz, exp in ((0x00FFFFFF, 0), (0x01000000, 1), (0x01000001, 1)):
    def mk(pz=pz, exp=exp):
        def f(i):
            m = Mont(i, [0x6D, 0, 1, 0xFF]); m.place(0, 0, pz); m.tick(); eq(m.result, exp)
        return f
    TESTS.append(('H5-%08X' % pz, mk()))

@test('H6')
def _(i):
    m = Mont(i, [0x6A, 0, 0x6B, 0x10, 0, 0xFF], other=True); m.place(0x500000, 0x600000, 0x700000); m.tick()
    eq((m.P[2], m.P[3], m.P[4]), (0x500000, 0x600000, 0x700000))
    m.place(0x600000, 0x600000, 0x700000); m.tick(); eq(m.result, 1)

for mv, exp, mx in ((120, 1, 0x400000), (119, 0, 0x400000)):
    def mk(mv=mv, exp=exp, mx=mx):
        def f(i):
            m = Mont(i, [0x6A, 0, 0x6B, 0x78, 0, 0xFF]); m.place(64 << 16, 0, 0); m.tick(); eq(m.ci, 2)
            m.place((64 + mv) << 16, 0, 0); m.tick(); eq(m.result, exp); eq(m.P[2], mx); eq(m.ci, 5)
        return f
    TESTS.append(('H7-%d' % mv, mk()))

@test('H8')
def _(i):
    m = Mont(i, [0x6A, 0, 0x6B, 0x78, 0, 0xFF]); m.place(64 << 16, 5 << 16, 6 << 16); m.tick()
    # InitializeEventData: only P0 (and the cursor/codes); mutant M15 also clears the memo
    m.P[0] = m.ci
    if i.init_clears: m.P[2] = m.P[3] = m.P[4] = 0
    eq((m.P[2], m.P[3], m.P[4]), (0x400000, 0x50000, 0x60000))

for rop, exp in ((0x6B, 1), (0x6C, 0), (0x6D, 0)):
    def mk(rop=rop, exp=exp):
        def f(i):
            m = Mont(i, [0x6A, 0x37, 0x03, rop, 0x10, 0x00, 0xFF]); m.place(0x500000, 0x500000, 0x500000)
            for _t in range(5): m.tick()
            eq(m.result, exp); eq(m.P[2], 4); eq(m.ci, 6)
        return f
    TESTS.append(('H9-%02X' % rop, mk()))

@test('H10')
def _(i):
    eq(i.label6b, 'If entity x moved >= threshold from the saved position')

# RB from the preparation's expected 'port' values (they equal my model with port=True: verified separately)
EXP = parse_expected('C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm2/proposed/RealBytesExpected.txt')
for nm, sc in EXP.items():
    def mk(nm=nm, sc=sc):
        def f(i):
            ev, tm, b = load(sc['map'])
            m = Mont(i, ev['Codes'], result=sc['r0'], ci=sc['start'], p0=table(ev, 'C')[sc['prog']])
            last = len(sc['ticks'])
            for k, t in enumerate(sc['ticks']):
                if nm == 'RB-56' and k == 2: pass
                m.place(*t['pos']); m.tick()
                e = t['port']
                # the draft asserts the executed list, Result, CodeIndex, memo and the direction (RB-55 template); assert all
                eq(m.trace, [(a, b) for a, b in e['ex']], 'executed t%d' % (k + 1))
                eq((m.result, m.ci, (m.P[2], m.P[3], m.P[4])), (e['res'], e['ci'], e['P']), 't%d state' % (k + 1))
                eq(m.logic.dir, e['dir'], 'dir t%d' % (k + 1))
        return f
    TESTS.append((nm, mk()))

MUT = collections.OrderedDict([
    ('today', dict(port=False, label6b='If entity x < threshold')),
    ('M1 Math.Abs', dict(abs_mode='Math.Abs')),
    ('M2 pixel form', dict(form='pixels')),
    ('M3 unsigned', dict(form='unsigned')),
    ('M4 <=', dict(form='le')),
    ('M5 invert 6B only (the spec: the 0x6B line)', dict(invert={0x6B})),
    ('M5b invert 6B+6C+6D (the matrix)', dict(invert={0x6B, 0x6C, 0x6D})),
    ('M6 long abs', dict(abs_mode='long')),
    ('M7 no abs', dict(abs_mode='none')),
    ('M8 6A forgets Z', dict(forget_z=True)),
    ('M9 6A writes Result:=1', dict(save_result=1)),
    ('M10 owner position', dict(read_owner=True)),
    ('M11 6D reads P3', dict(d_cell=3)),
    ('M12 6B returns 1', dict(ret6b=1)),
    ('M13 6A returns 2', dict(ret6a=2)),
    ('M14 Break clears', dict(break_clears=True)),
    ('M15 init clears', dict(init_clears=True)),
    ('M16 wait cell P5', dict(wait_cell=5)),
    ('M17 old label', dict(label6b='If entity x < threshold')),
])

if __name__ == '__main__':
    correct = Impl()
    bad = []
    for name, f in TESTS:
        try: f(correct)
        except Exception as e: bad.append((name, repr(e)))
    print('tests', len(TESTS), 'red on the correct port:', bad)
    out = {}
    for mname, kw in MUT.items():
        red = []
        for name, f in TESTS:
            try: f(Impl(**kw))
            except Exception: red.append(name)
        out[mname] = red
        print('%-46s red %2d: %s' % (mname, len(red), ' '.join(red)))
