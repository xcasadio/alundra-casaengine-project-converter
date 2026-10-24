import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
for m, idxs in ((73,(2,3,4)),(158,(2,))):
    d = L.load_map(m); si=d['SpriteInfo']
    for k,e in enumerate(si['Entities']['Entities']):
        if e and (e['EventCodesA_LoadIndex']&0x7f) in idxs:
            print('map',m,'rec',k,'sprite',e['SpriteTableIndex'],'A',e['EventCodesA_LoadIndex'],'C',e['EventCodesC_TickIndex'])
    doc = L.load_events(m)
    for i in idxs: L.show(doc,'A',i)
