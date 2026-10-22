"""Independent re-derivation of the sm2 sites over the 483 maps, with the roots of AlundraStoryChainOpcodeAudit.ReadRoots (B of every map event; A, C, D, E, F of every
record: index & 0x7F, nonzero, inside its table, pointing at code) and the walk of cfg2.reach_walkprogram (a port of WalkProgram, with the 0x40 extra roots).
python -B census_sm2.py -> prints counts; compares with skipped-sites.tsv"""
import collections, csv, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, tilemap2 as T

SLOT = 'ABCDEF'
KEYS = {'A': 'EventCodesA_LoadIndex', 'C': 'EventCodesC_TickIndex', 'D': 'EventCodesD_TouchIndex', 'E': 'EventCodesE_DeactivateIndex', 'F': 'EventCodesF_InteractIndex'}
TSV = 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv'


def roots_of(mid, d):
    roots = []
    for lname, o in T.objects(mid):
        cp = o['custom_properties']
        if lname == 'MapEvents':
            idx = int(cp.get('EventCodesBIndex', '0')) & 0x7F
            roots.append(('B', idx))
        elif lname == 'Entities':
            for s, k in KEYS.items():
                idx = int(cp.get(k, '0')) & 0x7F
                roots.append((s, idx))
    return roots


def walk_map(mid, d):
    codes = d['Codes']
    done = set()
    todo = list(roots_of(mid, d))
    reached = {}
    while todo:
        slot, idx = todo.pop(0)
        if (slot, idx) in done:
            continue
        done.add((slot, idx))
        table = d[C.TABLE_KEYS[slot]]
        if idx == 0 or idx >= len(table) or table[idx] == 0:
            continue
        seen, marks, ru, extra = cfg2.reach_walkprogram(codes, table[idx])
        for pc, op in seen.items():
            reached[(slot, idx, pc)] = op
        for e in extra:
            todo.append(e)
    return reached


t0 = time.time()
found = []
n_instr = 0
for mid in C.map_ids():
    base, d = C.load(mid)
    r = walk_map(mid, d)
    for (slot, idx, pc), op in r.items():
        if 0x6A <= op <= 0x6D:
            found.append((mid, slot, idx, pc, op))
print('maps', len(C.map_ids()), 'time %.1fs' % (time.time() - t0))
print('sites 0x6A-0x6D:', len(found), dict(collections.Counter(hex(o) for *_, o in found)))
rows = list(csv.DictReader(open(TSV, encoding='utf-8'), delimiter='\t'))
ref = {(int(r['map']), r['slot'], int(r['program']), int(r['pc']), int(r['opcode'], 16)) for r in rows if 0x6A <= int(r['opcode'], 16) <= 0x6D}
mine = set(found)
print('reference (skipped-sites.tsv) :', len(ref), ' mine:', len(mine), ' equal:', ref == mine, ' only-mine:', len(mine - ref), ' only-ref:', len(ref - mine))
print('only-mine', sorted(mine - ref)[:10])
print('only-ref', sorted(ref - mine)[:10])
print('per map:', dict(sorted(collections.Counter(m for m, *_ in found).items())))
