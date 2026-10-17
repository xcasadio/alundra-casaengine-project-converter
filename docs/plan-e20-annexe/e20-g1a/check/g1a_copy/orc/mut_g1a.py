"""mut_g1a.py - one-word mutants of the REAL code (in the interpreter's RAM only): vec_g1a.py must report a mismatch (or crash) for each.
Survivors are listed with their disassembly for a by-hand equivalence argument. Usage: python -B mut_g1a.py [N] > out"""
import struct
import subprocess
import sys

import capstone
import exelib

N = sys.argv[1] if len(sys.argv) > 1 else '400'
RANGES = [(0x8002F854, 0x8002F884), (0x8002EDBC, 0x8002EEAC), (0x8002EEAC, 0x8002EFE0), (0x8002F884, 0x8002FB14), (0x8006B848, 0x8006B8CC),
          (0x8007ED30, 0x8007EDA0), (0x8003A774, 0x8003A7B0), (0x80037E34, 0x80037F28)]
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)


def dis(addr, w):
    for i in md.disasm(struct.pack('<I', w), addr):
        return '%s %s' % (i.mnemonic, i.op_str)
    return '.word %08x' % w


def mutants(addr, w):
    op = w >> 26
    rs = (w >> 21) & 31
    rt = (w >> 16) & 31
    imm = w & 0xFFFF
    out = []
    if w == 0:
        return out
    if op == 4: out.append(('beq->bne', (5 << 26) | (w & 0x3FFFFFF)))
    elif op == 5: out.append(('bne->beq', (4 << 26) | (w & 0x3FFFFFF)))
    elif op == 6: out.append(('blez->bgtz', (7 << 26) | (w & 0x3FFFFFF)))
    elif op == 7: out.append(('bgtz->blez', (6 << 26) | (w & 0x3FFFFFF)))
    elif op == 1: out.append(('regimm flip', w ^ (1 << 16)))
    elif op in (0xA, 0xB, 8, 9):
        out.append(('imm+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
        out.append(('imm-1', (w & ~0xFFFF) | ((imm - 1) & 0xFFFF)))
    elif op == 0xC:
        out.append(('andi mask<<1', (w & ~0xFFFF) | ((imm << 1) & 0xFFFF)))
        out.append(('andi mask>>1', (w & ~0xFFFF) | (imm >> 1)))
    elif op == 0xD:
        out.append(('ori imm^lowbit', (w & ~0xFFFF) | (imm ^ (imm & -imm if imm else 1))))
    elif op == 0xF:
        out.append(('lui+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
    elif op in (0x20, 0x21, 0x23, 0x24, 0x25, 0x28, 0x29, 0x2B):
        out.append(('off+4', (w & ~0xFFFF) | ((imm + 4) & 0xFFFF)))
    elif op == 0 and (w & 63) in (0x2A, 0x2B):
        out.append(('slt<->sltu', w ^ 1))
    elif op == 0 and (w & 63) in (0x21, 0x23):
        out.append(('addu<->subu', w ^ 2))
    elif op == 0 and (w & 63) in (0x24, 0x25):
        out.append(('and<->or', w ^ 1))
    return out


if __name__ == '__main__':
    killed = 0
    surv = []
    total = 0
    for a, b in RANGES:
        for addr in range(a, b, 4):
            w = exelib.u32(addr)
            for name, nw in mutants(addr, w):
                total += 1
                try:
                    r = subprocess.run([sys.executable, '-B', 'vec_g1a.py', N, '%08x' % addr, '%08x' % nw], capture_output=True, text=True, timeout=120)
                    dead = r.returncode != 0
                except subprocess.TimeoutExpired:
                    dead = True
                if dead:
                    killed += 1
                else:
                    surv.append((addr, name, dis(addr, w), dis(addr, nw)))
                    print('SURVIVOR %08x %-16s %-28s -> %s' % (addr, name, dis(addr, w), dis(addr, nw)), flush=True)
    print('mutants %d killed %d survivors %d' % (total, killed, len(surv)))
