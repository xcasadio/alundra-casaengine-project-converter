import json, sys
ROOT = r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
n = int(sys.argv[1])
d = json.load(open(ROOT + '/tiled/map_%d.alundra.json' % n, encoding='utf-8-sig'))
print({k: (v if not isinstance(v, (list, dict)) else type(v).__name__) for k, v in d.items()})
cells = d['Cells']
print(len(cells), cells[0])
