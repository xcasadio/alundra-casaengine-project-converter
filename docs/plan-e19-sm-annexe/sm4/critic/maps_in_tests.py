"""For each of the 50 maps with an sm4 site, find the test files (Alundra.Tests/*.cs) that name the map: by world name, by '-ID"' suffix or by
the ID in a MapIndex/LoadMap argument."""
import json, re, os, glob, sys
sys.path.insert(0, '.')
import census_critic as c

rows = [l.rstrip('\n').split('\t') for l in open('my-skipped-sites.tsv')][1:]
sm4ops = {'0x15', '0x66', '0x72', '0x7A', '0x88', '0x96', '0x97', '0xB3'}
maps = sorted({int(r[0]) for r in rows if r[4] in sm4ops})
idx = json.load(open(c.PROJ + '/Maps/world-index.json', encoding='utf-8'))
tests = {}
for f in glob.glob(c.REPO + '/Alundra.Tests/*.cs') + glob.glob(c.REPO + '/Alundra.Tests/**/*.cs', recursive=True):
    try:
        tests[f] = open(f, encoding='utf-8-sig').read()
    except Exception:
        pass
for mid in maps:
    rel = idx[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    hits = []
    for f, t in tests.items():
        if base in t or ('-%d"' % mid) in t:
            hits.append(os.path.basename(f))
    print(mid, base.encode('ascii', 'replace').decode(), sorted(set(hits)))
