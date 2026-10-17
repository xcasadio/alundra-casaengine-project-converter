"""extract entity records + sprite headers (Contents) from every data-extracted map_N.json and map_alundra.json. Read-only. Writes cache/maps.pkl in the scratch folder."""
import glob, json, os, pickle, re, time
DATA = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\data"
HERE = os.path.dirname(os.path.abspath(__file__))
os.makedirs(os.path.join(HERE, 'cache'), exist_ok=True)
HK = ("Sector5Id","MoreFlags","CanPickup","FlagsPortraitShadowType","ProgramLoad","ProgramTick","ProgramTouch","ProgramDeactivate","ProgramInteract","BreakEffect","Contents","SizeX","SizeY","SizeZ")
EK = ("IsEnabled","SpriteDirection","SpriteTableIndex","XPos","YPos","Height","EventCodesA_LoadIndex","EventCodesC_TickIndex","_10","Contents")
def one(path):
    with open(path, encoding='utf-8') as fh: d = json.load(fh)
    si = d['SpriteInfo']
    ents = []
    for i, e in enumerate((si.get('Entities') or {}).get('Entities') or []):
        if e is None: continue
        r = {k: e.get(k) for k in EK}; r['idx'] = i; ents.append(r)
    recs = {}
    for i, r in enumerate(si.get('SpriteRecords') or []):
        if r:
            h = r['Header']; recs[i] = {k: h.get(k) for k in HK}
    return {'mapId': d['Info']['MapId'] if d.get('Info') else None, 'entities': ents, 'records': recs, 'spriteTable': si.get('SpriteTable')}
files = sorted(glob.glob(os.path.join(DATA, 'map_[0-9]*.json')), key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1)))
maps = {}; t0 = time.time()
for n, p in enumerate(files):
    fid = int(re.search(r'map_(\d+)\.json', p).group(1))
    maps[fid] = one(p)
    if n % 60 == 0: print(n, len(files), f'{time.time()-t0:.0f}s', flush=True)
g = one(os.path.join(DATA, 'map_alundra.json'))
pickle.dump({'maps': maps, 'global': g}, open(os.path.join(HERE, 'cache', 'maps.pkl'), 'wb'))
print('done', len(maps), 'maps', len(g['records']), 'global records', f'{time.time()-t0:.0f}s')
