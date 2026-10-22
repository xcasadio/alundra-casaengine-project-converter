"""A small second-method interpreter of REAL exported programs, mirroring the DLL loop (EventProgramRunner.RunOneScriptCall :378-468) for the few
opcodes the sm2 real-bytes tests run. Two modes: 'port' (0x6A-0x6D as rules R1/R2) and 'today' (0x6A-0x6D skipped by their size, Result untouched:
UnknownOpcode :3065-3091). Any opcode not modelled raises, so a scenario can only use what is modelled.
It is NOT the test: it is the independent derivation of the expected values (hand-written in the plan before the code) and of today's red values.
python -B mini_prog.py -> prints every scenario of scenarios.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C

M32 = 0xFFFFFFFF


def i32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


class Unmodelled(Exception):
    pass


class State:
    def __init__(self, codes, code_index, result, params=None):
        self.codes = codes
        self.CodeIndex = code_index
        self.Result = result
        self.P = list(params) if params else [0] * 10
        self._34 = 0
        self.trace = []


class Entity:
    def __init__(self, x=0, y=0, z=0):
        self.PosX, self.PosY, self.PosZ = x, y, z
        self.ForceAdjusted = 0
        self.Dir = 0                                  # TargetDirection (uint, 0..31): 0x08 adds v1, 0x0A adds 16, modulo 32
        # the OWNER of the program (a0 = logic entity differs from a1 = owner when 0x42/0x43 retargeted): its decoy position
        self.OwnerX, self.OwnerY, self.OwnerZ = 0x0DEC0114, 0x0DEC0118, 0x0DEC011C


# opcodes modelled as 'size only' (their effects are not needed by the control flow of the scenarios)
NOOP = {0x1A, 0x1B, 0x16, 0x17, 0x2A, 0x2B, 0x62, 0x63, 0xBD, 0x19, 0x1C, 0x2E, 0x89, 0x6F, 0x3E, 0x36}


def run_call(mode, state, ent, flags, max_instr=200):
    """One RunOneScriptCall. Returns (list of (pc, op, resultAfter)) executed in this call."""
    out = []
    n = 0
    codes = state.codes
    while True:
        n += 1
        if n > max_instr:
            out.append(('LOOP-BUDGET',))
            return out
        pc = state.CodeIndex
        if pc < 0 or pc >= len(codes):
            return out
        op = codes[pc]
        v = list(codes[pc:pc + 10]) + [0] * 10
        v = v[:10]
        if op == 0xFF:
            out.append((pc, op, state.Result))
            return out
        if op == 0x00:
            state.P[1] = 0
            if mode == 'mut-break-clears-memo':
                state.P[2] = state.P[3] = state.P[4] = 0
            state.CodeIndex += 1
            out.append((pc, op, state.Result))
            return out
        size = C.SIZES.get(op, 1) or 1
        r = None
        if 0x6A <= op <= 0x6D and mode == 'today':
            r = size
        elif op == 0x6A:
            px, py, pz = ent.PosX, ent.PosY, ent.PosZ
            if mode == 'mut-reads-owner':
                px, py, pz = ent.OwnerX, ent.OwnerY, ent.OwnerZ
            state.P[2], state.P[3] = px, py
            if mode != 'mut-6a-forgets-z':
                state.P[4] = pz
            if mode == 'mut-6a-writes-result':
                state.Result = 1
            r = 2 if mode == 'mut-6a-size-2' else 1
        elif op in (0x6B, 0x6C, 0x6D):
            k = {0x6B: 2, 0x6C: 3, 0x6D: 4}[op]
            if mode == 'mut-6d-reads-p3' and op == 0x6D:
                k = 3
            pos = {0x6B: ent.PosX, 0x6C: ent.PosY, 0x6D: ent.PosZ}[op]
            if mode == 'mut-reads-owner':
                pos = {0x6B: ent.OwnerX, 0x6C: ent.OwnerY, 0x6D: ent.OwnerZ}[op]
            d = i32(i32(state.P[k]) - i32(pos))
            if mode == 'mut-math-abs' and d == -(1 << 31):
                raise OverflowError('Math.Abs(int.MinValue)')
            if d < 0:
                d = i32(-d)
            thr = i32((v[1] | (v[2] << 8)) << 16)
            if mode == 'mut-pixel-form':
                res = 1 if ((d & M32) >> 16) >= (v[1] | (v[2] << 8)) else 0
            elif mode == 'mut-less-equal':
                res = 0 if d <= thr else 1
            elif mode == 'mut-inverted':
                res = 1 if d < thr else 0
            elif mode == 'mut-no-abs':
                dd = i32(i32(state.P[k]) - i32(pos))
                res = 0 if dd < thr else 1
            elif mode == 'mut-unsigned-threshold':
                res = 0 if (d & M32) < (thr & M32) else 1
            elif mode == 'mut-long-subtraction':
                dl = abs(i32(state.P[k]) - i32(pos))
                res = 0 if dl < thr else 1
            else:
                res = 0 if d < thr else 1
            state.Result = res
            r = 1 if mode == 'mut-6b-size-1' and op == 0x6B else 3
        elif op == 0x02:
            r = s16((v[2] << 8) | v[1])
        elif op == 0x03:
            r = s16((v[2] << 8) | v[1]) if state.Result != 0 else 3
        elif op == 0x04:
            r = s16((v[2] << 8) | v[1]) if state.Result == 0 else 3
        elif op in (0x30, 0x31):
            flag = v[1] + v[2] * 0x100
            mask = 1 << (v[1] & 0x1F)
            is_set = (flags.get(flag, 0) & mask) != 0
            want = op == 0x30
            r = s16((v[4] << 8) | v[3]) if is_set == want else 5
        elif op == 0x37:
            cell = 5 if mode == 'mut-wait-spares-p2' else 2      # the mutant keeps the Wait counter out of the X memo cell
            if state.P[1] != state.CodeIndex:
                state.P[1] = state.CodeIndex
                state.P[cell] = 0
                r = 0
            else:
                counter = state.P[cell]
                state.P[cell] = counter + 1
                r = 2 if counter >= v[1] else 0
        elif op == 0x78:
            state._34 = state.CodeIndex + 3
            r = s16((v[2] << 8) | v[1])
        elif op == 0x7D:
            r = state._34 - state.CodeIndex
        elif op == 0x7F:
            r = (state._34 - state.CodeIndex) if state.Result == 0 else 1
        elif op == 0x6E:
            state.Result = ent.ForceAdjusted
            r = 1
        elif op == 0x08:
            ent.Dir = (ent.Dir + v[1]) & 0x1F
            r = 2
        elif op == 0x0A:
            ent.Dir = (ent.Dir + 0x10) & 0x1F
            r = 1
        elif op in NOOP:
            r = size
        else:
            raise Unmodelled('opcode %02X @%d' % (op, pc))
        out.append((pc, op, state.Result))
        if r == 0:
            return out
        state.P[1] = 0
        state.CodeIndex += r


def program(mid, slot, prog):
    base, d = C.load(mid)
    return d['Codes'], d[C.TABLE_KEYS[slot]][prog]


def simulate_codes(mode, codes, entry, start_pc, result0, ticks, flags=None, params=None):
    """Like simulate() on a made-up program. A tick may carry 'reinit': True (InitializeEventData runs before the call: P0 and CodeIndex are set, the
    other parameters survive - unless the mutant says otherwise)."""
    st = State(codes, entry if start_pc is None else start_pc, result0, params)
    ent = Entity()
    flags = flags or {}
    res = []
    for t in ticks:
        ent.PosX, ent.PosY, ent.PosZ = t['pos']
        ent.ForceAdjusted = t.get('forceadjusted', 0)
        if t.get('reinit'):
            st.P[0] = entry
            st.CodeIndex = entry
            if mode == 'mut-init-clears-memo':
                st.P[2] = st.P[3] = st.P[4] = 0
        if t.get('norun'):
            res.append(([], st.Result, st.CodeIndex, tuple(st.P[2:5]), False, ent.Dir))
            continue
        try:
            ex = run_call(mode, st, ent, flags)
        except OverflowError:
            res.append((['THROWS'], st.Result, st.CodeIndex, tuple(st.P[2:5]), False, ent.Dir))
            continue
        res.append(([e[:2] for e in ex if len(e) == 3], st.Result, st.CodeIndex, tuple(st.P[2:5]), any(len(e) == 1 for e in ex), ent.Dir))
    return res


def simulate(mode, mid, slot, prog, start_pc, result0, ticks, flags=None, params=None):
    """ticks: list of dicts {'pos': (x,y,z) [in 16.16 words], 'forceadjusted': 0/1 (optional)}. Returns a list per tick of
    (executed [(pc, op)], resultAfter, codeIndexAfter, P2..P4 after, loop budget hit)."""
    codes, entry = program(mid, slot, prog)
    return simulate_codes(mode, codes, entry, start_pc, result0, ticks, flags, params)


def fmt(res):
    lines = []
    for i, (ex, result, ci, memo, budget, direction) in enumerate(res, 1):
        lines.append('   tick %d: executed %s | Result=%d CodeIndex=%d memo(P2,P3,P4)=(%s) TargetDirection=%d%s' % (
            i, ' '.join('%d:%02X' % (pc, op) for pc, op in ex), result, ci, ','.join('%08X' % (m & M32) for m in memo), direction, ' LOOP-BUDGET' if budget else ''))
    return '\n'.join(lines)
