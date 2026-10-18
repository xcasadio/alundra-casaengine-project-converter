import re, sys
sys.path.insert(0,'.')
import mipsdis
md = mipsdis.md; data = mipsdis._data
start = 0x80020000; end = 0x800CA800
code = data[mipsdis.off(start):mipsdis.off(end)]
pat = re.compile(r'(-?0x[0-9a-f]+|-?\d+)\(\$(\w+)\)')
loads=[]; stores=[]
for i in md.disasm(code, start):
    m=i.mnemonic
    if m in ('lw','lh','lhu','lb','lbu','sw','sh','sb'):
        mm=pat.search(i.op_str)
        if not mm: continue
        d=int(mm.group(1),16) if 'x' in mm.group(1) else int(mm.group(1))
        if d==0x280:
            (stores if m.startswith('s') else loads).append((i.address,i.mnemonic,i.op_str))
print('loads at disp 0x280:',len(loads)); 
for a in loads: print('  %08X %s %s'%a)
print('stores:',len(stores))
for a in stores: print('  %08X %s %s'%a)
