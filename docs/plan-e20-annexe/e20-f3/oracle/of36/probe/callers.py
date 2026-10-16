import sys, struct
sys.path.insert(0, r'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-e0k/oracle')
import exe
targets = {int(a,16) for a in sys.argv[1:]}
n = (len(exe.DATA)-0x800)//4
for i in range(n):
    a = exe.BASE + 4*i
    w = exe.u32(a)
    if w >> 26 == 3:
        t = ((w & 0x03FFFFFF) << 2) | (a & 0xF0000000)
        if t in targets:
            print("jal %08X at %08X" % (t, a))
    # j (tail)
    if w >> 26 == 2:
        t = ((w & 0x03FFFFFF) << 2) | (a & 0xF0000000)
        if t in targets:
            print("j   %08X at %08X" % (t, a))
