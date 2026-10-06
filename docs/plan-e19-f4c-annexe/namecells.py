import os, struct, json
# `lib` is the discovery's executable reader (a tool kept outside the repository: it reads ALUN_CD.EXE, France); this probe is the only file of the annex that needs the executable.
import lib
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from PIL import Image
import numpy as np
def rd(a, n): 
    o = a - lib.BASE + lib.OFF
    return lib.DATA[o:o+n]
cfg = rd(0x800A58BC, 16)
x,y,cols,rows = struct.unpack_from('<4h', cfg, 0)
ptrA, ptrB = struct.unpack_from('<II', cfg, 8)
print('cfg', x,y,cols,rows, hex(ptrA), hex(ptrB))
for p in (ptrA, ptrB):
    cells=[]
    for i in range(cols*rows):
        c = rd(p+20*i, 20)
        tag, rgbc, xy, uvc, wh = struct.unpack('<5I', c)
        cells.append(dict(i=i, code=rgbc>>24, x0=xy&0xFFFF, y0=xy>>16, u=uvc&0xFF, v=(uvc>>8)&0xFF, clut=uvc>>16, w=wh&0xFFFF, h=wh>>16))
    print(hex(p), 'codes', set(c['code'] for c in cells), 'cluts', set(c['clut'] for c in cells), 'sizes', set((c['w'],c['h']) for c in cells))
    print(cells[:4], cells[-2:])
    if p==ptrA: A=cells
    else: B=cells
print('A==B uv:', all((a['u'],a['v'])==(b['u'],b['v']) for a,b in zip(A,B)))
# compose from wind.png
wind = np.array(Image.open(os.path.join(REPO, 'data-extracted', 'ui', 'wind.png')).convert('RGBA'))
wj = json.load(open(os.path.join(REPO, 'data-extracted', 'ui', 'wind.json'), encoding='utf-8'))
print('wind.png', wind.shape)
out = np.zeros((rows*8, cols*8, 4), dtype=np.uint8)
pal=set()
for i,c in enumerate(A):
    r, cc = divmod(i, cols)
    tile = wind[c['v']:c['v']+8, c['u']:c['u']+8]
    out[r*8:r*8+8, cc*8:cc*8+8] = tile
    ent=[e for e in wj if e['U0']==c['u'] and e['V0']==c['v']]
    pal.add(tuple((e['Width'],e['Height'],e['PaletteIndex']) for e in ent))
print('wind.json palette entries for the cells:', pal)
png = np.array(Image.open(os.path.join(REPO, 'alundra-project', 'UI', 'Textures', 'g_textTilesConfiguration.png')).convert('RGBA'))
print('baked', png.shape, 'equal to the composition:', np.array_equal(png, out))
Image.fromarray(out).save('namebox_from_binary_cells.png')
