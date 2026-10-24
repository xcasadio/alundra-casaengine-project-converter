#!/usr/bin/env python3
"""E20.g1b T0 (a): census of the maps of the recipe. Read-only. Python 3, run: python -B census_recipe.py [MAP ...]
For every exported map: the wired liftables (header ProgramTick == 23, kind != 0), the platform-like placements with the program
of their tick slot (C table) decoded with decode_events.py (copied unchanged from e20-g3/dll/), the tops of 16 px, the B[1] lock ops.
"""
import json, os, re, sys, glob
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import decode_events as de
ROOT = de.ROOT + '/alundra-project'
REC = json.load(open(ROOT + '/Data/sprite-records.json', encoding='utf-8'))

def tilemaps():
    out = {}
    for f in glob.glob(ROOT + '/Maps/*/*/tilemap/*.tileMap'):
        m = re.search(r'-(\d+)\.tileMap$', os.path.basename(f))
        if m: out[int(m.group(1))] = f
    return out

def placements(path):
    d = json.load(open(path, encoding='utf-8'))
    for l in d['object_layers']:
        if l['name'] == 'Entities':
            for o in l['objects']:
                p = o['custom_properties']
                h = REC.get(p.get('PrefabAssetId'))
                if h is None: continue
                yield p, h

def row(p, h):
    flags = h['MoreFlags'] | (h['CanPickup'] << 8) | (h['FlagsPortraitShadowType'] << 16)
    return dict(idx=int(p['Index']), name=p.get('EntityName'), en=p.get('IsEnabled'), x=int(p['XPos']) * 12 + 12, y=int(p['YPos']) * 8 + 8,
                z=int(p.get('Height', 0)) * 8, flags=flags, kind=(flags >> 9) & 3, coll=bool(flags & 0x80),
                sx=h['SizeX'], sy=h['SizeY'], sz=h['SizeZ'], ox=h['OffsetX'], oy=h['OffsetY'], oz=h['OffsetZ'],
                prog=[int(p.get('EventCodes%s' % k) or 0) for k in ('A_LoadIndex', 'B_MapIndex', 'C_TickIndex', 'D_TouchIndex', 'E_DeactivateIndex', 'F_InteractIndex')],
                hdr=(h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['ProgramInteract']))

PLAT = re.compile(r'Plateforme|asiba|Chariot|Flotteur|pont en bois|Bloc . pousser', re.I)

def prog_ops(doc, slot, i):
    for s, k, st, en in de.all_programs(doc):
        if s == slot and k == i:
            return [(pc, op, v) for pc, op, v, bad in de.sweep(doc, st, en)]
    # a table index may point to a start shared with another index of the same table
    st = doc['EventCodes%sTable' % slot][i]
    for s, k, st2, en in de.all_programs(doc):
        if s == slot and st2 == st:
            return [(pc, op, v) for pc, op, v, bad in de.sweep(doc, st2, en)]
    return []

if __name__ == '__main__':
    files = tilemaps()
    only = [int(a) for a in sys.argv[1:]]
    for m in (only or sorted(files)):
        rows = [row(p, h) for p, h in placements(files[m])]
        lifts = [r for r in rows if r['kind'] and r['hdr'][1] == 23 and r['en']]
        plats = [r for r in rows if PLAT.search(r['name'] or '') and r['en']]
        print('== map %d: wired liftables %d, platform-like %d' % (m, len(lifts), len(plats)))
        for r in plats:
            print('   plat', r['idx'], r['name'], 'pos', (r['x'], r['y'], r['z']), 'size', (r['sx'], r['sy'], r['sz']), 'progs ABCDEF', r['prog'], 'hdr', r['hdr'])


def prog_of(doc, slot, raw):
    """The program of the table slot for a placement index (index & 0x7f, EventProgramRunner.InitializeEventData); None when empty."""
    tab = doc['EventCodes%sTable' % slot]
    i = raw & 0x7f
    if i >= len(tab): return None
    st = tab[i]
    for s, k, st2, en in de.all_programs(doc):
        if s == slot and st2 == st:
            return st2, en, [(pc, op, v) for pc, op, v, bad in de.sweep(doc, st2, en) if v is not None]
    return None

WALK = {0x1E, 0x1F}
GATE = {0x2C: 'noEntityByFn', 0x30: 'ifFlagOn', 0x31: 'ifFlagOff', 0x2F: 'pad', 0x3B: 'heroInArea', 0x24: 'waitForceAdj', 0x25: 'waitCollideZ', 0x0B: 'waitBeyondRadius',
        0x27: 'face', 0x1D: 'walkToward', 0x42: 'logic=hero', 0x43: 'logic=', 0x10: 'lose control', 0x11: 'gain control'}

def summarize(ops):
    out = []
    for pc, op, v in ops:
        nm = de.SIZES.get(op, (0, '?'))[1]
        out.append('%d:%02X%s' % (pc, op, ''.join(' %d' % x for x in v[1:])))
    return ' | '.join(out)

def classify(doc, slot, raw, facing):
    p = prog_of(doc, slot, raw)
    if p is None: return None
    st, en, ops = p
    return dict(start=st, end=en, walk=[(pc, v[1] | v[2] << 8) for pc, op, v in ops if op in WALK],
                gates=sorted({GATE[op] for pc, op, v in ops if op in GATE}), ops=summarize(ops))
