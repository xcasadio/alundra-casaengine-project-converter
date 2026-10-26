"""scan87.py - read-only: every use of the opcode 0x87 (touched by an attack type) in the exported events, with its parameters, per map. Run: python -B -I -X utf8 scan87.py"""
import sys, glob, json, re, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
res = collections.defaultdict(list)
for f in sorted(glob.glob(de.ROOT + '/alundra-project/Maps/*/*/events/*.events.json')):
    f = f.replace(chr(92), '/')
    m = re.search(r'-(\d+)\.events\.json$', f)
    n = int(m.group(1))
    doc = json.load(open(f, encoding='utf-8'))
    for slot, i, s, e in de.all_programs(doc):
        for pc, op, v, bad in de.sweep(doc, s, e):
            if bad:
                break
            if op == 0x87:
                res[n].append((slot, i, pc, list(v)))
for n in sorted(res):
    vs = collections.Counter(tuple(v) for _, _, _, v in res[n])
    print('map', n, dict(vs))
