import sys,re
sys.path.insert(0,'emu')
import lib
ins = lib.all_ins()
off = sys.argv[1]
mn = sys.argv[2].split(',')
pat = re.compile(r'^\$(\w+), %s\(\$(\w+)\)$' % re.escape(off))
for x in ins:
    if x.mnemonic in mn:
        m = pat.match(x.op_str)
        if m:
            print('%08x %s %s' % (x.address, x.mnemonic, x.op_str))
