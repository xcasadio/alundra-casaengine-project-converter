"""c4_slots.py - critic's own models of the slot pass 0x80038634, the lists pass 0x800384F4 and the birth raise 0x80039D04 (prefix to 0x80039D40),
run against the real code (cmips), every written byte compared.  Seed 5150.  Also the demonstration that an ON-DEMAND 'highest live slot' differs from
the stored count (death inside the tick) with the REAL query."""
import random
from cmips import *

HERO, STR, COUNT = 0x80127D30, 0x294, 0x80134360
TMPL = 0x80134368
PHYS, PCNT = 0x80127B28, 0x80127D28
COLL, CCNT = 0x80127C28, 0x80127D2C
DRAW, DCNT = 0x80134250, 0x8013435C


def w32(ram, a, v):
    v &= M
    for i in range(4):
        ram[a + i] = (v >> (8 * i)) & 0xFF


class S:
    def __init__(self, ram):
        self.ram = dict(ram); self.w = {}

    def g(self, a, n=4):
        v = 0
        for i in range(n):
            x = self.w.get(a + i, self.ram.get(a + i))
            if x is None:
                x = 0
            v |= x << (8 * i)
        return v

    def p(self, a, v, n=4):
        for i in range(n):
            self.w[a + i] = (v >> (8 * i)) & 0xFF


def slot_pass_model(s):
    top = 0
    for i in range(64):
        e = HERO + STR * i
        st = s.g(e + 0x10)
        if st == 4:
            for k in range(0, 0x294, 4):
                s.p(e + k, s.g(TMPL + k))
            s.p(e, i)
        elif st != 0:
            top = i
    s.p(COUNT, top)


def lists_model(s, inclusive=True):
    cnt = s32(s.g(COUNT))
    pc = cc = dc = 0
    s.p(CCNT, 0); s.p(DCNT, 0); s.p(PCNT, 0)
    last = cnt if inclusive else cnt - 1
    for i in range(0, last + 1):
        e = HERO + STR * i
        st = s.g(e + 0x10)
        live = (st - 2) & M < 2
        if live and s.g(e + 0x20) == 0:
            s.p(PHYS + 4 * pc, e); pc += 1
        if (s.g(e + 0x6C) & 0x80) and not (s.g(e + 0xB4) & 0x80) and s.g(e + 0x28) == 0:
            s.p(COLL + 4 * cc, e); cc += 1
        if live and (s.g(e + 0x1CC) & 3) != 3:
            s.p(DRAW + 4 * dc, e); dc += 1
    s.p(PCNT, pc); s.p(CCNT, cc); s.p(DCNT, dc)


def birth_model(s, ent):
    idx = s.g(ent)
    if s32(s.g(COUNT)) < s32(idx):
        s.p(COUNT, idx)


def mkworld(R, lists=False):
    ram = {}
    for i in range(64):
        e = HERO + STR * i
        w32(ram, e, R.getrandbits(32))
        w32(ram, e + 0x10, R.choice([0, 1, 2, 2, 3, 3, 4, 4, 5, R.getrandbits(32)]))
        for off in (0x20, 0x28, 0x6C, 0xB4, 0x1CC):
            w32(ram, e + off, R.choice([0, 0, R.getrandbits(32), 0x80, 3, 1, 0x80]))
    for k in range(0, 0x294, 4):
        w32(ram, TMPL + k, R.getrandbits(32) if R.random() < 0.7 else 0)
    w32(ram, COUNT, R.choice([-1 & M, 0, 1, 5, 20, 63, R.randint(0, 63)]))
    return ram


def cmp_real(ram, fn, args, model, **kw):
    m = Mem(ram, strict=False)
    c = Cpu(m)
    c.run(fn, args, **kw)
    s = S(ram)
    model(s)
    real = {}
    for a, sz, v in m.wlog:
        if 0x801FFE00 <= a < 0x80200000:
            continue
        for i in range(sz):
            real[a + i] = (v >> (8 * i)) & 0xFF
    return real == s.w, c


if __name__ == "__main__":
    R = random.Random(5150)
    N = 800
    bad = {"slot": 0, "lists": 0, "birth": 0, "lists_exclusive_mutant_distinguished": 0}
    cov = set()
    for n in range(N):
        ram = mkworld(R)
        ok, c = cmp_real(ram, 0x80038634, (), slot_pass_model)
        cov |= c.visited
        bad["slot"] += (not ok)
        ok, c = cmp_real(ram, 0x800384F4, (), lists_model)
        cov |= c.visited
        bad["lists"] += (not ok)
        ok2, _ = cmp_real(ram, 0x800384F4, (), lambda s: lists_model(s, False))
        bad["lists_exclusive_mutant_distinguished"] += (not ok2)
        # birth
        ent = HERO + STR * R.randint(0, 63)
        ok, c = cmp_real(ram, 0x80039D04, (ent, 0, 0, 0), lambda s: birth_model(s, ent), stop_at=[0x80039D40])
        bad["birth"] += (not ok)
    r1 = list(range(0x80038634, 0x800386D0, 4)); r2 = list(range(0x800384F4, 0x80038634, 4))
    print("results over %d random worlds (bad = real-vs-model mismatches; the last key counts worlds where the exclusive-bound mutant differs):" % N, bad)
    print("coverage slot pass %d/%d, lists pass %d/%d" % (len([a for a in r1 if a in cov]), len(r1), len([a for a in r2 if a in cov]), len(r2)))

    # ---- on-demand highest-live-slot vs the stored count (REAL query) ----
    import c3_query
    def build(top_status):
        ram = {}
        for i in range(0, 64):
            e = HERO + STR * i
            w32(ram, e + 0x10, 0)
        for i in range(1, 6):
            e = HERO + STR * i
            w32(ram, e + 0x10, 2); w32(ram, e + 0x6C, 1); w32(ram, e + 0x1C4, 0x80220000)
            w32(ram, e + 0x114, (10 * i) << 16); w32(ram, e + 0x118, 0)
        e6 = HERO + STR * 6
        w32(ram, e6 + 0x10, top_status); w32(ram, e6 + 0x6C, 1); w32(ram, e6 + 0x1C4, 0x80220000)
        w32(ram, e6 + 0x114, 60 << 16)
        w32(ram, HERO + 0x10, 2)
        for i in range(4): ram[0x801FFF10 + i] = (0x800CC158 >> (8 * i)) & 0xFF
        return ram
    # stored count = 6 (slot 6 was live when the slot pass ran), then slot 6 dies (status 4) before the query of the same tick
    for label, count, st6 in (("stored count 6, slot 6 dead (status 4)  [binary, same tick]", 6, 4),
                              ("on-demand highest live slot = 5            [candidate DLL port]", 5, 4)):
        ram = build(st6); w32(ram, COUNT, count)
        m = Mem(ram, strict=False); c = Cpu(m)
        n = c.run(0x8003AF70, (HERO, 1, 0, 0x800CC058))
        got = [(m.b(0x800CC058 + 4 * k) | m.b(0x800CC058 + 4 * k + 1) << 8 | m.b(0x800CC058 + 4 * k + 2) << 16 | m.b(0x800CC058 + 4 * k + 3) << 24 - 0) for k in range(n)]
        print("%-62s -> n=%d slots=%s" % (label, n, sorted((g - HERO) // STR for g in got)))
