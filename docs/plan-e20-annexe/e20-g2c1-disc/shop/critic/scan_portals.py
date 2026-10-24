import glob, json, re, sys
R='D:/development/repo/alundra-casaengine-project-converter'
T={163,258,298,398,134,302}
for f in sorted(glob.glob(R+'/data-extracted/data/map_*.json')):
    mm=re.search(r'map_(\d+)\.json',f.replace(chr(92),'/'))
    if not mm: continue
    m=int(mm.group(1))
    try: d=json.load(open(f,encoding='utf-8-sig'))
    except Exception as e: print('ERR',f,e); continue
    for k,p in enumerate(d['Info']['Portals']):
        if p['DestMapId'] in T or (m in T and not (p['X1']==0 and p['X2']==255)):
            print('src %3d portal#%d (%d,%d)-(%d,%d) face=%d -> map %d tile (%d,%d) z%d flags=%d wb=%d'%(m,k,p['X1'],p['Y1'],p['X2'],p['Y2'],p['RequiredFacingDirection'],p['DestMapId'],p['DestTileX'],p['DestTileY'],p['ZLevel'],p['Flags'],p['WarpBehaviorId']))
