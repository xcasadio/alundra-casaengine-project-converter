"""Critic: scan whole text for ANY I-type instruction with immediate in WANT (addiu, load/store, ori...). Also jal callers of targets."""
import sys, bisect
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2-critic')
import bx
START, END = 0x80020000, 0x800CA800
REG = ['zero','at','v0','v1','a0','a1','a2','a3','t0','t1','t2','t3','t4','t5','t6','t7','s0','s1','s2','s3','s4','s5','s6','s7','t8','t9','k0','k1','gp','sp','fp','ra']
OPN = {0x08:'addi',0x09:'addiu',0x0A:'slti',0x0B:'sltiu',0x0C:'andi',0x0D:'ori',0x0E:'xori',0x20:'lb',0x21:'lh',0x22:'lwl',0x23:'lw',0x24:'lbu',0x25:'lhu',0x26:'lwr',0x28:'sb',0x29:'sh',0x2A:'swl',0x2B:'sw',0x2E:'swr'}
mode = sys.argv[1]
if mode == 'imm':
    want = [int(a,0) for a in sys.argv[2:]]
    funcs=[]
    for a in range(START,END,4):
        w=bx.u32(a); op=w>>26
        if op==0x09 and ((w>>21)&31)==29 and ((w>>16)&31)==29 and (w&0x8000): funcs.append(a)
    for a in range(START,END,4):
        w=bx.u32(a); op=w>>26
        if op in OPN:
            d=w&0xFFFF
            if d&0x8000: d-=0x10000
            if d in want:
                i=bisect.bisect_right(funcs,a)-1
                f=funcs[i] if i>=0 else 0
                print('%08x %-5s %-3s %d(%s) fn~%08x'%(a,OPN[op],REG[(w>>16)&31],d,REG[(w>>21)&31],f))
elif mode=='jal':
    tg=[int(a,0) for a in sys.argv[2:]]
    for a in range(START,END,4):
        w=bx.u32(a)
        if (w>>26) in (2,3):
            t=((w&0x3FFFFFF)<<2)|0x80000000
            if t in tg: print('%08x %s -> %08x'%(a,'jal' if (w>>26)==3 else 'j',t))
