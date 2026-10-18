import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'oracle'))
import lib
a = int(sys.argv[1], 0); n = int(sys.argv[2], 0)
for i in lib.dis(a, n):
    print('%08X  %-8s %s' % (i.address, i.mnemonic, i.op_str))
