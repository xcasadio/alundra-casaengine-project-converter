import sys
sys.path.insert(0,'emu')
import lib
ins = lib.all_ins()
for i in ins:
    if i.mnemonic in ('sw','sh','sb') and i.op_str.replace(' ','').endswith(('0xf8(' + ')',)) or False:
        pass
import re
for k,i in enumerate(ins):
    if i.mnemonic in ('sw','sh') and re.search(r',\s*0xf8\(\$\w+\)$', i.op_str):
        print(hex(i.address), i.mnemonic, i.op_str)
