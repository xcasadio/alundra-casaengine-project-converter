import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
m = int(sys.argv[1]); rec = int(sys.argv[2])
d = L.load_map(m); si = d['SpriteInfo']; e = si['Entities']['Entities'][rec]
print({k: e[k] for k in e if k.startswith('EventCodes') or k in ('SpriteTableIndex','XPos','YPos','Height','IsEnabled','SpriteDirection')})
doc = L.load_events(m)
for X, key in (('A','EventCodesA_LoadIndex'),('C','EventCodesC_TickIndex'),('D','EventCodesD_TouchIndex'),('E','EventCodesE_DeactivateIndex'),('F','EventCodesF_InteractIndex')):
    idx = e[key] & 0x7f
    if idx: L.show(doc, X, idx)
