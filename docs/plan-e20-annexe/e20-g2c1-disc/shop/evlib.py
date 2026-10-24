"""evlib: read-only access to exported maps and event programs. python -I -B -X utf8"""
import glob, json, os, re, sys
ROOT = 'D:/development/repo/alundra-casaengine-project-converter'
SIZE_FILE = ROOT + '/Alundra/Events/EventOpcodeSizeTable.cs'
def load_sizes():
    sizes = {}
    for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', open(SIZE_FILE, encoding='utf-8-sig').read()):
        sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
    return sizes
SIZES = load_sizes()
_ev = {}
def events_path(mapidx):
    if not _ev:
        for f in glob.glob(ROOT + '/alundra-project/Maps/*/*/events/*.events.json'):
            f = f.replace(chr(92), '/')
            m = re.search(r'-(\d+)\.events\.json$', f)
            if m: _ev[int(m.group(1))] = f
    return _ev.get(mapidx)
def load_events(mapidx):
    p = events_path(mapidx)
    return json.load(open(p, encoding='utf-8-sig')) if p else None
def load_map(mapidx):
    return json.load(open(ROOT + '/data-extracted/data/map_%d.json' % mapidx, encoding='utf-8-sig'))
def prog_end(doc, start):
    starts = sorted({s for X in 'ABCDEF' for s in doc['EventCodes%sTable' % X]} | {len(doc['Codes'])})
    nxt = [s for s in starts if s > start]
    return nxt[0] if nxt else len(doc['Codes'])
def sweep(doc, start, end):
    codes = doc['Codes']; pc = start; ins = []
    if start < 0 or start >= len(codes): return ins
    while pc < end and pc < len(codes):
        op = codes[pc]; size = SIZES.get(op, (0, '?'))[0]
        if size < 1 or pc + size > len(codes):
            ins.append((pc, op, None, True)); break
        ins.append((pc, op, codes[pc:pc + size], False)); pc += size
    return ins
def show(doc, slot, idx, out=sys.stdout):
    tab = doc['EventCodes%sTable' % slot]
    if idx >= len(tab):
        print('  (index %d out of table of %d)' % (idx, len(tab)), file=out); return
    s = tab[idx]; e = prog_end(doc, s)
    print('== %s[%d] start=%d end=%d' % (slot, idx, s, e), file=out)
    for pc, op, v, bad in sweep(doc, s, e):
        nm = SIZES.get(op, (0, '?'))[1]
        print('   @%d 0x%02X %-24s %s%s' % (pc, op, nm, list(v[1:]) if v else '', ' BAD' if bad else ''), file=out)
        if bad: break

_tm = {}
def load_cells(mapidx):
    """returns dict with walkability, ground_property, slope, height lists (52x60) from the exported tileMap"""
    if mapidx in _tm: return _tm[mapidx]
    for f in glob.glob(ROOT + '/alundra-project/Maps/*/*/tilemap/*.tileMap'):
        f = f.replace(chr(92), '/')
        folder = os.path.basename(os.path.dirname(os.path.dirname(f)))
        mm = re.search(r'-(\d+)$', folder)
        if mm and int(mm.group(1)) == mapidx:
            tm = json.load(open(f, encoding='utf-8'))
            cells = json.loads(tm['custom_properties']['AlundraCells'])
            _tm[mapidx] = cells
            return cells
    return None

def all_map_ids():
    ids = []
    for f in glob.glob(ROOT + '/data-extracted/data/map_*.json'):
        mm = re.search(r'map_(\d+)\.json$', f.replace(chr(92), '/'))
        if mm: ids.append(int(mm.group(1)))
    return sorted(ids)
