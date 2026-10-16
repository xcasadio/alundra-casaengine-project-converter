import json, os, struct, sys
sys.dont_write_bytecode = True
DATAS = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/DATA/DATAS.BIN"
MAP = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json"
print('DATAS.BIN size', os.path.getsize(DATAS))
d = json.load(open(MAP, encoding='utf-8-sig'))
tabs = d['SpriteInfo']['SpriteEffectRecords']
print(len(tabs), list(tabs[0].keys()))
for i,t in enumerate(tabs):
    print(i, {k:(v if not isinstance(v,(list,dict)) else '...') for k,v in t.items()})
