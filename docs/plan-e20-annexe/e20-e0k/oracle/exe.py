"""Helpers: read ALUN_CD.EXE (France) by virtual address, disassemble with capstone.
Usage:
  python -I exe.py dis START [END]      disassemble [START, END) (hex), default 64 insns
  python -I exe.py words ADDR N         dump N words
  python -I exe.py bytes ADDR N         dump N bytes
  python -I exe.py fn ADDR              disassemble until the first jr ra (+ delay slot) after ADDR
"""
import struct, sys
sys.path.append(r'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')  # capstone lives in the user site, which -I disables
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN

EXE = "D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE = 0x80020000


def off(addr):
    return 0x800 + addr - BASE


def inrange(addr):
    return BASE <= addr < BASE + len(DATA) - 0x800


def u8(a):
    return DATA[off(a)]


def s8(a):
    v = u8(a)
    return v - 256 if v >= 128 else v


def u16(a):
    return struct.unpack_from("<H", DATA, off(a))[0]


def s16(a):
    return struct.unpack_from("<h", DATA, off(a))[0]


def u32(a):
    return struct.unpack_from("<I", DATA, off(a))[0]


def s32(a):
    return struct.unpack_from("<i", DATA, off(a))[0]


md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = False
md.skipdata = True


def dis(start, end=None, n=64):
    if end is None:
        end = start + 4 * n
    code = DATA[off(start):off(end)]
    out = []
    lui = {}
    for ins in md.disasm(code, start):
        note = ""
        ops = ins.op_str
        if ins.mnemonic == "lui":
            r, imm = ops.split(", ")
            lui[r] = int(imm, 0)
        elif ins.mnemonic in ("addiu", "ori") and ops.split(", ")[1] in lui:
            parts = ops.split(", ")
            hi = lui[parts[1]]
            imm = int(parts[2], 0)
            if ins.mnemonic == "addiu" and imm >= 0x8000:
                imm -= 0x10000
            note = "   ; =0x%08X" % (((hi << 16) + imm) & 0xFFFFFFFF)
        elif ins.mnemonic in ("lw", "sw", "lh", "lhu", "sh", "lb", "lbu", "sb") and "(" in ops:
            parts = ops.split(", ")
            disp, base = parts[1].split("(")
            base = base.rstrip(")")
            if base in lui:
                d = int(disp, 0) if disp else 0
                note = "   ; [0x%08X]" % (((lui[base] << 16) + d) & 0xFFFFFFFF)
        out.append("%08X: %08X  %-6s %s%s" % (ins.address, struct.unpack_from("<I", code, ins.address - start)[0], ins.mnemonic, ops, note))
    return out


def fn(start, maxn=4000):
    # until first jr ra then delay slot, but skip if more branches go further: keep simple
    code_end = start
    lastjr = None
    for i in range(maxn):
        a = start + 4 * i
        w = u32(a)
        if w == 0x03E00008:  # jr ra
            lastjr = a
            return dis(start, a + 8)
    return dis(start, start + 4 * maxn)


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "dis":
        s = int(sys.argv[2], 16)
        e = int(sys.argv[3], 16) if len(sys.argv) > 3 else None
        print("\n".join(dis(s, e)))
    elif cmd == "fn":
        print("\n".join(fn(int(sys.argv[2], 16))))
    elif cmd == "words":
        a = int(sys.argv[2], 16)
        n = int(sys.argv[3])
        for i in range(n):
            print("%08X: %08X" % (a + 4 * i, u32(a + 4 * i)))
    elif cmd == "bytes":
        a = int(sys.argv[2], 16)
        n = int(sys.argv[3])
        for i in range(0, n, 16):
            print("%08X: %s" % (a + i, " ".join("%02X" % u8(a + i + j) for j in range(min(16, n - i)))))
