"""Independent MIPS-I interpreter for CATE (0x8003A7B0..0x8003A9DC), written from scratch by the critic.
Decodes the raw words from ALUN_CD.EXE; the 3 callees are recorded. Compares to the report's pseudo-code.
Read-only on the repository / binary."""
import random, struct, sys
import exelib

BASE = 0x8003A7B0
END = 0x8003A9E0

M = {}  # sparse memory, byte addressed

def w8(a, v): M[a] = v & 0xFF
def r8(a): return M.get(a, 0xAA)
def w16(a, v): w8(a, v); w8(a + 1, v >> 8)
def r16(a): return r8(a) | (r8(a + 1) << 8)
def w32(a, v): w16(a, v); w16(a + 2, v >> 16)
def r32(a): return r16(a) | (r16(a + 2) << 16)
def s32(v): v &= 0xFFFFFFFF; return v - (1 << 32) if v & 0x80000000 else v
def s16(v): v &= 0xFFFF; return v - 0x10000 if v & 0x8000 else v
def u32(v): return v & 0xFFFFFFFF

def run(regs_init, calls):
    R = [0] * 32
    for k, v in regs_init.items(): R[k] = u32(v)

    def simple(w):
        """execute a non-branching instruction word"""
        op = w >> 26; rs = (w >> 21) & 31; rt = (w >> 16) & 31; rd = (w >> 11) & 31
        sh = (w >> 6) & 31; fn = w & 63; imm = w & 0xFFFF; simm = s16(imm)
        def setr(i, v):
            if i: R[i] = u32(v)
        if w == 0: return
        if op == 0:
            if fn == 0: setr(rd, R[rt] << sh)
            elif fn == 3: setr(rd, s32(R[rt]) >> sh)
            elif fn == 0x21: setr(rd, R[rs] + R[rt])
            elif fn == 0x25: setr(rd, R[rs] | R[rt])
            elif fn == 0x2A: setr(rd, 1 if s32(R[rs]) < s32(R[rt]) else 0)
            elif fn == 0x2B: setr(rd, 1 if R[rs] < R[rt] else 0)
            else: raise Exception("fn %x" % fn)
        elif op == 9: setr(rt, R[rs] + simm)
        elif op == 0xA: setr(rt, 1 if s32(R[rs]) < simm else 0)
        elif op == 0xC: setr(rt, R[rs] & imm)
        elif op == 0xD: setr(rt, R[rs] | imm)
        elif op == 0xF: setr(rt, imm << 16)
        elif op == 0x23: setr(rt, r32(u32(R[rs] + simm)))
        elif op == 0x21: setr(rt, s16(r16(u32(R[rs] + simm))))
        elif op == 0x25: setr(rt, r16(u32(R[rs] + simm)))
        elif op == 0x24: setr(rt, r8(u32(R[rs] + simm)))
        elif op == 0x2B: w32(u32(R[rs] + simm), R[rt])
        elif op == 0x29: w16(u32(R[rs] + simm), R[rt])
        else: raise Exception("op %x" % op)

    pc = BASE
    steps = 0
    while True:
        steps += 1
        assert steps < 5000
        w = exelib.u32(pc)
        op = w >> 26; rs = (w >> 21) & 31; rt = (w >> 16) & 31
        fn = w & 63; simm = s16(w & 0xFFFF)
        is_branch = False
        if op == 0 and fn == 8:
            is_branch = True; target = R[rs]; taken = True
        elif op == 2:
            is_branch = True; target = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2); taken = True
        elif op == 3:
            tgt = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
            simple(exelib.u32(pc + 4))
            calls.append((tgt, R[4], R[5], R[6], R[7], r32(R[29] + 0x10), r32(R[29] + 0x14), r32(R[29] + 0x18)))
            R[31] = pc + 8; R[2] = 0xDEAD
            pc += 8
            continue
        elif op in (4, 5, 6, 7):
            a, b = s32(R[rs]), s32(R[rt])
            taken = {4: a == b, 5: a != b, 6: a <= 0, 7: a > 0}[op]
            target = pc + 4 + (simm << 2)
            is_branch = True
        if is_branch:
            simple(exelib.u32(pc + 4))
            if taken:
                if op == 0 and fn == 8:
                    return R
                pc = target
            else:
                pc += 8
            continue
        simple(w)
        pc += 4

ndiff = 0
import collections
cause = collections.Counter()
T_BASE = 0x80127168
MAPPTR = 0x800DD86C
HDRPTR = 0x800E4334
E = 0x80127D30
MAP = 0x80300000
HDR = 0x80310000

def setup(box, cells, hdr9, ttab_oob):
    M.clear()
    bx, by, bz, bw, bd, bh = box
    w32(E + 0x1D4, 0x80500000)
    w32(E + 0x1FC, bx); w32(E + 0x200, by); w32(E + 0x204, bz)
    w32(E + 0x214, bw); w32(E + 0x218, bd); w32(E + 0x21C, bh)
    for p in range(1248): w16(T_BASE + 2 * p, p // 24)
    for p in range(-64, 0): w16(T_BASE + 2 * p, ttab_oob.get(p, random.randrange(0x10000)))
    for p in range(1248, 1248 + 400): w16(T_BASE + 2 * p, ttab_oob.get(p, random.randrange(0x10000)))
    w32(MAPPTR, MAP); w32(HDRPTR, HDR)
    w8(HDR + 9, hdr9)
    for (c, r), (fl, h, off) in cells.items():
        a = MAP + 0x604 + r * 0x1A0 + c * 8
        w16(a, fl); w8(a + 3, h); w16(a + 6, off)
    return

def cell_addr(c, r): return MAP + 0x604 + r * 0x1A0 + c * 8

def pseudo(box, cells_mem, hdr9, ttab):
    bx, by, bz, bw, bd, bh = box
    out = []
    # box is given as unsigned 32 -> convert
    sbx, sby, sbz, sbw, sbd, sbh = [s32(x) for x in box]
    colA = ttab(s16((sbx >> 16) & 0xFFFF))
    colB = ttab(s32(sbx + sbw) >> 16)
    rowA = sby >> 20
    rowB = s32(sby + sbd) >> 20
    seq = [(colA, rowA), (colB, rowA), (colA, rowB), (colB, rowB)]
    for col, row in seq:
        c = 0 if col <= 0 else (col if col < 0x34 else 0x33)
        r = 0 if row <= 0 else (row if row < 0x3C else 0x3B)
        a = cell_addr(c, r)
        fl = cells_mem[a][0]
        if not fl & 2: continue
        z0 = (cells_mem[a][1] & 0xFF) << 20
        zc = z0 + 0x80000
        if zc < sbz: continue
        if sbz + sbh < zc: continue
        cells_mem[a] = (fl & 0xFFFD, cells_mem[a][1], 0xFFFF)
        X = u32(col * 0x180000 + 0xC0000)
        Y = u32(row * 0x100000 + 0x80000)
        out.append(("cee", 0, hdr9, 0, X, Y, z0))
        out.append(("loot", 0xFF, X, Y, z0))
        out.append(("snd", 0x1F))
    return out


def decomp(box, cells_mem, hdr9):
    sbx, sby, sbz, sbw, sbd, sbh = [s32(x) for x in box]
    out = []
    if (sbx >> 16) >= 1248 or ((sbx + sbw) >> 16) >= 1248:
        return out, "guard"
    def idiv(a, b):
        q = abs(a) // abs(b)
        return q if (a >= 0) == (b > 0) else -q
    colA = idiv(sbx >> 16, 24); colB = idiv((sbx + sbw) >> 16, 24)
    rowA = idiv(sby >> 16, 16); rowB = idiv((sby + sbd) >> 20, 16)
    seq = [(colA, rowA), (colB, rowA), (colA, rowB), (colB, rowB)]
    for col, row in seq:
        c = 0 if col < 1 else (col if col <= 0x33 else 0x33)
        r = 0 if row < 1 else (row if row <= 0x3B else 0x3B)
        a = cell_addr(c, r)
        fl = cells_mem[a][0]
        if not fl & 2: continue
        zc = ((cells_mem[a][1] & 0xFF) << 20) + 0x80000
        if not (sbz <= zc <= sbz + sbh): continue
        cells_mem[a] = (fl & 0xFFFD, cells_mem[a][1], 0xFFFF)
        X = u32(col * 0x180000 + 0xC0000); Y = u32(row * 0x100000 + 0x80000)
        out.append(("cee", 0, hdr9, 0, X, Y, zc)); out.append(("loot", 0xFF, X, Y, zc)); out.append(("snd", 0x1F))
    return out, None

def main(n):
    random.seed(12345)
    mism = 0; cuts = 0
    for it in range(n):
        # choose cell data and box
        mode = random.random()
        bw = random.choice([0x80000 - 1, 0x100000 - 1, 0x180000 - 1, 0x2FFFFF, random.randrange(1, 0x400000)])
        bd = random.choice([0x80000 - 1, 0x100000 - 1, random.randrange(1, 0x400000)])
        bh = random.choice([0x80000 - 1, 0x100000 - 1, 0x400000, random.randrange(0, 0x800000)])
        if mode < 0.8:
            bx = random.randrange(0, 1248 << 16 - 0) if False else random.randrange(0, (1248 << 16))
            by = random.randrange(0, 960 << 16) >> 0
        else:
            bx = random.randrange(-(40 << 16), (1300 << 16)); by = random.randrange(-(40 << 16), (1000 << 16))
        bz = random.randrange(0, 0x1F00000)
        if random.random() < 0.6:
            # targeted: put the box corner on a cell and the Z on a boundary of that cell
            c0 = random.randrange(0, 52); r0 = random.randrange(0, 60)
            bx = (c0 * 24 + random.randrange(0, 24)) << 16
            by = (r0 * 16 + random.randrange(0, 16)) << 16
            h0 = random.randrange(0, 30)
            zc0 = (h0 << 20) + 0x80000
            bz = zc0 + random.choice([0, 1, -1, 0x80000, -bh, -bh + 1, -bh - 1, 5, -5, bh // 2 if False else 0])
            if bz < 0: bz = 0
        cells = {}
        tcell = None
        # populate the neighbourhood (and the clamped places) with random flagged cells
        for c in range(0, 52):
            for r in range(0, 60):
                if random.random() < 0.5:
                    h = random.randrange(0, 30)
                    cells[(c, r)] = (random.choice([2, 3, 0x42, 0, 0x102, 0xFF02]) if random.random() < 0.7 else 0, h, random.choice([0x1234, 0, 0xFFFF]))
        if 'c0' in dir() and random.random() < 0.9:
            for dc in (0, 1, 2):
                for dr in (0, 1, 2):
                    cc, rr = c0 + dc, r0 + dr
                    if 0 <= cc < 52 and 0 <= rr < 60:
                        cells[(cc, rr)] = (random.choice([2, 3, 0x42, 0x102]), h0 if random.random() < 0.7 else random.randrange(0, 30), random.choice([0x1234, 0xFFFF, 0]))
        # fill cells that ARE in memory by default: unspecified cells default to flags 0
        # force exact-boundary heights sometimes
        box = tuple(u32(x) for x in (bx, by, bz, bw, bd, bh))
        oob = {}
        setup(box, cells, 2, oob)
        # snapshot memory view for pseudo: dict addr -> (flags, h, off)
        mem = {}
        for c in range(52):
            for r in range(60):
                a = cell_addr(c, r)
                mem[a] = (r16(a) if (a in M or True) else 0, r8(a + 3) if (a + 3) in M else 0xAA, r16(a + 6) if (a + 6) in M else 0xAA)
        # unset cells read 0xAA bytes -> flags 0xAAAA (bit1 set!): fix by setting explicit zero for non-listed cells
        for c in range(52):
            for r in range(60):
                if (c, r) not in cells:
                    a = cell_addr(c, r); w16(a, 0); w8(a + 3, 0); w16(a + 6, 0x1234)
                    mem[a] = (0, 0, 0x1234)
                else:
                    a = cell_addr(c, r); mem[a] = (r16(a), r8(a + 3), r16(a + 6))
        def ttab(p):
            if 0 <= p < 1248: return p // 24
            v = r16(T_BASE + 2 * p)
            return s16(v)
        pre = dict(mem)
        calls = []
        regs = {4: E, 29: 0x801FFF00}
        # stack frame for args lives at sp
        run(regs, calls)
        # actual outputs
        got = []
        for (t, a0, a1, a2, a3, s10, s14, s18) in calls:
            if t == 0x8003BDD8: got.append(("cee", a0, a1, a2, a3, s10, s14))
            elif t == 0x80032C7C: got.append(("loot", a0, a1, a2, a3))
            elif t == 0x800490FC: got.append(("snd", a0))
            else: got.append(("?", t))
        exp = pseudo(box, mem, 2, ttab)
        # compare memory of cells afterwards
        memafter = {}
        for c in range(52):
            for r in range(60):
                a = cell_addr(c, r); memafter[a] = (r16(a), r8(a + 3), r16(a + 6))
        import copy
        mem2 = {}
        # rebuild pre-run memory view for decomp from the same cell definitions
        for c in range(52):
            for r in range(60):
                a = cell_addr(c, r)
                mem2[a] = pre[a]
        dout, why = decomp(box, mem2, 2)
        if dout != got or mem2 != memafter:
            global ndiff, cause
            ndiff += 1
            # classify: compare with same-rows/different-Z etc
            if why == "guard": cause["guard"] += 1
            else:
                cause["other"] += 1
        if got != exp or memafter != mem:
            mism += 1
            if mism <= 5:
                print("MISMATCH", it, [hex(x) for x in box]); print(" got", got); print(" exp", exp)
        cuts += len([g for g in got if g[0] == "cee"])
    print("vectors", n, "mismatches", mism, "cuts", cuts)
    print("decompilation differs from binary on", ndiff, "vectors;", dict(cause))

if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 2000)
