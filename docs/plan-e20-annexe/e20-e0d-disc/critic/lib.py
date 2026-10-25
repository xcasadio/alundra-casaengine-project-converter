import sys, struct
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import capstone
EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
D = open(EXE,'rb').read()
assert D[:8]==b'PS-X EXE'
pc0, gp0, taddr, tsize = struct.unpack_from('<IIII', D, 0x10)
BASE = taddr
def off(a): return 0x800 + a - BASE
def u8(a): return D[off(a)]
def u16(a): return struct.unpack_from('<H', D, off(a))[0]
def s16(a): return struct.unpack_from('<h', D, off(a))[0]
def u32(a): return struct.unpack_from('<I', D, off(a))[0]
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32+capstone.CS_MODE_LITTLE_ENDIAN)
def dis(a,b):
    n=(b-a)//4+1
    code=D[off(a):off(a)+4*n]
    return [(i.address,i.mnemonic,i.op_str) for i in md.disasm(code,a)]
def pr(a,b):
    for ad,m,o in dis(a,b): print('%08x %s %s'%(ad,m,o))
if __name__=='__main__':
    print(hex(pc0),hex(gp0),hex(taddr),hex(tsize), len(D))
    for arg in sys.argv[1:]:
        x,y=arg.split('-'); pr(int(x,16),int(y,16)); print('---')

def alldis(lo,hi):
    out=[]; a=lo
    while a<hi:
        got=list(md.disasm(D[off(a):off(min(hi,a+0x4000))],a))
        out+=got
        if got: a=got[-1].address+4
        else: a+=4
        if got and got[-1].address+4<min(hi,a): pass
    return out
