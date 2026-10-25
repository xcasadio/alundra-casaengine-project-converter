"""Whole-binary scanners: function starts, call sites with constant args."""
import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc')
import bx, struct, bisect

TEXT0 = 0x80020000
TEXT1 = 0x800CA800
N = (TEXT1 - TEXT0) // 4
W = [bx.u32(TEXT0 + 4 * i) for i in range(N)]

def word(a):
    return W[(a - TEXT0) >> 2]

# function starts: addiu $sp,$sp,-k  (0x27bd, imm negative)
FUNCS = []
for i in range(N):
    w = W[i]
    if (w >> 16) == 0x27BD and (w & 0x8000) and (w & 0xFFFF) != 0:
        FUNCS.append(TEXT0 + 4 * i)

def func_of(a):
    i = bisect.bisect_right(FUNCS, a) - 1
    return FUNCS[i] if i >= 0 else None

def jal_target(w):
    return ((w & 0x03FFFFFF) << 2) | 0x80000000

def is_branch_or_jump(w):
    op = w >> 26
    if op in (2, 3):  # j, jal
        return True
    if op in (1, 4, 5, 6, 7):
        return True
    if op == 0 and (w & 0x3F) in (8, 9):
        return True
    return False

def const_args(a):
    """constants loaded into $a0..$a3 before the jal at address a (also its delay slot). returns dict reg->value"""
    got = {}
    # delay slot
    cand = [word(a + 4)]
    k = a - 4
    steps = 0
    while steps < 10:
        w = word(k)
        # stop at previous branch/jump (its delay slot is k+4 already scanned as part of block? keep simple)
        if is_branch_or_jump(w):
            break
        cand.append(w)
        k -= 4
        steps += 1
    # cand[0] is the delay slot (latest write), then going backwards
    for w in cand:
        op = w >> 26
        rt = (w >> 16) & 31
        rs = (w >> 21) & 31
        imm = w & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        if 4 <= rt <= 7 and rt not in got:
            if op == 9 and rs == 0:  # addiu rt,$zero,imm
                got[rt] = simm
            elif op == 13 and rs == 0:  # ori rt,$zero,imm
                got[rt] = imm
            elif op == 15:  # lui (ignore)
                pass
    return {('a%d' % (r - 4)): v for r, v in got.items()}

def callsites(target):
    out = []
    for i in range(N):
        w = W[i]
        if (w >> 26) == 3 and jal_target(w) == target:
            out.append(TEXT0 + 4 * i)
    return out

if __name__ == '__main__':
    print('functions found:', len(FUNCS))
