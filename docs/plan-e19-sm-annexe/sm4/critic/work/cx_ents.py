import sys, glob, json, cx_corpus as C
mid=int(sys.argv[1])
b,d,f=C.load(mid)
tm=glob.glob(f+'/tilemap/*.tileMap')[0]
t=json.load(open(tm,encoding='utf-8'))
print(b, 'size', t['map_size'])
for l in t['object_layers']:
    nm=l.get('name')
    if nm=='Entities':
        for o in l['objects']:
            c=o['custom_properties']
            print('  Entity',c['Index'],'sprite',c['SpriteTableIndex'],'pos',c['XPos'],c['YPos'],'h',c['Height'],'A',c['EventCodesA_LoadIndex'],'B',c['EventCodesB_MapIndex'],'C',c['EventCodesC_TickIndex'],'D',c['EventCodesD_TouchIndex'],'E',c['EventCodesE_DeactivateIndex'],'F',c['EventCodesF_InteractIndex'])
    elif nm in ('MapEvents','Portals'):
        for o in l['objects']:
            c=o['custom_properties']
            if nm=='MapEvents': print('  MapEvent',c['Index'],'zone',c['X1'],c['Y1'],c['X2'],c['Y2'],'B',c['EventCodesBIndex'],'masked',c['EventCodesBIndexMasked'])
            else: print('  Portal',c['Index'],'zone',c['X1'],c['Y1'],c['X2'],c['Y2'],'dest',c['DestMapId'],c['DestTileX'],c['DestTileY'])
print({k:v for k,v in t['custom_properties'].items() if k in ('MapIndex','Gravity','BalanceLevel')})
