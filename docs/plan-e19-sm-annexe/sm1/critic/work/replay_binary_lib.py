"""Critic: replay the preparation's frozen vectors on MY OWN MIPS interpreter (mymips.py), real handlers through
the table 0x80098FAC. Compares result and size with the vector's frozen 'binary' block. Two junk variants beyond N."""
import json, sys, hashlib
sys.path.insert(0, 'work')
from mymips import *

VEC = sys.argv[1]
SLOT0 = 0x80127D30
STRIDE = 0x294
NWORD = 0x80134360
PROG = 0x80150000
CELL = 0x80150100
STATE = 0x80150200
FOREIGN = 0x80160000
OFF = dict(parent=0x0C, status=0x10, blocked=0x20, platform=0x28, ref=0x48, flags=0x6C, anim=0xB4,
           tx=0x120, ty=0x124, tz=0x128, riding=0x12C, xcoll=0x130)
TABLE = 0x80098FAC


def slot_addr(i):
    return SLOT0 + STRIDE * i


def build(v, junk):
    m = Mem()
    n = v['n']
    m.w32(NWORD, n)
    # program bytes + 0xFF
    code = v['code'] + [0xFF]
    for i, b in enumerate(code):
        m.wb(PROG + i, b)
    m.w32(CELL, PROG)
    m.w32(STATE + 0x2C, 7)

    def ptr(x):
        if x is None:
            return 0
        if x == 'foreign':
            return FOREIGN
        if x == 'blocked':
            return 0
        return slot_addr(x)

    for i, s in enumerate(v['slots']):
        a = slot_addr(i)
        m.w32(a + OFF['status'], s['status'])
        m.w32(a + OFF['ref'], s['ref'] & 0xFFFFFFFF)
        m.w32(a + OFF['flags'], s['flags'] & 0xFFFFFFFF)
        m.w32(a + OFF['anim'], s['anim'] & 0xFFFFFFFF)
        m.w32(a + OFF['tx'], s['tile'][0] & 0xFFFFFFFF)
        m.w32(a + OFF['ty'], s['tile'][1] & 0xFFFFFFFF)
        m.w32(a + OFF['tz'], s['tile'][2] & 0xFFFFFFFF)
        m.w32(a + OFF['riding'], ptr(s['riding']))
        m.w32(a + OFF['xcoll'], ptr(s['xcoll']))
        m.w32(a + OFF['parent'], ptr(s['parent']))
        m.w32(a + OFF['platform'], ptr(s['platform']))
        m.w32(a + OFF['blocked'], 0xBEEF0000 if s['blocked'] else 0)
    if junk:
        for i in range(n + 1, 10):
            a = slot_addr(i)
            m.w32(a + OFF['status'], 2)
            m.w32(a + OFF['ref'], (i % 8))
            m.w32(a + OFF['flags'], 0x80)
            m.w32(a + OFF['anim'], 0)
            m.w32(a + OFF['riding'], slot_addr(v['logic']))
            m.w32(a + OFF['xcoll'], slot_addr(v['logic']))
            m.w32(a + OFF['parent'], slot_addr(v['logic']))
            m.w32(a + OFF['platform'], 0)
    m.writes.clear()
    m.reads.clear()
    return m


def run(v, junk):
    m = build(v, junk)
    cpu = CPU(m, stubs={0x8003C914: lambda c: c.setreg(2, c.r[4]),   # validator replaced (result discarded, fatal for invalid id)
                        0x8003C840: lambda c: (_ for _ in ()).throw(Trap("debug trap"))})
    entry = u32_word(m, TABLE + 4 * v['op'])
    cpu.setreg(4, slot_addr(v['logic']))      # a0 = logic entity
    cpu.setreg(5, slot_addr(0))               # a1 = owner = hero (never read)
    cpu.setreg(6, CELL)                       # a2 = &code pointer
    cpu.setreg(7, STATE)                      # a3 = state
    cpu.setreg(29, 0x80180000)                # sp
    cpu.run(entry)
    res = m.r32(STATE + 0x2C, track=False)
    # what was written, apart from the stack and the results buffer
    bad = [w for w in m.writes if not (0x80170000 <= w[0] < 0x80190000) and not (0x8013D8D4 <= w[0] < 0x8013D8D4 + 4 * 12) and w[0] != STATE + 0x2C]
    # what was read among entity data above N
    maxaddr = slot_addr(v['n']) + STRIDE
    above = [r for r in m.reads if slot_addr(v['n'] + 1) <= r[0] < slot_addr(10)]
    return res, cpu.r[2], bad, above


def u32_word(m, a):
    return m.r32(a, track=False)


def main():
    head = None
    n = 0
    fails = 0
    kinds = {}
    for line in open(VEC, encoding='utf-8'):
        line = line.strip().rstrip(',')
        if not line.startswith('{"anon"'):
            continue
        v = json.loads(line)
        n += 1
        outs = []
        for junk in (True, False):
            res, v0, bad, above = run(v, junk)
            outs.append((res, v0))
            if bad:
                fails += 1
                print("WRITE OUTSIDE", v['id'], bad[:3])
            if above and junk is False:
                pass
            if above:
                fails += 1
                print("READ ABOVE N", v['id'], above[:3])
        for (res, v0) in outs:
            if res != v['binary']['result'] or v0 != v['binary']['v0']:
                fails += 1
                print("MISMATCH", v['id'], (res, v0), v['binary']['result'], v['binary']['v0'])
        if outs[0] != outs[1]:
            fails += 1
            print("JUNK-DEPENDENT", v['id'], outs)
        k = (hex(v['op']), outs[0][0])
        kinds[k] = kinds.get(k, 0) + 1
    print("vectors replayed:", n, "failures:", fails)
    print("result histogram:", sorted(kinds.items()))


if __name__=="__main__": main()
