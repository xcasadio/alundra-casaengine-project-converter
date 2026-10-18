import sys, struct, re
sys.path.append(r'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')  # capstone is in the user site, which -I disables
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE = 0x80020000
OFF = 0x800
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
def u32(a):
    o = a - BASE + OFF
    return struct.unpack_from("<I", DATA, o)[0]
def dis(a, n):
    o = a - BASE + OFF
    return list(md.disasm(DATA[o:o+4*n], a))

class _I:
    __slots__ = ('address','mnemonic','op_str')
    def __init__(s,a,m,o): s.address=a; s.mnemonic=m; s.op_str=o
_ALL = None
def all_ins():
    global _ALL
    if _ALL is None:
        _ALL = []
        N = (len(DATA)-OFF)//4
        for k in range(0, N, 4096):
            chunk = DATA[OFF+4*k: OFF+4*min(N,k+4096)]
            for j in range(0, len(chunk), 4):
                a = BASE + 4*(k+j//4)
                r = list(md.disasm(chunk[j:j+4], a))
                if r: _ALL.append(r[0])
                else: _ALL.append(_I(a, '.word', '0x%08x' % struct.unpack_from('<I', chunk, j)[0]))
    return _ALL
