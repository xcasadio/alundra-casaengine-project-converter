"""From-scratch tiny MIPS-I interpreter (own code). Only the instructions the handlers 6A-6D use + a few more.
Memory is a sparse dict of bytes; writes are logged."""
import sys, struct
sys.path.insert(0,'tools')
from mybin import D, off, u32
M32=0xFFFFFFFF
def s32(x):
    x&=M32
    return x-(1<<32) if x&0x80000000 else x
def s16(x):
    x&=0xFFFF
    return x-0x10000 if x&0x8000 else x

class Mem:
    def __init__(s): s.b={}; s.wlog=[]; s.rlog=[]
    def lb(s,a): s.rlog.append((a,1)); return s.b.get(a,0)
    def lw(s,a):
        assert a%4==0,hex(a)
        s.rlog.append((a,4))
        return sum(s.b.get(a+i,0)<<(8*i) for i in range(4))
    def sb(s,a,v): s.b[a]=v&0xFF; s.wlog.append((a,1,v&0xFF))
    def sw(s,a,v):
        assert a%4==0,hex(a)
        v&=M32
        for i in range(4): s.b[a+i]=(v>>(8*i))&0xFF
        s.wlog.append((a,4,v))
    def put32(s,a,v):
        v&=M32
        for i in range(4): s.b[a+i]=(v>>(8*i))&0xFF

def run(entry, regs, mem, maxsteps=200):
    """run until jr $ra to magic return address; returns regs. Supports delay slots."""
    R=[0]*32
    for k,v in regs.items(): R[k]=v&M32
    RA=0xDEAD0000
    R[31]=RA
    pc=entry
    steps=0
    def fetch(a): return u32(a)
    def exec1(w, pc):
        """execute a non-branch instruction word; returns nothing"""
        op=w>>26; rs=(w>>21)&31; rt=(w>>16)&31; rd=(w>>11)&31; sh=(w>>6)&31; fn=w&63; imm=w&0xFFFF
        def setr(i,v):
            if i: R[i]=v&M32
        if op==0:
            if fn==0x00: setr(rd, R[rt]<<sh)                 # sll
            elif fn==0x23: setr(rd, R[rs]-R[rt])              # subu
            elif fn==0x25: setr(rd, R[rs]|R[rt])              # or
            elif fn==0x2A: setr(rd, 1 if s32(R[rs])<s32(R[rt]) else 0)  # slt
            elif fn==0x21: setr(rd, R[rs]+R[rt])              # addu
            else: raise Exception(f"unsupported special fn {fn:#x} at {pc:#x}")
        elif op==0x09: setr(rt, R[rs]+s16(imm))               # addiu
        elif op==0x0E: setr(rt, R[rs]^imm)                    # xori
        elif op==0x23: setr(rt, mem.lw((R[rs]+s16(imm))&M32)) # lw
        elif op==0x24: setr(rt, mem.lb((R[rs]+s16(imm))&M32)) # lbu
        elif op==0x2B: mem.sw((R[rs]+s16(imm))&M32, R[rt])    # sw
        else: raise Exception(f"unsupported op {op:#x} at {pc:#x}")
    while steps<maxsteps:
        steps+=1
        w=fetch(pc); op=w>>26; rs=(w>>21)&31; rt=(w>>16)&31; imm=w&0xFFFF; fn=w&63
        if op==0 and fn==0x08:  # jr
            tgt=R[rs]
            exec1(fetch(pc+4),pc+4)
            if tgt==RA: return R
            pc=tgt; continue
        if op==1 and rt==1:     # bgez
            taken = s32(R[rs])>=0
            tgt=pc+4+(s16(imm)<<2)
            exec1(fetch(pc+4),pc+4)
            pc = tgt if taken else pc+8
            continue
        exec1(w,pc); pc+=4
    raise Exception("too many steps")

# fake pc-pointer layout used here: faithful RunScript layout: a3=S, a2=S+4 (pc pointer lives at S+4)
def call_handler(op, logic_pos, owner_pos, S_words, instr_bytes, ent_base=0x80150000, st_base=0x80160000, code_base=0x80170000):
    mem=Mem()
    E=ent_base; O=ent_base+0x400; S=st_base
    mem.put32(E+0x114,logic_pos[0]); mem.put32(E+0x118,logic_pos[1]); mem.put32(E+0x11C,logic_pos[2])
    mem.put32(O+0x114,owner_pos[0]); mem.put32(O+0x118,owner_pos[1]); mem.put32(O+0x11C,owner_pos[2])
    for off_,v in S_words.items(): mem.put32(S+off_,v)
    for i,b in enumerate(instr_bytes): mem.b[code_base+i]=b
    mem.put32(S+4, code_base)   # pc pointer lives at S+4
    h=u32(0x80098FAC+4*op)
    mem.wlog=[]; mem.rlog=[]
    R=run(h,{4:E,5:O,6:S+4,7:S},mem)
    return R[2], mem, S, E, O
