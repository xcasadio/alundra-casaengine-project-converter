"""h3_wmut.py - single-word mutants of the REAL code (in memory, never in the file) against the models: a mutant is KILLED when some vector gives a different observable than the (unchanged) model.
Operators: branch sense flip (beq/bne, blez/bgtz, regimm bit), immediate +1 / -1 (addiu andi ori slti sltiu), slt<->sltu (R and I forms), load/store offset +4 (+1 for bytes),
subu<->addu, shift amount +1, store removed (nop).  Survivors are printed with their disassembly: each must be judged EQUIVALENT by hand (dead code, stack frame words, register copies...).
usage: python -B h3_wmut.py [name ...] [--n 250] [--seed 5]     (names as in h3_vec.REGION)"""
import random
import sys
import struct
import capstone
from collections import Counter
import h3_vec as V
import h3_models as H
import h4_models as M
from machine import s32, M32

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)


def dis(w, a):
    i = list(md.disasm(struct.pack('<I', w), a))
    return ('%s %s' % (i[0].mnemonic, i[0].op_str)) if i else '.word %08x' % w


def mutants(w):
    op = w >> 26
    out = []
    rs = (w >> 21) & 31
    rt = (w >> 16) & 31
    funct = w & 63
    imm = w & 0xFFFF
    if op in (4, 5, 6, 7):
        out.append(('branch sense', w ^ (1 << 26)))
    if op == 1:
        out.append(('regimm sense', w ^ (1 << 16)))
    if op in (0x09, 0x0C, 0x0D, 0x0A, 0x0B):
        out.append(('imm+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
        out.append(('imm-1', (w & ~0xFFFF) | ((imm - 1) & 0xFFFF)))
    if op in (0x0A, 0x0B):
        out.append(('slti<->sltiu', w ^ (0x0A ^ 0x0B) << 26))
    if op == 0 and funct in (0x2A, 0x2B):
        out.append(('slt<->sltu', w ^ 1))
    if op == 0 and funct in (0x21, 0x23):
        out.append(('addu<->subu', w ^ 2))
    if op in (0x23, 0x25, 0x21, 0x2B, 0x29):
        out.append(('offset+4', (w & ~0xFFFF) | ((imm + 4) & 0xFFFF)))
    if op in (0x24, 0x20, 0x28):
        out.append(('offset+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
    if op == 0 and funct in (0, 2, 3) and w != 0:
        out.append(('shamt+1', w + (1 << 6)))
    if op in (0x2B, 0x29, 0x28):
        out.append(('store removed', 0))
    return out


def run_mutant(name, addr, word, vectors_fn, base, n, seed):
    """returns True when killed"""
    snap_ram, snap_init = base
    ram = bytearray(snap_ram)
    o = addr & 0x1FFFFFFF
    ram[o:o + 4] = struct.pack('<I', word)
    return vectors_fn(ram, snap_init)


def main():
    args = sys.argv[1:]
    n = 250
    seed = 5
    names = []
    i = 0
    while i < len(args):
        if args[i] == '--n': n = int(args[i + 1]); i += 2
        elif args[i] == '--seed': seed = int(args[i + 1]); i += 2
        else: names.append(args[i]); i += 1
    names = names or list(V.REGION)
    m, STATS, ctx = V.build()
    base = (bytes(m.ram), bytes(m.init))
    # pre-generate the vectors once per name (same worlds for every mutant): RAM image + descriptors
    total = killed = 0
    survivors = []
    for name in names:
        a0, a1 = V.REGION[name]
        rnd0 = random.Random(seed)
        worlds = []
        for k in range(n):
            m.ram[:] = base[0]; m.init[:] = base[1]
            s, sres, fres, nent = V.fill_world(m, rnd0, name)
            ram0 = bytes(m.ram)
            mem = M.Mem(ram0)
            env = H.Env3(mem, sres, fres, None)
            fn = V.MODEL[name]
            if name in ('C62', 'D30', 'E12', 'C73', 'E21'):
                fn(env, s); mres = None
            else:
                mres = fn(env)
            worlds.append((ram0, s, sres, fres, mres, bytes(mem.b), list(env.j)))
        # an unmutated check
        def check(ram_code, early=True):
            m.ram[:] = ram_code
            kill = False
            for (ram0, s, sres, fres, mres, memb, mj) in worlds:
                # restore the world but keep the (possibly mutated) code words
                code = bytes(ram_code[(a0 & 0x1FFFFFFF):(a1 & 0x1FFFFFFF)])
                m.ram[:] = ram0
                m.ram[(a0 & 0x1FFFFFFF):(a1 & 0x1FFFFFFF)] = code
                m.init[:] = base[1]
                ctx.j = []; ctx.spawn_results = list(sres); ctx.fx_results = list(fres)
                try:
                    if name in ('C62', 'D30', 'E12', 'C73', 'E21'):
                        m.call(V.ADDR[name], s, limit=400_000); rres = None
                    else:
                        rres = s32(m.call(V.ADDR[name], limit=400_000))
                        if mres is None: rres = None
                except Exception:
                    return True
                if rres != mres or ctx.j != mj:
                    return True
                for (a, b) in V.COMPARE:
                    if m.ram[a & 0x1FFFFFFF:b & 0x1FFFFFFF] != memb[a & 0x1FFFFFFF:b & 0x1FFFFFFF]:
                        return True
            return False
        assert not check(base[0]), 'the unmutated code must match its model on the pre-generated worlds: ' + name
        k_local = t_local = 0
        cnt = Counter()
        for addr in range(a0, a1, 4):
            w = struct.unpack_from('<I', base[0], addr & 0x1FFFFFFF)[0]
            for opname, mw in mutants(w):
                if mw == w:
                    continue
                ram = bytearray(base[0])
                ram[(addr & 0x1FFFFFFF):(addr & 0x1FFFFFFF) + 4] = struct.pack('<I', mw)
                t_local += 1
                if check(ram):
                    k_local += 1
                else:
                    survivors.append((name, addr, opname, dis(w, addr), dis(mw, addr)))
        total += t_local; killed += k_local
        print('%-13s mutants %4d killed %4d survivors %3d' % (name, t_local, k_local, t_local - k_local), flush=True)
    print('TOTAL mutants %d killed %d survivors %d' % (total, killed, total - killed))
    for s in survivors:
        print('SURVIVOR %-13s %08X %-14s %-34s -> %s' % s)


if __name__ == '__main__':
    main()
