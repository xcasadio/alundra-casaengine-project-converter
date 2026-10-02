"""gstatic.py - static facts of the whole corpus used by the corrected 0x24 census (E19.d D5, D-E19-24).
Linear decode of every map: flag setters (0x05, 0x32), 0x53 Change map operands, 0x2D activations; portals.
Pickled to gstatic.pkl."""
import collections
import glob
import os
import pickle
import re
import sys

BIN = os.path.dirname(os.path.abspath(__file__))  # archived: was a folder of the session scratchpad
sys.path.insert(0, BIN)
from m import Map, SZ, MAPS  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")


def all_maps():
    out = set()
    for f in glob.glob(MAPS + "/*/*/events/*.events.json"):
        base = os.path.basename(os.path.dirname(os.path.dirname(f)))
        mm = re.search(r"-(\d+)$", base)
        if mm:
            out.add(int(mm.group(1)))
    return sorted(out)


def build():
    setters = collections.defaultdict(list)   # flag (16 bit) -> [(map, pc)]
    ch53 = []                                  # (src map, pc, dest map, tx, ty, tz)
    portals = []                               # (src map, index, X1, Y1, X2, Y2, dest map, DX, DY, Z, Flags)
    act2d = collections.defaultdict(set)       # map -> {record}
    names = {}
    for mid in all_maps():
        mp = Map(mid)
        names[mid] = (mp.d["zone"], mp.d["name"])
        C = mp.C
        pc = sum(2 * len(t) for t in mp.T.values())
        while pc < len(C):
            op = C[pc]
            sz = SZ.get(op) or 1
            v = C[pc:pc + 10] + [0] * 10
            if op in (0x05, 0x32):
                setters[v[1] | (v[2] << 8)].append((mid, pc))
            elif op == 0x53:
                ch53.append((mid, pc, v[1] | (v[2] << 8), v[3], v[4], v[5]))
            elif op == 0x2D:
                act2d[mid].add(v[1])
            pc += sz
        for i, p in enumerate(mp.portals):
            portals.append((mid, i, int(p["X1"]), int(p["Y1"]), int(p["X2"]), int(p["Y2"]), int(p["DestMapId"]),
                            int(p["DestTileX"]), int(p["DestTileY"]), int(p.get("ZLevel", 0)), int(p["Flags"])))
    return dict(setters=dict(setters), ch53=ch53, portals=portals, act2d=dict(act2d), names=names)


if __name__ == "__main__":
    g = build()
    out = os.path.dirname(os.path.abspath(__file__)) + "/gstatic.pkl"
    pickle.dump(g, open(out, "wb"))
    print("maps", len(g["names"]), "flags with setters", len(g["setters"]), "0x53", len(g["ch53"]), "portals", len(g["portals"]))
    print("G1655 setters", g["setters"].get(1655))
