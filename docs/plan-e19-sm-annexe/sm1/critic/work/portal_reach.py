"""Critic: which maps lead to the 11 sm1 maps by a portal?  (a map that nothing leads to cannot be recetted by walking)"""
import json, os, collections

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
TARGETS = {27, 29, 30, 77, 78, 113, 136, 159, 226, 398, 400}
into = collections.defaultdict(set)
for mid_s, rel in idx.items():
    mid = int(mid_s)
    rel = rel.replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    p = os.path.dirname(REPO + '/alundra-project/' + rel) + '/tilemap/' + base + '.tmj'
    if not os.path.exists(p):
        continue
    d = json.load(open(p, encoding='utf-8-sig'))
    for l in d['layers']:
        if l['name'] == 'Portals':
            for o in l['objects']:
                pr = {q['name']: q['value'] for q in o['properties']}
                dest = pr.get('DestMapId')
                if dest in TARGETS and dest != mid:
                    into[dest].add(mid)
for t in sorted(TARGETS):
    print(t, 'reached by portals of maps', sorted(into[t]))
