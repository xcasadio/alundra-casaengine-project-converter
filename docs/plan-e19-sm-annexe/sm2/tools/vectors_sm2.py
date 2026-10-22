"""E19.sm2 value vectors: the REAL handlers 0x6A..0x6D of ALUN_CD.EXE (France), run in the minimal MIPS-I interpreter (emu.py, copy), on
deterministic edge cases + a fixed-seed random block. Writes ../vectors/vectors-sm2.tsv (and a footprint report).

Read-only on the binary (the path is in lib.py). Writes only next to ../ (the output folder). python -B vectors_sm2.py [OUT_TSV]

What is proven here (and stops the run when false):
  * every data read of a handler is in the MAPPED set (an address with no DLL member is a STOP);
  * every write of a handler is in the expected set; no write on the stack; the whole window 0x80100000..0x80190000 is compared;
  * the handler's result v0 is the instruction size (1, 3, 3, 3);
  * the BINARY model (written from the disassembly) and the DLL model (the int32 formula of rule R2, as C# would compute it) both
    reproduce every row.
"""
import hashlib, os, random, struct, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import emu, lib

M32 = 0xFFFFFFFF
s32 = emu.s32
ENT0, STRIDE = 0x80127D30, 0x294
STATE = 0x8013D9E8
CODE = 0x80150000
LO, HI = 0x80100000, 0x80190000
TABLE = 0x80098FAC
LOGIC_SLOT, OWNER_SLOT = 2, 5      # a0 = logic entity, a1 = owner: different, the owner carries decoys
OFF_POS = {'x': 0x114, 'y': 0x118, 'z': 0x11C}
SIZE = {0x6A: 1, 0x6B: 3, 0x6C: 3, 0x6D: 3}
AXIS = {0x6B: 'x', 0x6C: 'y', 0x6D: 'z'}
MEMO_OFF = {'x': 0xC, 'y': 0x10, 'z': 0x14}     # Parameters[2], [3], [4]
PATTERN = bytes(((x * 7 + 3) & 0xFF) for x in range(HI - LO))


def eaddr(slot):
    return ENT0 + slot * STRIDE


# ---------------------------------------------------------------------------------------------------------------- logging machine
class LogEmu(emu.Emu):
    """Emu that logs every DATA read and write (instruction fetches are not logged)."""

    def __init__(self):
        super().__init__()
        self.reads = []
        self.writes = []
        self._fetching = False

    def _log_r(self, a, size, v):
        if not self._fetching:
            self.reads.append((a & 0xFFFFFFFF, size, v))

    def rb(self, a):
        v = super().rb(a); self._log_r(a, 1, v); return v

    def rh(self, a):
        v = super().rh(a); self._log_r(a, 2, v); return v

    def rw(self, a):
        v = super().rw(a); self._log_r(a, 4, v); return v

    def rhs(self, a):
        # emu.rhs calls self.rh (already logged once there): do not log again
        return super().rhs(a)

    def wb(self, a, v):
        self.writes.append((a & 0xFFFFFFFF, 1, v & 0xFF)); super().wb(a, v)

    def wh(self, a, v):
        self.writes.append((a & 0xFFFFFFFF, 2, v & 0xFFFF)); super().wh(a, v)

    def ww(self, a, v):
        self.writes.append((a & 0xFFFFFFFF, 4, v & M32)); super().ww(a, v)

    def step(self):
        pc = self.pc
        self._fetching = True
        w = emu.Emu.rw(self, pc)
        self._fetching = False
        br = self.exec1(pc, w)
        if br is None:
            self.pc = pc + 4
        else:
            self._fetching = True
            dw = emu.Emu.rw(self, pc + 4)
            self._fetching = False
            self.exec1(pc + 4, dw)
            self.pc = br[1] if br[1] is not None else pc + 8


# ---------------------------------------------------------------------------------------------------------------- models
def binary_model(op, instr, pos, memo, result_in):
    """From the disassembly: returns (v0, result_out, memo_out dict)."""
    if op == 0x6A:
        return 1, result_in, {'x': pos['x'], 'y': pos['y'], 'z': pos['z']}
    axis = AXIS[op]
    d = s32((memo[axis] - pos[axis]) & M32)        # subu
    if d >= 0:
        pass                                       # bgez
    else:
        d = s32((-d) & M32)                        # negu (INT_MIN stays INT_MIN)
    thr = s32((((instr[1] | (instr[2] << 8)) << 16)) & M32)   # lbu, lbu, sll 8, or, sll 16
    r = 1 if d < thr else 0                        # slt (signed)
    return 3, r ^ 1, dict(memo)                    # xori 1


def dll_model(op, instr, pos, memo, result_in):
    """The rule R2 formula as C# int arithmetic in an unchecked context (what the case must compute)."""
    def i32(x):
        x &= M32
        return x - (1 << 32) if x & 0x80000000 else x
    if op == 0x6A:
        return 1, result_in, {'x': pos['x'], 'y': pos['y'], 'z': pos['z']}
    axis = AXIS[op]
    distance = i32(i32(memo[axis]) - i32(pos[axis]))
    if distance < 0:
        distance = i32(-distance)                  # unchecked negation: int.MinValue stays int.MinValue
    threshold = i32((instr[1] | (instr[2] << 8)) << 16)
    return 3, (0 if distance < threshold else 1), dict(memo)


# ---------------------------------------------------------------------------------------------------------------- the mapping of reads
def mapped_reads(op):
    """address -> (size, DLL member) that a handler may read. Anything else is a STOP."""
    logic = eaddr(LOGIC_SLOT)
    if op == 0x6A:
        m = {}
        for ax, off in OFF_POS.items():
            m[logic + off] = (4, 'logic EntityScriptProxy.Pos' + ax.upper())
        return m
    m = {STATE + 0: (4, 'state.Codes + state.CodeIndex (the pointer to the instruction; the DLL reads it through FillDataFromCommand)')}
    ax = AXIS[op]
    m[logic + OFF_POS[ax]] = (4, 'logic EntityScriptProxy.Pos' + ax.upper())
    m[STATE + MEMO_OFF[ax]] = (4, 'state.Parameters[%d]' % ({'x': 2, 'y': 3, 'z': 4}[ax]))
    m[CODE + 1] = (1, 'v[1] (instruction byte 1, zero-extended)')
    m[CODE + 2] = (1, 'v[2] (instruction byte 2, zero-extended)')
    return m


def mapped_writes(op):
    if op == 0x6A:
        return {STATE + 0xC: 'state.Parameters[2]', STATE + 0x10: 'state.Parameters[3]', STATE + 0x14: 'state.Parameters[4]'}
    return {STATE + 0x2C: 'state.Result'}


# ---------------------------------------------------------------------------------------------------------------- one run
def load_world(m, instr, pos, memo, result_in):
    base = LO & 0x1FFFFFFF
    m.ram[base:base + (HI - LO)] = PATTERN
    for slot in range(10):
        for off in range(0, STRIDE, 4):
            m.ww(eaddr(slot) + off, 0xA5000000 | (slot << 16) | off)
    for ax, off in OFF_POS.items():
        m.ww(eaddr(LOGIC_SLOT) + off, pos[ax])
        m.ww(eaddr(OWNER_SLOT) + off, 0x0DEC0000 | off)      # decoy: the owner is not the logic entity
    for off in range(0, 0x40, 4):
        m.ww(STATE + off, 0xC0DE0000 | off)
    m.ww(STATE + 0, CODE)
    for ax, off in MEMO_OFF.items():
        m.ww(STATE + off, memo[ax])
    m.ww(STATE + 0x2C, result_in)
    for i, byte in enumerate(list(instr) + [0xFF, 0xFF, 0xFF, 0xFF]):
        m.wb(CODE + i, byte)


def run_one(m, op, instr, pos, memo, result_in):
    m.reads.clear(); m.writes.clear()
    # load_world writes through the logging ww/wb: clear the logs after loading
    load_world(m, instr, pos, memo, result_in)
    m.reads.clear(); m.writes.clear()
    lo = LO & 0x1FFFFFFF
    before = bytes(m.ram[lo:lo + (HI - LO)])
    handler = m.rw(TABLE + 4 * op)
    m.reads.clear()
    v0 = m.call(handler, eaddr(LOGIC_SLOT), eaddr(OWNER_SLOT), STATE, STATE)
    after = bytes(m.ram[lo:lo + (HI - LO)])
    return v0, handler, before, after, list(m.reads), list(m.writes)


def check_footprint(op, reads, writes, before, after):
    ok_r = mapped_reads(op)
    seen = set()
    for a, size, v in reads:
        if a not in ok_r or ok_r[a][0] != size:
            raise SystemExit('STOP: unmapped read op %02X addr %#x size %d value %#x (no DLL member)' % (op, a, size, v))
        seen.add(a)
    if seen != set(ok_r):
        raise SystemExit('STOP: op %02X expected reads %s, saw %s' % (op, sorted(hex(a) for a in ok_r), sorted(hex(a) for a in seen)))
    ok_w = mapped_writes(op)
    for a, size, v in writes:
        if a not in ok_w or size != 4:
            raise SystemExit('STOP: unexpected write op %02X addr %#x size %d value %#x' % (op, a, size, v))
    # whole-window diff
    changed = {}
    for i in range(len(before)):
        if before[i] != after[i]:
            changed[LO + (i & ~3)] = struct.unpack_from('<I', after, i & ~3)[0]
    for a in changed:
        if a not in ok_w:
            raise SystemExit('STOP: op %02X changed a word outside the mapped writes: %#x' % (op, a))
    return changed


# ---------------------------------------------------------------------------------------------------------------- vector catalogue
POS_BASES = [0x00000000, 0x00010000, 0x01800000, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, 0x01234567, 0x00FF0000]
THRESHOLDS = [0x0000, 0x0001, 0x0010, 0x0078, 0x00FF, 0x0100, 0x7FFF, 0x8000, 0x8001, 0xFF00, 0xFFFF]


def vec_list(seed=20261018):
    rng = random.Random(seed)
    out = []
    seen = set()

    def add(group, op, instr, pos, memo, result_in):
        key = (op, tuple(instr), tuple(sorted(pos.items())), tuple(sorted(memo.items())), result_in)
        if key in seen:
            return
        seen.add(key)
        out.append((group, op, list(instr), dict(pos), dict(memo), result_in))

    base_pos = lambda p: {'x': p, 'y': (p * 3 + 0x10000) & M32, 'z': (p * 5 + 0x20000) & M32}
    base_memo = lambda: {'x': 0x11111111, 'y': 0x22222222, 'z': 0x33333333}

    # 0x6A
    for i, p in enumerate(POS_BASES):
        add('save', 0x6A, [0x6A], base_pos(p), base_memo(), [0, 1, 7, M32][i % 4])
    add('save', 0x6A, [0x6A], {'x': 0x80000000, 'y': 0x7FFFFFFF, 'z': 0xFFFFFFFF}, {'x': 0x80000000, 'y': 0x7FFFFFFF, 'z': 0xFFFFFFFF}, 1)  # memo already equal
    add('save', 0x6A, [0x6A], {'x': 0, 'y': 0, 'z': 0}, {'x': 0xDEADBEEF, 'y': 0xCAFEBABE, 'z': 0x0BADF00D}, 0)                       # stale memo overwritten by zeros
    add('save', 0x6A, [0x6A], {'x': 0x00180000, 'y': 0x00280000, 'z': 0x00300001}, {'x': 0, 'y': 0, 'z': 0}, 0)                       # a normal entity

    for op in (0x6B, 0x6C, 0x6D):
        ax = AXIS[op]
        for t in THRESHOLDS:
            lo, hi = t & 0xFF, t >> 8
            thr = s32((t << 16) & M32)
            mags = {0, 1, 2, 0x7FFFFFFF, 0x80000000}
            if thr > 0:
                mags |= {thr - 1, thr, thr + 1, thr * 2 if thr * 2 < 0x80000000 else thr}
            else:
                # negative or zero threshold: |thr| and neighbours (as unsigned magnitudes) plus the bit patterns of thr itself
                mags |= {(thr & M32), ((thr & M32) - 1) & M32, ((thr & M32) + 1) & M32}
            for sign in (+1, -1):
                for j, mag in enumerate(sorted(mags)):
                    pos_v = POS_BASES[(j + (0 if sign > 0 else 3)) % len(POS_BASES)]
                    diff = (sign * mag) & M32
                    memo_v = (pos_v + diff) & M32
                    pos = base_pos(0x00ABCDEF); pos[ax] = pos_v
                    memo = base_memo(); memo[ax] = memo_v
                    add('edge-threshold' if t < 0x8000 else 'edge-negative-threshold', op, [op, lo, hi], pos, memo, [0, 1, 7, M32][(j + t) % 4])
        # 32-bit wrap of the subtraction: memo - pos overflows int32
        wraps = [(0x7FFFFFFF, 0xFFFFFFFF), (0x80000000, 0x00000001), (0x80000000, 0x7FFFFFFF), (0x00000000, 0x80000000), (0xFFFFFFFF, 0x7FFFFFFF),
                 (0x7FFFFFFF, 0x80000000), (0x40000000, 0xC0000000), (0xC0000000, 0x40000000), (0x80000000, 0x80000000), (0x80000000, 0x00000000)]
        for memo_v, pos_v in wraps:
            for t in (0x0000, 0x0078, 0x7FFF, 0x8000, 0xFFFF):
                pos = base_pos(0x00ABCDEF); pos[ax] = pos_v
                memo = base_memo(); memo[ax] = memo_v
                add('edge-wrap', op, [op, t & 0xFF, t >> 8], pos, memo, 7)
        # fresh state: memo 0 (a 0x6D read without 0x6A in the program of a new entity): |0 - Pos| against the threshold
        for pos_v in (0, 1, 0x000FFFFF, 0x00100000, 0x00100001, 0x01000000, 0x01000001, 0x0FFFFFFF, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, 0xFFFF0000):
            for t in (0x0100, 0x0050, 0x0070, 0x0020):
                pos = base_pos(0x00ABCDEF); pos[ax] = pos_v
                memo = base_memo(); memo[ax] = 0
                add('fresh-memo', op, [op, t & 0xFF, t >> 8], pos, memo, 0)
        # the other axes must not matter: change the two other positions and memos around the same read
        for k in range(6):
            pos = {a: rng.getrandbits(32) for a in 'xyz'}
            memo = {a: rng.getrandbits(32) for a in 'xyz'}
            t = rng.choice([0x0078, 0x0010, 0x0001, 0x8000, 0x00FF])
            add('other-axes', op, [op, t & 0xFF, t >> 8], pos, memo, rng.choice([0, 1, 7, M32]))
        # random block (small 16.16 values like the game, then full 32-bit)
        for k in range(80):
            pos = {a: rng.choice([rng.getrandbits(26), rng.getrandbits(32), rng.randint(0, 0x2000000)]) for a in 'xyz'}
            memo = {a: rng.choice([rng.getrandbits(26), rng.getrandbits(32), pos[a] + rng.randint(-0x400000, 0x400000)]) & M32 for a in 'xyz'}
            memo = {a: memo[a] & M32 for a in 'xyz'}
            instr = [op, rng.choice([0, 1, 0x10, 0x78, 0xFF, rng.getrandbits(8)]), rng.choice([0, 0, 0, 1, 0x7F, 0x80, 0xFF, rng.getrandbits(8)])]
            add('random', op, instr, pos, memo, rng.choice([0, 1, 7, M32]))
    # random 0x6A
    for k in range(20):
        pos = {a: rng.getrandbits(32) for a in 'xyz'}
        memo = {a: rng.getrandbits(32) for a in 'xyz'}
        add('random', 0x6A, [0x6A], pos, memo, rng.choice([0, 1, 7, M32]))
    return out


HEADER = ['id', 'group', 'op', 'instr', 'pos_x', 'pos_y', 'pos_z', 'memo_p2', 'memo_p3', 'memo_p4', 'result_in',
          'v0', 'result_out', 'memo_p2_out', 'memo_p3_out', 'memo_p4_out', 'reads', 'writes']


def main():
    out_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'vectors', 'vectors-sm2.tsv')
    m = LogEmu()
    rows = []
    counts = {}
    for n, (group, op, instr, pos, memo, result_in) in enumerate(vec_list()):
        v0, handler, before, after, reads, writes = run_one(m, op, instr, pos, memo, result_in)
        changed = check_footprint(op, reads, writes, before, after)
        res = m.rw(STATE + 0x2C)
        # actual outputs
        memo_out = {ax: m.rw(STATE + off) for ax, off in MEMO_OFF.items()}
        # models
        b = binary_model(op, instr, pos, memo, result_in)
        d = dll_model(op, instr, pos, memo, result_in)
        got = (v0, res, memo_out)
        if got != b:
            raise SystemExit('STOP: binary model disagrees with the real handler: op %02X instr %s pos %s memo %s got %s model %s' % (op, instr, pos, memo, got, b))
        if got != d:
            raise SystemExit('STOP: DLL (C# int32) formula disagrees with the real handler: op %02X instr %s pos %s memo %s got %s model %s' % (op, instr, pos, memo, got, d))
        if v0 != SIZE[op]:
            raise SystemExit('STOP: v0 %d != size %d' % (v0, SIZE[op]))
        rnames = sorted({mapped_reads(op)[a][1].split(' (')[0] for a, s, v in reads})
        wnames = sorted({mapped_writes(op)[a] for a, s, v in writes})
        rows.append([
            'sm2-%04d' % n, group, '%02X' % op, ' '.join('%02X' % x for x in instr),
            '%08X' % pos['x'], '%08X' % pos['y'], '%08X' % pos['z'],
            '%08X' % memo['x'], '%08X' % memo['y'], '%08X' % memo['z'], '%08X' % result_in,
            str(v0), '%08X' % res, '%08X' % memo_out['x'], '%08X' % memo_out['y'], '%08X' % memo_out['z'],
            ';'.join(rnames), ';'.join(wnames)])
        counts[(op, group)] = counts.get((op, group), 0) + 1
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    lines = ['\t'.join(HEADER)] + ['\t'.join(r) for r in rows]
    text = '\n'.join(lines) + '\n'
    with open(out_path, 'w', newline='\n', encoding='utf-8') as f:
        f.write(text)
    print('rows: %d ; sha256 %s' % (len(rows), hashlib.sha256(text.encode('utf-8')).hexdigest()))
    for (op, group), c in sorted(counts.items()):
        print('  op %02X %-24s %d' % (op, group, c))
    # Result histograms
    for op in (0x6B, 0x6C, 0x6D):
        h = {}
        for r in rows:
            if int(r[2], 16) == op:
                h[r[12]] = h.get(r[12], 0) + 1
        print('  op %02X Result histogram: %s' % (op, h))


if __name__ == '__main__':
    main()
