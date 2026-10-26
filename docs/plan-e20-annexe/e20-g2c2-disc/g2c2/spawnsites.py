"""spawnsites.py - read-only: every call site of SpawnWarpEntity (0x80039F58) in ALUN_CD.EXE (France) with the constant it loads in $a1 (page) and $a2 (index), when a nearby
addiu $zero gives it. Run: python -B -I -X utf8 spawnsites.py"""
import sys
sys.path.insert(0, '.')
import bx
JAL = (3 << 26) | (0x80039F58 >> 2 & 0x3FFFFFF)
sites = []
for a in range(0x80020000, 0x800CA800, 4):
    if bx.u32(a) == JAL:
        a1 = a2 = None
        # scan back up to 14 words (and the delay slot after) for addiu $a1/$a2, $zero, imm (opcode 9, rs 0, rt 5 / 6)
        for b in list(range(a - 4, a - 60, -4)) + [a + 4]:
            w = bx.u32(b)
            if (w >> 26) == 9 and ((w >> 21) & 31) == 0:
                rt = (w >> 16) & 31
                imm = w & 0xFFFF
                if imm >= 0x8000: imm -= 0x10000
                if rt == 5 and a1 is None: a1 = imm
                if rt == 6 and a2 is None: a2 = imm
            if (w >> 26) == 0 and (w & 0x3F) == 0x21 and ((w >> 11) & 31) == 5 and ((w >> 16) & 31) == 0 and a1 is None:   # addu $a1, $zero?, ...
                pass
        sites.append((a, a1, a2))
print('call sites', len(sites))
for a, a1, a2 in sites:
    print('%08x a1=%s a2=%s' % (a, a1, a2))
