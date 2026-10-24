"""scan_off.py OFF [OFF...] : every load/store with immediate OFF in the text segment (any base register), with function start guess. python -I"""
import sys, struct
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cdis import DATA, T_ADDR, T_SIZE, off, u32, md
OPS = {0x20:'lb',0x21:'lh',0x23:'lw',0x24:'lbu',0x25:'lhu',0x28:'sb',0x29:'sh',0x2B:'sw'}
offs = [int(x, 0) for x in sys.argv[1:]]
N = T_SIZE // 4
# find function starts: addiu sp,sp,-N  (0x27bdXXXX with negative imm)
starts = []
for i in range(N):
    a = T_ADDR + 4 * i
    w = u32(a)
    if (w >> 16) == 0x27bd and (w & 0x8000):
        starts.append(a)
import bisect
def fn(a):
    k = bisect.bisect_right(starts, a) - 1
    return starts[k] if k >= 0 else 0
for i in range(N):
    a = T_ADDR + 4 * i
    w = u32(a)
    op = w >> 26
    if op in OPS and (w & 0xFFFF) in offs:
        rs = (w >> 21) & 31; rt = (w >> 16) & 31
        print('0x%08x fn~0x%08x %s r%d, 0x%x(r%d)' % (a, fn(a), OPS[op], rt, w & 0xFFFF, rs))
