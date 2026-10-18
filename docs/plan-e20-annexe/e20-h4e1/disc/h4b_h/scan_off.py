"""scan_off.py - list every load/store with displacement OFF (any base register) in the text of ALUN_CD.EXE, with a few instructions of context
and the enclosing function start (nearest preceding 'addiu sp,sp,-N' prologue).  usage: python -B scan_off.py 0xCC 0xD0 [--ctx N]"""
import sys, struct
import tools

def func_start(a):
    b = a
    while b > tools.T_ADDR:
        w = tools.w32(b)
        if (w >> 16) == 0x27BD and (w & 0x8000):   # addiu sp,sp,-N
            return b
        # leaf functions: preceded by jr ra + delay slot
        if tools.w32(b - 8) == 0x03E00008 and b != a:
            return b
        b -= 4
    return b

def scan(offs, ctx):
    out = []
    for a in range(tools.T_ADDR, tools.T_END, 4):
        w = tools.w32(a)
        op = w >> 26
        if op in (0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x28, 0x29, 0x2A, 0x2B, 0x2E):
            imm = w & 0xFFFF
            if imm in offs:
                out.append(a)
    return out

if __name__ == "__main__":
    offs = [int(x, 0) for x in sys.argv[1:] if not x.startswith("--")]
    ctx = 2
    for i, x in enumerate(sys.argv):
        if x == "--ctx":
            ctx = int(sys.argv[i + 1]); offs = [int(y, 0) for y in sys.argv[1:] if not y.startswith("--") and y != sys.argv[i + 1]]
    for a in scan(set(offs), ctx):
        print(f"fn~{func_start(a):08X}", tools.text(a))
