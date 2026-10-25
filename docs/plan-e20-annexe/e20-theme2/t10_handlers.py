import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc')
import bx, scan
# locate native handler tables by anchoring on known handler addresses
anchors = {'C60': 0x8007A8A0, 'C61': 0x8007A958, 'C94': 0x8007C0D8, 'E9': 0x8007EF50, 'E10': 0x8007F23C, 'E17': 0x8007F420, 'E1': 0x8007ED10, 'E2': 0x8007ED30, 'C23': 0x8006B848, 'C4': 0x80066984, 'C255': 0x8007C174}
found = {}
for name, addr in anchors.items():
    code = int(''.join(ch for ch in name if ch.isdigit()))
    slot = name[0]
    hits = []
    for a in range(0x80020000, 0x800CA800 - 3, 4):
        if bx.u32(a) == addr:
            hits.append(a)
    print(name, hex(addr), 'table entries at', [hex(h) for h in hits], 'implied base', [hex(h - 4 * code) for h in hits])
