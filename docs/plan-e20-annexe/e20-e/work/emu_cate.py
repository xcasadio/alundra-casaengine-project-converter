"""Mini MIPS-I interpreter for ALUN_CD.EXE (France): runs CheckAndTriggerTileEffect (0x8003A7B0) on a synthetic memory
and compares the effects with an independent Python pseudo-code (the pseudo-code of report.md).
Read-only on the EXE (through exelib). Writes only its own output next to itself.
Usage: python -B emu_cate.py [N]"""
import random
import struct
import sys

import exelib as e

M32 = 0xFFFFFFFF


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


class Mem:
    def __init__(self):
        self.d = {}  # byte addressed overlay

    def rb(self, a):
        a &= M32
        if a in self.d:
            return self.d[a]
        try:
            return e.DATA[e.off(a)]
        except ValueError:
            return 0

    def wb(self, a, v):
        self.d[a & M32] = v & 0xFF

    def rh(self, a):
        return self.rb(a) | (self.rb(a + 1) << 8)

    def rw(self, a):
        return self.rh(a) | (self.rh(a + 2) << 16)

    def wh(self, a, v):
        self.wb(a, v); self.wb(a + 1, v >> 8)

    def ww(self, a, v):
        self.wh(a, v); self.wh(a + 2, v >> 16)


class Cpu:
    def __init__(self, mem, stubs):
        self.m = mem
        self.r = [0] * 32
        self.pc = 0
        self.stubs = stubs  # addr -> python callable(cpu)
        self.hi = self.lo = 0
        self.steps = 0

    def run(self, entry, ra_sentinel=0xDEAD0000, limit=200000):
        self.pc = entry
        self.r[31] = ra_sentinel
        self.r[29] = 0x801FFF00
        npc = None
        while True:
            if self.pc == ra_sentinel:
                return
            self.steps += 1
            if self.steps > limit:
                raise RuntimeError("step limit")
            if self.pc in self.stubs:
                self.stubs[self.pc](self)
                self.pc = self.r[31]
                continue
            w = self.m.rw(self.pc)
            self.exec_one(w)

    # one instruction + its delay slot
    def exec_one(self, w):
        pc = self.pc
        op = w >> 26
        rs = (w >> 21) & 31; rt = (w >> 16) & 31; rd = (w >> 11) & 31; sh = (w >> 6) & 31
        imm = w & 0xFFFF; simm = imm - 0x10000 if imm & 0x8000 else imm
        r = self.r
        target = None  # branch target if taken
        link = False
        if op == 0:
            f = w & 63
            if f == 0: r[rd] = (r[rt] << sh) & M32
            elif f == 2: r[rd] = (r[rt] & M32) >> sh
            elif f == 3: r[rd] = (s32(r[rt]) >> sh) & M32
            elif f == 8: target = r[rs]
            elif f == 0x21: r[rd] = (r[rs] + r[rt]) & M32
            elif f == 0x23: r[rd] = (r[rs] - r[rt]) & M32
            elif f == 0x24: r[rd] = r[rs] & r[rt]
            elif f == 0x25: r[rd] = r[rs] | r[rt]
            elif f == 0x2A: r[rd] = 1 if s32(r[rs]) < s32(r[rt]) else 0
            elif f == 0x2B: r[rd] = 1 if (r[rs] & M32) < (r[rt] & M32) else 0
            elif f == 0x00 and w == 0: pass
            else: raise RuntimeError(f"funct {f:#x} at {pc:#x}")
        elif op == 2: target = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        elif op == 3: target = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2); link = True
        elif op == 4:
            if r[rs] == r[rt]: target = pc + 4 + (simm << 2)
        elif op == 5:
            if r[rs] != r[rt]: target = pc + 4 + (simm << 2)
        elif op == 6:
            if s32(r[rs]) <= 0: target = pc + 4 + (simm << 2)
        elif op == 7:
            if s32(r[rs]) > 0: target = pc + 4 + (simm << 2)
        elif op == 1:
            if rt == 0 and s32(r[rs]) < 0: target = pc + 4 + (simm << 2)
            elif rt == 1 and s32(r[rs]) >= 0: target = pc + 4 + (simm << 2)
            elif rt not in (0, 1): raise RuntimeError("regimm")
        elif op == 9: r[rt] = (r[rs] + simm) & M32
        elif op == 0xA: r[rt] = 1 if s32(r[rs]) < simm else 0
        elif op == 0xB: r[rt] = 1 if (r[rs] & M32) < (simm & M32) else 0
        elif op == 0xC: r[rt] = r[rs] & imm
        elif op == 0xD: r[rt] = r[rs] | imm
        elif op == 0xF: r[rt] = (imm << 16) & M32
        elif op == 0x20:
            v = self.m.rb(r[rs] + simm); r[rt] = (v - 256 if v & 0x80 else v) & M32
        elif op == 0x21:
            v = self.m.rh(r[rs] + simm); r[rt] = (v - 65536 if v & 0x8000 else v) & M32
        elif op == 0x23: r[rt] = self.m.rw(r[rs] + simm)
        elif op == 0x24: r[rt] = self.m.rb(r[rs] + simm)
        elif op == 0x25: r[rt] = self.m.rh(r[rs] + simm)
        elif op == 0x28: self.m.wb(r[rs] + simm, r[rt])
        elif op == 0x29: self.m.wh(r[rs] + simm, r[rt])
        elif op == 0x2B: self.m.ww(r[rs] + simm, r[rt])
        else:
            raise RuntimeError(f"opcode {op:#x} at {pc:#x}")
        r[0] = 0
        if op in (2, 3, 4, 5, 6, 7, 1) or (op == 0 and (w & 63) == 8):
            # delay slot
            if link: r[31] = pc + 8
            self.pc = pc + 4
            self.exec_one_slot()
            self.pc = target if target is not None else pc + 8
        else:
            self.pc = pc + 4

    def exec_one_slot(self):
        w = self.m.rw(self.pc)
        saved = self.pc
        # a delay slot never branches in the code we run
        op = w >> 26
        assert op not in (2, 3, 4, 5, 6, 7, 1), "branch in delay slot"
        self.exec_one(w)


# ---------- synthetic world ----------
ENT = 0x80127D30        # the hero address (any address works)
MAPBASE = 0x80300000    # pointer value stored at 0x800DD86C
HDR = 0x80320000        # pointer value stored at 0x800E4334
TABLE = 0x80127168


def build(mem, box, cells, hdr9):
    """box = dict(x, y, z, w, d, h, frame=1/0); cells = dict (col,row)->(flags16, height, wallofs16)"""
    for i in range(1248 + 64):
        mem.wh(TABLE + 2 * i, i // 24 if i < 1248 else 0x7777)   # past the table: marker 0x7777 (unknown in reality)
    mem.ww(0x800DD86C, MAPBASE)
    mem.ww(0x800E4334, HDR)
    mem.wb(HDR + 9, hdr9)
    mem.ww(ENT + 0x1D4, 0x80400000 if box["frame"] else 0)
    mem.ww(ENT + 0x1FC, box["x"] & M32)
    mem.ww(ENT + 0x200, box["y"] & M32)
    mem.ww(ENT + 0x204, box["z"] & M32)
    mem.ww(ENT + 0x214, box["w"] & M32)
    mem.ww(ENT + 0x218, box["d"] & M32)
    mem.ww(ENT + 0x21C, box["h"] & M32)
    for row in range(60):
        for col in range(52):
            a = MAPBASE + 0x604 + row * 416 + col * 8
            fl, ht, wo = cells.get((col, row), (0, 0, 0xFFFF))
            mem.wh(a, fl); mem.wb(a + 2, 0); mem.wb(a + 3, ht); mem.wh(a + 4, 0x1234); mem.wh(a + 6, wo)


def run_binary(box, cells, hdr9):
    mem = Mem()
    build(mem, box, cells, hdr9)
    log = []

    def create_effect(c):
        sp = c.r[29]
        log.append(("effect", c.r[4], c.r[5], c.r[6], s32(c.r[7]), s32(mem.rw(sp + 0x10)), s32(mem.rw(sp + 0x14))))

    def spawn_item(c):
        log.append(("loot", c.r[4], s32(c.r[5]), s32(c.r[6]), s32(c.r[7])))

    def sound(c):
        log.append(("sound", c.r[4]))

    cpu = Cpu(mem, {0x8003BDD8: create_effect, 0x80032C7C: spawn_item, 0x800490FC: sound})
    cpu.r[4] = ENT
    cpu.run(0x8003A7B0)
    cells_after = {}
    for row in range(60):
        for col in range(52):
            a = MAPBASE + 0x604 + row * 416 + col * 8
            cells_after[(col, row)] = (mem.rh(a), mem.rb(a + 3), mem.rh(a + 4), mem.rh(a + 6))
    return log, cells_after


def col_of(px):
    if px < 0 or px >= 1248:
        return None
    return px // 24


def pseudo(box, cells, hdr9):
    """The pseudo-code of the report, independent of the machine."""
    log = []
    cells = {k: list(v) for k, v in cells.items()}
    if not box["frame"]:
        return log, cells
    def lh_hi(x):  # lh 0x1fe(e): signed 16-bit high half
        v = (x >> 16) & 0xFFFF
        return v - 0x10000 if v & 0x8000 else v
    xa = lh_hi(box["x"] & M32); xb = s32((box["x"] + box["w"]) & M32) >> 16
    ya = s32(box["y"] & M32) >> 20; yb = s32((box["y"] + box["d"]) & M32) >> 20
    def tbl(px):
        assert 0 <= px < 1248
        return px // 24
    colA, colB = tbl(xa), tbl(xb)
    corners = [(colA, ya), (colB, ya), (colA, yb), (colB, yb)]
    for col, row in corners:
        c = min(max(col, 0), 0x33) if col > 0 else 0
        r = min(max(row, 0), 0x3B) if row > 0 else 0
        fl, ht, wo = cells.get((c, r), (0, 0, 0xFFFF))
        if not (fl & 2):
            continue
        z0 = ht << 20
        zc = z0 + 0x80000
        if zc < s32(box["z"] & M32):
            continue
        if s32(box["z"] & M32) + s32(box["h"] & M32) < zc:
            continue
        cells[(c, r)] = [fl & 0xFFFD, ht, 0xFFFF]
        x = col * 0x180000 + 0xC0000
        y = row * 0x100000 + 0x80000
        log.append(("effect", 0, hdr9, 0, x, y, z0))
        log.append(("loot", 0xFF, x, y, z0))
        log.append(("sound", 0x1F))
    return log, cells


def rand_case(rng):
    cells = {}
    ncell = rng.randint(0, 40)
    for _ in range(ncell):
        c = (rng.randint(0, 51), rng.randint(0, 59))
        cells[c] = (rng.choice([2, 3, 2, 0x82, 0x42, 0x0102, 0]), rng.randint(0, 8), rng.choice([0xFFFF, 0, 5, 77]))
    cx, cy = rng.randint(0, 51), rng.randint(0, 59)
    if rng.random() < 0.8:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if rng.random() < 0.6 and 0 <= cx + dx < 52 and 0 <= cy + dy < 60:
                    cells[(cx + dx, cy + dy)] = (rng.choice([2, 3, 0x82, 0, 0x102]), rng.randint(0, 3), 0xFFFF)
    x = (cx * 24 + rng.randint(-10, 30)) * 65536 + rng.randint(0, 65535)
    y = (cy * 16 + rng.randint(-8, 24)) * 65536 + rng.randint(0, 65535)
    x = min(max(x, 0), 1200 * 65536)
    y = min(max(y, 0), 940 * 65536)
    z = rng.randint(0, 5) * 0x100000 + rng.choice([0, 1, 0x7FFFF, 0x80000, 0x80001, 0x8FFFF, 0xFFFFF]) - rng.choice([0, 0x80000])
    box = dict(x=x, y=y, z=z, w=(rng.randint(1, 40) << 16) - 1, d=(rng.randint(1, 40) << 16) - 1, h=(rng.randint(1, 30) << 16) - 1, frame=1 if rng.random() < 0.9 else 0)
    # keep the right edge inside the table
    if (box["x"] + box["w"]) >> 16 >= 1248:
        box["w"] = (1247 << 16) - box["x"]
        box["w"] = max(box["w"], 0)
    return box, cells, rng.choice([2, 2, 2, 5, 0x11])


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
    rng = random.Random(20261017)
    bad = 0
    cuts = 0
    for i in range(n):
        box, cells, hdr9 = rand_case(rng)
        try:
            lb, cb = run_binary(box, cells, hdr9)
        except Exception as ex:
            print("emulator error", i, ex); bad += 1; continue
        lp, cp = pseudo(box, cells, hdr9)
        # compare logs and the final cell flags / wall offsets of every cell that exists in either
        same_log = lb == lp
        same_cells = True
        for k, v in cp.items():
            fb = cb[k]
            if (fb[0], fb[3]) != (v[0], v[2]):
                same_cells = False; break
        if not (same_log and same_cells):
            bad += 1
            if bad <= 5:
                print("MISMATCH", i, box, hdr9); print(" bin", lb); print(" pse", lp)
        cuts += sum(1 for t in lb if t[0] == "loot")
    print(f"vectors={n} mismatches={bad} cuts_seen={cuts}")
    return 1 if bad else 0




# ---------- the decompilation (GameEngine.cs:2441-2527) transcribed, to count how often it differs from the binary ----------
def decomp(box, cells, hdr9):
    log = []
    cells = {k: list(v) for k, v in cells.items()}
    if not box["frame"]:
        return log, cells
    X, Y, Z = s32(box["x"]), s32(box["y"]), s32(box["z"])
    def cdiv(a, b):  # C# integer division truncates toward zero
        q = abs(a) // abs(b)
        return q if (a >= 0) == (b >= 0) else -q
    xa = cdiv(X >> 16, 24); xb = cdiv(s32(X + box["w"]) >> 16, 24)
    ya = cdiv(Y >> 16, 16); yb = cdiv(s32(Y + box["d"]) >> 20, 16)      # the second division by 16 is in the decompilation
    cols = [xa, xb, xa, xb]; rows = [ya, ya, yb, yb]
    for col, row in zip(cols, rows):
        c = 0 if col < 1 else min(col, 0x33)
        r = 0 if row < 1 else min(row, 0x3B)
        fl, ht, wo = cells.get((c, r), (0, 0, 0xFFFF))
        if not (fl & 2):
            continue
        zc = (ht << 20) + 0x80000
        if Z <= zc <= Z + s32(box["h"]):
            cells[(c, r)] = [fl & 0xFFFD, ht, 0xFFFF]
            x = col * 0x180000 + 0xC0000; y = row * 0x100000 + 0x80000
            log.append(("effect", 0, hdr9, 0, x, y, zc)); log.append(("loot", 0xFF, x, y, zc)); log.append(("sound", 0x1F))
    return log, cells


def compare_decomp(n=4000):
    rng = random.Random(20261017)
    differ = same = 0
    only_z = rows_differ = 0
    for _ in range(n):
        box, cells, hdr9 = rand_case(rng)
        lb, cb = run_binary(box, cells, hdr9)
        ld, cd = decomp(box, cells, hdr9)
        cells_same = all((cb[k][0], cb[k][3]) == (v[0], v[2]) for k, v in cd.items())
        if lb == ld and cells_same:
            same += 1
            continue
        differ += 1
        # strip the Z of the effect and loot to see if only the Z differs
        strip = lambda l: [t[:-1] if t[0] in ("effect", "loot") else t for t in l]
        if strip(lb) == strip(ld) and cells_same:
            only_z += 1
        elif not cells_same or [t for t in lb if t[0] == "sound"] != [t for t in ld if t[0] == "sound"]:
            rows_differ += 1
    print(f"decompilation vs binary on {n} vectors: identical={same} differ={differ} (only the Z of the effect/loot differs={only_z}; other cause, i.e. rows/cells/counts={rows_differ})")


if __name__ == "__main__":
    # python -B emu_cate.py N          : binary (emulated) vs the report's pseudo-code
    # python -B emu_cate.py N decomp   : binary (emulated) vs the decompilation transcribed
    if len(sys.argv) > 2 and sys.argv[2] == "decomp":
        compare_decomp(int(sys.argv[1]))
    else:
        sys.exit(main())
