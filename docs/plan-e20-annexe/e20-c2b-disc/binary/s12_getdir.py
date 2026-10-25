"""S12: the real GetDirection 0x8002E814 run in the emulator on raw 16.16 differences (hero - toucher)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
sys.path.insert(0, os.path.join(HERE, 'oracle', 'tools'))
import emu
m = emu.Emu()
def gd(dx, dy):
    r = m.call(0x8002e814, dx & 0xFFFFFFFF, dy & 0xFFFFFFFF)
    return r
print("GetDirection(dx, dy) raw 16.16 (hero - toucher); 0 = down (+Y), 8 = left, 16 = up, 24 = right")
cases = [(0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)]
for dx, dy in cases:
    print("  (%+d, %+d) raw -> %s" % (dx, dy, gd(dx, dy)))
for dx, dy in ((14, 0), (-14, 0), (0, 10), (0, -10), (10, 8), (-10, 8), (10, -8), (-10, -8)):
    print("  (%+d px, %+d px) -> %s" % (dx, dy, gd(dx << 16, dy << 16)))
