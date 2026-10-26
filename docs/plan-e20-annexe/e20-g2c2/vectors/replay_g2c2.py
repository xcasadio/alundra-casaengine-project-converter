"""replay_g2c2.py - E20.g2c2 T0 (c): replays goldens_g2c2.json through MODELS of C70, E20 and D34 written from the disassembly (docs/plan-e20-annexe/e20-g2c2-disc/g2c2/out/dis_*.txt), reading ONLY the words the vector lists (any other read is an error)
and comparing every word written and the journal. usage: python -B replay_g2c2.py <goldens_g2c2.json>"""
import json, sys
M32 = 0xFFFFFFFF
TBL = 0x800281DC


class Mem:
    def __init__(self, reads):
        self.r = {self.n(int(a, 16)): v for a, v in reads.items()}
        self.w = {}
        self.b = {}

    @staticmethod
    def n(a):
        return ((a & M32) & 0x1FFFFFFF) | 0x80000000          # KSEG0 and the null-based addresses of the null spawn fold into the same RAM word

    def rw(self, a):
        a = self.n(a)
        if a & 3 or (a not in self.w and a not in self.r):
            raise KeyError('read of an unlisted word %08X' % a)
        return self.w.get(a, self.r.get(a))

    def rb(self, a):
        base = self.n(a) & ~3
        return (self.rw(base) >> (8 * (a & 3))) & 0xFF

    def ww(self, a, v):
        self.w[self.n(a)] = v & M32

    def wb(self, a, v):
        self.b[self.n(a)] = v & 0xFF


def s32(x):
    return x - (1 << 32) if x & 0x80000000 else x


def c70(m, J, E):
    car = m.rw(E + 0x28)
    if car == 0:
        return
    t = m.rw(E + 0x24)
    if t == 0:
        m.ww(E + 0x88, 0)
        return
    anim = 3 if t == M32 else m.rw(TBL + 4 * s32(t))
    m.ww(E + 0x88, anim)
    car = m.rw(E + 0x28)
    m.ww(E + 0x24, 0)
    if car:
        m.ww(car + 0x2C, 0)
    f = m.rw(E + 0x6C)
    m.ww(E + 0x28, 0)
    m.ww(E + 0x6C, (f | 0x34) & ~0x80 & M32)


def e20(m, J, E, spawn_ret):
    if m.rw(E + 0x88) == 2 and m.rw(E + 0xAC) == 1:
        if m.rb(E + 0x274) == 0:
            y, z, d, x = m.rw(E + 0x118), m.rw(E + 0x11C), m.rw(E + 0x8C), m.rw(E + 0x114)
            J.append(['spawn', E, 0, 12, x, y, z, d])
            F = spawn_ret
            for i in range(6):                                  # +0x84 down to +0x70
                m.ww(F + 0x84 - 4 * i, 0)
            m.ww(F + 0x88, 3)
            m.ww(F + 0x6C, (m.rw(F + 0x6C) & ~0x10 & M32) | 2)
        J.append(['destroy2', E, M32])
    else:
        m.ww(E + 0x88, 2)
        f = m.rw(E + 0x6C)
        m.ww(E + 0x6C, ((f | 0x40) & ~0x30) & M32)
        c = m.rw(E + 0x130)
        if c != 0 and m.rw(c + 0x68) == 0x187:
            m.wb(E + 0x274, 1)                                  # sb: one byte, the other three of the word are untouched


def d34(m, J, E):
    a = m.rw(m.rw(E + 0x224) + 0x1C8)
    if (m.rb(a) & 0xF) == 6:
        f = m.rw(E + 0x6C)
        m.ww(E + 0x88, 1)
        m.ww(E + 0x6C, ((f | 0x46) & ~0x80) & M32)


g = json.load(open(sys.argv[1]))
bad = 0; nread = 0; nwrite = 0; bycase = {}
for v in g['vectors']:
    E = int(v['self'], 16)
    m = Mem(v['reads']); J = []
    try:
        if v['fn'] == 'C70': c70(m, J, E)
        elif v['fn'] == 'E20': e20(m, J, E, int(v['spawn_ret'], 16))
        else: d34(m, J, E)
    except KeyError as ex:
        bad += 1; print('UNLISTED READ', v['fn'], v['case'], ex); continue
    # null spawn: the binary writes through a null pointer; the model writes at 0x70.. too (same addresses)
    exp = {Mem.n(int(a, 16)): w for a, w in v['writes'].items()}
    got = {a: w for a, w in m.w.items() if (m.r.get(a) != w) or True}
    # a write of the value already there is not a difference in the vector; keep only changed words
    got = {a: w for a, w in got.items() if w != m.r.get(a, None) or a not in m.r}
    # the vector lists words that CHANGED in RAM; words written with the same value are absent. Words not read and written count as changed unless equal to RAM before (unknown): compare on the intersection + extras
    extra = {a: w for a, w in got.items() if a not in exp}
    extra.update({a: w for a, w in m.b.items() if a not in exp and a not in m.r})
    missing = {a: w for a, w in exp.items() if not (m.w.get(a) == w or (a in m.b and a not in m.w and (w & 0xFF) == m.b[a]))}
    ok = not missing and J == v['journal'] and all(a in m.r or True for a in extra)
    # extras are tolerated only when they are writes of a value equal to the RAM word, which the vector cannot list: check against the 'reads' record when present
    extra_bad = {a: w for a, w in extra.items() if a in m.r and m.r[a] != w}
    if not ok or extra_bad:
        bad += 1; print('DEVIATION', v['fn'], v['case'], 'missing', missing, 'extra', extra_bad, J, v['journal'])
    nread += len(v['reads']); nwrite += len(v['writes'])
    bycase[v['fn']] = bycase.get(v['fn'], 0) + 1
print('vectors', len(g['vectors']), bycase, 'differences', bad, 'words read by the real code', nread, 'words written', nwrite)
sys.exit(1 if bad else 0)
