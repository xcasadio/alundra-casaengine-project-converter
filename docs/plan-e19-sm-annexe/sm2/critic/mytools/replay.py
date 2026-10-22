"""Critic's own replay of the sm2 vectors. Written from scratch:
 model A: direct Python bit arithmetic of the C# unchecked int32 formula (as I read the binary)
 model B: a tiny MIPS-I interpreter decoding raw instruction words from ALUN_CD.EXE (no capstone, no annex code)
Compares both against every row of the preparation's TSV."""
import struct, sys, csv
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE,'rb').read()
TADDR = 0x80020000
def fw(a): return struct.unpack_from("<I", D, 0x800 + a - TADDR)[0]
M32 = 0xFFFFFFFF
def s32(x):
    x &= M32
    return x - (1<<32) if x & 0x80000000 else x

# ---------- model B: tiny interpreter ----------
class Mem(dict):
    def lw(self,a): return self.get(a,0)
    def sw(self,a,v): self[a]=v&M32
def run(handler, a0, a2, a3, mem):
    reg = [0]*32
    reg[4]=a0; reg[6]=a2; reg[7]=a3; reg[31]=0xDEAD0000
    pc = handler
    nxt = None
    steps=0
    delay_target = None
    while True:
        steps+=1
        assert steps<200
        w = fw(pc)
        op = w>>26; rs=(w>>21)&31; rt=(w>>16)&31; rd=(w>>11)&31; sh=(w>>6)&31; fn=w&63
        imm = w & 0xFFFF; simm = imm-0x10000 if imm&0x8000 else imm
        npc = pc+4
        jump=None
        if w==0: pass
        elif op==0:
            if fn==0x00: reg[rd]=(reg[rt]<<sh)&M32
            elif fn==0x25: reg[rd]=reg[rs]|reg[rt]
            elif fn==0x23: reg[rd]=(reg[rs]-reg[rt])&M32   # subu (negu = subu rd,$0,rt)
            elif fn==0x2A: reg[rd]=1 if s32(reg[rs])<s32(reg[rt]) else 0   # slt
            elif fn==0x08: jump=reg[rs]
            else: raise Exception("fn %x at %x"%(fn,pc))
        elif op==1:
            if rt==1: # bgez
                if s32(reg[rs])>=0: jump=('b',pc+4+(simm<<2))
            else: raise Exception("regimm")
        elif op==0x09: reg[rt]=(reg[rs]+simm)&M32  # addiu
        elif op==0x0E: reg[rt]=reg[rs]^imm  # xori
        elif op==0x23:  # lw
            reg[rt]=mem.lw((reg[rs]+simm)&M32)
        elif op==0x2B: mem.sw((reg[rs]+simm)&M32, reg[rt])
        elif op==0x24: # lbu
            a=(reg[rs]+simm)&M32; wd=mem.lw(a&~3); reg[rt]=(wd>>(8*(a&3)))&0xFF
        else: raise Exception("op %x at %x"%(op,pc))
        # delay-slot handling: execute next instruction then jump
        if jump is not None:
            # execute delay slot
            pc2 = pc+4
            w2=fw(pc2)
            # run delay slot by recursion trick: single-step the same decode
            op2 = w2>>26; rs2=(w2>>21)&31; rt2=(w2>>16)&31; rd2=(w2>>11)&31; sh2=(w2>>6)&31; fn2=w2&63
            imm2 = w2 & 0xFFFF; simm2 = imm2-0x10000 if imm2&0x8000 else imm2
            if w2==0: pass
            elif op2==0 and fn2==0x23: reg[rd2]=(reg[rs2]-reg[rt2])&M32
            elif op2==0x09: reg[rt2]=(reg[rs2]+simm2)&M32
            elif op2==0x2B: mem.sw((reg[rs2]+simm2)&M32, reg[rt2])
            else: raise Exception("unhandled delay slot %08x at %x"%(w2,pc2))
            if isinstance(jump,tuple): pc=jump[1]
            else:
                if jump==0xDEAD0000: return reg[2]
                pc=jump
        else:
            pc=npc
        # note: lui handled below
HAND = {0x6A:0x8003F8DC,0x6B:0x8003F908,0x6C:0x8003F94C,0x6D:0x8003F990}
# add lui
_old = None
def run2(handler,a0,a2,a3,mem):
    return run(handler,a0,a2,a3,mem)

# ---------- model A ----------
def model_a(op, instr, pos, memo, result_in):
    px,py,pz = pos
    if op==0x6A:
        return 1, result_in, (px,py,pz)
    k = {0x6B:0,0x6C:1,0x6D:2}[op]
    d = s32(memo[k]-pos[k])
    if d<0: d = s32(-d)
    thr = s32(((instr[1] | (instr[2]<<8))<<16))
    res = 0 if d<thr else 1
    return 3, res, tuple(memo)

def main(tsv):
    rows = list(csv.DictReader(open(tsv,encoding='utf-8'),delimiter='\t'))
    bad=0
    for r in rows:
        op=int(r['op'],16)
        instr=bytes(int(x,16) for x in r['instr'].split())
        pos=tuple(int(r[k],16) for k in ('pos_x','pos_y','pos_z'))
        memo=tuple(int(r[k],16) for k in ('memo_p2','memo_p3','memo_p4'))
        rin=int(r['result_in'],16)
        # model A
        v0,res,mo = model_a(op,instr,pos,memo,rin)
        # model B
        E=0x80100000; S=0x80110000; IP=0x80120000; PC=0x80130000
        mem=Mem()
        mem.sw(E+0x114,pos[0]); mem.sw(E+0x118,pos[1]); mem.sw(E+0x11C,pos[2])
        mem.sw(S+0xC,memo[0]); mem.sw(S+0x10,memo[1]); mem.sw(S+0x14,memo[2]); mem.sw(S+0x2C,rin)
        # real layout: a2 = S+4, [S+4] = pointer to instruction bytes
        mem.sw(S+4, IP)
        for i,b in enumerate(instr+b'\xff\xff\xff\xff'):
            a=IP+i; w=mem.lw(a&~3); w &= ~(0xFF<<(8*(a&3))); w |= b<<(8*(a&3)); mem.sw(a&~3,w)
        # lui support: patch via fw hack
        v0b = run3(HAND[op],E,S+4,S,mem)
        resB=mem.lw(S+0x2C); moB=(mem.lw(S+0xC),mem.lw(S+0x10),mem.lw(S+0x14))
        exp_res=int(r['result_out'],16); exp_v0=int(r['v0']); exp_mo=tuple(int(r[k],16) for k in ('memo_p2_out','memo_p3_out','memo_p4_out'))
        okA = (v0,res&M32,tuple(m&M32 for m in mo))==(exp_v0,exp_res,exp_mo) if op!=0x6A else (v0,res&M32)==(exp_v0,exp_res)
        if op==0x6A: okA = okA and tuple(x&M32 for x in pos)==exp_mo
        okB = (v0b,resB,moB)==(exp_v0,exp_res,exp_mo)
        if not (okA and okB):
            bad+=1
            if bad<10: print("MISMATCH",r['id'],op,okA,okB)
    print("rows",len(rows),"bad",bad)

def run3(handler,a0,a2,a3,mem):
    # same as run, with lui
    reg=[0]*32; reg[4]=a0; reg[6]=a2; reg[7]=a3; reg[31]=0xDEAD0000
    pc=handler; steps=0
    def step(w,pc):
        op=w>>26; rs=(w>>21)&31; rt=(w>>16)&31; rd=(w>>11)&31; sh=(w>>6)&31; fn=w&63
        imm=w&0xFFFF; simm=imm-0x10000 if imm&0x8000 else imm
        if w==0: return None
        if op==0:
            if fn==0x00: reg[rd]=(reg[rt]<<sh)&M32
            elif fn==0x25: reg[rd]=reg[rs]|reg[rt]
            elif fn==0x23: reg[rd]=(reg[rs]-reg[rt])&M32
            elif fn==0x2A: reg[rd]=1 if s32(reg[rs])<s32(reg[rt]) else 0
            elif fn==0x08: return ('j',reg[rs])
            else: raise Exception("fn")
        elif op==1:
            assert rt==1
            if s32(reg[rs])>=0: return ('j',pc+4+(simm<<2))
        elif op==0x09: reg[rt]=(reg[rs]+simm)&M32
        elif op==0x0E: reg[rt]=reg[rs]^imm
        elif op==0x23: reg[rt]=mem.lw((reg[rs]+simm)&M32)
        elif op==0x2B: mem.sw((reg[rs]+simm)&M32,reg[rt])
        elif op==0x24:
            a=(reg[rs]+simm)&M32; wd=mem.lw(a&~3); reg[rt]=(wd>>(8*(a&3)))&0xFF
        else: raise Exception("op %x"%op)
        return None
    while True:
        steps+=1; assert steps<200
        w=fw(pc); j=step(w,pc)
        if j is None and (w>>26) in (0,1) and False: pass
        if j is not None:
            step(fw(pc+4),pc+4)  # delay slot
            if j[1]==0xDEAD0000: return reg[2]
            pc=j[1]
        else: pc+=4
if __name__=="__main__":
    main(sys.argv[1])
