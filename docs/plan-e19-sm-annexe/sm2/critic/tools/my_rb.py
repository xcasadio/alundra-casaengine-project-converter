"""Own model of the DLL interpreter for the opcodes of the real sm2 programs. Replays the RB scenarios of the preparation
(RealBytesExpected.txt) with and without the port, from the real exported bytes. Written from the DLL source (EventProgramRunner.cs), not from mini_prog.py."""
import sys, re, collections
sys.path.insert(0, 'tools')
from mycorpus import *

M32 = 0xFFFFFFFF
def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x

class St:
    def __init__(s, codes, start, result0, p0=None):
        s.codes = codes; s.ci = start; s.result = result0
        s.P = [0] * 10; s.P[0] = start if p0 is None else p0
        s._34 = 0; s._30 = 0
class Ent:
    def __init__(s): s.x = s.y = s.z = 0; s.dir = 0; s.anim = 0; s.force_adjusted = 0

def moved(memo, pos, lo, hi, port):
    # port-only helper; the DLL formula (32-bit)
    d = s32(memo - pos)
    if d < 0: d = s32(-d)
    thr = s32(((lo | (hi << 8)) << 16))
    return not (d < thr)

def flag_clear(): return False   # all flags clear

def run_call(st, ent, port):
    trace = []
    it = 0
    C = st.codes
    def byte(i): return C[i] if 0 <= i < len(C) else 0
    while True:
        it += 1
        assert it < 500, 'loop'
        ci = st.ci
        v = [byte(ci + k) for k in range(10)] if ci < len(C) else [0xFF] + [0] * 9
        cmd = v[0]
        if cmd == 0xFF:
            trace.append((ci, cmd)); return trace
        if cmd == 0x00:
            st.P[1] = 0; st.ci += 1; trace.append((ci, 0)); return trace
        sz = SIZE.get(cmd, 1)
        res = None
        if cmd == 0x02: res = s16(v[1] | (v[2] << 8))
        elif cmd == 0x03: res = s16(v[1] | (v[2] << 8)) if st.result != 0 else 3
        elif cmd == 0x04: res = s16(v[1] | (v[2] << 8)) if st.result == 0 else 3
        elif cmd == 0x08: ent.dir = (ent.dir + v[1]) & 0x1F; res = 2
        elif cmd == 0x0A: ent.dir = (ent.dir + 0x10) & 0x1F; res = 1
        elif cmd == 0x1A: ent.anim = v[1]; res = 2
        elif cmd == 0x1B: res = 3
        elif cmd == 0x2A or cmd == 0x2B: res = 1
        elif cmd == 0x30: res = 5          # flag on?  all clear: not taken (size 5)
        elif cmd == 0x31: res = s16(v[3] | (v[4] << 8))   # flag off: taken when clear
        elif cmd == 0x49: res = st.P[0] - st.ci
        elif cmd == 0x6A:
            if port: st.P[2], st.P[3], st.P[4] = ent.x, ent.y, ent.z
            res = 1
        elif cmd in (0x6B, 0x6C, 0x6D):
            if port:
                k = cmd - 0x6B
                pos = (ent.x, ent.y, ent.z)[k]
                st.result = 1 if moved(st.P[2 + k], pos, v[1], v[2], True) else 0
            res = 3
        elif cmd == 0x6E: st.result = ent.force_adjusted; res = 1
        elif cmd == 0x6F: st.result = 0; res = 1
        elif cmd == 0x16 or cmd == 0x17: res = 1
        elif cmd == 0x37: res = 2
        elif cmd == 0x78: st._34 = st.ci + 3; res = s16(v[1] | (v[2] << 8))
        elif cmd == 0x7D: res = st._34 - st.ci
        elif cmd == 0x7F: res = (st._34 - st.ci) if st.result == 0 else 1
        elif cmd == 0x89: res = 9
        else: raise Exception('opcode not modelled %02X @%d' % (cmd, ci))
        trace.append((ci, cmd))
        st.P[1] = 0
        st.ci += res

def parse_expected(path):
    scen = {}; cur = None
    for line in open(path, encoding='utf-8'):
        m = re.match(r'=== (\S+)\s+(\d+) C\[(\d+)\]', line)
        if m: cur = m.group(1); scen[cur] = {'map': int(m.group(2)), 'prog': int(m.group(3)), 'ticks': []}; continue
        m = re.search(r'first call at CodeIndex (\d+), Result0 (\d+)', line)
        if m and cur: scen[cur]['start'] = int(m.group(1)); scen[cur]['r0'] = int(m.group(2)); continue
        m = re.match(r'\s+tick (\d+)\s+pos=\((0x[0-9A-F]+), (0x[0-9A-F]+), (0x[0-9A-F]+)\)', line)
        if m and cur: scen[cur]['ticks'].append({'pos': tuple(int(m.group(i), 16) for i in (2, 3, 4))}); continue
        m = re.match(r'\s+(port|today)\s*: executed (.*?)\s+Result=(\d+) CodeIndex=(\d+) P2\.\.P4=\((0x[0-9A-F]+), (0x[0-9A-F]+), (0x[0-9A-F]+)\) TargetDirection=(\d+)', line)
        if m and cur:
            ex = [tuple(int(x, 16) if i else int(x) for i, x in enumerate(t.split(':'))) for t in m.group(2).split()]
            # tokens look like 272:6A -> (pc decimal, opcode hex)
            ex = []
            for t in m.group(2).split():
                a, b = t.split(':'); ex.append((int(a), int(b, 16)))
            scen[cur]['ticks'][-1][m.group(1)] = {'ex': ex, 'res': int(m.group(3)), 'ci': int(m.group(4)), 'P': tuple(int(m.group(i), 16) for i in (5, 6, 7)), 'dir': int(m.group(8))}
    return scen

if __name__ == '__main__':
    P = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm2/proposed/RealBytesExpected.txt'
    scen = parse_expected(P)
    bad = 0
    for name, sc in scen.items():
        ev, tm, b = load(sc['map'])
        for mode in ('port', 'today'):
            st = St(ev['Codes'], sc['start'], sc['r0'], p0=table(ev, 'C')[sc['prog']])
            ent = Ent()
            for ti, t in enumerate(sc['ticks']):
                ent.x, ent.y, ent.z = t['pos']
                tr = run_call(st, ent, mode == 'port')
                got = {'ex': tr, 'res': st.result, 'ci': st.ci, 'P': tuple(st.P[2:5]), 'dir': ent.dir}
                exp = t[mode]
                exp2 = {'ex': [(a, b) for a, b in exp['ex']], 'res': exp['res'], 'ci': exp['ci'], 'P': exp['P'], 'dir': exp['dir']}
                if got != exp2:
                    bad += 1
                    print('DIFF', name, mode, 'tick', ti + 1)
                    print('   mine', got); print('   prep', exp2)
    print('scenarios', len(scen), 'mismatches', bad)
