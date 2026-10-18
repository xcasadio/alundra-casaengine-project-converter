"""t_e13.py - E13 0x8007F30C, the wrapper 0x8003634C and the standalone cancel 0x80034BDC on my interpreter vs my models."""
import random, sys
from mydis import u32, s16, u16
from myemu import Emu, M32, s32
import t_driver as D

E13 = 0x8007F30C
KICK = 0x800DD874

def run_e13(anim88, ac, flags, kick0=0):
    e = Emu()
    ent = D.ent(5)
    e.poke(ent + 0x88, anim88); e.poke(ent + 0xAC, ac); e.poke(ent + 0x6C, flags); e.poke(KICK, kick0)
    dest = []
    def destroy(m):
        dest.append((m.r[4], s32(m.r[5]))); m.r[2] = 0
    e.hooks[0x8003A59C] = destroy
    e.run(E13, (ent,))
    return e, ent, dest

def model_e13(anim88, ac, flags, kick0):
    if anim88 == 1:
        return dict(kick=kick0, anim=1, flags=flags, destroy=(ac == 1))
    return dict(kick=2, anim=1, flags=((flags | 0x40) & ~0x30) & M32, destroy=False)

bad = 0; n = 0
vals88 = [0, 1, 2, 3, 0x32, 0xFFFFFFFF, 5]
valsac = [0, 1, 2, 0xFFFFFFFF]
flagsv = [0x132014, 0x132044, 0, 0xFFFFFFFF, 0x30, 0x10, 0x20, 0x40]
for a88 in vals88:
    for ac in valsac:
        for fl in flagsv:
            for k0 in (0, 2, 5, 0x7FFFFFFF):
                e, ent, dest = run_e13(a88, ac, fl, k0)
                m = model_e13(a88, ac, fl, k0)
                got = dict(kick=e.peek(KICK) if True else 0, anim=e.peek(ent + 0x88), flags=e.peek(ent + 0x6C), destroy=bool(dest))
                n += 1
                exp = dict(m); exp['kick'] = m['kick'] & M32; exp['anim'] = m['anim'] if a88 != 1 else 1
                if a88 == 1: exp['anim'] = 1
                if got != exp:
                    bad += 1
                    if bad < 5: print('E13 MISMATCH', a88, ac, hex(fl), k0, got, exp)
                if dest and dest[0] != (ent, -1):
                    bad += 1; print('E13 destroy args', dest)
print('E13 cases', n, 'mismatches', bad)

# wrapper 0x8003634C with item 0x2C: counter increments AFTER the driver, item cleared iff the driver returned nonzero
bad = 0
rng = random.Random(99)
for it in range(1500):
    st = D.rand_state(rng)
    e, box, newent = D.build(st)
    r = e.run(0x8003634C, ())
    m = D.model_driver(st)
    cnt0 = st['counter']
    exp_cnt = cnt0 if cnt0 > 0x7FFFFFFE else cnt0 + 1
    if r != m['ret']: bad += 1; print('wrapper ret', r, m['ret'])
    if e.peek(D.CNT) != exp_cnt: bad += 1; print('wrapper counter', e.peek(D.CNT), exp_cnt)
    exp_item = 0 if m['ret'] != 0 else m.get('item', 0x2C)
    if e.peek(D.ITEM) != exp_item: bad += 1; print('wrapper item', e.peek(D.ITEM), exp_item)
print('wrapper cases 1500 mismatches', bad)

# wrapper with item = 0 (no object in progress) returns 1 and touches nothing
e = Emu(); e.poke(D.ITEM, 0); e.poke(D.CNT, 7)
print('wrapper with no item ->', e.run(0x8003634C, ()), 'counter', e.peek(D.CNT))
# wrapper with other items: stubs (0x8003647c -> v1 = 1) -> returns 1, item cleared
for it in (0x21, 0x22, 0x24, 0x28, 0x33, 0x1E):
    e = Emu(); e.poke(D.ITEM, it); e.poke(D.CNT, 7)
    print('wrapper item', hex(it), '->', e.run(0x8003634C, ()), 'counter', e.peek(D.CNT), 'item', e.peek(D.ITEM))

# standalone cancel 0x80034BDC: types 0x11 at status 2, boundary count inclusive, nothing else touched
bad = 0
for it in range(2000):
    st = D.rand_state(rng)
    e, box, newent = D.build(st)
    before = {i: (e.peek(D.ent(i) + 0x10), e.peek(D.ent(i) + 0x20), e.peek(D.ent(i) + 0x88), e.peek(D.ent(i) + 0x68), e.peek(D.ent(i) + 0x6C)) for i in range(64)}
    e.run(0x80034BDC, ())
    for i in range(64):
        b = before[i]
        exp = b
        if 1 <= i <= st['count'] and b[3] == 0x11 and s32(b[0]) == 2:
            exp = (3, 0, 1, b[3], b[4])
        a = (e.peek(D.ent(i) + 0x10), e.peek(D.ent(i) + 0x20), e.peek(D.ent(i) + 0x88), e.peek(D.ent(i) + 0x68), e.peek(D.ent(i) + 0x6C))
        if a != exp:
            bad += 1
            if bad < 5: print('cancel mismatch slot', i, a, exp, 'count', st['count'])
print('cancel cases 2000 mismatches', bad)
# any write outside entity slots 1..count? (other fields)
writes = set()
e, box, newent = D.build(D.rand_state(random.Random(5)))
e.log.clear()
e.run(0x80034BDC, ())
print('cancel write offsets (mod stride):', sorted({(a - D.HERO) % D.STRIDE for a, sz, v in e.log}))
