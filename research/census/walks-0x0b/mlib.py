"""Own map/program loader (checker side). Data only: exported events + tilemap + sprite records."""
import glob
import json
import os
import re
import sys

ROOT = "D:/development/repo/alundra-casaengine-project-converter"
MAPS = ROOT + "/alundra-project/Maps"

_tbl = open(ROOT + "/Alundra/Scripts/EventOpcodeSizeTable.cs", encoding="utf-8-sig").read()
SZ, NM = {}, {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', _tbl):
    SZ[int(m.group(1), 16)] = int(m.group(2))
    NM[int(m.group(1), 16)] = m.group(3)


def mapdir(mid):
    for f in glob.glob(MAPS + "/*/*"):
        if re.search(r"-%d$" % mid, f.replace("\\", "/")):
            return f.replace("\\", "/")
    raise KeyError(mid)


def s16(v):
    return v - 65536 if v >= 32768 else v


def flag(lo, hi):
    f = lo | (hi << 8)
    return ("T%d" % (f & 0x7FFF)) if f & 0x8000 else ("G%d" % f)


class Map:
    def __init__(self, mid):
        d = mapdir(mid)
        ev = json.load(open(glob.glob(d + "/events/*.events.json")[0], encoding="utf-8"))
        self.C = ev["Codes"]
        self.T = {k: ev["EventCodes%sTable" % k] for k in "ABCDEF"}
        tm = json.load(open(glob.glob(d + "/tilemap/*.tileMap")[0], encoding="utf-8"))
        self.tm = tm
        layers = {l["name"]: l["objects"] for l in tm.get("object_layers", [])}
        self.layers = layers
        cp = tm.get("custom_properties", {})
        self.cells = json.loads(cp["AlundraCells"]) if "AlundraCells" in cp else None
        self.W, self.H = tm["map_size"]["w"], tm["map_size"]["h"]
        self.boundary = sum(2 * len(t) for t in self.T.values())
        self.labels = {}
        for k, t in self.T.items():
            for i, off in enumerate(t):
                if off:
                    self.labels.setdefault(off, []).append("%s[%d]" % (k, i))

    def fmt(self, pc):
        C = self.C
        op = C[pc]
        sz = SZ.get(op) or 1
        b = C[pc:pc + sz]
        x = ""
        if op in (0x02, 0x03, 0x04, 0x78, 0x79, 0x7A, 0x74):
            x = " -> @%d" % (pc + s16(b[1] | b[2] << 8))
        elif op in (0x30, 0x31, 0x7B, 0x7C):
            x = " %s -> @%d" % (flag(b[1], b[2]), pc + s16(b[3] | b[4] << 8))
        elif op in (0x05, 0x06, 0x32, 0x35, 0x36):
            x = " " + flag(b[1], b[2])
        elif op == 0x8A:
            x = " rec%d at (%d,%d,%d)" % (b[1], b[2] | b[3] << 8, b[4] | b[5] << 8, b[6] | b[7] << 8)
        elif op == 0x64:
            x = " recsel %s pos (%d,%d,%d)?" % (b[1], b[2] | b[3] << 8, b[4] | b[5] << 8, b[6])
        elif op == 0x0B:
            x = " dir/anim %d r=%d" % (b[1], b[2] | b[3] << 8) if sz >= 4 else ""
        return "%5d: 0x%02X %-42s %s%s" % (pc, op, NM.get(op, "?"), list(b[1:]), x), sz

    def listing(self, a, b):
        pc = self.boundary
        out = []
        while pc < len(self.C):
            sz = SZ.get(self.C[pc]) or 1
            if a <= pc <= b:
                if pc in self.labels:
                    out.append("  ;; " + " ".join(self.labels[pc]))
                out.append(self.fmt(pc)[0])
            pc += sz
        return out


_SR = None


def sprite_records():
    global _SR
    if _SR is None:
        _SR = json.load(open(ROOT + "/alundra-project/Data/sprite-records.json", encoding="utf-8"))
    return _SR


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    m = Map(int(sys.argv[1]))
    print("\n".join(m.listing(int(sys.argv[2]), int(sys.argv[3]))))
