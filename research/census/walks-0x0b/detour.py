"""detour.py - for every DLL-blocked state of a 0x0B / 0x1E site (rows_DLL.pkl), can the E4.d navigation detour of the DLL
(AlundraEventProgramRunner.TryEngageDetour, :2294-2321) route the walker?
  goal  = memorized start (px, truncated) + sign(OffsetX/Y[dir]) * (radius + 24), to a cell clamped to the grid;
  start = the cell of the contact position;
  grid  = navigation layer: walkable iff ((walkability | ground_property << 8) & 0x40) == 0 (NavigationWriter.cs:233-235),
          4-connected A* (NavigationQuery.AllowDiagonalMovement default false), start and goal must be walkable
          (GridPathfinder2D.cs:34-38).
Outcome per state: 'no grid path' (the walk keeps pushing: DLL block stands), 'path, blocked for the mover' (a cell of the
path hits the mover's own mask (ClassA/ClassB bits) or a rise > 3 px between consecutive path cells: likely still blocked),
'path clean' (the detour may finish the walk: DLL outcome uncertain). Approximation: cell centres, no footprint sweep."""
import collections
import os
import pickle
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from census_exec_orig import Map, OFFX, OFFY, mask_for, HERO_ID, header  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")
_maps = {}


def mapdata(mid):
    if mid not in _maps:
        _maps[mid] = Map(mid)
    return _maps[mid]


def cell_h(mp, cx, cy):
    i = cy * mp.W + cx
    return mp.cells["height"][i] * 16


def nav_ok(mp, cx, cy):
    i = cy * mp.W + cx
    return ((mp.cells["walkability"][i] | (mp.cells["ground_property"][i] << 8)) & 0x40) == 0


def mover_ok(mp, cx, cy, mask):
    i = cy * mp.W + cx
    return ((mp.cells["walkability"][i] | (mp.cells["ground_property"][i] << 8)) & mask) == 0


def bfs(mp, s, g):
    if not (nav_ok(mp, *s) and nav_ok(mp, *g)):
        return None
    prev = {s: None}
    q = collections.deque([s])
    while q:
        c = q.popleft()
        if c == g:
            break
        x, y = c
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= n[0] < mp.W and 0 <= n[1] < mp.H and n not in prev and nav_ok(mp, *n):
                prev[n] = c
                q.append(n)
    if g not in prev:
        return None
    path = []
    c = g
    while c is not None:
        path.append(c)
        c = prev[c]
    return path[::-1]


def assess(mid, pid, pin, end, d, rad):
    mp = mapdata(mid)
    sx = (OFFX[d] > 0) - (OFFX[d] < 0)
    sy = (OFFY[d] > 0) - (OFFY[d] < 0)
    gx = int(pin[0]) + sx * (rad + 24)
    gy = int(pin[1]) + sy * (rad + 24)
    goal = (min(max(gx // 24, 0), mp.W - 1), min(max(gy // 16, 0), mp.H - 1))
    start = (min(max(int(end[0]) // 24, 0), mp.W - 1), min(max(int(end[1]) // 16, 0), mp.H - 1))
    path = bfs(mp, start, goal)
    if path is None:
        return "no grid path", (start, goal)
    mask = mask_for(pid)
    for a, b in zip(path, path[1:]):
        if not mover_ok(mp, *b, mask):
            return "path, blocked for the mover (mask)", (start, goal, b)
        if cell_h(mp, *b) - cell_h(mp, *a) > 3:
            return "path, blocked for the mover (rise)", (start, goal, b)
    return "path clean", (start, goal, len(path))


def main():
    rows = pickle.load(open(HERE + "/rows_DLL.pkl", "rb"))
    out = {}
    for r in rows:
        if not r.get("reached"):
            continue
        mp = mapdata(r["map"])
        res = []
        for c, det in r["all"]:
            if c != "blocked":
                continue
            key = det["key"]
            if key == "H":
                pid = HERO_ID
            else:
                pid = {int(x["Index"]): x for x in mp.records}[key[1]].get("PrefabAssetId")
            res.append(assess(r["map"], pid, det["pin"], det["end"], det["dir"], det["rad"]))
        if res:
            out[(r["map"], r["pc"])] = res
    pickle.dump(out, open(HERE + "/detour.pkl", "wb"))
    agg = collections.Counter()
    for k, res in out.items():
        kinds = sorted({x[0] for x in res})
        agg[tuple(kinds)] += 1
    for k, n in agg.most_common():
        print(n, k)


if __name__ == "__main__":
    main()
