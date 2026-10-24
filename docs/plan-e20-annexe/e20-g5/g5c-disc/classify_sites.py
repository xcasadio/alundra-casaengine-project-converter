"""classify_sites.py - the 392 sites 0xBE by what the DLL does today for their id (sfx-manifest.json): looping tone (repeat=true: the voice never ends by itself), one-shot (repeat=false), no tone (the id is not playable: Play and Stop are both no-ops). Read-only.
Also: per map count, the maps concerned by the 4 families of g5 (pillar 26,30,341,343,344,346,347,423; statue 2,37,51,102,115,117,149,152,351,352,382; block 23,100,102,382; button 40,426) and the chain maps."""
import json, collections
m = {r['id']: r for r in json.load(open(r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Sounds\sfx-manifest.json", encoding='utf-8'))}
sites = json.load(open('sites_stop.json'))
be = [x for x in sites if x['op'] == 0xBE]
def kind(sid):
    r = m.get(sid)
    if r is None or not r['tones'] or r['vab_id'] == -2: return 'no tone (not playable)'
    if any(t['repeat'] for t in r['tones']): return 'looping'
    return 'one-shot'
c = collections.Counter(kind(x['id']) for x in be)
print('0xBE sites by what the DLL does today with the id:', dict(c), 'total', len(be))
for k in ('looping', 'one-shot', 'no tone (not playable)'):
    ids = collections.Counter(x['id'] for x in be if kind(x['id']) == k)
    print('  %-24s ids %d, sites %d, top %s' % (k, len(ids), sum(ids.values()), dict(ids.most_common(8))))
chain = [x for x in be if x['chain']]
print('chain sites by kind:', dict(collections.Counter(kind(x['id']) for x in chain)))
fam = {26,30,341,343,344,346,347,423, 2,37,51,102,115,117,149,152,351,352,382, 23,100, 40,426}
inf = [x for x in be if x['map'] in fam]
print('sites on the 23 maps of the four families of g5: %d on %d maps; of which id 25: %d' % (len(inf), len({x['map'] for x in inf}), sum(1 for x in inf if x['id'] == 25)))
print('maps with >=1 0xBE: %d ; of them with a looping id: %d' % (len({x['map'] for x in be}), len({x['map'] for x in be if kind(x['id']) == 'looping'})))
# 0xBD plays of looping ids without any 0xBE of that id in the same map: the sound loops until the world is cleared in the DLL, and (binary) until a Stop / the next key-off-all
bd = [x for x in sites if x['op'] == 0xBD]
bs = collections.defaultdict(set)
for x in be: bs[x['map']].add(x['id'])
nostop = collections.Counter()
for x in bd:
    if kind(x['id']) == 'looping' and x['id'] not in bs[x['map']]:
        nostop[x['id']] += 1
print('0xBD sites of a looping id with no 0xBE of that id anywhere in the map (the stop, if any, comes from elsewhere):', sum(nostop.values()), dict(nostop.most_common(10)))
