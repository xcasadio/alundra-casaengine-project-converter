import struct
from capstone import *
from capstone.mips import *
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
d = open(EXE, 'rb').read()
taddr = 0x80020000
def off(a): return 0x800 + (a - taddr)
def u32(a): return struct.unpack_from("<I", d, off(a))[0]
tab = 0x80098FAC
H = {op: u32(tab + 4 * op) for op in range(0, 0xC5)}
addrs = sorted(set(H.values()))
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = True
out = []
for op, h in sorted(H.items()):
    nxt = [a for a in addrs if a > h]
    end = nxt[0] if nxt else h + 0x400
    ins = list(md.disasm(d[off(h):off(end)], h))
    # reg -> offset from a3 (None if not derived)
    der = {MIPS_REG_A3: 0}
    hits = []
    passes = []
    for i in ins:
        m = i.mnemonic; ops = i.operands
        # stores
        if m in ('sw', 'sh', 'sb'):
            mem = ops[1].mem
            if mem.base in der:
                eff = der[mem.base] + mem.disp
                hits.append((eff, i.address, m + ' ' + i.op_str))
        # register definitions
        dest = None
        if ops and ops[0].type == MIPS_OP_REG and m not in ('sw', 'sh', 'sb', 'beq', 'bne', 'beqz', 'bnez', 'bgez', 'bgtz', 'blez', 'bltz', 'jr', 'j', 'jal', 'jalr', 'nop'):
            dest = ops[0].reg
        if dest is not None:
            if m == 'move' and ops[1].type == MIPS_OP_REG and ops[1].reg in der:
                der[dest] = der[ops[1].reg]
            elif m == 'addiu' and ops[1].type == MIPS_OP_REG and ops[1].reg in der and ops[2].type == MIPS_OP_IMM:
                der[dest] = der[ops[1].reg] + ops[2].imm
            elif m == 'addu' and any(o.type == MIPS_OP_REG and o.reg in der for o in ops[1:]):
                # unknown offset: mark as unknown-derived
                der[dest] = 'unk'
            else:
                der.pop(dest, None)
        # calls: pass of derived pointer in a0..a3
        if m in ('jal', 'jalr'):
            pa = [(r, der[r]) for r in (MIPS_REG_A0, MIPS_REG_A1, MIPS_REG_A2, MIPS_REG_A3) if r in der]
            if pa:
                passes.append((i.address, i.op_str, pa))
            # after a call a0-a3,v0,v1,t* are clobbered
            for r in (MIPS_REG_A0, MIPS_REG_A1, MIPS_REG_A2, MIPS_REG_A3, MIPS_REG_V0, MIPS_REG_V1, MIPS_REG_T0, MIPS_REG_T1, MIPS_REG_T2, MIPS_REG_T3):
                der.pop(r, None)
    # fix 'unk' offsets: avoid int comparison
    hits2 = [x for x in hits if isinstance(x[0], int)]
    cells = [(hex(e), hex(a)) for (e, a, t) in hits2 if e in (0xC, 0x10, 0x14)]
    unk = [(hex(a), t) for (e, a, t) in hits if not isinstance(e, int)]
    out.append((op, h, cells, unk, [(hex(a), o, [(str(r), x) for r, x in pa]) for a, o, pa in passes]))
for op, h, cells, unk, passes in out:
    if cells or unk or passes:
        print("%02X %08x writes=%s unk=%s calls_with_a3=%s" % (op, h, cells, unk, passes))
