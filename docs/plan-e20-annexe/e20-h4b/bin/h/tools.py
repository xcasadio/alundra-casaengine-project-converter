"""tools.py - own read-only helpers over ALUN_CD.EXE (France): disassembly, jal xrefs, data xrefs (lui + offset with register tracking).
usage:
  python -B tools.py dis START END
  python -B tools.py jal TARGET [TARGET...]
  python -B tools.py ref ADDR [ADDR...]          (data references; matches lui/addiu/ori + load/store offset, exact address)
  python -B tools.py refrange LO HI              (data references in [LO, HI))
  python -B tools.py func ADDR                   (disassemble from ADDR to the next 'jr ra' + delay slot)
Read-only: reads the EXE, writes nothing."""
import struct
import sys
import capstone

EXE_PATH = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
with open(EXE_PATH, "rb") as _f:
    DATA = _f.read()
PC0, GP0, T_ADDR, T_SIZE = struct.unpack_from("<IIII", DATA, 0x10)
T_END = T_ADDR + T_SIZE

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True


def off(a):
    return 0x800 + (a - T_ADDR)


def w32(a):
    return struct.unpack_from("<I", DATA, off(a))[0]


def ins_at(a):
    r = list(md.disasm(struct.pack("<I", w32(a)), a))
    return r[0] if r else None


def text(a):
    i = ins_at(a)
    w = w32(a)
    return f"{a:08X}: {w:08X}  " + (f"{i.mnemonic} {i.op_str}" if i else ".word")


def dis(lo, hi):
    for a in range(lo, hi, 4):
        print(text(a))


def jal_sites(t):
    out = []
    for a in range(T_ADDR, T_END, 4):
        w = w32(a)
        if (w >> 26) == 3 and (((w & 0x03FFFFFF) << 2) | 0x80000000) == t:
            out.append(a)
    return out


def func(a):
    b = a
    while b < T_END:
        w = w32(b)
        if w == 0x03E00008:            # jr ra
            dis(a, b + 8)
            return
        b += 4
    dis(a, b)


BRANCH_MN = {"beq", "bne", "blez", "bgtz", "bltz", "bgez", "beqz", "bnez", "b", "bal", "bltzal", "bgezal", "j", "jal", "jr", "jalr", "bnel", "beql"}


def data_refs(lo, hi):
    """all load/store/addiu/ori that, via a tracked lui register, address a value in [lo, hi). Register tracking is linear (no CFG): any
    write to the register other than lui/addiu/ori-from-itself drops the tracking; the result is a SUPERSET to be read, not a proof."""
    regs = {}
    res = []
    for a in range(T_ADDR, T_END, 4):
        i = ins_at(a)
        if i is None:
            continue
        m = i.mnemonic
        ops = [o.strip() for o in i.op_str.split(",")]
        try:
            if m == "lui":
                regs[ops[0]] = int(ops[1], 0) << 16
                continue
            if m in ("addiu", "ori", "addi"):
                rd, rs, imm = ops
                imm = int(imm, 0)
                if rs in regs:
                    v = (regs[rs] + imm) & 0xFFFFFFFF if m != "ori" else regs[rs] | (imm & 0xFFFF)
                    if lo <= v < hi:
                        res.append((a, f"{m} {i.op_str} -> {v:#010x}"))
                    regs[rd] = v
                else:
                    regs.pop(rd, None)
                continue
            if m in ("lw", "sw", "lh", "lhu", "sh", "lb", "lbu", "sb", "lwl", "lwr", "swl", "swr"):
                rt, mem = ops[0], ops[1]
                o, base = mem.rstrip(")").split("(")
                o = int(o, 0)
                if base in regs:
                    v = (regs[base] + o) & 0xFFFFFFFF
                    if lo <= v < hi:
                        res.append((a, f"{m} {i.op_str} -> {v:#010x}"))
                if m[0] == "l" and rt in regs:
                    regs.pop(rt, None)
                continue
            if m in BRANCH_MN:
                continue
            # any other instruction writing a register: drop that register
            if ops and ops[0] in regs and m not in ("sw", "sh", "sb"):
                regs.pop(ops[0], None)
        except Exception:
            pass
    return res


if __name__ == "__main__":
    c = sys.argv[1]
    if c == "dis":
        dis(int(sys.argv[2], 16), int(sys.argv[3], 16))
    elif c == "jal":
        for t in sys.argv[2:]:
            s = jal_sites(int(t, 16))
            print(f"jal {t}: {len(s)} sites: " + ", ".join(f"{x:#010x}" for x in s))
    elif c == "ref":
        for t in sys.argv[2:]:
            t = int(t, 16)
            r = data_refs(t, t + 1)
            print(f"ref {t:#010x}: {len(r)}")
            for a, s in r:
                print(f"  {a:#010x}  {s}")
    elif c == "refrange":
        for a, s in data_refs(int(sys.argv[2], 16), int(sys.argv[3], 16)):
            print(f"{a:#010x}  {s}")
    elif c == "func":
        func(int(sys.argv[2], 16))
