import sys
sys.path.insert(0, r'.')
from bx import *
print([hex(w) for w in words(0x800281EC, 6)])
print("DestroyEntity 8003a59c:")
pr(0x8003a59c, 0x8003a6a0)
