import json,sys,glob,os
sys.path.insert(0,'.')
import corpus
# usage: mapinfo.py MAP  -> prints world path, map events, entities (compact), portals
mid=int(sys.argv[1])
rel=corpus._index[str(mid)].replace(chr(92),'/')
folder=corpus.PROJECT+'/'+os.path.dirname(rel)
print('world-index entry:',rel)
tm=glob.glob(folder+'/tilemap/*.tileMap')[0]
d=json.load(open(tm,encoding='utf-8'))
cp=d['custom_properties']
print('size',d['map_size'],{k:cp[k] for k in cp if k in ('MapIndex','Gravity','BalanceLevel','C','D','E','F')})
for l in d['object_layers']:
    nm=l.get('name')
    if nm in ('MapEvents','Portals'):
        for o in l['objects']:
            c=o['custom_properties']
            if nm=='MapEvents':
                print('  MapEvent',c['Index'],'zone',c['X1'],c['Y1'],c['X2'],c['Y2'],'B',c['EventCodesBIndex'],'masked',c['EventCodesBIndexMasked'])
            else:
                print('  Portal',c['Index'],'zone',c['X1'],c['Y1'],c['X2'],c['Y2'],'dest',c['DestMapId'],c['DestTileX'],c['DestTileY'],'z',c['ZLevel'],'flags',c['Flags'])
    elif nm=='Entities' and '-e' in sys.argv:
        for o in l['objects']:
            c=o['custom_properties']
            print('  Entity',c['Index'],'sprite',c['SpriteTableIndex'],'pos',c['XPos'],c['YPos'],'h',c['Height'],'A',c['EventCodesA_LoadIndex'],'B',c['EventCodesB_MapIndex'],'C',c['EventCodesC_TickIndex'],'D',c['EventCodesD_TouchIndex'],'E',c['EventCodesE_DeactivateIndex'],'F',c['EventCodesF_InteractIndex'])
