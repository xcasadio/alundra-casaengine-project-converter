"""my_goldens_g2c2.py - E20.g2c2 T0 (c): SELF-CONTAINED reference vectors for C70 (0x8007B7B0, the carried half of the torch), E20 (0x8007F6C8, the torch consumed and its flame) and D34 (0x8007E8AC, the brambles),
produced on the REAL code of ALUN_CD.EXE in the copied MIPS interpreter (the rig of e20-h3/critic/run, the method of my_goldens_e19.py of e20-h3a): every data word read is recorded, every word written, the journal of the stubbed calls.
The cases are STRUCTURED (branch by branch, then random fill), not drawn blindly: the first cases of each function name the branch they cover.
Run from a COPY of docs/plan-e20-annexe/e20-h3/critic/run with this file beside it (and ../results/): python -B my_goldens_g2c2.py [per_function=30] [seed] -> ../results/goldens_g2c2.json"""
import json, random, sys
import machine
from machine import M32
from my_rig import Rig, ent, H, diffs, CELLS, ATKAREA

C70, E20, D34 = 0x8007B7B0, 0x8007F6C8, 0x8007E8AC
ENDS = {C70: 0x8007B834, E20: 0x8007F7CC, D34: 0x8007E8F0}


def poke_all(m, e, kw):
    for k, v in kw.items():
        m.poke(e + k, v & M32)


def setup_c70(rig, rnd, k):
    m = rig.m
    E = ent(rnd.randrange(1, 40))
    other = ent(rnd.randrange(41, 63))
    # (carrier, code) per case: the named branches first
    plan = [(0, 0), (0, 3), (H, 0), (H, M32), (H, 1), (H, 2), (H, 3), (H, 4), (H, 5), (H, 6), (other, 2), (other, M32), (H, 1), (0, M32)]
    carrier, code = plan[k] if k < len(plan) else (rnd.choice([0, H, other, H, H]), rnd.choice([0, M32, 1, 2, 3, 4, 5, 6, 7, rnd.getrandbits(3)]))
    flags = rnd.choice([0x3323E2, 0x3323E2, 0x332376, 0x80, 0x0, M32, 0x34, rnd.getrandbits(32), 0x3323E2 | 0x80])
    poke_all(m, E, {0x28: carrier, 0x24: code, 0x6C: flags, 0x88: rnd.choice([0, 1, 2, 3, 5, rnd.getrandbits(32)]), 0x68: 0x189})
    m.poke(H + 0x2C, E if carrier == H else rnd.choice([0, E]))
    m.poke(other + 0x2C, rnd.choice([0, 0x55, E]))
    return E


def setup_e20(rig, rnd, k):
    m = rig.m
    E = ent(rnd.randrange(1, 40))
    C = ent(rnd.randrange(41, 55))
    F = ent(rnd.randrange(56, 63))                         # the entity the stub of SpawnWarpEntity returns
    drop391, other = 0x187, 0x189
    # (anim 0x88, reset 0xAC, byte 0x274, contact type or None for 0, spawn: 'slot'|'null')
    plan = [(2, 1, 0, None, 'slot'), (2, 1, 1, None, 'slot'), (2, 1, 0, None, 'null'), (2, 1, 5, None, 'slot'), (2, 1, 0x100, None, 'slot'),
            (2, 0, 0, None, 'slot'), (2, 2, 0, None, 'slot'), (3, 1, 0, None, 'slot'), (0, 1, 0, None, 'slot'), (1, 0, 0, None, 'slot'),
            (2, 0, 0, drop391, 'slot'), (3, 1, 0, drop391, 'slot'), (0, 0, 0, other, 'slot'), (1, 1, 1, drop391, 'slot'), (0, 2, 7, 0, 'slot'),
            (3, 2, 0, drop391, 'slot'), (2, 0, 0, drop391 + 1, 'slot'), (2, 0, 0, 0x188, 'slot'), (5, 0, 0, drop391, 'slot'), (2, 1, 0xAB, drop391, 'slot')]
    if k < len(plan):
        anim, rst, byte, ctype, sp = plan[k]
    else:
        anim, rst, byte = rnd.choice([2, 2, 2, 0, 1, 3]), rnd.choice([1, 1, 0, 2]), rnd.choice([0, 0, 1, 2, 255, rnd.getrandbits(8)])
        ctype, sp = rnd.choice([None, None, drop391, other, 0x187]), rnd.choice(['slot', 'slot', 'null'])
    word274 = byte | (rnd.getrandbits(24) << 8 if k >= 3 else 0)         # the upper bytes of the word are not the byte: lbu reads only +0x274
    poke_all(m, E, {0x88: anim, 0xAC: rst, 0x274: word274, 0x6C: rnd.choice([0x332346, 0x3323E2, 0x332376, rnd.getrandbits(32)]),
                      0x114: rnd.getrandbits(32), 0x118: rnd.getrandbits(32), 0x11C: rnd.choice([0, 0x80000, 0xFFF80000, rnd.getrandbits(32)]),
                      0x8C: rnd.choice([0, 8, 0x10, 0x18, rnd.getrandbits(5)]), 0x68: 0x189, 0x130: 0 if ctype is None else C})
    if ctype is not None:
        m.poke(C + 0x68, ctype)
    # the entity of the stub: its flags, animation and six codes are random and non-zero where the plan asks (the six codes at +0x70..+0x84)
    poke_all(m, F, {0x6C: rnd.choice([0x132146 | 0x10, 0x10, M32, rnd.getrandbits(32)]), 0x88: rnd.choice([0, 1, 5, rnd.getrandbits(32)])})
    for i in range(6):
        m.poke(F + 0x70 + 4 * i, rnd.choice([60, 9, 17, M32, rnd.getrandbits(32), 1]) if k % 3 != 2 else 1 + rnd.getrandbits(31))
    rig.spawn_ret = 0 if sp == 'null' else F
    return E


def setup_d34(rig, rnd, k):
    m = rig.m
    E = ent(rnd.randrange(1, 40))
    T = ent(rnd.randrange(41, 55))
    plan = [6, 6, 6, 5, 7, 0, 0xF, 0x86, 0x96, 0xF6, 1, 2, 3, 4, 8, 9, 10, 11, 12, 13, 14, 0x06, 0x66, 0xA6, 0x36]
    byte = plan[k] if k < len(plan) else rnd.choice([6, 0x86, rnd.getrandbits(8), 5, 7])
    for i in range(16):
        m.poke8(ATKAREA + 2 * i, rnd.getrandbits(8))
    a = ATKAREA + 2 * rnd.randrange(16)
    m.poke8(a, byte)
    flags = rnd.choice([0xC3, 0x47, 0x80, 0x0, M32, rnd.getrandbits(32), 0xC3])
    poke_all(m, E, {0x224: T, 0x6C: flags, 0x88: rnd.choice([0, 1, 2, 5, rnd.getrandbits(32)]), 0x68: rnd.choice([238 + 0x100 - 0x100, 239, 0x100 + 238])})
    poke_all(m, T, {0x1C8: a})
    return E


SETUP = {C70: setup_c70, E20: setup_e20, D34: setup_d34}
NAME = {C70: 'C70', E20: 'E20', D34: 'D34'}


def run(per, seed):
    rig = Rig(); rig.freeze(); m = rig.m
    rnd = random.Random(seed)
    reads = set()
    orig = (machine.Machine.rw, machine.Machine.rh, machine.Machine.rb)
    rec = {'on': False}

    def wrap(f):
        def g(self, a):
            if rec['on']:
                aa = a & M32
                if aa != self.pc and aa != self.pc + 4: reads.add(aa & ~3)
            return f(self, a)
        return g
    machine.Machine.rw, machine.Machine.rh, machine.Machine.rb = wrap(orig[0]), wrap(orig[1]), wrap(orig[2])
    out = []
    for addr in (C70, E20, D34):
        for k in range(per):
            rig.reset()
            rig.spawn_ret = 0
            E = SETUP[addr](rig, rnd, k)
            before = bytes(m.ram); reads.clear(); rig.J = []
            rec['on'] = True
            ret = m.call(addr, E)
            rec['on'] = False
            after = bytes(m.ram)
            rd = {a: int.from_bytes(before[a & 0x1FFFFFFF:(a & 0x1FFFFFFF) + 4], 'little') for a in sorted(reads) if (a & 0x1FFFFFFF) < 0x1FE000}
            dw = diffs(before, after)
            words = sorted({a & ~3 for a in dw})
            wr = {a: int.from_bytes(after[a & 0x1FFFFFFF:(a & 0x1FFFFFFF) + 4], 'little') for a in words}
            out.append(dict(fn=NAME[addr], case=k, addr='%08X' % addr, self='%08X' % E, reads={'%08X' % a: v for a, v in rd.items()}, writes={'%08X' % a: v for a, v in wr.items()},
                            ret=None, journal=[[j[0]] + (list(j[1]) if isinstance(j[1], tuple) else list(j[1:])) for j in rig.J], spawn_ret='%08X' % rig.spawn_ret))
    machine.Machine.rw, machine.Machine.rh, machine.Machine.rb = orig
    json.dump(dict(note=__doc__.split('\n')[0], seed=seed, vectors=out), open('../results/goldens_g2c2.json', 'w'), separators=(',', ':'))
    print('vectors', len(out))


if __name__ == '__main__':
    run(int(sys.argv[1]) if len(sys.argv) > 1 else 30, int(sys.argv[2]) if len(sys.argv) > 2 else 20261026)
