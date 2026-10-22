"""Critic's own corpus walker: independent re-derivation of (a) the 6A-6D sites reached from the entry of each program of each table,
(b) the map graph (portals + 0x53) and BFS from 389. Written from scratch from the DLL's WalkProgram (Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:163-)."""
import json, os, re, sys, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dllmodel import load_codes, SIZES, IDX, PROJECT, sext16

SLOTS = 'ABCDEF'
TK = {s: 'EventCodes%sTable' % s for s in SLOTS}


def walk(codes, entry):
    """returns dict pc->op reached (both branches), set of extra roots (slot, idx) from 0x40, marks/returns handled like WalkProgram"""
    n = len(codes)
    seen = {}
    marks = set()
    retusers = set()
    extra = set()
    work = [entry]

    def b(i):
        return codes[i] if 0 <= i < n else 0

    def s16(lo):
        return sext16(b(lo) | (b(lo + 1) << 8))

    while True:
        while work:
            pc = work.pop()
            while 0 <= pc < n and pc not in seen:
                op = codes[pc]
                seen[pc] = op
                size = SIZES.get(op)
                if size is None or size == 0 or op == 0xFF:
                    break
                jumped = False
                stop = False
                if op == 0x02:
                    pc += s16(pc + 1)
                    jumped = True
                elif op in (0x03, 0x04, 0x74):
                    work.append(pc + s16(pc + 1))
                elif op in (0x30, 0x31):
                    work.append(pc + s16(pc + 3))
                elif op in (0x57, 0x58):
                    for dd in range(4):
                        work.append(pc + s16(pc + 1 + 2 * dd))
                    stop = True
                elif op == 0x78:
                    marks.add(pc + 3)
                    pc += s16(pc + 1)
                    jumped = True
                elif op == 0x79:
                    marks.add(pc + 3)
                    work.append(pc + s16(pc + 1))
                elif op in (0x7B, 0x7C):
                    marks.add(pc + 5)
                    work.append(pc + s16(pc + 3))
                elif op == 0x7D:
                    retusers.add(pc)
                    stop = True
                elif op in (0x7E, 0x7F, 0x80, 0x81):
                    retusers.add(pc)
                elif op == 0x49:
                    pc = entry
                    jumped = True
                elif op == 0x4B:
                    work.append(entry)
                elif op == 0x40:
                    if 2 <= b(pc + 1) <= 5:
                        extra.add((SLOTS[b(pc + 1)], b(pc + 2) & 0x7F))
                if stop:
                    break
                if not jumped:
                    pc += size
        # returns: each return user continues at every mark
        added = False
        for ru in retusers:
            for m in marks:
                if m not in seen:
                    work.append(m)
                    added = True
        if not added:
            break
    return seen, extra


def tilemap_objects(mid):
    rel = IDX[str(mid)].replace(chr(92), '/')
    folder = PROJECT + '/' + os.path.dirname(rel)
    for f in os.listdir(folder + '/tilemap'):
        if f.endswith('.tileMap'):
            d = json.load(open(folder + '/tilemap/' + f, encoding='utf-8'))
            for layer in d['object_layers']:
                for o in layer.get('objects', []):
                    yield layer['name'], o
            return
    raise FileNotFoundError(folder)


if __name__ == '__main__':
    ids = sorted(int(k) for k in IDX)
    edges = collections.defaultdict(set)
    sites = []
    for mid in ids:
        d = load_codes(mid)
        codes = d['Codes']
        seen_all = {}
        for s in SLOTS:
            tab = d[TK[s]]
            for i, entry in enumerate(tab):
                if entry == 0 and i == 0:
                    continue
                if entry == 0:
                    continue
                seen, extra = walk(codes, entry)
                for pc, op in seen.items():
                    seen_all.setdefault(pc, (op, s, i))
                    if op in (0x6A, 0x6B, 0x6C, 0x6D):
                        sites.append((mid, s, i, pc, op))
                    if op == 0x53:
                        edges[mid].add((codes[pc + 1] | (codes[pc + 2] << 8), '0x53'))
        for ln, o in tilemap_objects(mid):
            if ln == 'Portals':
                edges[mid].add((int(o['custom_properties']['DestMapId']), 'portal'))
    # BFS
    dist = {389: 0}
    q = collections.deque([389])
    while q:
        u = q.popleft()
        for dst, k in edges.get(u, []):
            if dst not in dist:
                dist[dst] = dist[u] + 1
                q.append(dst)
    out = {'reached': len(dist), 'of': len(ids), 'dist': {t: dist.get(t) for t in (55, 56, 57, 58, 59, 60, 305, 306, 307, 344, 442)}}
    print(json.dumps(out))
    uniq = sorted(set(sites))
    c = collections.Counter(mid for (mid, s, i, pc, op) in uniq)
    print('sites (union over all table entries, by (map,slot,idx,pc,op)):', len(uniq))
    print(sorted(c.items()))
    json.dump({'edges': {str(k): sorted(v) for k, v in edges.items()}, 'sites': uniq}, open(sys.argv[1], 'w'))
