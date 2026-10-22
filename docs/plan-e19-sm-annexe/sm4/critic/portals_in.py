"""Incoming portal edges (Portals object layer: DestMapId) for every map; lists the sm4 maps with their sources."""
import json, sys, collections
sys.path.insert(0, '.')
import census_critic as c

idx = json.load(open(c.PROJ + '/Maps/world-index.json', encoding='utf-8'))
incoming = collections.defaultdict(list)
for mid in sorted(int(k) for k in idx):
    base, ev, tm = c.load_map(mid, idx)
    for layer in tm['object_layers']:
        if layer['name'] == 'Portals':
            for o in layer['objects']:
                p = o['custom_properties']
                incoming[int(p['DestMapId'])].append(mid)
rows = [l.rstrip('\n').split('\t') for l in open('my-skipped-sites.tsv')][1:]
sm4 = {'0x15', '0x66', '0x72', '0x7A', '0x88', '0x96', '0x97', '0xB3'}
maps = sorted({int(r[0]) for r in rows if r[4] in sm4})
for m in maps:
    base = idx[str(m)].split(chr(92))[-1].encode('ascii', 'replace').decode()
    print(m, base, 'incoming portals from', sorted(set(incoming.get(m, []))))
