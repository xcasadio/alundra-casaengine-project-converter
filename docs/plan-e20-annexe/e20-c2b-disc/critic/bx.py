"""Binary helper for ALUN_CD.EXE (France). Run with python -I; capstone lives in the user site (added explicitly)."""
import sys
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import capstone, struct

EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
data = open(EXE, 'rb').read()
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = False

def off(a):
    return 0x800 + a - 0x80020000

def u8(a): return data[off(a)]
def u16(a): return struct.unpack_from('<H', data, off(a))[0]
def u32(a): return struct.unpack_from('<I', data, off(a))[0]
def s16(a): return struct.unpack_from('<h', data, off(a))[0]

def dis(a, b):
    """list of (addr, word, mnemonic, ops) between a and b inclusive"""
    code = data[off(a): off(a) + (b - a) + 4]
    out = []
    for ins in md.disasm(code, a):
        w = int.from_bytes(code[ins.address - a: ins.address - a + 4], 'little')
        out.append((ins.address, w, ins.mnemonic, ins.op_str))
    return out

def pr(a, b):
    for ad, w, m, o in dis(a, b):
        print("%08x %08x %s %s" % (ad, w, m, o))

def words(a, n):
    return [u32(a + 4 * i) for i in range(n)]

if __name__ == '__main__':
    for arg in sys.argv[1:]:
        x, y = arg.split('-')
        pr(int(x, 16), int(y, 16))
        print('-----')
