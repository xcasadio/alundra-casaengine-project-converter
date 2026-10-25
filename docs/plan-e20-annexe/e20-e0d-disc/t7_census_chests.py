"""Census of treasure chests (global sprite type 30) in the exported maps. Read-only.
Page rule (E20.e0c annex, binary 0x8003A270): SpriteDirection bit 7 = map page, else global page.
Writes t7_chests.tsv and prints statistics."""
import json, re, glob, collections, io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
P = 'D:/development/repo/alundra-casaengine-project-converter/'
OUT = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest/'
wi = json.load(open(P + 'alundra-project/Maps/world-index.json', encoding='utf-8'))
BS = chr(92)
def mname(mid):
    s = wi.get(str(mid), '')
    parts = s.split(BS)
    return (parts[1] + '/' + parts[2]) if len(parts) > 2 else s
etc = json.load(open(P + 'data-extracted/data/ETC_RES.R.json', encoding='utf-8'))
ek = list(etc.keys())
def item_name(i):
    k = 0x200 + i + 1
    return etc[ek[k]] if k < len(ek) else None
rows = []
files = glob.glob(P + 'data-extracted/data/map_*.json')
maps = {}
for fp in files:
    mm = re.search(r'map_(\d+)\.json$', fp)
    if mm:
        maps[int(mm.group(1))] = fp
for mid in sorted(maps):
    d = json.load(open(maps[mid], encoding='utf-8'))
    si = d.get('SpriteInfo')
    if not si:
        continue
    ents = (si.get('Entities') or {}).get('Entities') or []
    for i, e in enumerate(ents):
        if not e:
            continue
        if e.get('SpriteTableIndex') != 30:
            continue
        page = 'M' if (e['SpriteDirection'] & 0x80) else 'G'
        rows.append(dict(map=mid, idx=i, en=e['IsEnabled'], page=page, x=e['XPos'], y=e['YPos'], h=e['Height'], dirb=e['SpriteDirection'],
                         contents=e['Contents'], flag=e['_10'],
                         A=e['EventCodesA_LoadIndex'], B=e['EventCodesB_MapIndex'], C=e['EventCodesC_TickIndex'], D=e['EventCodesD_TouchIndex'], E=e['EventCodesE_DeactivateIndex'], F=e['EventCodesF_InteractIndex']))
print('records with SpriteTableIndex==30:', len(rows))
print('by page/enabled:', collections.Counter((r['page'], r['en']) for r in rows))
G = [r for r in rows if r['page'] == 'G' and r['en']]
print('global page, enabled:', len(G), ' of which map 0:', sum(1 for r in G if r['map'] == 0))
H = [r for r in G if r['map'] != 0]
print('chests outside map 0:', len(H), ' on', len(set(r['map'] for r in H)), 'maps')
withc = [r for r in H if r['contents']]
print('with Contents != 0:', len(withc), ' on', len(set(r['map'] for r in withc)), 'maps; empty (Contents 0):', len(H) - len(withc))
script = [r for r in H if any(r[k] for k in 'ABCDEF')]
print('chests with a non-zero event-program index A..F (scripted record):', len(script))
print(' per slot:', {k: sum(1 for r in H if r[k]) for k in 'ABCDEF'})
nof = [r for r in withc if r['flag'] == 0]
print('with Contents and flag(_10)==0 (no persistence):', len(nof), [(r['map'], r['idx'], r['contents']) for r in nof][:40])
tmp = [r for r in withc if r['flag'] & 0x8000]
print('flag in the temporary bank (bit15):', len(tmp))
inv = [r for r in withc if (r['flag'] & 0x7FFF) >= 0x800]
print('flag invalid (>=0x800):', len(inv))
fl = collections.Counter(r['flag'] for r in withc if r['flag'])
dup = {k: v for k, v in fl.items() if v > 1}
print('distinct flags:', len(fl), ' flags shared by several chests:', len(dup), dup)
print('contents >= 0x62 (random table) among chests:', sum(1 for r in withc if r['contents'] >= 0x62))
print('empty chests with a flag:', sum(1 for r in H if not r['contents'] and r['flag']))
print('Contents histogram (item: count):')
hist = collections.Counter(r['contents'] for r in withc)
for k in sorted(hist):
    print('  %3d 0x%02x %-18s %3d' % (k, k, item_name(k), hist[k]))
print('dirb histogram:', collections.Counter(r['dirb'] for r in H))
print('flag range min/max among chests with flag:', min(fl), max(fl))
with open(OUT + 't7_chests.tsv', 'w', encoding='utf-8') as f:
    f.write('map\tarea\tidx\tx\ty\th\tdirb\tcontents\titem\tflag\tflagbank\tA\tB\tC\tD\tE\tF\ten\tpage\n')
    for r in sorted(rows, key=lambda r: (r['map'], r['idx'])):
        f.write('\t'.join(str(v) for v in (r['map'], mname(r['map']), r['idx'], r['x'], r['y'], r['h'], r['dirb'], r['contents'], item_name(r['contents']) or '', r['flag'] & 0x7FFF, 'T' if r['flag'] & 0x8000 else ('P' if r['flag'] else '-'), r['A'], r['B'], r['C'], r['D'], r['E'], r['F'], r['en'], r['page'])) + '\n')
