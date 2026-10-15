import sys, os
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import exe
print([exe.s32(0x800236D4 + 4 * i) / 65536 for i in range(24)])
