import json, glob, os, collections
os.chdir(r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps")
cnt = collections.Counter()
where = collections.defaultdict(list)
for tm in glob.glob('**/tilemap/*.tmj', recursive=True):
    try:
        d = json.load(open(tm, encoding='utf-8'))
    except Exception:
        continue
    parts = tm.replace(os.sep, '/').split('/')
    mid = int(parts[-3].rsplit('-', 1)[-1])

    def walk(layers):
        for l in layers:
            if l.get('type') == 'objectgroup':
                for o in l['objects']:
                    props = {p['name']: p['value'] for p in o.get('properties', [])}
                    c = props.get('Contents')
                    if c:
                        cnt[c] += 1
                        if c in (0x53,):
                            where[c].append((mid, props.get('EntityName'), props.get('Index'), o['x'], o['y'],
                                             {k: v for k, v in props.items() if k.startswith('EventCodes') and v}))
            if 'layers' in l:
                walk(l['layers'])
    walk(d['layers'])
print("Contents histogram (top):", cnt.most_common(12))
print("Contents==0x53:", where[0x53])
print("0x45..0x56 contents:", {hex(k): v for k, v in sorted(cnt.items()) if 0x45 <= k <= 0x56})
