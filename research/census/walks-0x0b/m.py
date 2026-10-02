"""Shared helpers for the E19.c arcs surface (478, 416): map loading, sizes, Dispatch case set, disassembly.
Read-only on the repo."""
import glob, json, os, re, sys

ROOT = "D:/development/repo/alundra-casaengine-project-converter"
MAPS = ROOT + "/alundra-project/Maps"
DATA = ROOT + "/alundra-project/Data"

_tbl = open(ROOT + "/Alundra/Scripts/EventOpcodeSizeTable.cs", encoding="utf-8-sig").read()
SZ, NM = {}, {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', _tbl):
    SZ[int(m.group(1), 16)] = int(m.group(2)); NM[int(m.group(1), 16)] = m.group(3)

# Dispatch case labels of the CURRENT runner (parsed from the switch, lines 512..1447), plus 0x00/0xFF of the loop.
_run = open(ROOT + "/Alundra/Scripts/AlundraEventProgramRunner.cs", encoding="utf-8-sig").read().splitlines()
_start = next(i for i, l in enumerate(_run) if "private int Dispatch(int command" in l)
_end = next(i for i in range(_start, len(_run)) if re.match(r"^\s+default:", _run[i]))
CASES = set()
for l in _run[_start:_end]:
    mm = re.match(r"^\s+case 0x([0-9A-Fa-f]{2}):", l)
    if mm:
        CASES.add(int(mm.group(1), 16))
LOOP = {0x00, 0xFF}


def map_dir(mid):
    for f in glob.glob(MAPS + "/*/*/events/*.events.json"):
        f = f.replace("\\", "/")
        folder = os.path.dirname(os.path.dirname(f))
        base = os.path.basename(folder)
        mm = re.search(r"-(\d+)$", base)
        if mm and int(mm.group(1)) == mid:
            tm = glob.glob(folder + "/tilemap/*.tileMap")[0].replace("\\", "/")
            return dict(folder=folder, name=base, zone=os.path.basename(os.path.dirname(folder)), events=f, tilemap=tm)
    raise KeyError(mid)


class Map:
    def __init__(self, mid):
        self.mid = mid
        d = map_dir(mid)
        self.d = d
        ev = json.load(open(d["events"], encoding="utf-8"))
        self.C = ev["Codes"]
        self.T = {k: ev["EventCodes%sTable" % k] for k in "ABCDEF"}
        tm = json.load(open(d["tilemap"], encoding="utf-8"))
        self.tm = tm
        layers = {l["name"]: l["objects"] for l in tm.get("object_layers", [])}
        self.layers = layers
        self.records = [o["custom_properties"] for o in layers.get("Entities", [])]
        self.mapevents = [o["custom_properties"] for o in layers.get("MapEvents", [])]
        self.portals = [o["custom_properties"] for o in layers.get("Portals", [])]
        cp = tm.get("custom_properties", {})
        self.cells = json.loads(cp["AlundraCells"]) if "AlundraCells" in cp else None
        self.W, self.H = tm["map_size"]["w"], tm["map_size"]["h"]

    def cell(self, name, x, y):
        return self.cells[name][y * self.W + x]


def s16(lo, hi):
    v = lo | (hi << 8)
    return v - 65536 if v >= 32768 else v


def flagname(lo, hi):
    f = lo | (hi << 8)
    return ("T%d" % (f & 0x7fff)) if f & 0x8000 else ("G%d" % f)


def fmt(C, pc):
    op = C[pc]
    sz = SZ.get(op) or 1
    b = C[pc:pc + sz]
    st = "IMPL" if (op in CASES or op in LOOP) else "----"
    extra = ""
    if op in (0x02, 0x03, 0x04, 0x78, 0x79):
        extra = " -> %d" % (pc + s16(b[1], b[2]))
    if op in (0x30, 0x31, 0x7B, 0x7C):
        extra = " %s -> %d" % (flagname(b[1], b[2]), pc + s16(b[3], b[4]))
    if op in (0x05, 0x06, 0x32, 0x35, 0x36):
        extra = " " + flagname(b[1], b[2])
    if op == 0x74:
        extra = " -> %d" % (pc + s16(b[1], b[2]))
    return "%5d: %s 0x%02X %-40s %s%s" % (pc, st, op, NM.get(op, "?"), list(b[1:]), extra), sz


def dump(mp, out=sys.stdout):
    C = mp.C
    boundary = sum(2 * len(t) for t in mp.T.values())
    labels = {}
    for k, t in mp.T.items():
        for i, off in enumerate(t):
            if off >= boundary:
                labels.setdefault(off, []).append("%s[%d]" % (k, i))
    pc = boundary
    while pc < len(C):
        if pc in labels:
            print("  ;; entry " + " ".join(labels[pc]), file=out)
        line, sz = fmt(C, pc)
        print(line, file=out)
        pc += sz


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    mid = int(sys.argv[1])
    mp = Map(mid)
    print("MAP", mid, mp.d["zone"], "/", mp.d["name"], "codes", len(mp.C), "size", mp.W, "x", mp.H)
    print("CASES", len(CASES), " ".join("%02X" % c for c in sorted(CASES)))
    for k, t in mp.T.items():
        print(" table", k, {i: v for i, v in enumerate(t) if v})
    dump(mp)
    print("Records:")
    for r in mp.records:
        print(" ", {k: r.get(k) for k in ("Index", "SpriteTableIndex", "SpriteDirection", "IsEnabled", "XPos", "YPos", "Height",
                                           "EventCodesA_LoadIndex", "EventCodesC_TickIndex", "EventCodesD_TouchIndex",
                                           "EventCodesE_DeactivateIndex", "EventCodesF_InteractIndex", "EntityName")})
    print("MapEvents:")
    for e in mp.mapevents:
        print(" ", e)
    print("Portals:")
    for p in mp.portals:
        print(" ", p)
    if mp.cells:
        print("cell layers:", {k: (len(v) if isinstance(v, list) else v) for k, v in mp.cells.items()})
