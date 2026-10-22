"""Critic: my OWN mini-interpreter of the opcodes that occur in the 53 real-bytes scenarios, written from the DLL source (Dispatch cases read
at HEAD), to re-derive the 'red today' end states (0x60/0x61/0x3D skipped by size: Result untouched) and compare them with the preparation's.
Bytes come from the EXPORT (Codes), cut at the scenario's cut pc: a fetch at or beyond the cut reads 0xFF (End).
The world is not needed for the red-today run, except 0x43 (Result 1 when the searched record exists, logic := that record) - every record exists here,
and the 'logic' slot is compared by record id."""
import json, re, glob, sys

root = "D:/development/repo/alundra-casaengine-project-converter"
BASE = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm1/tests/"
S = json.load(open(BASE + 'real-bytes-scenarios.json'))
s_ = open(root + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8-sig').read()
SZ = {int(a, 16): int(b) for a, b, c in re.findall(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((-?\d+),\s*"([^"]*)"', s_)}

idx = json.load(open(root + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
codes_of = {}


def codes(mapid):
    if mapid not in codes_of:
        rel = idx[str(mapid)].replace(chr(92), '/')
        base = rel.rsplit('/', 1)[1][:-6]
        folder = root + '/alundra-project/' + rel.rsplit('/', 1)[0]
        d = json.load(open(folder + '/events/' + base + '.events.json', encoding='utf-8-sig'))
        codes_of[mapid] = d['Codes']
    return codes_of[mapid]


def s16(b0, b1):
    v = b0 | (b1 << 8)
    return v - 65536 if v >= 32768 else v


class St:
    pass


def run_call(code, cut, st, skipped):
    """One RunOneScriptCall. Returns nothing; mutates st (ci, result, flags, p0, p1, p2, t30, logic_id)."""
    n = 0
    while True:
        n += 1
        if n > 4000:
            raise RuntimeError('loop')
        if st.ci >= cut:
            return  # FillDataFromCommand: 0xFF -> End
        v = [code[st.ci + i] if st.ci + i < cut else 0 for i in range(10)]
        op = v[0]
        if op == 0xFF:
            return
        if op == 0:
            st.p1 = 0
            st.ci += 1
            return
        r = None
        if op == 0x02:
            r = s16(v[1], v[2])
        elif op == 0x03:
            r = s16(v[1], v[2]) if st.result != 0 else 3
        elif op == 0x04:
            r = s16(v[1], v[2]) if st.result == 0 else 3
        elif op == 0x05:
            st.flags.add((v[2] << 8) | v[1])
            r = 3
        elif op == 0x06:
            st.flags.discard((v[2] << 8) | v[1])
            r = 3
        elif op == 0x30:
            r = s16(v[3], v[4]) if ((v[2] << 8) | v[1]) in st.flags else 5
        elif op == 0x31:
            r = s16(v[3], v[4]) if ((v[2] << 8) | v[1]) not in st.flags else 5
        elif op == 0x37:
            if st.p1 != st.ci:
                st.p1 = st.ci
                st.p2 = 0
                r = 0
            else:
                c = st.p2
                st.p2 = c + 1
                r = 2 if c >= v[1] else 0
        elif op == 0x49:
            r = st.p0 - st.ci
        elif op == 0x4A:
            r = (st.p0 - st.ci) if st.result != 0 else 1
        elif op == 0x4B:
            r = (st.p0 - st.ci) if st.result == 0 else 1
        elif op == 0x42:
            st.logic = 'hero'
            r = 1
        elif op == 0x43:
            st.logic = v[1]
            st.result = 1
            r = 2
        elif op == 0x73:
            st.t30 = v[1]
            r = 2
        elif op == 0x74:
            st.t30 -= 1
            r = 3 if st.t30 <= 0 else s16(v[1], v[2])
        elif op in (0x60, 0x61):
            if not skipped:
                raise RuntimeError('implemented run needs a world')
            r = 2
        elif op == 0x3D:
            if not skipped:
                raise RuntimeError('implemented run needs a world')
            r = 7
        elif op in (0x1A, 0x59, 0x5B, 0xBD, 0x63, 0x62, 0x10, 0x11):
            r = SZ[op]  # effect opcodes without bearing on pc/Result/flags of these scenarios
        else:
            raise RuntimeError('opcode %02X at %d not modelled' % (op, st.ci))
        if r == 0:
            return
        st.p1 = 0
        st.ci += r


ok = 0
bad = []
for sc in S:
    code = codes(sc['map'])
    cut = sc['cut']
    st = St()
    st.ci = sc['start']
    st.result = sc['result0']
    st.flags = set(sc['flags_at_start'])
    st.p0 = sc['entry']
    st.p1 = 0
    st.p2 = 0
    st.t30 = 0
    st.logic = 'hero'
    # sanity: the scenario's bytes equal the export's bytes at the same pcs
    hexb = ' '.join('%02X' % b for b in code[sc['start']:cut])
    if hexb != sc['bytes_hex']:
        bad.append((sc['id'], sc['title'], 'BYTES DIFFER', hexb, sc['bytes_hex']))
        continue
    got = []
    for call in range(sc['calls']):
        run_call(code, cut, st, skipped=True)
        got.append((st.ci, st.result, sorted(st.flags)))
    want = [(e['end_pc'], e['result'], sorted(e['flags'])) for e in sc['red']]
    # the first end-state of an 'expected' run may involve the start flags: compare end_pc/result/flags
    if got != want:
        bad.append((sc['id'], sc['title'], got, want))
    else:
        ok += 1
print('scenarios', len(S), 'red-today reproduced by my interpreter', ok)
for b in bad:
    print('MISMATCH', b)
