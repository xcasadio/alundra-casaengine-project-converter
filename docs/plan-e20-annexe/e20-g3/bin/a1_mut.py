"""a1_mut.py - one-word mutants of the three functions of a1_vec.py (lists builder 0x800384F4-0x8003862C, FindEntityCollisionCandidate 0x80036F34-0x800370BC,
GetCollisionOnZ 0x80037F28-0x80038060), each run against the vectors of a1_vec.py: a mutant is KILLED when at least one model/real mismatch (or a machine error) appears.
Usage: python -B -I a1_mut.py [N_per_mutant]   -> out/a1_mut.txt
Operators: branch sense flip (beq/bne, beqz/bnez, blez/bgtz, bltz/bgez), slt<->sltu, slti<->sltiu, immediate +1/-1 (addiu, andi, ori, slti, sltiu, lui), andi mask lowest-bit toggle,
load/store displacement +4, subu<->addu, move->subu-zero (register copy kill), nop replacement of a store."""
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import a1_vec as V
import exelib
import capstone

N = int(sys.argv[1]) if len(sys.argv) > 1 else 160
RANGES = [(0x800384F4, 0x80038630), (0x80036F34, 0x800370C0), (0x80037F28, 0x80038064), (0x80036BFC, 0x80036D90), (0x80036D94, 0x80036F30)]
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)


def word(a):
    return exelib.u32(a)


def mutants():
    out = []
    for (lo, hi) in RANGES:
        for a in range(lo, hi, 4):
            w = word(a)
            op = w >> 26
            funct = w & 63
            rt = (w >> 16) & 31
            imm = w & 0xFFFF
            cand = []
            if op in (4, 5, 6, 7):                         # beq bne blez bgtz
                cand.append(('branch flip', w ^ (1 << 26)))
            if op == 1 and rt in (0, 1):                   # bltz bgez
                cand.append(('bltz/bgez flip', w ^ (1 << 16)))
            if op == 0 and funct in (0x2A, 0x2B):          # slt sltu
                cand.append(('slt/sltu', w ^ 1))
            if op in (0x0A, 0x0B):                         # slti sltiu
                cand.append(('slti/sltiu', w ^ (1 << 26)))
            if op in (0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0F):
                cand.append(('imm+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
                cand.append(('imm-1', (w & ~0xFFFF) | ((imm - 1) & 0xFFFF)))
            if op in (0x23, 0x2B):                         # lw sw
                cand.append(('disp+4', (w & ~0xFFFF) | ((imm + 4) & 0xFFFF)))
            if op == 0 and funct in (0x21, 0x23):          # addu subu
                cand.append(('addu/subu', w ^ 2))
            if op == 0 and funct == 0x21 and ((w >> 16) & 31) == 0:      # move rd, rs (addu rd, rs, zero) -> subu rd, rs, zero? keep: mutate to zero source
                cand.append(('move->zero', w & ~(31 << 21)))
            if op == 0x2B:
                cand.append(('store->nop', 0))
            for name, nw in cand:
                if nw != w:
                    out.append((a, w, nw, name))
    return out


def one(job):
    a, w, nw, name = job
    try:
        bad, tot = V.main(N=N, MUT=(a, nw), quiet=True)
        dead = any(bad.values())
    except Exception:      # machine error, step limit: the mutant is observable
        dead = True
    return (a, w, nw, name, dead)


def main():
    import multiprocessing as mp
    ms = mutants()
    with mp.Pool(22) as pool:
        res = pool.map(one, ms, chunksize=4)
    killed = sum(1 for r in res if r[4])
    sur = [r for r in res if not r[4]]
    lines = ['mutants %d killed %d survivors %d (N=%d vectors per mutant)' % (len(res), killed, len(sur), N)]
    for a, w, nw, name, dead in sur:
        ins = list(md.disasm(w.to_bytes(4, 'little'), a))
        mut = list(md.disasm(nw.to_bytes(4, 'little'), a))
        lines.append('SURVIVOR %08X %-14s %s %s  ->  %s %s' % (a, name, ins[0].mnemonic if ins else '?', ins[0].op_str if ins else '', mut[0].mnemonic if mut else '?', mut[0].op_str if mut else ''))
    open(os.path.join(V.L.OUT, 'a1_mut.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    import multiprocessing as mp
    mp.freeze_support()
    main()
