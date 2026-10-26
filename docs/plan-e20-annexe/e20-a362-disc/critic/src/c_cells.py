import json
d=json.load(open("D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_362.json",encoding='utf-8-sig'))
M=d['Map']; W=M['Width']; H=M['Height']
T={ (t['TileX'],t['TileY']):t for t in M['MapTiles']}
print('W,H',W,H)
print('cols 2..22, rows 0..22: (walkability,height) ; legend: walkability hex digit / height')
print('      '+' '.join('%2d'%c for c in range(2,23)))
for r in range(0,23):
    print('row %2d '%r + ' '.join('%x/%d'%(T[(c,r)]['Walkability']&0xf, T[(c,r)]['Height']) for c in range(2,23)))
