"""Read-only. Census of 0xA7 (load music n, v[2] = play now) operands per track index over every reachable
instruction of alundra-project/Maps/**/*.events.json, reusing the branch-following walker of
e16-0/q2-opcodes/scan_opcodes_v2.py (MapWalker, size table from Alundra/Scripts/EventOpcodeSizeTable.cs).
Also lists the maps whose map-entry table value (Maps/music-index.json) is each track.
Prints, per track index: map-entry maps, 0xA7 sites (map, n, v2)."""
import sys, os, json
from collections import defaultdict
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "e16-0", "q2-opcodes"))
import scan_opcodes_v2 as sc

REPO = sc.REPO
size_table = sc.parse_size_table(sc.SIZE_TABLE_CS)
files = sorted(sc.MAPS_DIR.glob("**/*.events.json"))
a7 = defaultdict(list)  # n -> [(map, v2, addr)]
for fp in files:
    doc = json.loads(fp.read_text(encoding="utf-8"))
    codes = doc.get("Codes") or []
    map_index = doc.get("MapIndex")
    offs = []
    for name in sc.TABLE_NAMES:
        for off in doc.get(f"EventCodes{name}Table") or []:
            if isinstance(off, int) and 0 <= off < len(codes):
                offs.append(off)
    w = sc.MapWalker(codes, size_table, sorted(set(offs)))
    reach = set()
    for off in set(offs):
        s, _ = w.reach(off)
        reach |= s
    for a in sorted(reach):
        d = w.decode(a)
        if d and d[0] == 0xA7:
            a7[codes[a + 1]].append((map_index, fp.parent.parent.name if fp.parent.name == "events" else fp.parent.name, codes[a + 2], a))

mi = json.load(open(REPO / "alundra-project/Maps/music-index.json"))
wi = json.load(open(REPO / "alundra-project/Maps/world-index.json"))
by_track = defaultdict(list)
for k, v in mi.items():
    by_track[v].append(int(k))  # -1 maps: silent in the original (ADR-0004, 0x80049b04), kept apart
out = {}
for n in range(0, 47):
    out[n] = {"map_entry": sorted(by_track.get(n, [])), "a7": a7.get(n, [])}
    if out[n]["map_entry"] or out[n]["a7"]:
        names = sorted(set(os.path.basename(os.path.dirname(wi[str(m)])) for m in out[n]["map_entry"] if str(m) in wi))
        print(f"track {n}: map-entry {len(out[n]['map_entry'])} maps {out[n]['map_entry'][:12]}{'...' if len(out[n]['map_entry']) > 12 else ''}")
        if names:
            print(f"   names: {names[:8]}{'...' if len(names) > 8 else ''}")
        if out[n]["a7"]:
            sites = sorted(set((m, nm, v2) for m, nm, v2, a in out[n]["a7"]))
            print(f"   0xA7 sites {len(out[n]['a7'])}: {sites[:14]}{'...' if len(sites) > 14 else ''}")
json.dump({str(k): v for k, v in out.items()}, open(os.path.join(HERE, "a7scan.json"), "w"), indent=1)
