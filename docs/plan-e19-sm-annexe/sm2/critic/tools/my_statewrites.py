"""For each opcode handler of the table 0x80098FAC, find stores whose base register is derived from a3 (the state pointer),
following the control flow inside the function (both branch targets), and calls (jal) that receive the state pointer.
Origins tracked: ('S',k) = a3 + k. Approximate (no merge of paths: each path explored with its own register map, visited set on (pc, frozen map))."""
import sys
sys.path.insert(0, 'tools')
from mybin import *
M32 = 0xFFFFFFFF

def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x

def decode(w):
    return w >> 26, (w >> 21) & 31, (w >> 16) & 31, (w >> 11) & 31, (w >> 6) & 31, w & 63, w & 0xFFFF

RET_CLOBBER = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 24, 25]

def scan(entry, regs0, depth=0, memo=None, maxn=4000):
    """returns (stores:set of (k) offsets relative to S, calls:list of (callee, argmap), unknown_jr:bool)"""
    stores = set(); calls = []
    seen = set()
    work = [(entry, tuple(sorted(regs0.items())))]
    n = 0
    while work and n < maxn:
        pc, rm = work.pop()
        regs = dict(rm)
        while True:
            key = (pc, tuple(sorted(regs.items())))
            if key in seen: break
            seen.add(key); n += 1
            if n > maxn: break
            if not (taddr <= pc < taddr + tsize): break
            w = u32(pc)
            op, rs, rt, rd, sh, fn, imm = decode(w)
            nxt = pc + 4
            delay = None
            def apply(w2, regs):
                op, rs, rt, rd, sh, fn, imm = decode(w2)
                if op == 0:
                    if fn in (0x21, 0x25, 0x20) and rt == 0 and rs in regs:   # move
                        if rd: regs[rd] = regs[rs]
                    elif fn in (0x21, 0x25) and rs == 0 and rt in regs:
                        if rd: regs[rd] = regs[rt]
                    else:
                        if rd in regs: del regs[rd]
                elif op in (0x09, 0x08):   # addiu
                    if rs in regs and regs[rs][0] == 'S':
                        if rt: regs[rt] = ('S', regs[rs][1] + s16(imm))
                    elif rt in regs: del regs[rt]
                elif op in (0x2B, 0x28, 0x29):  # sw sb sh
                    if rs in regs and regs[rs][0] == 'S':
                        stores.add((regs[rs][1] + s16(imm), {0x2B: 4, 0x28: 1, 0x29: 2}[op]))
                elif op in (0x23, 0x24, 0x20, 0x21, 0x25, 0x0F, 0x0D, 0x0C, 0x0A, 0x0B, 0x0E):
                    if rt in regs: del regs[rt]
                return regs
            # control flow
            if op == 2 or op == 3:   # j / jal
                tgt = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
                regs = apply(u32(pc + 4), regs)
                if op == 3:
                    args = {a: regs[a] for a in (4, 5, 6, 7) if a in regs and regs[a][0] == 'S'}
                    # note: delay slot already applied; args as of call
                    if args: calls.append((tgt, args))
                    for r in RET_CLOBBER:
                        if r in regs: del regs[r]
                    pc += 8; continue
                else:
                    # tail jump into code: follow
                    pc = tgt; continue
            if op == 0 and fn == 8:   # jr
                regs = apply(u32(pc + 4), regs)
                break
            if op == 0 and fn == 9:   # jalr
                regs = apply(u32(pc + 4), regs)
                args = {a: regs[a] for a in (4, 5, 6, 7) if a in regs and regs[a][0] == 'S'}
                if args: calls.append(('jalr', args))
                for r in RET_CLOBBER:
                    if r in regs: del regs[r]
                pc += 8; continue
            if op in (4, 5, 6, 7, 1):   # branches
                tgt = pc + 4 + (s16(imm) << 2)
                regs2 = apply(u32(pc + 4), dict(regs))
                work.append((tgt, tuple(sorted(regs2.items()))))
                regs = regs2
                pc += 8; continue
            regs = apply(w, regs)
            pc += 4
    return stores, calls

if __name__ == '__main__':
    import json
    out = {}
    for opc in range(0, 0x100):
        h = u32(0x80098FAC + 4 * opc)
        if not (taddr <= h < taddr + tsize):
            continue
        st, calls = scan(h, {7: ('S', 0)})
        out[opc] = (h, sorted(st), [(c if c == 'jalr' else hex(c), a) for c, a in calls])
    for opc, (h, st, calls) in out.items():
        wr = [o for o, sz in st if o in (0xC, 0x10, 0x14)]
        if st or calls:
            print(f"{opc:02X} h={h:08x} stores(rel S)={[(hex(o), sz) for o, sz in st]} calls={[(c, {k: v for k, v in a.items()}) for c, a in calls]}")
