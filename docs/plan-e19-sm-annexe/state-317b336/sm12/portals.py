import glob, re, collections, os, sys
os.chdir('D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps')
dest = collections.defaultdict(set)
n = 0
for p in glob.glob('**/tilemap/*.tileMap', recursive=True):
    t = open(p, encoding='utf-8').read()
    q = p.replace('\\', '/')
    m = re.search(r'-(\d+)/tilemap', q)
    src = int(m.group(1)) if m else None
    for mm in re.finditer(r'"DestMapId"\s*:\s*"?(\d+)"?', t):
        dest[int(mm.group(1))].add(src)
    n += 1
print(n, 'tilemaps')
for k in [55, 56, 57, 58, 59, 60, 27, 29, 30, 113, 400, 136, 159, 77, 78, 398, 226, 307, 344, 442]:
    print(k, '<- portals of maps', sorted(x for x in dest.get(k, []) if x is not None))
