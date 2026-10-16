"""Which handlers touch the program-state words +0xC/+0x10/+0x14 (Parameters[2..4]) and +8 (Parameters[1]) through a3 (the state pointer)?
Heuristic (read-only): in each handler span, track registers aliasing a3 (entry a3, `move rX, a3`), and report loads/stores with those bases.
Usage: python -B stateuse.py"""
import capstone, exelib, collections

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True
TABLE = 0x80098FAC
H = {o: exelib.u32(TABLE + 4 * o) for o in range(256)}
STARTS = sorted(set(H.values()))
LOADS = ("lw", "lbu", "lb", "lhu", "lh")
STORES = ("sw", "sb", "sh")
WATCH = {4: 'P0', 8: 'P1', 0xC: 'P2', 0x10: 'P3', 0x14: 'P4', 0x18: 'P5', 0x1C: 'P6', 0x20: 'P7', 0x24: 'P8', 0x28: 'P9', 0x2C: 'Result', 0x30: '_30', 0x34: '_34'}
rows = {}
for op in range(256):
    s = H[op]
    nxt = [a for a in STARTS if a > s]
    e = nxt[0] if nxt else s + 0x200
    if e - s > 0x600:
        continue
    code = exelib.DATA[exelib.off(s):exelib.off(s) + (e - s)]
    alias = {'a3'}
    used = collections.OrderedDict()
    for ins in md.disasm(code, s):
        m = ins.mnemonic
        ops = ins.operands
        if m in ('move',) and len(ops) == 2 and ins.reg_name(ops[1].reg) == 'a3':
            alias.add(ins.reg_name(ops[0].reg))
        elif m in LOADS + STORES and ops[1].type == capstone.mips.MIPS_OP_MEM:
            base = ins.reg_name(ops[1].mem.base)
            if base in alias and ops[1].mem.disp in WATCH:
                used.setdefault(WATCH[ops[1].mem.disp], set()).add('W' if m in STORES else 'R')
        # an alias is dropped when overwritten (rough)
        if m in LOADS + ('addiu', 'addu', 'lui', 'ori', 'or') and ops and ops[0].type == capstone.mips.MIPS_OP_REG:
            r = ins.reg_name(ops[0].reg)
            if r in alias and r != 'a3' and not (m == 'or' and False):
                alias.discard(r)
        if m in ('jal',):
            alias.discard('a3')  # a3 is caller-saved; only copies in s-regs survive
            alias = {r for r in alias if r.startswith('s')}
            alias.add('a3x')
    if used:
        rows[op] = used
for op, used in rows.items():
    flds = ' '.join(f"{k}:{''.join(sorted(v))}" for k, v in used.items())
    print(f"{op:#04x} {H[op]:#010x}  {flds}")
