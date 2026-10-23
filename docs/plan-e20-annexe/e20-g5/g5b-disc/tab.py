"""tab.py - read the opcode handler table of ALUN_CD.EXE (0x80098FAC + 4*op) and print the handler address and span of the three opcodes. Read-only."""
import sys, os
HERE=os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE,'work'))
import exelib
base=0x80098FAC
tab=[exelib.u32(base+4*i) for i in range(0xC5)]
addrs=sorted(set(tab))
def nxt(a):
    for x in addrs:
        if x>a: return x
    return None
for op in (0x71,0x72,0x73,0xA8,0xA9,0xAA,0xAB):
    a=tab[op]
    print('op 0x%02X  table slot 0x%08X -> handler 0x%08X  next handler 0x%08X  span %d bytes (%d words)'%(op,base+4*op,a,nxt(a),nxt(a)-a,(nxt(a)-a)//4))
# every other opcode sharing an address
import collections
c=collections.defaultdict(list)
for i,a in enumerate(tab): c[a].append(i)
for op in (0x72,0xA9,0xAA):
    print('sharing 0x%08X:'%tab[op], ['0x%02X'%x for x in c[tab[op]]])
