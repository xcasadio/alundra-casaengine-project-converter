"""my_goldens_e19.py - E20.h3a T0 (a): 25 SELF-CONTAINED reference vectors for E19 (0x8007F690, the lid handler (E, 19)), produced on the REAL code of ALUN_CD.EXE in the
copied MIPS interpreter, with the method of critic/run/my_goldens.py (every data word read is recorded, every word written, the return value, the journal of the stubbed calls).
Run from a COPY of docs/plan-e20-annexe/e20-h3/critic/run with this file beside it: python -B my_goldens_e19.py [25] [seed] -> ../results/goldens_e19.json"""
import json, random, sys
import machine
from machine import M32
from my_rig import Rig, ent, H, diffs
import my_diff as D

ADDR, END = 0x8007F690, 0x8007F6C8


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
    for k in range(per):
        rig.reset(); D.gen(rig, rnd)
        E = rnd.choice([ent(rnd.randrange(1, 64))] * 8 + [H])
        before = bytes(m.ram); reads.clear(); rig.J = []
        rec['on'] = True
        ret = m.call(ADDR, E)
        rec['on'] = False
        after = bytes(m.ram)
        rd = {a: int.from_bytes(before[a & 0x1FFFFFFF:(a & 0x1FFFFFFF) + 4], 'little') for a in sorted(reads) if (a & 0x1FFFFFFF) < 0x1FE000}
        dw = diffs(before, after)
        words = sorted({a & ~3 for a in dw})
        wr = {a: int.from_bytes(after[a & 0x1FFFFFFF:(a & 0x1FFFFFFF) + 4], 'little') for a in words}
        out.append(dict(fn='E19', addr='%08X' % ADDR, self='%08X' % E, reads={'%08X' % a: v for a, v in rd.items()}, writes={'%08X' % a: v for a, v in wr.items()}, ret=None,
                        journal=[[j[0]] + list(j[1:]) for j in rig.J], spawn_ret='%08X' % rig.spawn_ret, eff_ret='%08X' % rig.eff_ret))
    machine.Machine.rw, machine.Machine.rh, machine.Machine.rb = orig
    json.dump(dict(note=__doc__.split('\n')[0], seed=seed, vectors=out), open('../results/goldens_e19.json', 'w'), separators=(',', ':'))
    print('vectors', len(out))


if __name__ == '__main__':
    run(int(sys.argv[1]) if len(sys.argv) > 1 else 25, int(sys.argv[2]) if len(sys.argv) > 2 else 20261026)
