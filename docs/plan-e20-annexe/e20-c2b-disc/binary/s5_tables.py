import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mx, bx
def tab(a, n, label):
    print(label, hex(a))
    for i in range(n):
        print("   [%2d] %08x" % (i, bx.u32(a + 4*i)))
tab(0x80022D1C, 8, "slope dispatch (s2 = hero+0x18C)")
# state table for the general handler: 0x80022D3C ; index = state - 0x1C ?
print("general slope handler state table 0x80022D3C (first 0x30 entries, index = state-0x1C):")
for i in range(0x30):
    v = bx.u32(0x80022D3C + 4*i)
    print("   state %02x -> %08x" % (0x1C + i, v))
