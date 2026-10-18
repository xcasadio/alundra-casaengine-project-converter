import json, glob, os
d = json.load(open("data-extracted/data/map_388.json", encoding="utf-8-sig"))
p = d['Info'].get('Portals')
print("portals of 388:", json.dumps(p)[:500])
hits = []
for f in glob.glob("data-extracted/data/map_*.json"):
    try:
        j = json.load(open(f, encoding="utf-8-sig"))
    except Exception:
        continue
    pp = (j.get('Info') or {}).get('Portals')
    if not pp:
        continue
    for q in pp:
        if isinstance(q, dict):
            for k, v in q.items():
                if k == 'DestMapId' and v == 388:
                    hits.append((os.path.basename(f), k, q))
print(hits[:10], len(hits))
