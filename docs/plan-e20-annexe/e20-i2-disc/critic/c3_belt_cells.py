import json, sys
ROOT = r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
n = int(sys.argv[1])
d = json.load(open(ROOT + '/tiled/map_%d.alundra.json' % n, encoding='utf-8-sig'))
cells = d['Cells']
for c in cells:
    if c['Slope'] & 0xC0:
        s = c['Slope']
        print('x=%2d y=%2d h=%2d walk=%02x gp=%3d slope=%02x lock=%d dir=%d gates=%d%d  deep=%d' % (c['X'], c['Y'], c['Height'], c['Walkability'], c['GroundProperty'], s, (s>>7)&1, (s>>4)&3, (s>>3)&1, (s>>2)&1, int((c['GroundProperty']&0xE)==8)))
