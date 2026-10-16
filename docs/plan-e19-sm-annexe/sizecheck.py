#!/usr/bin/env python
"""Cross-check the size table against the binary (label script2). Read-only on ALUN_CD.EXE.

For each opcode 0x00-0xC4, disassemble the handler from the handler table (0x80098FAC + 4*op) up to the next handler
address, and collect the constants loaded in $v0 by 'addiu $v0,$zero,N' / 'ori $v0,$zero,N' (the instruction size a handler
returns when it falls through). A handler whose size appears in that set agrees; otherwise it is listed for a manual look.
This is a heuristic (a handler can return a computed jump delta, share an epilogue, or call a sub-function): INFERENCE only.
Output: size-check.tsv
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

src = open(REPO + '/Alundra/Scripts/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),', src)}

base = 0x80098FAC
tab = [w(base + 4 * i) for i in range(256)]
addrs = sorted(set(tab[:0xC5]))
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = False

def next_boundary(a):
    for x in addrs:
        if x > a:
            return x
    return a + 0x400

rows = []
for op in range(0xC5):
    a = tab[op]
    b = next_boundary(a)
    consts = set()
    n = (b - a) // 4
    words = [w(a + 4 * k) for k in range(n)]
    for k, wd in enumerate(words):
        ins = list(md.disasm(struct.pack('<I', wd), a + 4 * k))
        if not ins:
            continue
        i = ins[0]
        m = re.match(r'\$v0, \$zero, (-?(?:0x)?[0-9a-fA-F]+)$', i.op_str)
        if i.mnemonic in ('addiu', 'ori') and m:
            consts.add(int(m.group(1), 0))
    sz = SIZES.get(op)
    status = 'agree' if sz in consts else ('size0-table' if sz == 0 else 'check')
    rows.append((op, a, b - a, sz, sorted(consts), status))

with open(OUT + '/size-check.tsv', 'w', encoding='utf-8', newline='\n') as f:
    f.write('opcode\thandler\tspan_bytes\ttable_size\tv0_constants\tstatus\n')
    for op, a, span, sz, consts, status in rows:
        f.write('%02X\t%08X\t%d\t%s\t%s\t%s\n' % (op, a, span, sz, ' '.join(str(c) for c in consts), status))
print(sum(1 for r in rows if r[5] == 'agree'), 'agree;', sum(1 for r in rows if r[5] == 'check'), 'to check;', sum(1 for r in rows if r[5] == 'size0-table'), 'table size 0')
for r in rows:
    if r[5] == 'check':
        print('%02X %08X span=%d table=%s consts=%s' % (r[0], r[1], r[2], r[3], r[4]))
