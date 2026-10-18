"""Scan the text segment for lui/ld-st pairs that reference a given absolute address (+/- window)."""
import sys, struct
from mydis import *
def scan(addr, window=8, width=4):
    hi = (addr + 0x8000) >> 16 & 0xFFFF
    lo = (addr & 0xFFFF)
    n = (len(DATA)-0x800)//4
    res = []
    for i in range(n):
        a = TADDR + 4*i
        w = struct.unpack_from("<I", DATA, 0x800+4*i)[0]
        op = w>>26
        imm = w & 0xFFFF
        if op in (0x20,0x21,0x23,0x24,0x25,0x28,0x29,0x2B,0x22,0x26,0x2A,0x2E) and imm == lo:
            rs = (w>>21)&31
            # find lui with same reg in the previous window insts
            for k in range(1, window+1):
                if i-k < 0: break
                w2 = struct.unpack_from("<I", DATA, 0x800+4*(i-k))[0]
                if (w2>>26)==0x0F and ((w2>>16)&31)==rs and (w2&0xFFFF)==hi:
                    res.append((a, k)); break
        if op in (0x09,) and imm == lo:
            rs = (w>>21)&31
            for k in range(1, window+1):
                if i-k < 0: break
                w2 = struct.unpack_from("<I", DATA, 0x800+4*(i-k))[0]
                if (w2>>26)==0x0F and ((w2>>16)&31)==rs and (w2&0xFFFF)==hi:
                    res.append((a, -k)); break
    return res
if __name__=="__main__":
    addr=int(sys.argv[1],16)
    for a,k in scan(addr, int(sys.argv[2]) if len(sys.argv)>2 else 8):
        w=struct.unpack_from("<I", DATA, off(a))[0]
        ins=next(md.disasm(struct.pack("<I",w), a))
        print(f"{a:08x} (lui {abs(k)} back) {ins.mnemonic} {ins.op_str}")
