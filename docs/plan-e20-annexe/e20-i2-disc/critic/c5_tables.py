import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2-critic')
import bx, struct
def s32(v): return v - (1<<32) if v & 0x80000000 else v
X = [s32(w) for w in bx.words(0x80028B94, 16)]
Y = [s32(w) for w in bx.words(0x80028BD4, 16)]
for i in range(16):
    print('%2d  X=%9d  Y=%9d' % (i, X[i], Y[i]))
