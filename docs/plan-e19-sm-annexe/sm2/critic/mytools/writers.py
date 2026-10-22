"""Critic: independent scan of every handler of the table 0x80098FAC (ops 0x00..0xC4) for stores into the program state cells
S+0xC/0x10/0x14 (Parameters[2..4]) through $a3 (the state) or an alias of $a3 (move/addu/addiu), and for calls that receive $a3 (helper may write).
Straight-line + all branch targets inside the handler range are scanned (no need for exact CFG: we scan every instruction in [h, next_h))."""
import struct, sys
from capstone import *
from capstone.mips import *

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
d = open(EXE, 'rb').read()
taddr = 0x80020000


def off(a):
    return 0x800 + (a - taddr)


def u32(a):
    return struct.unpack_from("<I", d, off(a))[0]


tab = 0x80098FAC
H = {op: u32(tab + 4 * op) for op in range(0, 0xC5)}
addrs = sorted(set(H.values()))
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = True
REG = {'$a3': MIPS_REG_A3}
res = {}
for op, h in sorted(H.items()):
    # the end: next handler address, else +0x400
    nxt = [a for a in addrs if a > h]
    end = nxt[0] if nxt else h + 0x400
    code = d[off(h):off(end)]
    ins = list(md.disasm(code, h))
    alias = {MIPS_REG_A3}
    stores = []
    calls = []
    a3_args = False
    for i in ins:
        m = i.mnemonic
        ops = i.operands
        # alias tracking (very simple)
        if m in ('move',) and ops[1].type == MIPS_OP_REG and ops[1].reg in alias:
            alias.add(ops[0].reg)
        elif m in ('addiu', 'addu', 'or') and len(ops) >= 2 and any(o.type == MIPS_OP_REG and o.reg in alias for o in ops[1:]) and ops[0].type == MIPS_OP_REG:
            if m == 'addiu' and ops[2].type == MIPS_OP_IMM:
                alias.add(ops[0].reg)   # alias with offset: record separately
                stores.append(('ALIAS-ADDIU', i.address, i.mnemonic + ' ' + i.op_str))
            else:
                alias.add(ops[0].reg)
        if m in ('sw', 'sh', 'sb', 'swl', 'swr'):
            mem = ops[1].mem
            if mem.base in alias:
                stores.append((mem.disp, i.address, i.mnemonic + ' ' + i.op_str))
        if m in ('jal', 'jalr'):
            # does the call get a3/alias in a0..a3?
            calls.append((i.address, i.mnemonic + ' ' + i.op_str))
        if m in ('lw',) and ops[1].mem.base in alias and False:
            pass
    interesting = [s for s in stores if s[0] in (0xC, 0x10, 0x14, 'ALIAS-ADDIU')]
    others = sorted(set(s[0] for s in stores if isinstance(s[0], int)))
    res[op] = (h, end, interesting, others, calls)

print("op  handler   range_end  writes_to_P2P3P4 (disp,addr)   other_a3_store_disps   calls")
for op, (h, end, interesting, others, calls) in sorted(res.items()):
    ws = [(hex(s[0]) if isinstance(s[0], int) else s[0], hex(s[1])) for s in interesting]
    print("%02X  %08x  %08x   %s   %s   %s" % (op, h, end, ws, [hex(o) for o in others], [hex(c[0]) for c in calls][:3] if calls else ''))
