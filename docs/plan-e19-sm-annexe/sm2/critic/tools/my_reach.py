"""Own map graph: portals (DestMapId through the identity table) + 0x53 (internal index) over every program of every table.
Also the 0x38 remap census, and BFS variants."""
import sys, collections, json
sys.path.insert(0, 'tools')
from mycorpus import *

edges = collections.defaultdict(set)  # src -> {(dst, kind, detail)}
remaps = []
for mid in sorted(int(k) for k in IDX):
    ev, tm, base = load(mid)
    codes = ev['Codes']
    for l in tm['object_layers']:
        if l['name'] == 'Portals':
            for o in l['objects']:
                edges[mid].add((int(o['custom_properties']['DestMapId']), 'portal', 'P' + o['custom_properties']['Index']))
    allseen = {}
    for s in SL:
        t = table(ev, s)
        for i, entry in enumerate(t):
            if i == 0 or entry == 0: continue
            extra = set()
            seen = walk_program(codes, entry, extra)
            for pc, op in seen.items(): allseen.setdefault(pc, (op, s, i))
    for pc, (op, s, i) in allseen.items():
        if op == 0x53:
            edges[mid].add((codes[pc + 1] | (codes[pc + 2] << 8), '0x53', '%s[%d]@%d' % (s, i, pc)))
        if op == 0x38:
            remaps.append((mid, s, i, pc, codes[pc + 1] | (codes[pc + 2] << 8), codes[pc + 3] | (codes[pc + 4] << 8)))

print('0x38 remap sites:', len(remaps))
rem_idx = collections.defaultdict(set)
for (m, s, i, pc, idx, val) in remaps: rem_idx[idx].add(val)
print('remapped ids -> values:', {k: sorted(v) for k, v in sorted(rem_idx.items())})

def bfs(start, banned=set(), kinds=('portal', '0x53')):
    dist = {start: 0}; par = {}
    q = collections.deque([start])
    while q:
        u = q.popleft()
        for dst, kind, det in sorted(edges.get(u, [])):
            if kind not in kinds: continue
            if kind == 'portal' and dst in banned: continue
            if dst not in dist and str(dst) in IDX:
                dist[dst] = dist[u] + 1; par[dst] = (u, kind, det); q.append(dst)
    return dist, par

def path(par, t):
    p = []
    while t in par:
        u, k, d = par[t]; p.append((u, t, k, d)); t = u
    return list(reversed(p))

TARG = [55, 56, 57, 58, 59, 60, 305, 306, 307, 344, 442]
for name, banned in (('identity', set()), ('without any remapped id used by a portal', set(rem_idx.keys()))):
    dist, par = bfs(389, banned)
    print('\nBFS from 389,', name, ': reached', len(dist), 'of', len(IDX))
    for t in TARG:
        print('  map', t, dist.get(t, 'NOT reached'))
dist, par = bfs(389)
for t in (344, 442):
    print('path to', t, [(a, b, k, d) for a, b, k, d in path(par, t)])
# inbound edges of the targets
inb = collections.defaultdict(list)
for src, es in edges.items():
    for dst, kind, det in es: inb[dst].append((src, kind, det))
for t in TARG:
    print('inbound', t, sorted(set((s, k) for s, k, d in inb.get(t, []) if s != t)))
# 0x38 remaps landing on/at those maps
print('remaps touching targets:', [r for r in remaps if r[4] in TARG or r[5] in TARG])
