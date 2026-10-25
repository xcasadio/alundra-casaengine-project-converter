import sys, struct, bisect, json
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import capstone

EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
D = open(EXE, 'rb').read()
BASE = 0x80020000
END = BASE + 0xAA800
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)

def o(a): return 0x800 + a - BASE
def u8(a): return D[o(a)]
def u16(a): return struct.unpack_from('<H', D, o(a))[0]
def s16(a): return struct.unpack_from('<h', D, o(a))[0]
def u32(a): return struct.unpack_from('<I', D, o(a))[0]

N = (END - BASE) // 4
W = [u32(BASE + 4 * i) for i in range(N)]
def w(a): return W[(a - BASE) >> 2]

def dis(a, b):
    code = D[o(a): o(a) + (b - a) + 4]
    return [(i.address, i.mnemonic, i.op_str) for i in md.disasm(code, a)]

def pr(a, b):
    for ad, m, ops in dis(a, b):
        print('%08x %08x %s %s' % (ad, w(ad), m, ops))

FUNCS = []
for i in range(N):
    x = W[i]
    if (x >> 16) == 0x27BD and (x & 0x8000) and (x & 0xFFFF) != 0:
        FUNCS.append(BASE + 4 * i)

def func_of(a):
    i = bisect.bisect_right(FUNCS, a) - 1
    return FUNCS[i] if i >= 0 else None

def jaltgt(x): return ((x & 0x03FFFFFF) << 2) | 0x80000000

def callers(target):
    return [BASE + 4 * i for i in range(N) if (W[i] >> 26) == 3 and jaltgt(W[i]) == target]

def is_br(x):
    op = x >> 26
    if op in (2, 3, 1, 4, 5, 6, 7): return True
    if op == 0 and (x & 0x3F) in (8, 9): return True
    return False

def cargs(a):
    got = {}
    cand = [w(a + 4)]
    k = a - 4
    for _ in range(12):
        x = w(k)
        if is_br(x): break
        cand.append(x)
        k -= 4
    for x in cand:
        op = x >> 26; rt = (x >> 16) & 31; rs = (x >> 21) & 31
        imm = x & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        if 4 <= rt <= 7 and rt not in got:
            if op == 9 and rs == 0: got[rt] = simm
            elif op == 13 and rs == 0: got[rt] = imm
            elif op == 9 and rs == rt: pass
    return {'a%d' % (r - 4): v for r, v in got.items()}
