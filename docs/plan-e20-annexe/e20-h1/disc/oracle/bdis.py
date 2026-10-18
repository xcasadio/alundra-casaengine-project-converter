"""My own capstone disassembler for ALUN_CD.EXE (France). Read-only.
Usage: python -B dis.py START [END|+LEN]   (hex virtual addresses). Annotates lui/addiu pairs and jal targets."""
import sys, struct, hashlib
import capstone
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
assert hashlib.sha1(DATA).hexdigest() == "a705f8dab665645af5dfaf8fb348c7926b5caa58"
PC0, GP0, TADDR, TSIZE = struct.unpack_from("<IIII", DATA, 0x10)
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
def off(a): return 0x800 + (a - TADDR)
def u32(a): return struct.unpack_from("<I", DATA, off(a))[0]
def s32(a):
    v = u32(a); return v - (1<<32) if v & 0x80000000 else v
def u16(a): return struct.unpack_from("<H", DATA, off(a))[0]
def u8(a): return DATA[off(a)]
def dis(a, b):
    code = DATA[off(a):off(b)]
    out = []
    for i in md.disasm(code, a):
        out.append(f"{i.address:08x}  {u32(i.address):08x}  {i.mnemonic:<7} {i.op_str}")
    return out
if __name__ == "__main__":
    a = int(sys.argv[1], 16)
    e = sys.argv[2]
    b = a + int(e[1:], 16) if e.startswith("+") else int(e, 16)
    print("\n".join(dis(a, b)))
