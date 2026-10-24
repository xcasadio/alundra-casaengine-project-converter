import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
doc = L.load_events(140)
L.show(doc,'C',18)
d = L.load_map(140); si=d['SpriteInfo']
for k,e in enumerate(si['Entities']['Entities']):
    if e and ((e['EventCodesC_TickIndex']&0x7f)==18 or k in (17,)):
        print('rec',k,'sprite',e['SpriteTableIndex'],'C',e['EventCodesC_TickIndex'],'pos',e['XPos'],e['YPos'])
doc = L.load_events(158)
L.show(doc,'C',42)
d = L.load_map(158); si=d['SpriteInfo']
for k,e in enumerate(si['Entities']['Entities']):
    if e and ((e['EventCodesC_TickIndex']&0x7f) in range(42,56) or k==11):
        print('rec',k,'sprite',e['SpriteTableIndex'],'C',e['EventCodesC_TickIndex'],'pos',e['XPos'],e['YPos'])
L.show(doc,'C',11)
