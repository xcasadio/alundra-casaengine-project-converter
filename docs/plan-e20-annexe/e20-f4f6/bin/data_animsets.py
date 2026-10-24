import json,sys
d=json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json', encoding='utf-8-sig'))
sr=d['SpriteInfo']['SpriteRecords']
def dump(i):
    r=sr[i]; h=r['Header']
    flags=h['MoreFlags']|(h['CanPickup']<<8)|(h['FlagsPortraitShadowType']<<16)
    print("=== global %d  flags=0x%06X codes A%d C%d D%d E%d F%d  sz=%d,%d,%d off=%d,%d,%d brk=%d"%(i,flags,h['ProgramLoad'],h['ProgramTick'],h['ProgramTouch'],h['ProgramDeactivate'],h['ProgramInteract'],h['SizeX'],h['SizeY'],h['SizeZ'],h['OffsetX'],h['OffsetY'],h['OffsetZ'],h['BreakEffect']))
    for a,s in enumerate(r['AnimSets']):
        print("  animset %d: IsZForce=%s Speed=%d Accel(byte0xD)=0x%02X Flags=0x%02X Sfx=%d Unknown=%s"%(a,s['IsZForceApplied'],s['Speed'],s['Acceleration'],s['Flags'],s['Sfx'],s['Unknown']))
        for dd,an in enumerate(s['PreloadedAnims']):
            if an is None: print("     dir %d: None"%dd); continue
            fr=an['Frames']
            tot=sum(f['Delay']&0x7F for f in fr if f['Delay']&0x80)
            print("     dir %d: nframes=%d delays=%s  (sum of 0x80-flagged=%d)  coll=%s"%(dd,len(fr),[hex(f['Delay']) for f in fr],tot,[ (f['CollisionData']['OffsetX'],f['CollisionData']['OffsetY'],f['CollisionData']['OffsetZ'],f['CollisionData']['Width'],f['CollisionData']['Depth'],f['CollisionData']['Height']) if f.get('CollisionData') else None for f in fr][:6]))
for a in sys.argv[1:]: dump(int(a))
