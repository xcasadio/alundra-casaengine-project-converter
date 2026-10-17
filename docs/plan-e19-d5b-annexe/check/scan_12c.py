import sys, re
sys.path.insert(0,'emu')
import lib
ins = lib.all_ins()
for k,i in enumerate(ins):
    if re.search(r'0x12c\(\$\w+\)$', i.op_str) and i.mnemonic in ('lw','sw'):
        print(hex(i.address), i.mnemonic, i.op_str)
