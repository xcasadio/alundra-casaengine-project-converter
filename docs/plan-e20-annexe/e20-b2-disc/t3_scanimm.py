import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-proj/o8')
import bx
imm = int(sys.argv[1], 16)
a = 0x80020000
end = 0x80020000 + len(bx.data) - 0x800
names = {0x23: 'lw', 0x2B: 'sw', 0x21: 'lh', 0x25: 'lhu', 0x29: 'sh', 0x20: 'lb', 0x24: 'lbu', 0x28: 'sb', 0x09: 'addiu', 0x0D: 'ori'}
while a < end:
    w = bx.u32(a)
    op = w >> 26
    if op in names and (w & 0xFFFF) == imm:
        print(hex(a), names[op], 'rt=%d rs=%d' % ((w >> 16) & 31, (w >> 21) & 31))
    a += 4
