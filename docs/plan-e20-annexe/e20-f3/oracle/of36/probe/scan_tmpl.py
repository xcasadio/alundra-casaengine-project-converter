import sys
sys.path.insert(0, r'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-e0k/oracle')
import exe
lo, hi = 0x8013C608, 0x8013C688
n = (len(exe.DATA)-0x800)//4
words = [exe.u32(exe.BASE+4*i) for i in range(n)]
def sx(v): return v-0x10000 if v&0x8000 else v
# track lui per register within a window
for i,w in enumerate(words):
    op = w>>26
    if op == 0x0F:  # lui
        rt=(w>>16)&31; hiv=(w&0xFFFF)<<16
        for j in range(i+1, min(i+40, n)):
            w2=words[j]; op2=w2>>26
            rs=(w2>>21)&31; rt2=(w2>>16)&31; imm=sx(w2&0xFFFF)
            if op2 in (0x23,0x25,0x21,0x20,0x24,0x28,0x29,0x2B) and rs==rt:
                a=(hiv+imm)&0xFFFFFFFF
                if lo<=a<hi or (0x80138608<=a<0x8013C608 and False):
                    print('%08X %s %08X' % (exe.BASE+4*j, {0x2B:'sw',0x29:'sh',0x28:'sb'}.get(op2,'ld'), a))
            if op2==0x09 and rs==rt and rt2==rt:  # addiu rt, rt, imm : creates pointer
                a=(hiv+imm)&0xFFFFFFFF
                if lo<=a<hi: print('%08X addiu-ptr %08X' % (exe.BASE+4*j, a))
            if op2==0x0F and rt2==rt: break
            if (w2>>16)&31==rt and op2 not in (0x2B,0x29,0x28,0x2A,0x2E,4,5,1) and op2!=0x09: break
