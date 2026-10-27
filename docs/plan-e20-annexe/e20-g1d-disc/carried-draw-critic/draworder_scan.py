import json, glob, sys, collections
base = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Entities"
def scan(pattern):
    mn, mx = 10**9, -10**9
    props = collections.Counter()
    nfiles = 0
    byfile = {}
    for f in glob.glob(base + pattern):
        nfiles += 1
        d = json.load(open(f, encoding='utf-8'))
        vals = [p.get('default_draw_order', 0) for p in d.get('parts', [])]
        for t in d.get('tracks', []):
            props[t.get('property')] += 1
            for k, v in t.items():
                if k.endswith('_keyframes') and 'draw' in k.lower():
                    for kf in v:
                        vals.append(kf['value'])
        if vals:
            mn = min(mn, min(vals)); mx = max(mx, max(vals))
            byfile[f.split('/')[-1]] = (min(vals), max(vals))
    return nfiles, mn, mx, props, byfile
for pat in ["/Alundra/bankalundra_0_anim*.anim2d", "/Bombe/*.anim2d"]:
    n, mn, mx, props, byfile = scan(pat)
    print(pat, "files", n, "draw order min", mn, "max", mx, "track props", dict(props))
    top = sorted(byfile.items(), key=lambda kv: -kv[1][1])[:5]
    print("  top max files:", top)
    low = sorted(byfile.items(), key=lambda kv: kv[1][0])[:3]
    print("  lowest min files:", low)
