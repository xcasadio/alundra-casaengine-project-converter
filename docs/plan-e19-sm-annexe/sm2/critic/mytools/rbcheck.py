import re, sys, collections
sys.path.insert(0, '.')
from dllmodel import *

P = sys.argv[1]
txt = open(P + '/proposed/RealBytesExpected.txt', encoding='utf-8').read().split('\n')
scen = []
cur = None
for ln in txt:
    m = re.match(r'=== (\S+)\s+(\d+) C\[(\d+)\]', ln)
    if m:
        cur = {'name': m.group(1), 'map': int(m.group(2)), 'c': int(m.group(3)), 'ticks': []}
        scen.append(cur)
        continue
    m = re.match(r'\s+map (\d+), program C\[(\d+)\], entry (\d+), first call at CodeIndex (\d+), Result0 (\d+)', ln)
    if m:
        cur['entry'] = int(m.group(3))
        cur['start'] = int(m.group(4))
        cur['r0'] = int(m.group(5))
        continue
    m = re.match(r'\s+tick (\d+)\s+pos=\((0x[0-9A-Fa-f]+), (0x[0-9A-Fa-f]+), (0x[0-9A-Fa-f]+)\)', ln)
    if m:
        cur['ticks'].append({'pos': tuple(int(m.group(i), 16) for i in (2, 3, 4))})
        continue
    m = re.match(r'\s+(port|today)\s*: executed (.*?)\s+Result=(\d+) CodeIndex=(\d+) P2\.\.P4=\((0x[0-9A-Fa-f]+), (0x[0-9A-Fa-f]+), (0x[0-9A-Fa-f]+)\) TargetDirection=(\d+)', ln)
    if m:
        ex = [(int(t.split(':')[0]), int(t.split(':')[1], 16)) for t in m.group(2).split()]
        cur['ticks'][-1][m.group(1)] = dict(ex=ex, res=int(m.group(3)), ci=int(m.group(4)),
                                            p=tuple(int(m.group(i), 16) for i in (5, 6, 7)), td=int(m.group(8)))
bad = 0
tot = 0
for sc in scen:
    d = load_codes(sc['map'])
    codes = d['Codes']
    assert d['EventCodesCTable'][sc['c']] == sc['entry'], (sc['name'], d['EventCodesCTable'][sc['c']], sc['entry'])
    for mode in ('port', 'today'):
        r = Runner(mode)
        e = Ent()
        st = St(codes, sc['start'], sc['r0'])
        for t, tk in enumerate(sc['ticks'], 1):
            e.PosX, e.PosY, e.PosZ = tk['pos']
            ex = r.run(e, st)
            got = dict(ex=ex, res=st.Result, ci=st.CodeIndex, p=tuple(x & M32 for x in st.P[2:5]), td=e.TargetDirection)
            exp = tk.get(mode)
            if exp is None:
                continue
            tot += 1
            ok = got['ex'] == exp['ex'] and got['res'] == exp['res'] and got['ci'] == exp['ci'] and got['p'] == exp['p'] and got['td'] == exp['td']
            if not ok:
                bad += 1
                print('DIFF', sc['name'], mode, 'tick', t)
                print('  mine', got)
                print('  prep', exp)
print('scenarios', len(scen), 'tick-checks', tot, 'diffs', bad)
