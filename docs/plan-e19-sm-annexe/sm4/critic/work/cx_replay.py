"""Replay THEIR vectors (JSON only) on MY interpreter (cx_mips) running the real handlers; compare with their expectations.
Usage: python -B cx_replay.py <vectors dir>"""
import json, sys, os, collections
import cx_exe as X
import cx_world as W
from cx_mips import M32

vdir = sys.argv[1]
fieldkey = {v[0]: k for k, v in W.F.items()}
back = {'lastAnim': 'lastTargetAnimId', 'lastDir': 'lastTargetDir', 'targetAnimId': 'targetAnimId', 'targetDir': 'targetDir',
        'riding': 'ridingSlot', 'xcoll': 'xCollSlot', 'parent': 'parentSlot', 'platform': 'hasPlatform'}


def classify(addr):
    for i in range(0, 80):
        a = W.ent(i)
        if a <= addr < a + W.STRIDE:
            off = addr - a
            for f, (o, sz) in W.F.items():
                if o <= off < o + sz:
                    return ('slot', i, f)
            return ('slot-other', i, off)
    if W.STATS <= addr < W.STATS + 16:
        for k, (o, sz) in W.STATF.items():
            if W.STATS + o <= addr < W.STATS + o + sz:
                return ('stats', k)
    if W.PAD <= addr < W.PAD + 0x20:
        for k, o in W.PADF.items():
            if W.PAD + o <= addr < W.PAD + o + 2:
                return ('pad', k)
    if 0x8013D8D8 <= addr < 0x8013D8D8 + 0x400:
        return ('buffer',)
    if addr >= 0x801FF000:
        return ('stack',)
    return ('other', hex(addr))


tot = collections.Counter()
bad = []
for fn in ['op72.json', 'op88.json', 'op96.json', 'op97.json', 'opB3.json']:
    d = json.load(open(os.path.join(vdir, fn)))
    op = d['opcode']
    for v in d['vectors']:
        if v.get('outOfTable'):
            tot['skip-outOfTable'] += 1
            continue
        w = v['world']
        ins = list(v['instruction']) + [0xFF]
        m, cpu, ret = W.run(op, w['slots'], w['stats'], ins, v['logicSlot'], v['ownerSlot'], w.get('pad'))
        init_m, _ = W.build(w['slots'], w['stats'], w.get('pad'), ins, v['logicSlot'], v['ownerSlot'])
        n = len(w['slots'])
        snap = W.snapshot(m, n)
        # classify writes -> final values per (kind,key)
        fin = {}
        other = []
        for (a, nb, val) in m.writes:
            for b in range(nb):
                c = classify(a + b)
                if c[0] in ('slot', 'stats', 'pad'):
                    fin[c] = True
                elif c[0] in ('buffer', 'stack'):
                    pass
                else:
                    other.append(c)
        # expected
        e = v['expect']
        exp_slot = {(x['slot'], x['field']): x['value'] for x in e['slotWrites']}
        exp_stats = {x['field']: x['value'] for x in e['statsWrites']}
        exp_pad = {x['field']: x['value'] for x in e['padWrites']}
        problems = []
        if ret != e['returned']:
            problems.append(('returned', ret, e['returned']))
        if other:
            problems.append(('write-elsewhere', other[:3]))
        # my written set
        my_slot = {}
        for c in fin:
            if c[0] == 'slot':
                fld = back.get(c[2], c[2])
                my_slot[(c[1], fld)] = snap['slots'][c[1]][c[2]]
        for (s, f), val in exp_slot.items():
            key = (s, f)
            if key not in my_slot:
                problems.append(('missing-slot-write', key))
            else:
                mine = my_slot[key]
                if f in ('status', 'flags', 'targetAnimId', 'targetDir', 'lastTargetAnimId', 'lastTargetDir'):
                    ok = (mine & M32) == (val & M32)
                else:
                    ok = mine == val
                if not ok:
                    problems.append(('slot-value', key, mine, val))
        for key in my_slot:
            if key not in exp_slot:
                problems.append(('extra-slot-write', key, my_slot[key]))
        my_stats = {c[1]: snap['stats'][c[1]] for c in fin if c[0] == 'stats'}
        if my_stats != exp_stats:
            problems.append(('stats', my_stats, exp_stats))
        my_pad = {c[1]: snap['pad'][c[1]] for c in fin if c[0] == 'pad'}
        if my_pad != exp_pad:
            problems.append(('pad', my_pad, exp_pad))
        if snap['result'] != 7:
            problems.append(('result-changed', snap['result']))
        # red today per my own definition: any observable value differs from the initial snapshot
        init_snap = W.snapshot(init_m, n)
        red = (snap != init_snap)
        if red != v['today']['redToday']:
            problems.append(('redToday', red, v['today']['redToday']))
        tot['vectors'] += 1
        tot['red' if red else 'green'] += 1
        tot['op' + format(op, 'X')] += 1
        if problems:
            tot['bad'] += 1
            bad.append((v['id'], problems))
print(dict(tot))
for b in bad[:30]:
    print(b)
print("BAD", len(bad))
