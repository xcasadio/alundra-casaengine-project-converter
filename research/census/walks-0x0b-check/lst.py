"""lst.py MAP [FROM TO] - own listing of a map's program (linear decode), records and map events (checker side)."""
import glob
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pop import maps, SZ_BIN, s16  # noqa: E402

ROOT = "D:/development/repo/alundra-casaengine-project-converter"
_tbl = open(ROOT + "/Alundra/Scripts/EventOpcodeSizeTable.cs", encoding="utf-8-sig").read()
NM = {int(m.group(1), 16): m.group(2) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\(\d+,\s*"([^"]*)"\)', _tbl)}


def flag(lo, hi):
    f = lo | (hi << 8)
    return ("T%d" % (f & 0x7FFF)) if f & 0x8000 else ("G%d" % f)


def main():
    mid = int(sys.argv[1])
    lo = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    hi = int(sys.argv[3]) if len(sys.argv) > 3 else 10 ** 9
    folder, evf = maps()[mid]
    ev = json.load(open(evf, encoding="utf-8"))
    C = ev["Codes"]
    T = {k: ev["EventCodes%sTable" % k] for k in "ABCDEF"}
    labels = {}
    for k, t in T.items():
        for i, o in enumerate(t):
            if o:
                labels.setdefault(o, []).append("%s[%d]" % (k, i))
    pc = sum(2 * len(t) for t in T.values())
    while pc < len(C):
        op = C[pc]
        sz = SZ_BIN.get(op) or 1
        if lo <= pc <= hi:
            if pc in labels:
                print("   ;; " + " ".join(labels[pc]))
            v = C[pc:pc + sz]
            x = ""
            if op in (0x02, 0x03, 0x04, 0x74, 0x78, 0x79, 0x7A):
                x = " -> %d" % (pc + s16(v[1], v[2]))
            elif op in (0x30, 0x31, 0x7B, 0x7C):
                x = " %s -> %d" % (flag(v[1], v[2]), pc + s16(v[3], v[4]))
            elif op in (0x05, 0x06, 0x32, 0x35, 0x36, 0x80, 0x81):
                x = " %s" % flag(v[1], v[2])
            print("%5d: %02X %-42s %s%s" % (pc, op, NM.get(op, "?")[:42], list(v[1:]), x))
        pc += sz
    if len(sys.argv) <= 2 or "-r" in sys.argv:
        tm = json.load(open(glob.glob(folder + "/tilemap/*.tileMap")[0], encoding="utf-8"))
        layers = {l["name"]: l["objects"] for l in tm.get("object_layers", [])}
        for o in layers.get("Entities", []):
            r = o["custom_properties"]
            print("rec", {k: r.get(k) for k in ("Index", "EntityName", "IsEnabled", "SpriteDirection", "XPos", "YPos", "Height",
                                                "EventCodesA_LoadIndex", "EventCodesC_TickIndex", "EventCodesD_TouchIndex",
                                                "EventCodesF_InteractIndex", "PrefabAssetId")})
        for o in layers.get("MapEvents", []):
            print("mev", o["custom_properties"])


if __name__ == "__main__":
    main()
