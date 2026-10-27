"""Find jal/j callers and literal word references of given addresses in ALUN_CD.EXE text+data.
usage: python -I s3_callers.py 0x80037488 ..."""
import sys, bisect
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2')
import bx
START, END = 0x80020000, 0x800CA800
funcs = []
for a in range(START, END, 4):
    w = bx.u32(a)
    if (w >> 26) == 0x09 and ((w >> 21) & 31) == 29 and ((w >> 16) & 31) == 29 and (w & 0x8000):
        funcs.append(a)
def fn(a):
    i = bisect.bisect_right(funcs, a) - 1
    return funcs[i] if i >= 0 else 0
for arg in sys.argv[1:]:
    t = int(arg, 0)
    wjal = 0x0C000000 | ((t >> 2) & 0x3FFFFFF)
    wj = 0x08000000 | ((t >> 2) & 0x3FFFFFF)
    print('== %08x' % t)
    for a in range(START, END, 4):
        w = bx.u32(a)
        if w == wjal:
            print('  jal at %08x  fn~%08x' % (a, fn(a)))
        elif w == wj:
            print('  j   at %08x  fn~%08x' % (a, fn(a)))
        elif w == t:
            print('  literal word at %08x' % a)
