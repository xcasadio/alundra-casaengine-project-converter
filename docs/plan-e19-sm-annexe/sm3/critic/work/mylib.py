# Critic-own binary access (written from scratch). Read-only on ALUN_CD.EXE.
import struct
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
assert DATA[:8] == b"PS-X EXE"
TADDR = struct.unpack_from("<I", DATA, 0x18)[0]
TSIZE = struct.unpack_from("<I", DATA, 0x1C)[0]
def off(a):
    o = 0x800 + a - TADDR
    assert 0x800 <= o < len(DATA), hex(a)
    return o
def u32(a): return struct.unpack_from("<I", DATA, off(a))[0]
def s16(a): return struct.unpack_from("<h", DATA, off(a))[0]
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = False
def dis(a, n):
    return list(md.disasm(DATA[off(a):off(a)+4*n], a))
