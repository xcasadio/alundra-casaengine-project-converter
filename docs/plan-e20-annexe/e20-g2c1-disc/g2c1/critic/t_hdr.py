import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
d = L.load_map(72); r = d['SpriteInfo']['SpriteRecords']
print(json.dumps(r[117]['Header'], indent=0)[:900])
print(list(r[117].keys()))
