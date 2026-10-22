import sys
sys.path.insert(0,'.')
import lib
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
md=Cs(CS_ARCH_MIPS, CS_MODE_MIPS32+CS_MODE_LITTLE_ENDIAN)
def table(op): return lib.u32(0x80098FAC+4*op)
def listing(start,n):
    out=[]
    for i in md.disasm(lib.DATA[start-lib.BASE+lib.OFF:start-lib.BASE+lib.OFF+4*n],start):
        out.append('0x%08x  %-8s %s'%(i.address,i.mnemonic,i.op_str))
    return out
if __name__=='__main__':
    for a in sys.argv[1:]:
        if a.startswith('op'):
            op=int(a[2:],16); h=table(op)
            print('; opcode 0x%02X handler 0x%08X'%(op,h))
            n=int(sys.argv[sys.argv.index(a)+1]) if False else 80
            L=listing(h,n)
            # stop after the first jr ra + delay slot
            res=[]
            seen=False
            for l in L:
                res.append(l)
                if 'jr' in l and '$ra' in l: seen=True; continue
                if seen: break
            print('\n'.join(res))
        elif a.startswith('0x'):
            print('\n'.join(listing(int(a,16),40)))
