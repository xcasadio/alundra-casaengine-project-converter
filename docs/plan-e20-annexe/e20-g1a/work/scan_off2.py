import sys,re
sys.path.insert(0,'emu')
import lib
ins = lib.all_ins()
pat = re.compile(r'^\$(\w+), (0x28|0x2c)\(\$(\w+)\)$')
for i,x in enumerate(ins):
    if x.mnemonic in ('sw','lw'):
        m = pat.match(x.op_str)
        if m and m.group(3) not in ('sp','gp'):
            print('%08x %s $%s, %s($%s)' % (x.address,x.mnemonic,m.group(1),m.group(2),m.group(3)))
