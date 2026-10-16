#!/usr/bin/env python
"""Cross-check which handlers write the program state's Result (+0x2C) in the binary vs in the DLL (label script2). Read-only.

Binary side: any 'sw rt, 0x2c(rs)' in the handler span (table 0x80098FAC); also +0x30 (_30 loop counter) and +0x34 (_34 return mark)
and +8..+0x28 (Parameters). DLL side: the Dispatch block (cases.json lines) mentions 'Result' assignment or calls a helper that writes it.
This is a HEURISTIC to find disagreements to look at; INFERENCE only.
Output: result-check.tsv
"""
import struct, os, re, json
import capstone

P = 'D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE'
OUT = os.path.dirname(os.path.abspath(__file__))
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
data = open(P, 'rb').read()

def w(addr):
    off = 0x800 + addr - 0x80020000
    return struct.unpack('<I', data[off:off + 4])[0]

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
base = 0x80098FAC
tab = [w(base + 4 * i) for i in range(0xC5)]
addrs = sorted(set(tab))

def nb(a):
    for x in addrs:
        if x > a:
            return x
    return a + 0x400

src = open(REPO + '/Alundra/Scripts/AlundraEventProgramRunner.cs', encoding='utf-8').read().split('\n')
cases = {int(k, 16): v for k, v in json.load(open(OUT + '/cases.json')).items()}
order = sorted(cases.items(), key=lambda kv: kv[1])
blocks = {}
for n, (op, line) in enumerate(order):
    end = order[n + 1][1] if n + 1 < len(order) else 2154
    blocks[op] = '\n'.join(src[line - 1:end - 1])
# helpers known to write Result
RESULT_HELPERS = ('CheckFlagsOn', 'CheckFlagsOff', 'DestroyMatchingEntities', 'CheckEntityInAabb', 'HandleMapTriggerCommand')

rows = []
for op in range(0xC5):
    a = tab[op]; b = nb(a)
    offs = set()
    for k in range((b - a) // 4):
        ins = list(md.disasm(struct.pack('<I', w(a + 4 * k)), a + 4 * k))
        if not ins:
            continue
        i = ins[0]
        if i.mnemonic == 'sw':
            m = re.match(r'\$\w+, (-?(?:0x)?[0-9a-fA-F]+)\(\$\w+\)', i.op_str)
            if m:
                o = int(m.group(1), 0)
                if o in (0x2C, 0x30, 0x34) or 8 <= o <= 0x28:
                    offs.add(o)
    bin_result = 0x2C in offs
    dll_result = None
    if op in blocks:
        blk = blocks[op]
        dll_result = bool(re.search(r'\bResult\s*=', blk)) or any(h in blk for h in RESULT_HELPERS)
    rows.append((op, bin_result, dll_result, sorted(hex(o) for o in offs)))

with open(OUT + '/result-check.tsv', 'w', encoding='utf-8', newline='\n') as f:
    f.write('opcode\tbinary_writes_Result\tdll_writes_Result\tbinary_state_offsets_stored\n')
    for op, br, dr, offs in rows:
        f.write('%02X\t%s\t%s\t%s\n' % (op, br, dr if dr is not None else 'n/a(skipped)', ' '.join(offs)))
mism = [(op, br, dr) for op, br, dr, _ in rows if dr is not None and br != dr]
print('DLL cases compared', sum(1 for r in rows if r[2] is not None), 'disagreements', len(mism))
for op, br, dr in mism:
    print('%02X binary=%s dll=%s' % (op, br, dr))
