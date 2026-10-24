"""hazards.py - D1: both interpreters ignore the MIPS-I load delay slot (the instruction right after a load would see the OLD value of the loaded register on
the real R3000).  This is safe only if the code never reads the loaded register in that slot.  This script checks it over EVERY instruction the oracle
executed (S-A, S-B and random scripts): for each executed load, the next instruction in memory must not read the loaded register (capstone register
access of the next instruction).  A load in a branch delay slot is checked against both successors (fall-through and branch target).
writes out/load-delay-check.txt"""
import os
import random
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib
import oracle
import world as W

LOADS = ('lb', 'lbu', 'lh', 'lhu', 'lw', 'lwl', 'lwr')
BRANCH = ('b', 'beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'bal', 'jal', 'jalr', 'j', 'jr')


def main(outdir, nrand=20, ticks=80):
    cov = set()
    for fn in (oracle.scenario_A, oracle.scenario_B):
        cov |= fn()[0].cov
    rnd = random.Random(7)
    bits = [W.UP, W.RIGHT, W.DOWN, W.LEFT, W.TRIANGLE, W.CIRCLE, W.CROSS, W.SQUARE]
    for k in range(nrand):
        m, _ = oracle.setup()
        pad = W.Pad(); cur = 0
        for t in range(ticks):
            r = rnd.random()
            if r < 0.25: cur ^= rnd.choice(bits)
            elif r < 0.27: cur = 0
            W.tick(m, pad, cur)
        cov |= m.cov
    def word(a):
        return lib.u32(a)

    def reads(w):
        """register numbers read by the instruction word w (MIPS-I integer subset)"""
        op = w >> 26
        rs, rt = (w >> 21) & 31, (w >> 16) & 31
        if op == 0:
            fn = w & 63
            if fn in (0x00, 0x02, 0x03): return {rt}
            if fn in (0x08, 0x09, 0x11, 0x13): return {rs}
            if fn in (0x10, 0x12, 0x0C, 0x0D): return set()
            return {rs, rt}
        if op == 1: return {rs}
        if op in (2, 3, 0xF): return set()
        if op in (4, 5): return {rs, rt}
        if op in (6, 7): return {rs}
        if op in (0x22, 0x26): return {rs, rt}                  # lwl / lwr merge the old rt
        if 0x20 <= op <= 0x26: return {rs}                      # loads
        if 0x28 <= op <= 0x2E: return {rs, rt}                  # stores
        return {rs}                                             # immediates

    def is_load(w):
        return (w >> 26) in (0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26)

    def is_branch(w):
        op = w >> 26
        return op in (1, 2, 3, 4, 5, 6, 7) or (op == 0 and (w & 63) in (8, 9))

    def target(a, w):
        op = w >> 26
        if op in (1, 4, 5, 6, 7):
            imm = w & 0xFFFF
            simm = imm - 0x10000 if imm & 0x8000 else imm
            return a + 4 + (simm << 2)
        if op in (2, 3):
            return ((a + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        return None
    nloads = 0
    bad = []
    for pc in sorted(cov):
        if not (0x80020000 <= pc < 0x800CA800):
            continue
        w = word(pc)
        if not is_load(w):
            continue
        dst = (w >> 16) & 31
        if dst == 0:
            continue
        nloads += 1
        succ = [pc + 4]
        pw = word(pc - 4)
        if is_branch(pw):                                       # the load sits in a delay slot: next executed = branch target or pc + 4
            t = target(pc - 4, pw)
            if t is not None:
                succ.append(t)
            elif (pw >> 26) == 0:                               # jr / jalr: target unknown statically; the load result must not be $ra-used: flag it
                succ.append(None)
        for sx in succ:
            if sx is None:
                bad.append((pc, 'load in a jr/jalr delay slot', w, 0, 0))
                continue
            nw = word(sx)
            pair = (w >> 26) == 0x22 and (nw >> 26) == 0x26 and ((nw >> 16) & 31) == dst      # lwl; lwr on the same register: the architecture's unaligned-load idiom, allowed
            if dst in reads(nw) and not pair:
                bad.append((pc, 'next uses the loaded register', w, sx, word(sx)))
    L = ['executed distinct instructions: %d; executed loads checked: %d; loaded register read by the next instruction: %d' % (len(cov), nloads, len(bad))]
    for b in bad:
        L.append('  HAZARD load %08x: %s (load word %08x, next %08x word %08x)' % b)
    L.append('VERDICT: ' + ('no executed load is followed by a use of its register: ignoring the load delay slot does not change the result' if not bad else 'HAZARDS FOUND'))
    open(os.path.join(outdir, 'load-delay-check.txt'), 'w', encoding='ascii').write('\n'.join(L) + '\n')
    print('\n'.join(L[:6]))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
