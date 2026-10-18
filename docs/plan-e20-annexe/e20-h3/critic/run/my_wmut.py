"""my_wmut.py - critic: single-word mutants of the REAL code (branch flip, nop-out, immediate +1) run against MY models: a mutant that no vector separates from the model is listed (survivor).
usage: python -B my_wmut.py <function> [vectors=120] [seed]"""
import random, sys, struct
from machine import M32, s32
from my_rig import Rig, Mem, diffs, ent, H
import my_diff as D
import my_models as MM
import lib
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)

def mutants(w):
    op = w >> 26
    out = []
    if w != 0:
        out.append(('nop', 0))
    if op in (4, 5, 6, 7):
        flip = {4: 5, 5: 4, 6: 7, 7: 6}[op]
        out.append(('flip', (w & 0x03FFFFFF) | (flip << 26)))
    if op in (8, 9, 0xA, 0xB, 0xC, 0xD, 0x20, 0x21, 0x23, 0x24, 0x25, 0x28, 0x29, 0x2B):
        imm = w & 0xFFFF
        out.append(('imm+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
        if op in (0x20, 0x21, 0x23, 0x24, 0x25, 0x28, 0x29, 0x2B):
            out.append(('imm+4', (w & ~0xFFFF) | ((imm + 4) & 0xFFFF)))
    if op == 0 and (w & 0x3F) in (0x21, 0x23, 0x24, 0x25, 0x2A, 0x2B):
        alt = {0x21: 0x23, 0x23: 0x21, 0x24: 0x25, 0x25: 0x24, 0x2A: 0x2B, 0x2B: 0x2A}[w & 0x3F]
        out.append(('alu-swap', (w & ~0x3F) | alt))
    return out

def run(name, nvec, seed):
    addr, end = D.FUNCS[name]
    rig = Rig(); rig.freeze(); m = rig.m
    base = bytearray(rig.base)
    surv = []; total = 0; killed = 0
    for a in range(addr, end, 4):
        o = a & 0x1FFFFFFF
        w = struct.unpack_from('<I', base, o)[0]
        for tag, nw in mutants(w):
            if nw == w: continue
            total += 1
            struct.pack_into('<I', rig.base if False else base, o, nw)
            rig.base = bytes(base)
            rnd = random.Random(seed + a)
            bad = False
            try:
                for n in range(nvec):
                    rig.reset(); D.gen(rig, rnd)
                    m.r[:] = [0] * 32; m.hi = m.lo = 0          # a crashed mutant must not leak registers into the next vector
                    if name in ('start_beans', 'start_bomb', 'drv_beans', 'drv_bomb', 'cancel_beans', 'cancel_bomb', 'dispatch') and rnd.random() < 0.85:
                        D.friendly(rig, rnd)
                    E = rnd.choice([ent(rnd.randrange(1, 64))] * 8 + [H])
                    before = bytes(m.ram); M = Mem(before); Jm = []
                    retm = D.model_for(name, M, Jm, rig, E)
                    rig.J = []
                    if name in ('start_beans', 'start_bomb', 'drv_beans', 'drv_bomb', 'cancel_beans', 'cancel_bomb', 'dispatch'):
                        ret = m.call(addr, limit=20000)
                    else:
                        ret = m.call(addr, E, limit=20000)
                    after = bytes(m.ram)
                    dm = {x: v for x, v in M.changed(before).items() if (x & 0x1FFFFFFF) < 0x1FE000}
                    okret = (retm is None) or ((ret & M32) == (retm & M32)) or name == 'dispatch'
                    if not (okret and list(rig.J) == Jm and diffs(before, after) == dm):
                        bad = True; break
            except Exception:
                bad = True          # crash / step limit = detected
            struct.pack_into('<I', base, o, w)
            if bad: killed += 1
            else: surv.append((a, tag, '%08x -> %08x' % (w, nw), ' '.join('%s %s' % (i.mnemonic, i.op_str) for i in md.disasm(w.to_bytes(4, 'little'), a))))
    rig.base = bytes(base)
    print('%-13s mutants %4d killed %4d survivors %3d' % (name, total, killed, len(surv)))
    for s in surv: print('   SURV %08x %-8s %s   [%s]' % s)

if __name__ == '__main__':
    names = [sys.argv[1]] if sys.argv[1] != 'all' else list(D.FUNCS)
    for nm in names:
        run(nm, int(sys.argv[2]) if len(sys.argv) > 2 else 120, int(sys.argv[3]) if len(sys.argv) > 3 else 1)
