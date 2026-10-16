import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
print("table 0x800237F4: rows by +0x98 (adir), columns ((dir+2)&0x1C)>>2")
for r in range(4):
    print(r, [lib.u32(0x800237F4 + 32*r + 4*c) for c in range(8)])
print("0x80023644 (record spawner direction table by SpriteDirection&3):", [hex(lib.u32(0x80023644+4*i)) for i in range(4)])
print("0x800237B4 events jump table:", [hex(lib.u32(0x800237B4+4*i)) for i in range(5)])
