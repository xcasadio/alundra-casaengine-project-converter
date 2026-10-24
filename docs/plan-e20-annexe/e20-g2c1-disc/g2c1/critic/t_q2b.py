import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
doc = L.load_events(72)
for slot, idx in [('A',12),('C',12),('A',26),('C',26),('C',27)]:
    L.show(doc, slot, idx)
