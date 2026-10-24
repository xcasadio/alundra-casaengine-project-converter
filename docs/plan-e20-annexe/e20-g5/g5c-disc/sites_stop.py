"""sites_stop.py - every program site of the sound opcodes 0xBD (Play, 0x800490FC), 0xBE (Stop, 0x80049634) and, for the sibling defect, 0x75 (Stop, byte id, 0x8003FA9C) and 0x12 (Play, byte id) over all exported maps
(alundra-project/Maps/*/*/events/*.events.json, read-only). Writes sites_stop.json and a text summary.
Usage: python -B -X utf8 sites_stop.py"""
import sys, glob, re, collections, json
sys.path.insert(0, '.')
import decode_events as de
CHAIN = {389,390,476,478,392,391,416,163,162,165,164,172,169,170,171,173,174,175,10,179,176,177,178,180,181,182,135,183,184,185}
maps = sorted({int(re.search(r'-(\d+)\.events\.json$', f.replace(chr(92), '/')).group(1)) for f in glob.glob(de.ROOT + '/alundra-project/Maps/*/*/events/*.events.json')})
OPS = {0xBD: 'play16', 0xBE: 'stop16', 0x75: 'stop8', 0x12: 'play8'}
sites = []   # dicts
progs_seq = {}   # (map, slot, idx) -> [(pc, op, id)]
for mp in maps:
    doc = de.load_map(mp)
    if doc is None: continue
    for slot, idx, st, en in de.all_programs(doc):
        seq = []
        for pc, op, v, bad in de.sweep(doc, st, en):
            if bad: break
            if op in OPS:
                sid = (v[1] | (v[2] << 8)) if op in (0xBD, 0xBE) else v[1]
                seq.append((pc, op, sid))
                sites.append(dict(map=mp, slot=slot, idx=idx, pc=pc, op=op, id=sid, chain=mp in CHAIN))
        progs_seq[(mp, slot, idx)] = seq
json.dump(sites, open('sites_stop.json', 'w'))
out = []
def P(*a):
    s = ' '.join(str(x) for x in a); out.append(s); print(s)
for op in (0xBD, 0xBE, 0x75, 0x12):
    s = [x for x in sites if x['op'] == op]
    ids = collections.Counter(x['id'] for x in s)
    P('0x%02X (%s): %d sites on %d maps; on the 30 chain maps: %d sites on %d maps; distinct ids %d; top ids %s' % (
        op, OPS[op], len(s), len({x['map'] for x in s}), sum(1 for x in s if x['chain']), len({x['map'] for x in s if x['chain']}), len(ids), dict(ids.most_common(14))))
# per-slot of the stops
for op in (0xBE, 0x75):
    s = [x for x in sites if x['op'] == op]
    P('0x%02X by slot:' % op, dict(collections.Counter(x['slot'] for x in s)))
# pairing: a stop of id X in a program is 'paired' when an earlier Play of the same id (0xBD or 0x12) exists earlier in the SAME program sweep
paired = unpaired = 0
unp = []
for key, seq in progs_seq.items():
    for i, (pc, op, sid) in enumerate(seq):
        if op in (0xBE, 0x75):
            if any(o2 in (0xBD, 0x12) and s2 == sid for (p2, o2, s2) in seq[:i]):
                paired += 1
            else:
                unpaired += 1; unp.append((key, pc, op, sid))
P('stops (0xBE and 0x75) preceded in the same program by a play of the same id: %d ; not preceded: %d' % (paired, unpaired))
P('unpaired by id:', dict(collections.Counter(x[3] for x in unp).most_common(15)))
# the 12 chain 0xBE sites, and the 0x75 chain sites
P('--- chain maps: every 0xBE/0x75 site')
for x in sites:
    if x['chain'] and x['op'] in (0xBE, 0x75):
        seq = progs_seq[(x['map'], x['slot'], x['idx'])]
        P('  map %d  %s[%d] pc %d  op 0x%02X id %d   program sound ops: %s' % (x['map'], x['slot'], x['idx'], x['pc'], x['op'], x['id'], [(('%02X' % o), s) for (p, o, s) in seq]))
# id 25: how many maps, how many programs per map play 25
P('--- id 25 (the stone scraping): plays per map (0xBD)')
c25 = collections.Counter(x['map'] for x in sites if x['op'] == 0xBD and x['id'] == 25)
P('maps with a 0xBD 25:', len(c25), ' top:', dict(c25.most_common(12)))
c25s = collections.Counter(x['map'] for x in sites if x['op'] == 0xBE and x['id'] == 25)
P('maps with a 0xBE 25:', len(c25s), ' top:', dict(c25s.most_common(12)))
open('sites_stop_summary.txt', 'w', encoding='utf-8').write('\n'.join(out) + '\n')
