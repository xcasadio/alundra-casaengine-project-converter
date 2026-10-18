"""Independent check: on map 60, which skipped sites are reached only from roots whose raw index has bit 0x80 clear."""
import collections
import json
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, "census_mine.py"), encoding="utf-8").read()
src = src.rsplit("\nmain()", 1)[0]
ns = {"__name__": "census_lib"}
exec(compile(src, "census_mine.py", "exec"), ns)

repo = ns["REPO"]
project = ns["PROJECT"]
index = json.load(open(project + "/Maps/world-index.json", encoding="utf-8"))
SKIPPED = {0x15, 0x3C, 0x3D, 0x56, 0x5F, 0x60, 0x61, 0x66, 0x6A, 0x6B, 0x6C, 0x6D, 0x72, 0x7A, 0x88, 0x96, 0x97, 0x9E, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB9, 0xBB, 0xC2}
clear_roots_all = []
for mid_s, rel in index.items():
    mid = int(mid_s)
    rel = rel.replace("\\", "/")
    folder = os.path.dirname(project + "/" + rel)
    base = os.path.splitext(os.path.basename(rel))[0]
    ev = folder + "/events/" + base + ".events.json"
    tmd = folder + "/tilemap"
    if not os.path.exists(ev) or not os.path.isdir(tmd):
        print("missing", mid)
        continue
    doc = json.load(open(ev, encoding="utf-8"))
    tm = json.load(open(tmd + "/" + [f for f in os.listdir(tmd) if f.endswith(".tileMap")][0], encoding="utf-8"))
    clear, setr = [], []
    keys = [("MapEvents", "B", "EventCodesBIndex")] + [("Entities", s, k) for s, k in (("A", "EventCodesA_LoadIndex"), ("C", "EventCodesC_TickIndex"), ("D", "EventCodesD_TouchIndex"), ("E", "EventCodesE_DeactivateIndex"), ("F", "EventCodesF_InteractIndex"))]
    for layer in tm["object_layers"]:
        for lname, slot, key in keys:
            if layer["name"] != lname:
                continue
            for o in layer["objects"]:
                raw = int(o["custom_properties"][key])
                idx = raw & 0x7F
                t = doc["EventCodes%sTable" % slot]
                if idx and idx < len(t) and t[idx]:
                    (setr if raw & 0x80 else clear).append((slot, idx))
                    if not raw & 0x80:
                        clear_roots_all.append((mid, slot, idx, raw, o.get("name")))
    if not clear:
        continue
    with_all = {(s, i, pc, op) for s, i, pc, op in ns["walk_map"](doc, setr + clear) if op in SKIPPED}
    with_set = {(s, i, pc, op) for s, i, pc, op in ns["walk_map"](doc, setr) if op in SKIPPED}
    only = sorted(with_all - with_set)
    print(f"map {mid}: clear roots {clear} -> skipped sites only via clear roots: {len(only)}")
    for s, i, pc, op in only:
        print(f"   {s}[{i}] @{pc} {op:02X}")
print("all clear roots:", clear_roots_all)
