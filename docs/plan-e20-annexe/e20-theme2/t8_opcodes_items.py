import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc')
import bx, scan
# opcode dispatch table: 0x800992AC is the entry for 0xC0 per the annex => base = 0x800992AC - 4*0xC0
base = 0x800992AC - 4 * 0xC0
ops = {}
for op in range(0, 0x100):
    h = bx.u32(base + 4 * op)
    ops.setdefault(h, []).append(op)
print('table base', hex(base), 'distinct handlers', len(ops))
# functions of interest
inter = {
    'GetNumberOfItem': 0x8004E428,
    'UseItem(consume)': 0x8004E5C4,
    'GiveItem': 0x80033F00,
    'AddOneItemIfUnlocked?': 0x8004E5F0,
}
# find all functions that contain a jal to these
import collections
for name, tgt in inter.items():
    cs = scan.callsites(tgt)
    byf = collections.defaultdict(list)
    for a in cs:
        byf[scan.func_of(a)].append(a)
    print(name, hex(tgt), 'callers:', len(cs), 'in functions', [hex(f) for f in sorted(byf)])
    # match against opcode handlers (handler start addresses)
    for f in sorted(byf):
        for h, l in ops.items():
            if h == f:
                print('   opcode handler', hex(f), 'ops', [hex(o) for o in l])
