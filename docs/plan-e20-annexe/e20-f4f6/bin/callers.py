import sys
sys.path.insert(0, 'src')
import lib
ins = lib.all_ins()
targets = [int(x, 16) for x in sys.argv[1:]]
for t in targets:
    print('== callers of %08X' % t)
    for i in ins:
        if i.mnemonic in ('jal', 'j') and i.op_str.startswith('0x'):
            if int(i.op_str, 16) == t:
                print('  %08X %s' % (i.address, i.mnemonic))
