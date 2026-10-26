import sys
sys.path.insert(0, '.')
from bx import *
hits=[]
for a in range(0x80020000, 0x800CA800, 4):
    w = u32(a)
    if (w >> 26) == 0x2B and (w & 0xFFFF) == 0x28 and ((w>>21)&31) != 29 and ((w>>16)&31) != 0:
        hits.append(a)
print(len(hits))
for a in hits:
    w=u32(a)
    print('=== %08x rt=%d rs=%d' % (a, w>>16&31, w>>21&31))
    for ad, ww, m, o in dis(a-20, a+4):
        print('   %08x %s %s' % (ad, m, o))
