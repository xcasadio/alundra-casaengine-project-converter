"""Critic's own model of the DLL runner at HEAD (subset of opcodes), written from the C# source read at HEAD.
mode 'port': 6A-6D implemented as in the proposal ; mode 'today': 6A-6D skipped by size (UnknownOpcode)."""
import json, os, re
M32 = 0xFFFFFFFF


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJECT = REPO + '/alundra-project'
IDX = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))


def load_codes(mid):
    rel = IDX[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    folder = PROJECT + '/' + os.path.dirname(rel)
    d = json.load(open(folder + '/events/' + base + '.events.json', encoding='utf-8'))
    return d


# sizes from the DLL table, parsed by me
SRC = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', SRC)}


def sext16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


class Ent:
    def __init__(s):
        s.PosX = s.PosY = s.PosZ = 0
        s.TargetDirection = 0
        s.TargetAnim = 0
        s.ForceZ = 0
        s.ForceAdjusted = 0
        s.Collided = 0


class St:
    def __init__(s, codes, ci=0, result=0):
        s.Codes = codes
        s.CodeIndex = ci
        s.P = [0] * 10
        s.Result = result
        s._30 = 0
        s._34 = 0
        s.Sp = 0


class Runner:
    def __init__(s, mode='port', flags=None):
        s.mode = mode
        s.flags = flags or {}   # flag id -> word
        s.trace = []
        s.unknown = []

    def fill(s, st):
        c = st.Codes
        if c is None or st.CodeIndex >= len(c):
            return [0xFF] + [0] * 9
        v = [0] * 10
        for i in range(min(10, len(c) - st.CodeIndex)):
            v[i] = c[st.CodeIndex + i]
        return v

    def getflag(s, fid):
        return s.flags.get(fid, 0)

    def dispatch(s, cmd, logic, owner, v, st):
        m = s.mode
        if cmd == 0x01:
            return 1
        if cmd == 0x02:
            return sext16((v[2] << 8) | v[1])
        if cmd == 0x03:
            return sext16((v[2] << 8) | v[1]) if st.Result != 0 else 3
        if cmd == 0x04:
            return sext16((v[2] << 8) | v[1]) if st.Result == 0 else 3
        if cmd == 0x08:
            logic.TargetDirection = (logic.TargetDirection + v[1]) & 0x1f
            return 2
        if cmd == 0x09:
            logic.TargetDirection = v[1] & 0x1f
            return 2
        if cmd == 0x0A:
            logic.TargetDirection = (logic.TargetDirection + 0x10) & 0x1f
            return 1
        if cmd == 0x1A:
            logic.TargetAnim = v[1]
            return 2
        if cmd == 0x1B:
            logic.ForceZ = sext16((v[2] << 8) | v[1]) * 0x10000 >> 8
            return 3
        if cmd == 0x2A:
            return 1
        if cmd in (0x30, 0x31):
            flag = v[1] + v[2] * 256
            mask = 1 << (v[1] & 0x1f)
            isset = (s.getflag(flag) & mask) != 0
            want = (cmd == 0x30)
            return sext16((v[4] << 8) | v[3]) if isset == want else 5
        if cmd == 0x37:
            if st.P[1] != st.CodeIndex:
                st.P[1] = st.CodeIndex
                st.P[2] = 0
                return 0
            c = st.P[2]
            st.P[2] = c + 1
            return 2 if c >= v[1] else 0
        if cmd == 0x6E:
            st.Result = logic.ForceAdjusted
            return 1
        if cmd == 0x6F:
            st.Result = logic.Collided
            return 1
        if cmd == 0x78:
            st._34 = st.CodeIndex + 3
            return sext16((v[2] << 8) | v[1])
        if cmd == 0x7D:
            return st._34 - st.CodeIndex
        if cmd == 0x7E:
            return st._34 - st.CodeIndex if st.Result != 0 else 1
        if cmd == 0x7F:
            return st._34 - st.CodeIndex if st.Result == 0 else 1
        if cmd == 0x89:
            return 9   # bare host: no reference
        if cmd in (0x6A, 0x6B, 0x6C, 0x6D) and m == 'port':
            if cmd == 0x6A:
                st.P[2] = logic.PosX
                st.P[3] = logic.PosY
                st.P[4] = logic.PosZ
                return 1
            k = cmd - 0x6B
            pos = (logic.PosX, logic.PosY, logic.PosZ)[k]
            d = s32(st.P[2 + k] - pos)
            if d < 0:
                d = s32(-d)
            thr = s32(((v[1] | (v[2] << 8)) << 16))
            st.Result = 0 if d < thr else 1
            return 3
        # unknown
        if cmd not in SIZES or SIZES[cmd] <= 0:
            s.unknown.append((cmd, 'nosize'))
            return 0
        s.unknown.append((cmd, 'skipped'))
        return SIZES[cmd]

    def run(s, owner, st, logic=None):
        """one RunOneScriptCall; returns list of (pc,opcode)"""
        logic = logic or owner
        ex = []
        it = 0
        while True:
            it += 1
            if it > 1024:
                ex.append(('LOOPBUDGET',))
                return ex
            ci = st.CodeIndex
            v = s.fill(st)
            if st.Codes is not None and st.CodeIndex < len(st.Codes):
                st.Sp = st.Codes[st.CodeIndex]
            cmd = v[0]
            if cmd == 0xFF:
                ex.append((ci, 0xFF))
                return ex
            if cmd == 0:
                st.P[1] = 0
                st.CodeIndex += 1
                ex.append((ci, 0))
                return ex
            r = s.dispatch(cmd, logic, owner, v, st)
            ex.append((ci, cmd))
            if r == 0:
                return ex
            st.P[1] = 0
            st.CodeIndex += r
