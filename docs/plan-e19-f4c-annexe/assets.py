"""Read-only access to the exported project: sprite id/name -> (RGBA array of the sheet, (x, y, w, h))."""
import json, os
import numpy as np
from PIL import Image
PROJ = r'D:\development\repo\alundra-casaengine-project-converter\alundra-project'
_ai = json.load(open(os.path.join(PROJ, 'AssetInfos.json'), encoding='utf-8'))['asset_infos']
BY_ID = {e['id']: e for e in _ai}
BY_NAME = {}
for e in _ai:
    BY_NAME[e['name']] = e['id']
_png = {}
def png(rel):
    if rel not in _png:
        _png[rel] = np.array(Image.open(os.path.join(PROJ, rel)).convert('RGBA'))
    return _png[rel]
def sprite(name_or_id):
    sid = name_or_id if name_or_id in BY_ID else BY_NAME[name_or_id]
    sp = json.load(open(os.path.join(PROJ, BY_ID[sid]['file_name']), encoding='utf-8'))
    loc = sp['location']
    sheet = json.load(open(os.path.join(PROJ, BY_ID[sp['sprite_sheet_asset_id']]['file_name']), encoding='utf-8'))
    return png(BY_ID[sheet['texture_asset_id']]['file_name']), (loc['x'], loc['y'], loc['w'], loc['h'])
def sprite_pixels(name_or_id):
    img, (x, y, w, h) = sprite(name_or_id)
    return img[y:y+h, x:x+w].copy()
def portraits():
    rows = []
    for ln in open(r'D:\development\repo\alundra-casaengine-project-converter\docs\plan-e19-f4-annexe\portraits_table.tsv', encoding='utf-8').read().splitlines()[1:]:
        p = ln.split('\t')
        rows.append(dict(bank=int(p[0]), map=int(p[1]), sig=p[2], id=p[3], w=int(p[4]), h=int(p[5]), ax=int(p[6]), ay=int(p[7]), etc=p[8], name=p[9]))
    return rows
