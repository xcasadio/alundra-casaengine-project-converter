import sys, lib
targets = [int(x,16) for x in sys.argv[1:]]
for i in lib.all_ins():
    if i.mnemonic in ('jal','j'):
        try: t = int(i.op_str,16)
        except: continue
        if t in targets: print('%08x %s %08x' % (i.address, i.mnemonic, t))
