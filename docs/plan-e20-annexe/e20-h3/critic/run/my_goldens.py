"""my_goldens.py - critic: SELF-CONTAINED reference vectors for a C# replay.  Every data word the real code READS is recorded (not only those that differ from a baseline), plus every word it
WRITES, the return value and the journal of the stubbed calls.  Unlisted words are NOT read by the function.  usage: python -B my_goldens.py [per_function=25] [seed] -> ../results/goldens_critic.json"""
import json, random, sys
import machine
from machine import M32, s32
from my_rig import Rig, Mem, diffs, ent, H
import my_diff as D

def run(per, seed):
    rig = Rig(); rig.freeze(); m = rig.m
    rnd = random.Random(seed)
    reads = set()
    orig_rw, orig_rh, orig_rb = machine.Machine.rw, machine.Machine.rh, machine.Machine.rb
    rec = {'on': False}
    def rw(self, a):
        if rec['on']:
            aa = a & M32
            if aa != self.pc and aa != self.pc + 4: reads.add(aa & ~3)
        return orig_rw(self, a)
    def rh(self, a):
        if rec['on']:
            aa = a & M32
            if aa != self.pc and aa != self.pc + 4: reads.add(aa & ~3)
        return orig_rh(self, a)
    def rb(self, a):
        if rec['on']:
            aa = a & M32
            if aa != self.pc and aa != self.pc + 4: reads.add(aa & ~3)
        return orig_rb(self, a)
    machine.Machine.rw, machine.Machine.rh, machine.Machine.rb = rw, rh, rb
    out = []
    for name, (addr, end) in D.FUNCS.items():
        for k in range(per):
            rig.reset(); D.gen(rig, rnd)
            if name in ('start_beans', 'start_bomb', 'drv_beans', 'drv_bomb', 'cancel_beans', 'cancel_bomb', 'dispatch') and rnd.random() < 0.85:
                D.friendly(rig, rnd)
            E = rnd.choice([ent(rnd.randrange(1, 64))] * 8 + [H])
            before = bytes(m.ram); reads.clear(); rig.J = []
            rec['on'] = True
            if name in ('start_beans', 'start_bomb', 'drv_beans', 'drv_bomb', 'cancel_beans', 'cancel_bomb', 'dispatch'):
                ret = m.call(addr); arg = None
            else:
                ret = m.call(addr, E); arg = E
            rec['on'] = False
            after = bytes(m.ram)
            rd = {a: int.from_bytes(before[a & 0x1FFFFFFF:(a & 0x1FFFFFFF) + 4], 'little') for a in sorted(reads) if (a & 0x1FFFFFFF) < 0x1FE000}
            dw = diffs(before, after)
            words = sorted({a & ~3 for a in dw})
            wr = {a: int.from_bytes(after[a & 0x1FFFFFFF:(a & 0x1FFFFFFF) + 4], 'little') for a in words}
            out.append(dict(fn=name, addr='%08X' % addr, self=('%08X' % arg) if arg else None,
                            reads={'%08X' % a: v for a, v in rd.items()}, writes={'%08X' % a: v for a, v in wr.items()},
                            ret=(ret & M32) if name in ('start_beans', 'start_bomb', 'drv_beans', 'drv_bomb') else None,
                            journal=[[j[0]] + list(j[1:]) if not isinstance(j[1], tuple) else [j[0]] + list(j[1]) for j in rig.J],
                            spawn_ret='%08X' % rig.spawn_ret, eff_ret='%08X' % rig.eff_ret))
    machine.Machine.rw, machine.Machine.rh, machine.Machine.rb = orig_rw, orig_rh, orig_rb
    json.dump(dict(note=__doc__.split('\n')[0], seed=seed, vectors=out), open('../results/goldens_critic.json', 'w'), separators=(',', ':'))
    print('vectors', len(out))

if __name__ == '__main__':
    run(int(sys.argv[1]) if len(sys.argv) > 1 else 25, int(sys.argv[2]) if len(sys.argv) > 2 else 20261018)
