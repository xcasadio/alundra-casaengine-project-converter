"""For each 'lw rX, 0x4334(rY)' (rY = lui 0x800E), list the loads based on rX in the following 60 instructions (disp), until rX is overwritten."""
import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2-critic')
import bx
REG = ['zero','at','v0','v1','a0','a1','a2','a3','t0','t1','t2','t3','t4','t5','t6','t7','s0','s1','s2','s3','s4','s5','s6','s7','t8','t9','k0','k1','gp','sp','fp','ra']
OPS = {0x20:'lb',0x21:'lh',0x23:'lw',0x24:'lbu',0x25:'lhu',0x28:'sb',0x29:'sh',0x2B:'sw'}
sites = [0x8002c350,0x8002c394,0x8002c754,0x8002cc80,0x8002cc88,0x8002ccc4,0x8002cd1c,0x8002fbdc,0x8002fee8,0x800300ac,0x800302d8,0x80031680,0x8003673c,0x80036904,0x80036b1c,0x80039098,0x8003a960,0x800663b0,0x80067bbc,0x8006a1a8,0x8006b4a8,0x8007c3c8]
for s in sites:
    w = bx.u32(s); op = w >> 26; rt = (w >> 16) & 31; base = (w >> 21) & 31
    if op not in OPS: print('?? %08x' % s); continue
    kind = OPS[op]
    if kind == 'sw': print('%08x STORE sw %s,0x4334(%s)' % (s, REG[rt], REG[base])); continue
    reg = rt; res = []
    a = s + 4
    for i in range(80):
        w = bx.u32(a); op = w >> 26
        d = w & 0xFFFF
        if d & 0x8000: d -= 0x10000
        b = (w >> 21) & 31; t = (w >> 16) & 31
        if op in OPS and b == reg:
            res.append('%s %s,%d(%s)@%08x' % (OPS[op], REG[t], d, REG[reg], a))
        # overwritten?
        wr = None
        if op in (0x20,0x21,0x23,0x24,0x25,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F): wr = t
        elif op == 0:
            fn = w & 63; rd = (w >> 11) & 31
            if fn not in (0x08,0x18,0x19,0x1A,0x1B): wr = rd
        if wr == reg and op != 0x2B:
            if not (op in OPS and b == reg and OPS[op].startswith('l') and t != reg):
                res.append('(rewritten @%08x)' % a); break
        if op in (2, 3) or (op == 0 and (w & 63) == 8):
            res.append('(jump @%08x)' % a); break
        a += 4
    print('%08x %s %s,0x4334 -> %s' % (s, kind, REG[rt], ' ; '.join(res)))
