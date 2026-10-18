"""scan_off.py - read-only scan of ALUN_CD.EXE (France) text for loads/stores with a given immediate offset (default 0x144 = IsOnGround).
usage: python -B scan_off.py 0x144 [-- more offsets]"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
offs = [int(a, 0) for a in sys.argv[1:]] or [0x144]
ins = lib.all_ins()
# function starts heuristic: addiu sp,sp,-N
starts = [i.address for i in ins if i.mnemonic == 'addiu' and i.op_str.startswith('$sp, $sp, -')]
import bisect
def func_of(a):
    k = bisect.bisect_right(starts, a) - 1
    return starts[k] if k >= 0 else 0
for off in offs:
    pat = '0x%x(' % off
    print('== offset %#x' % off)
    for i in ins:
        if i.mnemonic in ('sw', 'sh', 'sb', 'lw', 'lh', 'lhu', 'lb', 'lbu') and pat in i.op_str:
            print('%08x %-5s %-24s func~%08x' % (i.address, i.mnemonic, i.op_str, func_of(i.address)))
