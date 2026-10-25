import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sys, re
from lib import *
# linear scan of text segment for data references to target addresses
def scan(targets, lo=0x80020000, hi=None):
    hi = hi or (BASE+tsize)
    ins = alldis(lo,hi)
    res = {t:[] for t in targets}
    regs = {}
    for k,i in enumerate(ins):
        m=i.mnemonic; o=i.op_str.replace(' ','').split(',')
        if m=='lui':
            regs[o[0]] = int(o[1],16)<<16
        elif m in ('lw','sw','lbu','lb','sb','lh','lhu','sh') :
            mm=re.match(r'(-?0x[0-9a-f]+|-?\d+)\((\$\w+)\)',o[1])
            if mm and mm.group(2) in regs:
                a=(regs[mm.group(2)]+int(mm.group(1),0))&0xffffffff
                for t in targets:
                    if a==t: res[t].append((i.address,m,i.op_str))
        elif m in ('addiu','ori') and len(o)==3 and o[1] in regs:
            a=(regs[o[1]]+int(o[2],0))&0xffffffff
            for t in targets:
                if a==t: res[t].append((i.address,m,i.op_str))
            if o[0]==o[1]: regs[o[0]]=a
            else: regs[o[0]]=a
        elif m=='jal':
            pass
        # invalidate dest reg writes
        if m in ('lw','lbu','lb','lh','lhu','move','addu','subu','sll','srl','sra','and','or','andi','slt','sltiu','sltu','mflo','mfhi') :
            d=o[0]
            if m!='lui' and d in regs and m not in ('sw','sb','sh'): regs.pop(d,None)
        if m in ('jr','j','jal','jalr','b'): pass
    return res
if __name__=='__main__':
    ts=[int(x,16) for x in sys.argv[1:]]
    r=scan(ts)
    for t in ts:
        print(hex(t), 'image u32=%08x'%u32(t))
        for x in r[t]: print('  %08x %s %s'%x)
