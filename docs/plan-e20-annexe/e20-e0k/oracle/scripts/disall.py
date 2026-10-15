import sys, os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import exe
print(hex(exe.BASE), len(exe.DATA))
end = exe.BASE + len(exe.DATA) - 0x800
print(hex(end))
# disassemble whole text 0x80020000..0x800CA800
out = exe.dis(0x80020000, 0x800CA800)
open(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'out', 'dis_all.txt'), 'w').write("\n".join(out))
print(len(out))
