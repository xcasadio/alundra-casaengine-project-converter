"""cmipsmini.py - critic's own MIPS-I interpreter for ALUN_CD.EXE (France). Read-only on the exe.
Written independently of the discovery's harness (no code shared). Runs the REAL code of a function with
stubbed call targets, sparse little-endian memory, delay slots honoured, 32-bit wrap.
"""
import struct
import capstone
import exelib

M32 = 0xFFFFFFFF
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True
from capstone import mips_const as MC


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


class Mem:
    def __init__(self):
        self.d = {}      # address -> byte (writes)
        self.reads = []  # (addr, size) trace when enabled
        self.writes = []
        self.trace = False

    def rb(self, a):
        a &= M32
        if a in self.d:
            return self.d[a]
        try:
            return exelib.DATA[exelib.off(a)]
        except ValueError:
            return 0

    def wb(self, a, v):
        self.d[a & M32] = v & 0xFF

    def r32(self, a):
        v = self.rb(a) | self.rb(a + 1) << 8 | self.rb(a + 2) << 16 | self.rb(a + 3) << 24
        if self.trace:
            self.reads.append((a & M32, 4))
        return v

    def w32(self, a, v):
        v &= M32
        for i in range(4):
            self.wb(a + i, v >> (8 * i))
        if self.trace:
            self.writes.append((a & M32, 4, v))

    def r16(self, a):
        if self.trace:
            self.reads.append((a & M32, 2))
        return self.rb(a) | self.rb(a + 1) << 8

    def w16(self, a, v):
        for i in range(2):
            self.wb(a + i, v >> (8 * i))
        if self.trace:
            self.writes.append((a & M32, 2, v & 0xFFFF))

    def r8(self, a):
        if self.trace:
            self.reads.append((a & M32, 1))
        return self.rb(a)

    def w8(self, a, v):
        self.wb(a, v)
        if self.trace:
            self.writes.append((a & M32, 1, v & 0xFF))


_cache = {}


def decode(addr):
    if addr in _cache:
        return _cache[addr]
    code = exelib.DATA[exelib.off(addr):exelib.off(addr) + 4]
    ins = next(md.disasm(code, addr))
    _cache[addr] = ins
    return ins


REG = {}
for n in dir(MC):
    pass
NAMES = ['zero', 'at', 'v0', 'v1', 'a0', 'a1', 'a2', 'a3', 't0', 't1', 't2', 't3', 't4', 't5', 't6', 't7',
         's0', 's1', 's2', 's3', 's4', 's5', 's6', 's7', 't8', 't9', 'k0', 'k1', 'gp', 'sp', 'fp', 'ra']
RN = {n: i for i, n in enumerate(NAMES)}
RN['s8'] = 30


def regnum(s):
    s = s.strip().lstrip('$')
    if s in RN:
        return RN[s]
    return int(s)


class CPU:
    def __init__(self, mem):
        self.m = mem
        self.r = [0] * 32
        self.hi = 0
        self.lo = 0
        self.stubs = {}   # addr -> python callable(cpu) that sets v0
        self.calls = []
        self.steps = 0

    def ea(self, op):
        # "off($reg)" or "($reg)"
        o, rest = op.split('(')
        base = regnum(rest.rstrip(')'))
        off = int(o, 0) if o.strip() else 0
        return (self.r[base] + off) & M32

    def run(self, entry, args=(), sp=0x801FFE00, maxsteps=200000):
        r = self.r
        for i in range(32):
            r[i] = 0
        r[29] = sp
        r[31] = 0xDEAD0000
        for i, a in enumerate(args[:4]):
            r[4 + i] = a & M32
        # args beyond 4 on stack at sp+16...
        for i, a in enumerate(args[4:]):
            self.m.w32(sp + 16 + 4 * i, a)
        pc = entry
        self.steps = 0
        while pc != 0xDEAD0000:
            self.steps += 1
            if self.steps > maxsteps:
                raise RuntimeError('runaway at %#x' % pc)
            if pc in self.stubs:
                self.calls.append((pc, [r[4], r[5], r[6], r[7]], self.m.r32(r[29] + 16), self.m.r32(r[29] + 20), self.m.r32(r[29] + 24)))
                self.stubs[pc](self)
                pc = r[31]
                continue
            ins = decode(pc)
            npc = self.exec(ins, pc)
            if npc is None:
                pc += 4
            else:
                # branch/jump: execute delay slot then jump
                tgt, link = npc
                ds = decode(pc + 4)
                if link:
                    r[31] = pc + 8
                self.exec(ds, pc + 4, in_delay=True)
                pc = tgt
            r[0] = 0
        return r[2]

    def exec(self, ins, pc, in_delay=False):
        r = self.r
        m = ins.mnemonic
        ops = [o.strip() for o in ins.op_str.split(',')] if ins.op_str else []
        R = lambda i: r[regnum(ops[i])]
        def W(i, v):
            n = regnum(ops[i])
            if n:
                r[n] = v & M32
        if m == 'nop':
            return None
        if m in ('addiu', 'addi'):
            W(0, R(1) + int(ops[2], 0)); return None
        if m in ('addu', 'add'):
            W(0, R(1) + R(2)); return None
        if m in ('subu', 'sub'):
            W(0, R(1) - R(2)); return None
        if m == 'negu':
            W(0, -R(1)); return None
        if m == 'move':
            W(0, R(1)); return None
        if m == 'lui':
            W(0, int(ops[1], 0) << 16); return None
        if m == 'ori':
            W(0, R(1) | (int(ops[2], 0) & 0xFFFF)); return None
        if m == 'andi':
            W(0, R(1) & (int(ops[2], 0) & 0xFFFF)); return None
        if m == 'xori':
            W(0, R(1) ^ (int(ops[2], 0) & 0xFFFF)); return None
        if m == 'and':
            W(0, R(1) & R(2)); return None
        if m == 'or':
            W(0, R(1) | R(2)); return None
        if m == 'xor':
            W(0, R(1) ^ R(2)); return None
        if m == 'nor':
            W(0, ~(R(1) | R(2))); return None
        if m == 'slt':
            W(0, 1 if s32(R(1)) < s32(R(2)) else 0); return None
        if m == 'sltu':
            W(0, 1 if R(1) < R(2) else 0); return None
        if m == 'slti':
            W(0, 1 if s32(R(1)) < int(ops[2], 0) else 0); return None
        if m == 'sltiu':
            W(0, 1 if R(1) < (int(ops[2], 0) & M32) else 0); return None
        if m == 'sll':
            W(0, R(1) << int(ops[2], 0)); return None
        if m == 'srl':
            W(0, R(1) >> int(ops[2], 0)); return None
        if m == 'sra':
            W(0, s32(R(1)) >> int(ops[2], 0)); return None
        if m == 'sllv':
            W(0, R(1) << (R(2) & 31)); return None
        if m == 'srlv':
            W(0, R(1) >> (R(2) & 31)); return None
        if m == 'srav':
            W(0, s32(R(1)) >> (R(2) & 31)); return None
        if m == 'mult':
            p = s32(R(0)) * s32(R(1)); self.lo = p & M32; self.hi = (p >> 32) & M32; return None
        if m == 'multu':
            p = R(0) * R(1); self.lo = p & M32; self.hi = (p >> 32) & M32; return None
        if m == 'mflo':
            W(0, self.lo); return None
        if m == 'mfhi':
            W(0, self.hi); return None
        if m == 'lw':
            W(0, self.m.r32(self.ea(ops[1]))); return None
        if m == 'sw':
            self.m.w32(self.ea(ops[1]), R(0)); return None
        if m == 'lh':
            v = self.m.r16(self.ea(ops[1])); W(0, v - 0x10000 if v & 0x8000 else v); return None
        if m == 'lhu':
            W(0, self.m.r16(self.ea(ops[1]))); return None
        if m == 'sh':
            self.m.w16(self.ea(ops[1]), R(0)); return None
        if m == 'lb':
            v = self.m.r8(self.ea(ops[1])); W(0, v - 0x100 if v & 0x80 else v); return None
        if m == 'lbu':
            W(0, self.m.r8(self.ea(ops[1]))); return None
        if m == 'sb':
            self.m.w8(self.ea(ops[1]), R(0)); return None
        # control flow: return (target, link)
        def tgt(i):
            return int(ops[i], 0)
        if m == 'beq':
            return (tgt(2), False) if R(0) == R(1) else self._nt(pc)
        if m == 'bne':
            return (tgt(2), False) if R(0) != R(1) else self._nt(pc)
        if m == 'beqz':
            return (tgt(1), False) if R(0) == 0 else self._nt(pc)
        if m == 'bnez':
            return (tgt(1), False) if R(0) != 0 else self._nt(pc)
        if m == 'blez':
            return (tgt(1), False) if s32(R(0)) <= 0 else self._nt(pc)
        if m == 'bgtz':
            return (tgt(1), False) if s32(R(0)) > 0 else self._nt(pc)
        if m == 'bltz':
            return (tgt(1), False) if s32(R(0)) < 0 else self._nt(pc)
        if m == 'bgez':
            return (tgt(1), False) if s32(R(0)) >= 0 else self._nt(pc)
        if m == 'j':
            return (tgt(0), False)
        if m == 'jal':
            t = tgt(0)
            return (t, True)
        if m == 'jr':
            return (R(0), False)
        if m == 'jalr':
            return (R(0), True)
        raise NotImplementedError('%#x %s %s' % (pc, m, ins.op_str))

    def _nt(self, pc):
        # not taken: execute delay slot then continue at pc+8
        return (pc + 8, False)
