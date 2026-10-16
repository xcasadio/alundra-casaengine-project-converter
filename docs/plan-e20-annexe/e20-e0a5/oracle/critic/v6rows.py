import json,sys
fn=r"D:\development\repo\alundra-casaengine-project-converter\docs\plan-e20-annexe\e20-e0a\oracle\out\v6.json"
rows=[json.loads(l) for l in open(fn) if l.strip()]
print(len(rows), list(rows[0].keys()))
r=rows[0]
print(json.dumps(r)[:1800])
