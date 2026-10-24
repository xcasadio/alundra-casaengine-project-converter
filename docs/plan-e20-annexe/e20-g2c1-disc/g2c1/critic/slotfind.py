"""slotfind.py ADDR... : which (slot, code) of the master table 0x80098F4C point inside a function [ADDR]. python -I"""
import sys, os
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cdis import u32
MASTER = 0x80098F4C
want = [int(x, 16) for x in sys.argv[1:]]
for slot in range(6):
    base = u32(MASTER + 4 * slot)
    for code in range(0, 400):
        try:
            p = u32(base + 4 * code)
        except Exception:
            break
        if p in want:
            print('slot %s code %d -> 0x%08x (table 0x%08x)' % ('ABCDEF'[slot], code, p, base))
