"""Static map graph of the export: portal edges (DestMapId, identity MapIdToInternalMapIndex) + 0x53 'Change map' edges
(v2<<8|v1, an internal index = world-index key) read over ALL programs of ALL six tables of each map (reach sets of
cfg2.reach_walkprogram). Read-only. python -B reach_maps.py [targets...]
Output: inbound edges of the target maps, BFS reachability from the new-game map 389 over (portals, 0x53), 0x38 remaps seen.
"""
import collections, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, tilemap2 as T

START = 389


def main():
    targets = [int(a) for a in sys.argv[1:]] or [55, 56, 57, 58, 59, 60, 307, 344, 442]
    edges = collections.defaultdict(set)   # src -> {(dst, kind, detail)}
    remaps = []
    n53 = 0
    for mid in C.map_ids():
        try:
            base, d = C.load(mid)
        except FileNotFoundError:
            continue
        codes = d['Codes']
        # portals
        try:
            for lname, o in T.objects(mid):
                if lname == 'Portals':
                    dest = int(o['custom_properties']['DestMapId'])
                    edges[mid].add((dest, 'portal', 'P%s' % o['custom_properties']['Index']))
        except FileNotFoundError:
            pass
        seen_all = {}
        for s in 'ABCDEF':
            table = d[C.TABLE_KEYS[s]]
            for i, entry in enumerate(table):
                if i == 0 or entry == 0:
                    continue
                seen, marks, ru, extra = cfg2.reach_walkprogram(codes, entry)
                for pc, op in seen.items():
                    seen_all.setdefault(pc, (op, s, i))
        for pc, (op, s, i) in seen_all.items():
            if op == 0x53:
                dst = codes[pc + 1] | (codes[pc + 2] << 8)
                edges[mid].add((dst, '0x53', '%s[%d]@%d' % (s, i, pc)))
                n53 += 1
            elif op == 0x38:
                remaps.append((mid, s, i, pc, codes[pc + 1:pc + 5]))
    print('0x53 sites (reach sets, all tables): %d ; 0x38 remap sites: %d' % (n53, len(remaps)))
    for r in remaps[:20]:
        print('   0x38', r)
    inbound = collections.defaultdict(list)
    for src, es in edges.items():
        for dst, kind, det in es:
            inbound[dst].append((src, kind, det))
    print()
    for t in targets:
        ins = sorted(inbound.get(t, []))
        ext = [e for e in ins if e[0] != t]
        print('map %d: %d inbound edges (%d from another map)' % (t, len(ins), len(ext)))
        for e in ext[:40]:
            print('     from map %d via %s %s' % e)
        if not ext:
            print('     (none from another map)')
    # BFS
    dist = {START: 0}
    q = collections.deque([START])
    while q:
        u = q.popleft()
        for dst, kind, det in edges.get(u, []):
            if dst not in dist:
                dist[dst] = dist[u] + 1
                q.append(dst)
    print('\nBFS from %d over portals + 0x53: %d maps reached of %d' % (START, len(dist), len(C.map_ids())))
    for t in targets:
        print('   map %d: %s' % (t, ('reached at distance %d' % dist[t]) if t in dist else 'NOT reached'))
    unreached = sorted(set(C.map_ids()) - set(dist))
    print('unreached maps: %d' % len(unreached))
    json.dump({str(k): sorted(list(v)) for k, v in edges.items()}, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'evidence', 'map_edges.json'), 'w'))
    json.dump(sorted(unreached), open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'evidence', 'unreached_from_389.json'), 'w'))


if __name__ == '__main__':
    main()
