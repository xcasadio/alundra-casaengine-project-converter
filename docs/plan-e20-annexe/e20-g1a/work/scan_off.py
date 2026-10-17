import sys,re
sys.path.insert(0,'emu')
import lib
ins = lib.all_ins()
pat = re.compile(r'^\$(\w+), (0x28|0x2c)\(\$(\w+)\)$')
rows=[]
for i,x in enumerate(ins):
    if x.mnemonic in ('sw','lw'):
        m = pat.match(x.op_str)
        if m:
            rows.append((x.address,x.mnemonic,m.group(1),m.group(2),m.group(3)))
print(len(rows))
for r in rows:
    if 0x8002E000 <= r[0] <= 0x80080000:
        print('%08x %s $%s, %s($%s)' % r)
