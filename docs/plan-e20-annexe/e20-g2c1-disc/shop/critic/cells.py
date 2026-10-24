import sys, collections
sys.path.insert(0,'.')
import evlib as L
m=int(sys.argv[1])
c=L.load_cells(m); d=L.load_map(m)
W=d['Map']['Width']; H=d['Map']['Height']
print(c.keys())
cnt=collections.Counter(w & 0xFFFF for w in c['walkability'])
print(sorted(cnt.items(), key=lambda x:-x[1])[:30])
print(collections.Counter(c['height']).most_common(10))
