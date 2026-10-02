"""census_exec.py - E19.d D5: static census of the 0x24 (Wait force adjusted) sites of the whole corpus.

Population  : every 0x24 at an instruction boundary of the linear decode of the code region of every map (429 expected).
Reachability: roots = the B entry of every map event; for every record the A, C, D, E and F slot entries whose index has
              bit 0x80 set (entry = table[index & 0x7F]); reachable = every instruction reached from a root by following
              every jump, branch and call (all branches open). DLL-only = reached only from the slot entries whose index has
              the bit 0x80 clear (the DLL falls back on the map's own table). Dormant = neither.
Method      : for every reachable or DLL-only site, a path-sensitive forward walk of each root program tracks the logic entity
              (0x42/0x43), its animation (0x1A, 0x59, 0x5A, 0x5B), its direction (0x09, 0x08, 0x0A, 0x0C, 0x3A, 0x5A, 0x5B), its position
              when pinned (spawn record, 0x64/0x65/0x8A) and the hero lock (0x10/0x11). At the site, each reaching state is cast on the
              cell grid from the footprint of the actor, in its direction: first cell that blocks its mask, or whose height step is
              higher than the controller's step height.
Classes     : wall found / zero speed / no wall / indeterminate (worst state of the site wins: indeterminate > no wall > zero speed > wall).
"""
import collections
import glob
import json
import math
import os
import re
import sys

BIN = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BIN)
from m import Map, SZ, NM, s16, ROOT, MAPS  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")

# ------------------------------------------------------------------------------------------------ constants from the DLL
_tables = open(ROOT + "/Alundra/Scripts/AnimationTables.cs", encoding="utf-8-sig").read()


def _cs_array(name):
    m = re.search(r"public static readonly \w+\[\] %s =\s*\{(.*?)\};" % name, _tables, re.S)
    out = []
    for tok in re.finditer(r"unchecked\(\(short\)(0x[0-9a-fA-F]+)\)|(0x[0-9a-fA-F]+)", m.group(1)):
        if tok.group(1):
            v = int(tok.group(1), 16)
            out.append(v - 65536 if v >= 32768 else v)
        else:
            out.append(int(tok.group(2), 16))
    return out


OFFX = _cs_array("OffsetXList")
OFFY = _cs_array("OffsetYList")
HEIGHTS = _cs_array("HeightsTable_800236d4")
CARDINAL = [0, 0x10, 8, 0x18]  # AnimationTables.CardinalDirectionTable
assert len(OFFX) == 32 and len(OFFY) == 32 and len(HEIGHTS) == 24, (len(OFFX), len(OFFY), len(HEIGHTS))

SPRITES = json.load(open(ROOT + "/alundra-project/Data/sprite-records.json", encoding="utf-8-sig"))
ASSETS = {e["id"]: e for e in json.load(open(ROOT + "/alundra-project/AssetInfos.json", encoding="utf-8-sig"))["asset_infos"]}
HERO_ID = "192c2eeb-bdda-5bb4-92d6-39a4ae298307"

_step_cache = {}


def step_height(pid):
    if pid in _step_cache:
        return _step_cache[pid]
    val = None
    info = ASSETS.get(pid)
    if info:
        path = ROOT + "/alundra-project/" + info["file_name"].replace("\\", "/")
        if os.path.exists(path):
            d = json.load(open(path, encoding="utf-8-sig"))
            for c in d.get("components", []):
                if c.get("type") == "CharacterControllerComponent":
                    val = c["settings"].get("step_height")
    _step_cache[pid] = val
    return val


def header(pid):
    return SPRITES.get(pid)


def anim_speed(pid, anim):
    h = header(pid)
    if h is None or anim is None:
        return None
    for a in h["AnimSets"]:
        if a["Anim"] == anim:
            return a["Speed"]
    return 0


def mask_for(pid):
    h = header(pid)
    flags = h["MoreFlags"]
    mask = 0x40
    if flags & 0x08:
        mask |= 0x01
    if flags & 0x01:
        mask |= 0x1000
    return mask


# ------------------------------------------------------------------------------------------------ control flow
def succ(C, pc, start, calls):
    op = C[pc]
    sz = SZ.get(op) or 1
    v = C[pc:pc + 10] + [0] * 10
    if op == 0xFF:
        return []
    if op == 0x02:
        return [pc + s16(v[1], v[2])]
    if op in (0x03, 0x04):
        return [pc + s16(v[1], v[2]), pc + 3]
    if op in (0x30, 0x31, 0x7B, 0x7C):
        return [pc + s16(v[3], v[4]), pc + 5]
    if op == 0x78:
        calls.add(pc + 3)
        return [pc + s16(v[1], v[2])]
    if op in (0x79, 0x7A):
        calls.add(pc + 3)
        return [pc + s16(v[1], v[2]), pc + 3]
    if op == 0x74:
        return [pc + s16(v[1], v[2]), pc + 3]
    if op == 0x49:
        return [start]
    if op in (0x4A, 0x4B):
        return [start, pc + 1]
    if op in (0x57, 0x58):
        return [pc + s16(v[1], v[2]), pc + s16(v[3], v[4]), pc + s16(v[5], v[6]), pc + s16(v[7], v[8])]
    if op == 0x7D:
        return list(calls)
    return [pc + sz]


def reach(C, start):
    calls = set()
    while True:
        before = len(calls)
        seen = set()
        work = [start]
        while work:
            pc = work.pop()
            if pc in seen or pc < 0 or pc >= len(C):
                continue
            seen.add(pc)
            work.extend(succ(C, pc, start, calls))
        if len(calls) == before:
            return seen


# ------------------------------------------------------------------------------------------------ cells
class Cells:
    def __init__(self, mp):
        self.W, self.H = mp.W, mp.H
        c = mp.cells
        self.walk, self.gp, self.slope, self.hgt = c["walkability"], c["ground_property"], c["slope"], c["height"]

    def idx(self, px, py):
        cx = min(max(int(math.floor(px)) // 24, 0), self.W - 1)
        cy = min(max(int(math.floor(py)) // 16, 0), self.H - 1)
        return cy * self.W + cx

    def blocked(self, px, py, mask):
        i = self.idx(px, py)
        return ((self.walk[i] | (self.gp[i] << 8)) & mask) != 0

    def ground(self, px, py):
        i = self.idx(px, py)
        h, kind = self.hgt[i], self.slope[i] & 3
        x, y = int(math.floor(px)), int(math.floor(py))
        if kind == 1:
            return (h - 1) * 16 + 16 - (y % 16)
        if kind == 2:
            return (h - 1) * 16 + HEIGHTS[(23 - (x % 24)) % 24]
        if kind == 3:
            return (h - 1) * 16 + HEIGHTS[x % 24]
        return h * 16


_cast_cache = {}


def cast(cells, pos, d, hdr, mask, step):
    key = (id(cells), pos, d, id(hdr), mask, step)
    r = _cast_cache.get(key)
    if r is None:
        r = _cast(cells, pos, d, hdr, mask, step)
        _cast_cache[key] = r
    return r


def _cast(cells, pos, d, hdr, mask, step):
    """Cast the footprint of an actor from pos (px) along direction d. Returns (kind, euclidean_px, cell, why, iterations) where kind is
    'wall' or 'nowall'; one iteration moves the major axis by 1 px."""
    vx, vy = OFFX[d], OFFY[d]
    m = max(abs(vx), abs(vy))
    if m == 0:
        return ("zero", 0, None, "", 0)
    sx, sy = vx / m, vy / m
    ox, oy, sw, sh = hdr["OffsetX"], hdr["OffsetY"], max(hdr["SizeX"], 1), max(hdr["SizeY"], 1)
    x, y = float(pos[0]), float(pos[1])

    def corners(px, py):
        x1, y1 = px + ox, py + oy
        x2, y2 = x1 + sw - 1, y1 + sh - 1
        return [(x1, y1), (x2, y1), (x1, y2), (x2, y2)]

    g = max(cells.ground(cx, cy) for cx, cy in corners(x, y))
    limit = int((cells.W * 24 + cells.H * 16) * 1.5) + 50
    travelled = 0.0
    n = 0
    for _ in range(limit):
        nx, ny = x + sx, y + sy
        if nx < -24 or ny < -16 or nx > cells.W * 24 + 24 or ny > cells.H * 16 + 16:
            return ("nowall", travelled, None, "left the map", n)
        cs = corners(nx, ny)
        for cx, cy in cs:
            if cells.blocked(cx, cy, mask):
                return ("wall", travelled, (cells.idx(cx, cy) % cells.W, cells.idx(cx, cy) // cells.W), "mask", n)
        ng = max(cells.ground(cx, cy) for cx, cy in cs)
        if step is not None and ng - g > step:
            i = cells.idx(*cs[0])
            return ("wall", travelled, (i % cells.W, i // cells.W), "step %d>%s" % (ng - g, step), n)
        g = ng
        x, y = nx, ny
        travelled += math.hypot(sx, sy)
        n += 1
    return ("nowall", travelled, None, "limit", n)


# ------------------------------------------------------------------------------------------------ dataflow
HERO = "H"
UNKNOWN = "U"
SUSPEND = {0x00, 0x0B, 0x1C, 0x1D, 0x1E, 0x1F, 0x24, 0x36, 0x37, 0x38, 0x39, 0x3B, 0x44, 0x50, 0x51, 0x5C, 0x0D, 0x9F, 0x2F, 0x70, 0x6E}
CAP = 300


def rec_pos(r):
    return (int(r["XPos"]) * 12 + 12, int(r["YPos"]) * 8 + 8)


def rec_dir(r):
    return CARDINAL[int(r.get("SpriteDirection", 0)) & 3]


class Ctx:
    def __init__(self, mp):
        self.mp = mp
        self.cells = Cells(mp) if mp.cells else None
        self.rec = {int(r["Index"]): r for r in mp.records}

    def pid(self, actor):
        if actor == HERO:
            return HERO_ID
        if isinstance(actor, tuple) and actor[0] == "R":
            r = self.rec.get(actor[1])
            return r.get("PrefabAssetId") if r else None
        return None

    def name(self, actor):
        if actor == HERO:
            return "Alundra (hero)"
        if isinstance(actor, tuple) and actor[0] == "R":
            r = self.rec.get(actor[1])
            return "rec%d %s" % (actor[1], r.get("EntityName", "?") if r else "?")
        return "unknown"

    def init_actor(self, actor):
        # (anim, dir, pin, moved)
        if actor == HERO:
            return (None, None, None, False)
        if isinstance(actor, tuple) and actor[0] == "R" and actor[1] in self.rec:
            r = self.rec[actor[1]]
            return (0, rec_dir(r), rec_pos(r), False)
        return (None, None, None, False)


def getA(actors, a, ctx):
    for x in actors:
        if x[0] == a:
            return x[1:]
    return ctx.init_actor(a)


def setA(actors, a, st):
    out = [x for x in actors if x[0] != a]
    out.append((a,) + st)
    return tuple(sorted(out, key=lambda t: repr(t[0])))


def targets(sel, logic, actors):
    if sel == 0x80:
        return [logic]
    if sel == 0x81:
        return [HERO]
    if sel < 0x80:
        return [("R", sel)]
    return sorted({x[0] for x in actors} | {logic}, key=repr)


def apply_dir(ctx, actors, a, newdir):
    anim, d, pin, moved = getA(actors, a, ctx)
    if moved and newdir != d:
        pin = None
    return setA(actors, a, (anim, newdir, pin, moved))


def resolve_dir(ctx, actors, a, enc):
    """-> list of possible directions (None = unknown)."""
    mode, low = enc >> 5, enc & 0x1F
    cur = getA(actors, a, ctx)[1]
    if mode == 0:
        return [low]
    if mode == 1:
        return [None if cur is None else (cur + low) & 31]
    if mode == 2:
        return [CARDINAL[enc & 3]]
    if mode == 4:
        return list(CARDINAL)
    return [None]


def travel_actor(ctx, actors, a, iters=None, ticks=None):
    """Move the actor `a` along its direction: by `iters` major-axis pixels, or for `ticks` logic ticks at its animation speed,
    clipped at the first wall; with neither, until the wall (a missing wall loses the pin)."""
    anim, d, pin, mv = getA(actors, a, ctx)
    pid = ctx.pid(a)
    hdr = header(pid) if pid else None
    if pin is None or d is None or hdr is None or ctx.cells is None:
        return actors
    sp = anim_speed(pid, anim)
    if iters is None and ticks is not None:
        if not sp:
            return actors
        m = max(abs(OFFX[d]), abs(OFFY[d]))
        iters = ticks * sp * m / 65536.0
    res = cast(ctx.cells, pin, d, header(pid), mask_for(pid), step_height(pid))
    if res[0] == "zero":
        return actors
    wall_iters = res[4] if res[0] == "wall" else None
    if iters is None:
        if wall_iters is None:
            return setA(actors, a, (anim, d, None, mv))
        n = wall_iters
    else:
        n = iters if wall_iters is None else min(iters, wall_iters)
    vx, vy = OFFX[d], OFFY[d]
    m = max(abs(vx), abs(vy))
    npin = (pin[0] + vx / m * n, pin[1] + vy / m * n)
    npin = (round(npin[0], 3), round(npin[1], 3))
    return setA(actors, a, (anim, d, npin, mv))


def step_state(ctx, op, v, st, pc):
    """-> list of successor states for the effect of one instruction. st = (logic, actors, lock)."""
    logic, actors, lock = st
    outs = []

    def one(l=logic, a=actors, k=lock):
        outs.append((l, a, k))

    if op == 0x10:
        one(k=1)
    elif op == 0x11:
        one(k=0)
    elif op == 0x42:
        one(l=HERO)
    elif op == 0x43:
        sel = v[1]
        # found-only: the searched record is assumed present (an absent one leaves the logic entity unchanged in the binary)
        if sel < 0x80:
            one(l=("R", sel))
        elif sel == 0x81:
            one(l=HERO)
        elif sel == 0x80:
            one()
        else:
            one(l=UNKNOWN)
    elif op == 0x1A:
        anim, d, pin, mv = getA(actors, logic, ctx)
        one(a=setA(actors, logic, (v[1], d, pin, mv)))
    elif op == 0x0B:  # Walk wait: TargetAnimationId = v1 (WalkUntilBeyondRadius), radius = v2 | v3 << 8
        anim, d, pin, mv = getA(actors, logic, ctx)
        one(a=setA(actors, logic, (v[1], d, pin, mv)))
    elif op == 0x09:
        one(a=apply_dir(ctx, actors, logic, v[1] & 31))
    elif op == 0x08:
        d = getA(actors, logic, ctx)[1]
        one(a=apply_dir(ctx, actors, logic, None if d is None else (d + v[1]) & 31))
    elif op == 0x0A:
        d = getA(actors, logic, ctx)[1]
        one(a=apply_dir(ctx, actors, logic, None if d is None else (d + 16) & 31))
    elif op == 0x3A:
        one(a=apply_dir(ctx, actors, logic, CARDINAL[v[1] & 3]))
    elif op == 0x0C:
        for d in CARDINAL:
            one(a=apply_dir(ctx, actors, logic, d))
    elif op in (0x59, 0x5A, 0x5B):
        sel = v[1]
        for dopt in (resolve_dir(ctx, actors, logic, v[2] if op == 0x5A else v[3]) if op != 0x59 else [False]):
            a2 = actors
            for t in targets(sel, logic, actors):
                if op in (0x59, 0x5B):
                    anim, d, pin, mv = getA(a2, t, ctx)
                    a2 = setA(a2, t, (v[2], d, pin, mv))
                if op in (0x5A, 0x5B):
                    a2 = apply_dir(ctx, a2, t, dopt)
            one(a=a2)
    elif op == 0x64:
        px, py = v[2] | (v[3] << 8), v[4] | (v[5] << 8)
        a2 = actors
        for t in targets(v[1], logic, actors):
            anim, d, pin, mv = getA(a2, t, ctx)
            a2 = setA(a2, t, (anim, d, (px, py), False))
        one(a=a2)
    elif op == 0x65:
        dx, dy = s16(v[2], v[3]), s16(v[4], v[5])
        a2 = actors
        for t in targets(v[1], logic, actors):
            anim, d, pin, mv = getA(a2, t, ctx)
            a2 = setA(a2, t, (anim, d, None if pin is None else (pin[0] + dx, pin[1] + dy), mv))
        one(a=a2)
    elif op == 0x8A:
        px, py = v[2] | (v[3] << 8), v[4] | (v[5] << 8)
        r = ctx.rec.get(v[1])
        one(a=setA(actors, ("R", v[1]), (0, rec_dir(r) if r else None, (px, py), False)))
    elif op in (0x8B, 0x89):
        a2 = actors
        tg = [("R", v[1])] if op == 0x8B else targets(v[2], logic, actors)
        for t in tg:
            anim, d, pin, mv = getA(a2, t, ctx)
            a2 = setA(a2, t, (anim if op == 0x89 else 0, d, None, mv))
        one(a=a2)
    elif op == 0x2D:
        one()
    else:
        one()
    # waits of a known duration move every actor that walks; the other suspensions leave it moving for an unknown time
    res = []
    for (l, a, k) in outs:
        a2 = a
        if op == 0x37:
            for x in list(a2):
                a2 = travel_actor(ctx, a2, x[0], ticks=v[1])
        elif op == 0x00:
            for x in list(a2):
                a2 = travel_actor(ctx, a2, x[0], ticks=1)
        elif op == 0x24:
            a2 = travel_actor(ctx, a2, l)  # until the wall
        elif op in (0x0B,):
            a2 = travel_actor(ctx, a2, l, iters=float(v[2] | (v[3] << 8)))
        elif op in (0x1E, 0x1F):
            a2 = travel_actor(ctx, a2, l, iters=float(v[1] | (v[2] << 8)))
        elif op in SUSPEND:
            for x in a:
                anim, d, pin, mv = getA(a2, x[0], ctx)
                sp = anim_speed(ctx.pid(x[0]), anim)
                if sp:
                    a2 = setA(a2, x[0], (anim, d, pin, True))
        res.append((l, a2, k))
    outs = res
    return outs


def run_program(ctx, root, owner, slot, C):
    start = root
    calls = set()
    reach(C, start)  # warm: fills nothing (calls local) - recompute below
    seen_calls = set()
    # compute calls via a reachability pass that exposes the call set
    c = set()
    while True:
        before = len(c)
        seen = set()
        work = [start]
        while work:
            pc = work.pop()
            if pc in seen or pc < 0 or pc >= len(C):
                continue
            seen.add(pc)
            work.extend(succ(C, pc, start, c))
        if len(c) == before:
            break
    calls = c
    init_logic = HERO if slot == "B" else owner
    init = (init_logic, tuple(), 0)
    states = collections.defaultdict(set)
    work = [(start, init)]
    widened = set()
    while work:
        pc, st = work.pop()
        if pc < 0 or pc >= len(C) or st in states[pc]:
            continue
        if len(states[pc]) >= CAP:
            widened.add(pc)
            continue
        states[pc].add(st)
        op = C[pc]
        sz = SZ.get(op) or 1
        v = C[pc:pc + 10] + [0] * 10
        nxt = succ(C, pc, start, calls)
        for ns in step_state(ctx, op, v, st, pc):
            for n in nxt:
                work.append((n, ns))
    return states, widened


def classify_state(ctx, st, zone, pc):
    """-> (cls, detail dict)"""
    logic, actors, lock = st
    if logic in (UNKNOWN, None) or not (logic == HERO or (isinstance(logic, tuple) and logic[0] == "R" and logic[1] in ctx.rec)):
        return "indeterminate", dict(actor=ctx.name(logic), why="logic entity not deducible")
    pid = ctx.pid(logic)
    hdr = header(pid) if pid else None
    anim, d, pin, moved = getA(actors, logic, ctx)
    det = dict(actor=ctx.name(logic), anim=anim, dir=d, pin=pin, moved=moved, lock=lock)
    if hdr is None:
        return "indeterminate", dict(det, why="no sprite header for the actor")
    if anim is None:
        return "indeterminate", dict(det, why="animation not deducible")
    sp = anim_speed(pid, anim)
    det["speed"] = sp
    if sp == 0:
        return "zero", det
    if d is None:
        return "indeterminate", dict(det, why="direction not deducible")
    if ctx.cells is None:
        return "indeterminate", dict(det, why="map without cells")
    mask = mask_for(pid)
    step = step_height(pid)
    det["mask"] = mask
    det["step"] = step
    if pin is not None:
        res = cast(ctx.cells, pin, d, hdr, mask, step)
        det["from"] = "pinned %s" % (pin,)
        det.update(res=res)
        return ("wall" if res[0] == "wall" else "nowall"), det
    if logic == HERO and zone is not None:
        x1, y1, x2, y2 = zone
        worst, dmin, dmax, n = "wall", 1e9, -1, 0
        for ty in range(max(y1, 0), min(y2, ctx.cells.H - 1) + 1):
            for tx in range(max(x1, 0), min(x2, ctx.cells.W - 1) + 1):
                p = (tx * 24 + 12, ty * 16 + 8)
                if ctx.cells.blocked(p[0] + hdr["OffsetX"], p[1] + hdr["OffsetY"], mask):
                    continue
                res = cast(ctx.cells, p, d, hdr, mask, step)
                n += 1
                if res[0] != "wall":
                    worst = "nowall"
                else:
                    dmin, dmax = min(dmin, res[1]), max(dmax, res[1])
        det["from"] = "every free cell of the zone (%d casts)" % n
        det["res"] = (worst, (dmin, dmax), None, "zone")
        return worst, det
    return "indeterminate", dict(det, why="position not deducible")


PRIO = {"indeterminate": 4, "nowall": 3, "zero": 2, "wall": 1}
CLASSNAME = {"wall": "wall found", "zero": "zero speed", "nowall": "no wall", "indeterminate": "indeterminate"}


def all_maps():
    out = []
    for f in glob.glob(MAPS + "/*/*/events/*.events.json"):
        base = os.path.basename(os.path.dirname(os.path.dirname(f)))
        mm = re.search(r"-(\d+)$", base)
        if mm:
            out.append(int(mm.group(1)))
    return sorted(set(out))


SLOTS = (("A", "EventCodesA_LoadIndex"), ("C", "EventCodesC_TickIndex"), ("D", "EventCodesD_TouchIndex"),
         ("E", "EventCodesE_DeactivateIndex"), ("F", "EventCodesF_InteractIndex"))


def main():
    rows = []
    totals = collections.Counter()
    notes = []
    for mid in all_maps():
        mp = Map(mid)
        C = mp.C
        boundary = sum(2 * len(t) for t in mp.T.values())
        sites = []
        pc = boundary
        while pc < len(C):
            op = C[pc]
            if op == 0x24:
                sites.append(pc)
            pc += SZ.get(op) or 1
        if not sites:
            continue
        ctx = Ctx(mp)
        roots = []  # (kind, slot, owner, start, zone, label)
        fallback = []
        for e in mp.mapevents:
            idx = int(e["EventCodesBIndex"])
            if idx & 0x7F:
                t = mp.T["B"]
                if (idx & 0x7F) < len(t) and t[idx & 0x7F]:
                    zone = (int(e["X1"]), int(e["Y1"]), int(e["X2"]), int(e["Y2"]))
                    roots.append(("map", "B", HERO, t[idx & 0x7F], zone, "mapevent%s B[%d]" % (e["Index"], idx & 0x7F)))
        for r in mp.records:
            for slot, key in SLOTS:
                idx = int(r.get(key, 0))
                t = mp.T[slot]
                if (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
                    item = ("rec", slot, ("R", int(r["Index"])), t[idx & 0x7F], None, "rec%s %s[%d]" % (r["Index"], slot, idx & 0x7F))
                    (roots if idx & 0x80 else fallback).append(item)
        reach_r = {}
        for rt in roots:
            reach_r[rt] = reach(C, rt[3])
        reach_f = {}
        for rt in fallback:
            reach_f[rt] = reach(C, rt[3])
        for s in sites:
            rr = [rt for rt in roots if s in reach_r[rt]]
            ff = [rt for rt in fallback if s in reach_f[rt]]
            if rr:
                rc = "reachable"
            elif ff:
                rc = "DLL only"
            else:
                rc = "dormant"
            totals[rc] += 1
            row = dict(map=mid, name=mp.d["name"], zone=mp.d["zone"], pc=s, reach=rc, roots=[r[5] for r in (rr or ff)])
            if rc != "dormant":
                best = None
                for rt in (rr or ff):
                    states, widened = run_program(ctx, rt[3], rt[2], rt[1], C)
                    sts = states.get(s, set())
                    if not sts:
                        # the walk could not carry a state to the site (e.g. widened): undecidable
                        cand = ("indeterminate", dict(why="no state reaches the site in the walk of %s" % rt[5]))
                        cands = [cand]
                    else:
                        cands = [classify_state(ctx, st, rt[4], s) for st in sts]
                    for cls, det in cands:
                        det = dict(det, root=rt[5], slot=rt[1])
                        if best is None or PRIO[cls] > PRIO[best[0]]:
                            best = (cls, det)
                        row.setdefault("all", []).append((cls, det))
                row["class"], row["detail"] = best
                lock = any(det.get("lock") for _, det in row["all"])
                row["lock"] = lock
                totals["class:" + best[0]] += 1
            rows.append(row)
    return rows, totals


if __name__ == "__main__":
    rows, totals = main()
    import pickle
    out = os.path.dirname(os.path.abspath(__file__))
    with open(out + "/census.pkl", "wb") as f:
        pickle.dump((rows, dict(totals)), f)
    print("sites", len(rows), dict(totals))
    print("maps with sites", len({r["map"] for r in rows}))
    print("reachable maps", len({r["map"] for r in rows if r["reach"] == "reachable"}))
