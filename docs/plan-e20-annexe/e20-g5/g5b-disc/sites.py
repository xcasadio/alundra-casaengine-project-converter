"""sites.py - every program site of 0x72, 0xA9, 0xAA over all exported maps (linear sweep limited to each program's range, like op_sites_all.py), with:
 - the 5 instructions before and the 5 after (opcode names from the DLL's size table),
 - the placements (records of data-extracted/data/map_N.json) that run the program (slot letter + index & 0x7f), with the entity's type name (roster),
 - whether the map is in the story chain (the 30 maps).
Writes sites_all.txt and sites_all.json next to itself. Read-only on the repository."""
import os, sys, json, glob, re, csv, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import decode_events as de
R = 'D:/development/repo/alundra-casaengine-project-converter'
chain = {389,390,476,478,392,391,416,163,162,165,164,172,169,170,171,173,174,175,10,179,176,177,178,180,181,182,135,183,184,185}
rows = list(csv.DictReader(open(R + '/docs/plan-e14-annexe/e14-roster/roster.tsv', encoding='utf-8'), delimiter='\t'))
G = {int(r['table_index']): r['name_fr'] for r in rows if r['page'] == 'G'}
Mn = {int(r['table_index']): r['name_fr'] for r in rows if r['page'] == 'M'}
SLOTK = dict(A='EventCodesA_LoadIndex', C='EventCodesC_TickIndex', D='EventCodesD_TouchIndex', E='EventCodesE_DeactivateIndex', F='EventCodesF_InteractIndex', B='EventCodesB_MapIndex')
WANT = (0x72, 0xA9, 0xAA)
maps = sorted({int(re.search(r'-(\d+)\.events\.json$', f.replace(chr(92), '/')).group(1)) for f in glob.glob(de.ROOT + '/alundra-project/Maps/*/*/events/*.events.json')})
out = []
js = []
for mp in maps:
    doc = de.load_map(mp)
    if doc is None:
        continue
    users = collections.defaultdict(list)
    try:
        d = json.load(open(R + '/data-extracted/data/map_%d.json' % mp, encoding='utf-8-sig'))
        ents = d['SpriteInfo']['Entities']['Entities']
    except Exception:
        ents = []
    for k, e in enumerate(ents):
        if e is None or not e['IsEnabled']:
            continue
        page = 'M' if e['SpriteDirection'] & 0x80 else 'G'
        nm = (Mn if page == 'M' else G).get(e['SpriteTableIndex'], '?')
        for s, key in SLOTK.items():
            if e.get(key):
                users[(s, e[key] & 0x7f)].append('rec%d:%s%d(%s)' % (k, page, e['SpriteTableIndex'], nm[:20]))
    for slot, idx, st, en in de.all_programs(doc):
        ins = de.sweep(doc, st, en)
        for n, (pc, op, v, bad) in enumerate(ins):
            if bad:
                break
            if op in WANT:
                ctx = []
                for pc2, op2, v2, bad2 in ins[max(0, n - 5):n + 6]:
                    nm2 = de.SIZES.get(op2, (0, '?'))[1]
                    ctx.append('%s@%d 0x%02X %s %s' % ('>>' if pc2 == pc else '  ', pc2, op2, nm2[:30], list(v2[1:]) if v2 else '?'))
                u = users.get((slot, idx), [])
                out.append('== map %d %s[%d] @%d 0x%02X %s  chain=%s  users: %s' % (mp, slot, idx, pc, op, list(v[1:]), mp in chain, ', '.join(u) if u else '(none)'))
                out.extend(ctx)
                js.append(dict(map=mp, slot=slot, idx=idx, pc=pc, op=op, operands=list(v[1:]), chain=mp in chain, users=u, start=st, end=en))
open(os.path.join(HERE, 'sites_all.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
json.dump(js, open(os.path.join(HERE, 'sites_all.json'), 'w'), indent=1)
c = collections.Counter((s['op']) for s in js)
print({'0x%02X' % k: v for k, v in c.items()}, 'sites')
for op in WANT:
    ss = [s for s in js if s['op'] == op]
    print('0x%02X' % op, len(ss), 'sites,', len({s['map'] for s in ss}), 'maps; operands', dict(collections.Counter(tuple(s['operands']) for s in ss)), '; chain', sorted({s['map'] for s in ss if s['chain']}))
