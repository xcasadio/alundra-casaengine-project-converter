import sys
sys.path.insert(0, r'.')
from bx import *
for a in (0x800281DC, 0x80027400, 0x800270C0):
    print(hex(a), words(a, 8))
print("C70 dis E20:")
pr(0x8007F6C8, 0x8007F7D0)
