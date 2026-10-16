import json, os
base=r"D:\development\repo\alundra-casaengine-project-converter\docs\plan-e20-annexe\e20-e0a\oracle\out"
for fn in ('v1.json','v2.json','v3.json','v7.json'):
    txt=open(os.path.join(base,fn),encoding='utf-8').read()
    try:
        d=json.loads(txt)
    except Exception:
        d=[json.loads(l) for l in txt.splitlines() if l.strip()]
    if isinstance(d,dict):
        print(fn,'dict keys',list(d.keys())[:8])
        rows=d.get('vectors') or d.get('rows') or []
    else:
        rows=d
    print(fn,len(rows), json.dumps(rows[0])[:300] if rows else '')
    if rows and 'p' in rows[0]:
        hb=[r for r in rows if r['p'].get('has_box')]
        print('   has_box=1:',len(hb),'sprites',sorted(set((r.get('idx'),r.get('page')) for r in hb))[:40])
