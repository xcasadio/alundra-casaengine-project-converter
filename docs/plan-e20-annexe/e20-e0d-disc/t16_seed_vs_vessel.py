import sys, re
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import bx
def body(a, n):
    out = []
    for ad, w, m, o in bx.dis(a, a + 4 * n - 4):
        out.append((ad - a, w, m, o))
    return out
V = body(0x800333AC, 0x7F)
S = body(0x8003382C, 0x7F)
# find the end of each function (jr $ra)
def trim(b):
    for i, (off, w, m, o) in enumerate(b):
        if m == 'jr':
            return b[:i + 2]
    return b
V = trim(V); S = trim(S)
print('instructions: vessel', len(V), 'seed', len(S))
def norm(o):
    return re.sub(r'0x80[0-9a-f]{6}', 'ADDR', o)
diff = 0
for (ov, wv, mv, pv), (os_, ws, ms, ps) in zip(V, S):
    if (mv, norm(pv)) != (ms, norm(ps)) or wv != ws:
        diff += 1
        print('  +0x%03x vessel: %-6s %-30s | seed: %-6s %-30s' % (ov, mv, pv, ms, ps))
print('differing instructions', diff, 'of', min(len(V), len(S)))
