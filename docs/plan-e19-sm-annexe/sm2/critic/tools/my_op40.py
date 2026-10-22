import sys; sys.path.insert(0,'tools')
from mycorpus import *
for mid in (55,56,57):
    ev,tm,b=load(mid)
    codes=ev['Codes']
    w=walk_map(ev,tm)
    ops40=[(s,i,pc,codes[pc+1],codes[pc+2]) for s,i,pc,op in w if op==0x40]
    print(mid,b,'0x40 reached:',ops40)
    r=roots(ev,tm); print(' roots',r)
    # entities
    for layer in tm['object_layers']:
        if layer['name']=='Entities':
            for o in layer['objects']:
                p=o['custom_properties']
                c=int(p['EventCodesC_TickIndex'])
                print('  ent',o.get('name'),o.get('id'),'prefab',p.get('PrefabAssetId', p.get('Prefab')),'C',c,'&7F',c&0x7f,'A',p['EventCodesA_LoadIndex'],'D',p['EventCodesD_TouchIndex'],'F',p['EventCodesF_InteractIndex'],'E',p['EventCodesE_DeactivateIndex'], 'x',o.get('x'),'y',o.get('y'))
