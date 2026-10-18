import json, sys, os, glob, collections
root = sys.argv[1]
sr = json.load(open(os.path.join(root, 'Data', 'sprite-records.json'), encoding='utf-8-sig'))
flags = {}
for v in sr.values():
    flags[v['BankKey']] = v['MoreFlags'] | (v['CanPickup'] << 8) | (v['FlagsPortraitShadowType'] << 16)

def props(o):
    p = {}
    for x in o.get('properties', []) or []:
        p[x['name']] = x['value']
    return p

inst = collections.Counter()
pairs = set()
pairs_local = set()
inst_local = 0
inst_global = 0
maps_with = set()
dump_map = sys.argv[2] if len(sys.argv) > 2 else None
for f in glob.glob(os.path.join(root, 'Maps', '**', '*.tmj'), recursive=True):
    m = json.load(open(f, encoding='utf-8-sig'))
    name = os.path.basename(f)
    mapid = name.rsplit('-', 1)[-1].replace('.tmj', '')
    def walk(layers):
        for L in layers:
            if L.get('type') == 'group':
                yield from walk(L.get('layers', []))
            elif L.get('type') == 'objectgroup':
                for o in L.get('objects', []):
                    yield L.get('name'), o
    for lname, o in walk(m.get('layers', [])):
        p = props(o)
        if 'SpriteTableIndex' not in p:
            continue
        idx = int(p['SpriteTableIndex'])
        sd = int(p.get('SpriteDirection', 0) or 0)
        local = (sd & 0x80) != 0
        bk = str(idx) if local else 'alundra_%d' % idx
        fl = flags.get(bk)
        if dump_map and mapid == dump_map:
            print('MAP', mapid, lname, o.get('id'), o.get('name'), 'idx', idx, 'sd', sd, 'bk', bk, hex(fl) if fl is not None else None, {k: p[k] for k in p if k.startswith('EventCodes') or k in ('PosZ','Z','Height','PixelX','PixelY')}, o.get('x'), o.get('y'))
        if fl is not None and fl & 0x100000:
            inst[bk] += 1
            pairs.add((mapid, bk))
            maps_with.add(mapid)
            if local:
                inst_local += 1
                pairs_local.add((mapid, bk))
            else:
                inst_global += 1
print('instances total', sum(inst.values()), 'local', inst_local, 'global', inst_global)
print('pairs (map,bank) total', len(pairs), 'local pairs', len(pairs_local), 'maps', len(maps_with))
print(sorted(inst.items(), key=lambda t: -t[1]))
