"""c0b.py - E19.d2 discovery, surface census0b: census of the 0x0B and 0x1E walks under three collision rules.

A copy of the corrected 0x24 census model (scratchpad e19d2/census_v2.py, D-E19-24: real starts by 0x8A / 0x2D+0x64,
real hero arrivals, flag dependencies, path-sensitive walk) with these changes:
  * population: every 0x0B and 0x1E at an instruction boundary of the linear decode (1258 + 1083 sites);
  * the walk of every program uses the geometry of geo.py under MODE = DLL | FIX | ORIG (cells, entity boxes, slide, border);
  * a 0x0B AND a 0x1E whose walk is blocked before the radius are cut ("not reached (walk blocked)" downstream). The exact
    end rule is used: blocked iff the reachable distance D < r, i.e. the 1 px iteration count n < r (the 0x24 census cut at
    n < r - 1, P4 of the E19.d verification);
  * 0x1E is modelled WITHOUT the E4.d navigation detour (the original has none); the DLL detour is assessed apart
    (detour_check) on every DLL-blocked state;
  * MODE ORIG/FIX read the obstacle cloud (cloud.pkl, built from the DLL run by cloud.py): resting positions of every record.
Read-only on the repository.
"""
import collections
import math
import os
import pickle
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import census_exec_orig as X  # noqa: E402
from census_exec_orig import (Map, SZ, s16, HERO, UNKNOWN, CAP, CARDINAL, OFFX, OFFY, SPRITES, HERO_ID, succ, reach, rec_pos,  # noqa: E402
                              rec_dir, header, anim_speed, mask_for, getA, setA, targets, SLOTS)
import geo  # noqa: E402

_orig_targets = X.targets


def targets(sel, logic, actors):
    return [t for t in _orig_targets(sel, logic, actors) if not (isinstance(t, tuple) and t[0] == "CF")]


X.targets = targets


def class_bits(ctx, actors, actor):
    for x in actors or ():
        if x[0] == ("CF", actor):
            return x[1]
    pid = ctx.pid(actor)
    h = header(pid) if pid else None
    return (hflags(h) & 0x9) if h else 0

sys.stdout.reconfigure(encoding="utf-8")
G = pickle.load(open(HERE + "/gstatic.pkl", "rb"))
MODE = os.environ.get("MODE", "DLL")
RULE = MODE
CLASSOPS = os.environ.get("CLASSOPS", "0") == "1"
JUMP_PASS = {(10, 2456), (10, 6394), (331, 2456), (331, 6394)} if (os.environ.get("JUMPS", "1" if MODE == "ORIG" else "0") == "1") else set()
OBST = os.environ.get("OBST", "max")   # ORIG/FIX obstacles: 'max' = the cloud (cloud.pkl); 'cert' = S1 + S2 (see walk_obstacles)
STORY = set(range(162, 190)) | {44, 10, 14, 15, 114, 115, 116, 117, 135, 136, 362}
WALKOPS = (0x0B, 0x1E)
CLOUD = {}
if MODE in ("ORIG", "FIX") and OBST == "max":
    CLOUD = pickle.load(open(os.environ.get("CLOUD", HERE + "/cloud.pkl"), "rb"))

ENTRY_SPEC = {
    (10, "B[20]"): [dict(kind="0x53", src=176, pc=600)],
    (176, "B[7]"): [dict(kind="0x53", src=179, pc=568)],
    (179, "B[3]"): [dict(kind="0x53", src=176, pc=533), dict(kind="0x53", src=44, pc=985)],
    (178, "B[1]"): [dict(kind="portal", src=176, idx=7, needs=[1655])],
    (135, "B[14]"): [dict(kind="0x53", src=10, pc=2463)],
    (10, "B[8]"): [dict(kind="contact", rec=34, flag=300)],
}
CARD = CARDINAL


def hflags(h):
    return h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)


def anim_flag80(pid, anim):
    h = header(pid)
    if h is None or anim is None:
        return False
    for a in h["AnimSets"]:
        if a["Anim"] == anim:
            return bool(a["Acceleration"] & 0x80)
    return False


def tile_pos(tx, ty):
    return (tx * 24 + 12, ty * 16 + 8)


def is_T(f):
    return bool(f & 0x8000)


def flagname(f):
    return ("T%d" % (f & 0x7FFF)) if f & 0x8000 else ("G%d" % f)


class Cells(X.Cells):
    def cell_of(self, px, py):
        i = self.idx(px, py)
        return (i % self.W, i // self.W)


class BPin(tuple):
    pass


class Root:
    def __init__(self, kind, slot, owner, start, zone, label, dll_only=False):
        self.kind, self.slot, self.owner, self.start, self.zone, self.label, self.dll_only = kind, slot, owner, start, zone, label, dll_only
        self.entry = label.split()[-1]


class MCtx(X.Ctx):
    def __init__(self, mp):
        super().__init__(mp)
        self.cells = Cells(mp) if mp.cells else None
        self.load_spawned = set()
        for i, r in self.rec.items():
            if int(r.get("IsEnabled", 1)) != 0 and (int(r.get("SpriteDirection", 0)) & 0x40):
                self.load_spawned.add(i)
        self.mid = mp.mid
        self.obstacles = []
        self.static_obstacles = []
        self.scriptc = set()
        if MODE in ("ORIG", "FIX") and OBST == "max":
            cl = CLOUD.get(mp.mid, {})
            for (key, x, y, src) in cl.get("points", []):
                pid = HERO_ID if key == HERO else self.rec.get(key[1], {}).get("PrefabAssetId")
                h = header(pid) if pid else None
                if h is None or self.cells is None:
                    continue
                mv = mover_for(self, key, 0)
                z = geo.ground_max(self.cells, mv, x, y)
                self.obstacles.append(geo.Obstacle(key, x, y, z, h, src))

    def init_actor(self, actor):
        if actor == HERO:
            return (None, None, None, False)
        if isinstance(actor, tuple) and actor[0] == "R" and actor[1] in self.rec:
            r = self.rec[actor[1]]
            return (0, rec_dir(r), rec_pos(r) if actor[1] in self.load_spawned else None, False)
        return (None, None, None, False)


_mover_cache = {}


def mover_for(ctx, actor, anim, cbits=None):
    pid = ctx.pid(actor)
    key = (pid, actor == HERO, anim, cbits)
    mv = _mover_cache.get(key)
    if mv is None:
        h = header(pid)
        f = hflags(h)
        mask = mask_for(pid)
        if cbits is not None:
            mask = 0x40 | (0x01 if cbits & 8 else 0) | (0x1000 if cbits & 1 else 0)
        mv = geo.Mover(actor, h, mask, bool(f & 0x100), bool(f & 0x2000), bool(f & 0x80), anim_flag80(pid, anim))
        _mover_cache[key] = mv
    if mv.key != actor:
        mv2 = geo.Mover.__new__(geo.Mover)
        mv2.__dict__.update(mv.__dict__)
        mv2.key = actor
        return mv2
    return mv


_walk_cache = {}
PROV = {}          # (mid, actor, endpin) -> (pc, op, frompin, kind, reason, obstacle (key, x, y, src) or None)
CURPC = [None, None]


def rec_z(ctx, rid, pin, h):
    """Z of a record standing at pin: its record height (Height * 8, the spawn Z of 0x2D) raised to the ground; with Gravity
    it rests on the ground. A gravity-less record floating more than 2 px above the ground is a platform a mover may stand
    on (map 389 rec2 under the sailor rec11): None, i.e. not a certain obstacle."""
    g = geo.ground_max(ctx.cells, mover_for(ctx, ("R", rid), 0), pin[0], pin[1])
    rz = int(ctx.rec[rid].get("Height", 0)) * 8
    if hflags(h) & 0x100:
        return g
    if rz > g + 2:
        return None
    return max(g, rz)


def walk_obstacles(ctx, actor, actors):
    """MODE ORIG/FIX obstacles. OBST=max: the cloud. OBST=cert: the obstacles whose presence at the time of the walk is
    certain by structure: S2 = load-spawned records with no C program at all, Collidable, never moved, destroyed, decollided or
    deactivated by a reachable op of the map (ctx.static_obstacles); S1 = records spawned earlier in the SAME program
    (0x2D / 0x8A / 0x8B, carried in the walk state) that have no script C program, while not destroyed / decollided by
    that program (step_state2 drops their pin)."""
    if MODE not in ("ORIG", "FIX"):
        return []
    if OBST == "max":
        return [o for o in ctx.obstacles if o.key != actor]
    out = [o for o in ctx.static_obstacles if o.key != actor]
    for (key, anim, d, pin, mv) in actors or ():
        if key == actor or pin is None or not (isinstance(key, tuple) and key[0] == "R") or key[1] not in ctx.rec:
            continue
        if key in ctx.scriptc:
            continue
        pid = ctx.rec[key[1]].get("PrefabAssetId")
        h = header(pid)
        if h is None or not (hflags(h) & 0x80) or anim_flag80(pid, anim if anim is not None else 0):
            continue
        z = rec_z(ctx, key[1], pin, h)
        if z is None:
            continue
        out.append(geo.Obstacle(key, float(pin[0]), float(pin[1]), z, h, "same program"))
    return out


def walk_actor(ctx, actor, anim, pin, d, radius=None, max_iters=None, actors=None):
    obs = walk_obstacles(ctx, actor, actors) if ctx.cells is not None else []
    okey = tuple(sorted((repr(o.key), o.x, o.y) for o in obs)) if (OBST == "cert" and obs) else None
    cb = class_bits(ctx, actors, actor) if CLASSOPS else None
    key = (ctx.mid, actor, anim, tuple(pin), d, radius, max_iters, RULE, okey, cb)
    r = _walk_cache.get(key)
    if r is not None:
        return r
    mv = mover_for(ctx, actor, anim, cb)
    r = geo.walk(ctx.cells, mv, obs, RULE, pin[0], pin[1], d, OFFX[d], OFFY[d], radius=radius, max_iters=max_iters)
    _walk_cache[key] = r
    return r


def obst_desc(o):
    if o is None:
        return None
    return (o.key, round(o.x, 2), round(o.y, 2), o.src)


def travel_actor(ctx, actors, a, iters=None, ticks=None):
    """replacement of census_exec.travel_actor: iters = a walk radius (0x0B/0x1E/0x1F), ticks = a timed walk, neither = 0x24."""
    anim, d, pin, mv = getA(actors, a, ctx)
    pid = ctx.pid(a)
    hdr = header(pid) if pid else None
    if pin is None or d is None or hdr is None or ctx.cells is None:
        return actors
    sp = anim_speed(pid, anim)
    if not sp:
        return actors
    m = max(abs(OFFX[d]), abs(OFFY[d]))
    if m == 0:
        return actors
    if ticks is not None:
        r = walk_actor(ctx, a, anim, pin, d, max_iters=ticks * sp * m / 65536.0, actors=actors)
    elif iters is not None:
        r = walk_actor(ctx, a, anim, pin, d, radius=iters, actors=actors)
    else:
        r = walk_actor(ctx, a, anim, pin, d, actors=actors)
    if r["kind"] == "left":
        return setA(actors, a, (anim, d, None, mv))
    npin = (round(r["x"], 3), round(r["y"], 3))
    if npin != tuple(pin):
        k = (ctx.mid, a, npin)
        if k not in PROV:
            PROV[k] = (CURPC[0], CURPC[1], tuple(pin), r["kind"], r["reason"], obst_desc(r["obst"]))
    return setA(actors, a, (anim, d, npin, mv))


X.travel_actor = travel_actor


# -------------------------------------------------------------------------------------------------------------- map data
class MapData:
    def __init__(self, mid):
        self.mid = mid
        self.mp = Map(mid)
        self.C = self.mp.C
        self.ctx = MCtx(self.mp)
        self.boundary = sum(2 * len(t) for t in self.mp.T.values())
        self.sites = []
        pc = self.boundary
        while pc < len(self.C):
            op = self.C[pc]
            if op in WALKOPS:
                self.sites.append(pc)
            pc += SZ.get(op) or 1
        self.roots, self.fallback = [], []
        mp = self.mp
        for e in mp.mapevents:
            idx = int(e["EventCodesBIndex"])
            t = mp.T["B"]
            if (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
                zone = (int(e["X1"]), int(e["Y1"]), int(e["X2"]), int(e["Y2"]))
                self.roots.append(Root("map", "B", HERO, t[idx & 0x7F], zone, "mapevent%s B[%d]" % (e["Index"], idx & 0x7F)))
        for r in mp.records:
            for slot, key in SLOTS:
                idx = int(r.get(key, 0))
                t = mp.T[slot]
                if (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
                    rt = Root("rec", slot, ("R", int(r["Index"])), t[idx & 0x7F], None, "rec%s %s[%d]" % (r["Index"], slot, idx & 0x7F),
                              dll_only=not (idx & 0x80))
                    (self.roots if idx & 0x80 else self.fallback).append(rt)
        self._calls = {}
        self.reach = {}
        for rt in self.roots + self.fallback:
            self.reach[rt.label] = reach(self.C, rt.start)
        union = set()
        for rt in self.roots + self.fallback:
            union |= self.reach[rt.label]
        self.union = union
        if MODE in ("ORIG", "FIX") and OBST == "cert":
            self.build_static(union)
        self.setters = collections.defaultdict(list)
        self.act2d = set()
        self.lit_writes = collections.defaultdict(set)
        pc = self.boundary
        while pc < len(self.C):
            op = self.C[pc]
            sz = SZ.get(op) or 1
            v = self.C[pc:pc + 10] + [0] * 10
            if pc in union:
                if op in (0x05, 0x32):
                    self.setters[v[1] | (v[2] << 8)].append(pc)
                elif op == 0x2D:
                    self.act2d.add(v[1])
                elif op == 0x8A:
                    self.lit_writes[v[1]].add((v[2] | (v[3] << 8), v[4] | (v[5] << 8)))
                elif op == 0x64 and v[1] < 0x80:
                    self.lit_writes[v[1]].add((v[2] | (v[3] << 8), v[4] | (v[5] << 8)))
            pc += sz

    def build_static(self, union):
        ctx = self.ctx
        ctx.scriptc = {rt.owner for rt in self.roots + self.fallback if rt.slot == "C"}
        touched = set()
        pc = self.boundary
        while pc < len(self.C):
            op = self.C[pc]
            sz = SZ.get(op) or 1
            v = self.C[pc:pc + 10] + [0] * 10
            if pc in union:
                if op in (0x59, 0x5A, 0x5B, 0x64, 0x65, 0x2E, 0x43, 0x2D, 0x8A, 0x62) and v[1] < 0x80:
                    touched.add(v[1])
                elif op == 0x63 and v[1] < 0x80 and ((v[2] | (v[3] << 8)) & 0x80):
                    touched.add(v[1])
                elif op in (0x89, 0x8B) and v[2] < 0x80:
                    touched.add(v[2])
            pc += sz
        self.static_touched = touched
        for i, r in ctx.rec.items():
            if i not in ctx.load_spawned or i in touched:
                continue
            if int(r.get("EventCodesC_TickIndex", 0)) & 0x7F:
                continue                       # a C program (script or native AI) may move it
            own = [rt for rt in self.roots if rt.owner == ("R", i)]
            bad = False
            for rt in own:
                for q in self.reach[rt.label]:
                    o2 = self.C[q]
                    w = self.C[q:q + 4] + [0] * 4
                    if o2 in (0x2E, 0x19, 0x5B, 0x59, 0x1A, 0x64, 0x65, 0x0B, 0x1E, 0x1F) or (o2 == 0x63 and ((w[2] | (w[3] << 8)) & 0x80)):
                        bad = True
            if bad:
                continue
            pid = r.get("PrefabAssetId")
            h = header(pid)
            if h is None or not (hflags(h) & 0x80) or anim_flag80(pid, 0) or ctx.cells is None:
                continue
            p = rec_pos(r)
            z = rec_z(ctx, i, p, h)
            if z is None:
                continue
            ctx.static_obstacles.append(geo.Obstacle(("R", i), float(p[0]), float(p[1]), z, h, "load static"))

    def calls_of(self, start):
        if start in self._calls:
            return self._calls[start]
        c = set()
        while True:
            before = len(c)
            seen = set()
            work = [start]
            while work:
                pc = work.pop()
                if pc in seen or pc < 0 or pc >= len(self.C):
                    continue
                seen.add(pc)
                work.extend(succ(self.C, pc, start, c))
            if len(c) == before:
                break
        self._calls[start] = c
        return c

    def reach_from(self, pc, start):
        return reach_from(self.C, pc, start, self.calls_of(start))


def reach_from(C, pc0, start, calls):
    seen = set()
    work = [pc0]
    while work:
        pc = work.pop()
        if pc in seen or pc < 0 or pc >= len(C):
            continue
        seen.add(pc)
        work.extend(succ(C, pc, start, calls))
    return seen


class Env:
    def __init__(self):
        self.setflags = {}
        self.setpins = collections.defaultdict(set)
        self.writes = collections.defaultdict(set)

    def grow(self, other):
        ch = False
        for f, k in other.setflags.items():
            if f not in self.setflags or (k and not self.setflags[f]):
                self.setflags[f] = k or self.setflags.get(f, 0)
                ch = True
        for r, s in other.writes.items():
            if not s <= self.writes[r]:
                self.writes[r] |= s
                ch = True
        for f, s in other.setpins.items():
            if not s <= self.setpins[f]:
                self.setpins[f] |= s
                ch = True
        return ch


def hero_free(md, pos):
    hdr = header(HERO_ID)
    m = mask_for(HERO_ID)
    cells = md.ctx.cells
    ox, oy, sw, sh = hdr["OffsetX"], hdr["OffsetY"], max(hdr["SizeX"], 1), max(hdr["SizeY"], 1)
    x1, y1 = pos[0] + ox, pos[1] + oy
    pts = [(x1, y1), (x1 + sw - 1, y1), (x1, y1 + sh - 1), (x1 + sw - 1, y1 + sh - 1)]
    return all(0 <= px < cells.W * 24 and 0 <= py < cells.H * 16 and not cells.blocked(px, py, m) for px, py in pts)


def box_samples(md, x1, y1, x2, y2, limit=60):
    pos = []
    for stride in (4, 6, 8, 12, 24):
        pos = []
        for ty in range(y1, y2 + 1):
            for tx in range(x1, x2 + 1):
                for px in range(tx * 24, tx * 24 + 24, stride):
                    for py in range(ty * 16, ty * 16 + 16, stride):
                        if hero_free(md, (px, py)):
                            pos.append((px, py))
        if len(pos) <= limit:
            break
    return pos


def contact_samples(md, rec):
    r = md.ctx.rec[rec]
    rp = rec_pos(r)
    rh = header(r.get("PrefabAssetId"))
    hh = header(HERO_ID)
    rx1, ry1 = rp[0] + rh["OffsetX"], rp[1] + rh["OffsetY"]
    rx2, ry2 = rx1 + max(rh["SizeX"], 1) - 1, ry1 + max(rh["SizeY"], 1) - 1
    hw, hhh = max(hh["SizeX"], 1), max(hh["SizeY"], 1)
    out = set()
    for py in range(ry1 - hhh - hh["OffsetY"], ry2 + 1 - hh["OffsetY"] + 1):
        out.add((rx1 - hw - hh["OffsetX"], py))
        out.add((rx2 + 1 - hh["OffsetX"], py))
    for px in range(rx1 - hw - hh["OffsetX"], rx2 + 2 - hh["OffsetX"]):
        out.add((px, ry1 - hhh - hh["OffsetY"]))
        out.add((px, ry2 + 1 - hh["OffsetY"]))
    return sorted(p for p in out if hero_free(md, p))


def portal_arrivals(srcmap, idx):
    p = [q for q in G["portals"] if q[0] == srcmap and q[1] == idx][0]
    _, _, x1, y1, x2, y2, dest, dx, dy, z, flags = p
    d = CARD[(flags & 0x3000) >> 12]
    out = []
    for ty in range(y1, y2 + 1):
        for tx in range(x1, x2 + 1):
            out.append((tile_pos(dx + tx - x1, dy + ty - y1), d, None, dest))
    return out


def auto_arrivals(mid):
    out = []
    for q in G["portals"]:
        if q[6] == mid:
            _, i, x1, y1, x2, y2, dest, dx, dy, z, flags = q
            d = CARD[(flags & 0x3000) >> 12]
            for ty in range(y1, y2 + 1):
                for tx in range(x1, x2 + 1):
                    out.append((tile_pos(dx + tx - x1, dy + ty - y1), d, None, "portal %d.%d" % (q[0], i)))
    for (src, pc, dest, tx, ty, tz) in G["ch53"]:
        if dest == mid:
            out.append((tile_pos(tx, ty), None, None, "0x53 %d@%d" % (src, pc)))
    return out


RES = {}
PREV = {}
STACK = []


class WalkResult:
    def __init__(self):
        self.states = collections.defaultdict(set)
        self.cuts = []
        self.widened = set()
        self.entry_cut = None
        self.entries = []


class MapResult:
    pass


def hero_initial(md, rt, env):
    mid = md.mid
    spec = ENTRY_SPEC.get((mid, rt.entry))
    if spec:
        starts, cuts = [], []
        for s in spec:
            for f in s.get("needs", []):
                ok, why = flag_reached(f, mid)
                if not ok:
                    cuts.append(("flag", "%s (%s)" % (flagname(f), why)))
                    break
            else:
                if s["kind"] == "0x53":
                    src = analyze_map(s["src"])
                    if src is None:
                        for (m_, pc_, dest_, tx_, ty_, tz_) in G["ch53"]:
                            if m_ == s["src"] and pc_ == s["pc"]:
                                starts.append((tile_pos(tx_, ty_), None, None, "0x53 %d@%d (static tile, cycle)" % (s["src"], s["pc"])))
                        continue
                    sts = [(st, rr) for rr in src.walks.values() for st in rr.states.get(s["pc"], ())]
                    if not sts:
                        cuts.append(explain_unreached(src, s["pc"], "entry 0x53 %d@%d" % (s["src"], s["pc"])))
                        continue
                    sm = src.md
                    v = sm.C[s["pc"]:s["pc"] + 8]
                    pin = tile_pos(v[3], v[4])
                    seen = set()
                    for st, _ in sts:
                        a = getA(st[1], HERO, sm.ctx)
                        key = (a[0], a[1])
                        if key in seen:
                            continue
                        seen.add(key)
                        starts.append((pin, a[1], a[0], "0x53 %d@%d" % (s["src"], s["pc"])))
                elif s["kind"] == "portal":
                    for pin, d, an, dest in portal_arrivals(s["src"], s["idx"]):
                        starts.append((pin, d, an, "portal %d.%d" % (s["src"], s["idx"])))
                elif s["kind"] == "contact":
                    md.contact_at[0x8000 | s["flag"]] = contact_samples(md, s["rec"])
                    starts.append((None, None, None, "contact rec%d at T%d" % (s["rec"], s["flag"])))
        if starts:
            return starts, None
        return [], (cuts[0] if cuts else ("entry", "no entry"))
    x1, y1, x2, y2 = rt.zone
    arr = [a for a in auto_arrivals(mid) if x1 <= a[0][0] // 24 <= x2 and y1 <= a[0][1] // 16 <= y2]
    if arr:
        return [(p, d, an, why) for p, d, an, why in arr], None
    return [(None, None, None, "no arrival in the zone")], None


def initial_states(md, env, rt):
    ctx = md.ctx
    if rt.kind == "map":
        starts, cut = hero_initial(md, rt, env)
        sts = []
        for pin, d, an, why in starts:
            actors = setA((), HERO, (an, d, pin, False))
            sts.append((HERO, actors, 0))
        return sts, cut, [w for _, _, _, w in starts]
    rid = rt.owner[1]
    cands = set()
    if rid in ctx.load_spawned:
        cands.add(rec_pos(ctx.rec[rid]))
    cands |= md.lit_writes.get(rid, set())
    cands |= env.writes.get(rid, set())
    if not cands and rid in md.act2d:
        cands.add(rec_pos(ctx.rec[rid]))
    r = ctx.rec[rid]
    sts = []
    if not cands:
        sts.append((rt.owner, setA((), rt.owner, (0, rec_dir(r), None, False)), 0))
        return sts, None, ["no position source"]
    for p in sorted(cands):
        sts.append((rt.owner, setA((), rt.owner, (0, rec_dir(r), p, False)), 0))
    return sts, None, ["start %s" % (sorted(cands),)]


def walk_params(ctx, op, v, st):
    """-> (anim, d, pin, radius, pid) of the walk of a 0x0B / 0x1E executed in state st, or None fields."""
    logic, actors, lock = st
    anim, d, pin, mv = getA(actors, logic, ctx)
    if op == 0x0B:
        anim = v[1]
        rad = v[2] | (v[3] << 8)
    else:
        rad = v[1] | (v[2] << 8)
    return anim, d, pin, rad, ctx.pid(logic)


def step_state2(md, env, new, op, v, st, pc, sz):
    ctx = md.ctx
    logic, actors, lock = st
    if CLASSOPS and op in (0x28, 0x29, 0x2A, 0x2B) and (logic == HERO or (isinstance(logic, tuple) and logic[0] == "R")):
        b = class_bits(ctx, actors, logic)
        b = {0x28: b | 8, 0x29: b & ~8, 0x2A: b | 1, 0x2B: b & ~1}[op]
        actors = setA(actors, ("CF", logic), (b, None, None, False))
        return [(logic, actors, lock)], None
    if MODE in ("ORIG", "FIX") and OBST == "cert" and (op == 0x2E or op == 0x19 or (op == 0x63 and ((v[2] | (v[3] << 8)) & 0x80))):
        tg = [logic] if op == 0x19 else targets(v[1], logic, actors)
        for t in tg:
            if isinstance(t, tuple) and t[0] == "R" and t != logic and any(x[0] == t for x in actors):
                an_, d_, pin_, mv_ = getA(actors, t, ctx)
                actors = setA(actors, t, (an_, d_, None, mv_))
        st = (logic, actors, lock)
    if (md.mid, pc) in JUMP_PASS and op == 0x0B:
        anim, d, pin, rad, pid = walk_params(ctx, op, v, st)
        if pin is not None and d is not None:
            m = max(abs(OFFX[d]), abs(OFFY[d]))
            npin = (round(pin[0] + OFFX[d] / m * rad, 3), round(pin[1] + OFFY[d] / m * rad, 3))
            a_, d_, p_, mv_ = getA(actors, logic, ctx)
            return [(logic, setA(actors, logic, (v[1], d, npin, mv_)), lock)], None
    if op in WALKOPS and ctx.cells is not None:
        anim, d, pin, rad, pid = walk_params(ctx, op, v, st)
        if pin is not None and d is not None and pid and header(pid) is not None and anim is not None:
            sp = anim_speed(pid, anim)
            if sp:
                r = walk_actor(ctx, logic, anim, pin, d, radius=rad, actors=actors)
                if r["kind"] == "blocked":
                    return [], ("walk", "%s @%d blocked after %d px of %d (%s %s%s)" % (
                        "0x0B" if op == 0x0B else "0x1E", pc, r["n"], rad, r["reason"], r["cell"] or "",
                        (" by %s" % (obst_desc(r["obst"]),)) if r["obst"] else ""))
    if op == 0x64 or op == 0x8A:
        px, py = v[2] | (v[3] << 8), v[4] | (v[5] << 8)
        if op == 0x8A:
            new.writes[v[1]].add((px, py))
        else:
            for t in targets(v[1], logic, actors):
                if isinstance(t, tuple) and t[0] == "R":
                    new.writes[t[1]].add((px, py))
    if op in (0x05, 0x32):
        f = v[1] | (v[2] << 8)
        new.setflags[f] = new.setflags.get(f, 0) or lock
        if is_T(f):
            hp = getA(actors, HERO, ctx)[2]
            if isinstance(hp, BPin):
                new.setpins[f].add(tuple(hp))
    if op == 0x3B:
        x1, x2, y1, y2 = v[1], v[2], v[3], v[4]
        key = (x1, y1, x2, y2)
        if key not in md.box_cache:
            md.box_cache[key] = box_samples(md, x1, y1, x2, y2)
        outs = []
        for p in md.box_cache[key]:
            anim, d, pin, mv = getA(actors, HERO, ctx)
            outs.append((logic, setA(actors, HERO, (anim, d, BPin(p), False)), lock))
        return (outs or [(logic, actors, lock)]), None
    if op == 0x2D and v[1] in ctx.rec:
        t = ("R", v[1])
        anim, d, pin, mv = getA(actors, t, ctx)
        if pin is None:
            r = ctx.rec[v[1]]
            return [(logic, setA(actors, t, (anim if anim is not None else 0, d if d is not None else rec_dir(r), rec_pos(r), False)), lock)], None
        return [(logic, actors, lock)], None
    outs = X.step_state(ctx, op, v, st, pc)
    return refine_lost(ctx, actors, outs), None


def refine_lost(ctx, a_in, outs):
    res = []
    for (l, a, kk) in outs:
        lost = []
        for x in a:
            if x[3] is None:
                prev = getA(a_in, x[0], ctx)
                if prev[2] is not None and prev[3]:
                    lost.append((x[0], prev))
        if not lost:
            res.append((l, a, kk))
            continue
        variants = [a]
        for actor, prev in lost:
            anim_p, d_p, pin_p, mv = prev
            pid = ctx.pid(actor)
            hdr = header(pid) if pid else None
            alts = [pin_p]
            if hdr and d_p is not None and ctx.cells is not None and anim_speed(pid, anim_p):
                rr = walk_actor(ctx, actor, anim_p, pin_p, d_p, actors=a_in)
                if rr["kind"] == "blocked":
                    alts.append((round(rr["x"], 3), round(rr["y"], 3)))
                else:
                    alts = [None]
            else:
                alts = [None] if not (hdr and d_p is not None) else [pin_p]
            nv = []
            for va in variants:
                cur = getA(va, actor, ctx)
                for p in alts:
                    nv.append(setA(va, actor, (cur[0], cur[1], p, False)))
            variants = nv
        for va in variants:
            res.append((l, va, kk))
    return res


def run_program(md, env, new, rt, starts, entry_cut):
    wr = WalkResult()
    wr.entry_cut = entry_cut
    if not starts:
        return wr
    C = md.C
    start = rt.start
    calls = md.calls_of(start)
    work = [(start, s) for s in starts]
    while work:
        pc, st = work.pop()
        if pc < 0 or pc >= len(C) or st in wr.states[pc]:
            continue
        if len(wr.states[pc]) >= CAP:
            wr.widened.add(pc)
            continue
        wr.states[pc].add(st)
        op = C[pc]
        sz = SZ.get(op) or 1
        v = C[pc:pc + 10] + [0] * 10
        logic, actors, lock = st
        nxt = succ(C, pc, start, calls)
        nxt0 = list(nxt)
        f = v[1] | (v[2] << 8)
        if op == 0x36 and flag_dead(md, env, f):
            wr.cuts.append((pc + sz, "flag", flagname(f)))
            continue
        if op == 0x36 and f in env.setflags and env.setflags[f]:
            st = (logic, actors, 1)
        if op in (0x30, 0x31, 0x7B, 0x7C) and flag_dead(md, env, f):
            if op in (0x30, 0x7B):
                wr.cuts.append((nxt[0], "flag", flagname(f)))
                nxt = nxt[1:]
            else:
                wr.cuts.append((nxt[1], "flag", flagname(f)))
                nxt = nxt[:1]
        CURPC[0], CURPC[1] = pc, op
        outs, cut = step_state2(md, env, new, op, v, st, pc, sz)
        if cut:
            wr.cuts.append((pc + sz, cut[0], cut[1]))
        on_succ = None
        carried = None
        if op in (0x30, 0x7B, 0x36):
            on_succ = nxt0[0]
        elif op in (0x31, 0x7C):
            on_succ = nxt0[1]
        inherit = 0
        if on_succ is not None:
            inherit = env.setflags.get(f, 0)
            if f in md.contact_at:
                carried = md.contact_at[f]
                inherit = 1
            elif is_T(f) and env.setpins.get(f):
                carried = sorted(env.setpins[f])
        for ns in outs:
            for n in nxt:
                if n == on_succ and (carried or inherit):
                    l_, a_, k_ = ns
                    k2 = 1 if (k_ or inherit) else 0
                    if carried:
                        an_, d_, pin_, mv_ = getA(a_, HERO, md.ctx)
                        for p in carried:
                            work.append((n, (l_, setA(a_, HERO, (an_, d_, p, False)), k2)))
                    else:
                        work.append((n, (l_, a_, k2)))
                else:
                    work.append((n, ns))
    return wr


FLAGDEAD = {}
CYCLE = False


def flag_dead(md, env, f):
    if is_T(f):
        return f in md.setters and f not in env.setflags
    key = f
    if key not in FLAGDEAD:
        if not G["setters"].get(f):
            FLAGDEAD[key] = False
        else:
            global CYCLE
            CYCLE = False
            ok, why = flag_reached(f, md.mid)
            if CYCLE:
                return False
            FLAGDEAD[key] = not ok
    return FLAGDEAD[key]


def analyze_map(mid):
    if mid in RES:
        return RES[mid]
    if mid in STACK:
        global CYCLE
        if mid in PREV:
            return PREV[mid]
        CYCLE = True
        return None
    STACK.append(mid)
    try:
        md = MapData(mid)
        md.box_cache = {}
        md.contact_at = {}
        env = Env()
        walks = {}
        for it in range(12):
            new = Env()
            walks = {}
            for rt in md.roots:
                sts, cut, why = initial_states(md, env, rt)
                wr = run_program(md, env, new, rt, sts, cut)
                wr.entries = why
                walks[rt.label] = wr
            if not env.grow(new):
                break
        union = set()
        for rt in md.roots:
            union |= md.reach[rt.label]
        for rt in md.fallback:
            if any(s in md.reach[rt.label] and s not in union for s in md.sites):
                sts, cut, why = initial_states(md, env, rt)
                wr = run_program(md, env, Env(), rt, sts, cut)
                wr.entries = why
                walks[rt.label] = wr
        res = MapResult()
        res.md, res.env, res.walks, res.iterations = md, env, walks, it + 1
        RES[mid] = res
        return res
    finally:
        STACK.pop()


def explain_unreached(res, pc, what):
    md = res.md
    causes = []
    for rt in md.roots + md.fallback:
        if rt.label not in res.walks or pc not in md.reach[rt.label]:
            continue
        wr = res.walks[rt.label]
        if wr.entry_cut:
            causes.append(wr.entry_cut)
            continue
        for (spc, kind, det) in wr.cuts:
            if pc in md.reach_from(spc, rt.start):
                causes.append((kind, det))
    if not causes:
        return ("indeterminate", "no state reaches @%d and no cut explains it (%s)" % (pc, what))
    for k in ("walk", "flag", "entry"):
        for c in causes:
            if c[0] == k:
                return c
    return causes[0]


def flag_reached(f, asking_map):
    global CYCLE
    setters = G["setters"].get(f)
    if not setters:
        return True, "no setter in the corpus (set outside the scripts)"
    notes = []
    live = 0
    for (m, pc) in setters:
        res = analyze_map(m)
        if res is None:
            CYCLE = True
            return True, "cycle"
        rts = [rt for rt in res.md.roots + res.md.fallback if pc in res.md.reach[rt.label]]
        if not rts:
            continue
        live += 1
        if any(res.walks.get(rt.label) and res.walks[rt.label].states.get(pc) for rt in rts):
            return True, "set at %d @%d" % (m, pc)
        notes.append("%d @%d: %s" % (m, pc, explain_unreached(res, pc, "setter")[1]))
    if not live:
        return True, "no live setter in the corpus (only dormant code sets it)"
    return False, "no reached setter: " + "; ".join(notes)


# -------------------------------------------------------------------------------------------------------------- classification
def classify_walk(md, st, op, v, pc):
    ctx = md.ctx
    logic, actors, lock = st
    if logic in (UNKNOWN, None) or not (logic == HERO or (isinstance(logic, tuple) and logic[0] == "R" and logic[1] in ctx.rec)):
        return "indet", dict(actor=ctx.name(logic), why="logic entity not deducible", lock=lock)
    anim, d, pin, rad, pid = walk_params(ctx, op, v, st)
    hdr = header(pid) if pid else None
    det = dict(actor=ctx.name(logic), key=logic, anim=anim, dir=d, pin=tuple(pin) if pin is not None else None, rad=rad, lock=lock)
    if hdr is None:
        return "indet", dict(det, why="no sprite header")
    if anim is None:
        return "indet", dict(det, why="animation not deducible")
    sp = anim_speed(pid, anim)
    det["speed"] = sp
    if not sp:
        return "zero", det
    if d is None:
        return "indet", dict(det, why="direction not deducible")
    if ctx.cells is None:
        return "indet", dict(det, why="map without cells")
    if pin is None:
        return "indet", dict(det, why="position not deducible")
    if (md.mid, pc) in JUMP_PASS:
        det.update(kind="done", n=rad, reason="jump (certified)", cell=None, obst=None, slid=0, end=None)
        return "pass", det
    r = walk_actor(ctx, logic, anim, tuple(pin), d, radius=rad, actors=actors)
    det.update(kind=r["kind"], n=r["n"], reason=r["reason"], cell=r["cell"], obst=obst_desc(r["obst"]), slid=r["slid"],
               end=(round(r["x"], 3), round(r["y"], 3)))
    if r["kind"] == "blocked":
        return "blocked", det
    return "pass", det


def site_row(res, s):
    md = res.md
    mid = md.mid
    op = md.C[s]
    v = md.C[s:s + 10] + [0] * 10
    rr = [rt for rt in md.roots if s in md.reach[rt.label]]
    ff = [rt for rt in md.fallback if s in md.reach[rt.label]]
    rc = "reachable" if rr else ("DLL only" if ff else "dormant")
    row = dict(map=mid, name=md.mp.d["name"], zone=md.mp.d["zone"], pc=s, op=op, reach=rc, roots=[r.label for r in (rr or ff)],
               story=mid in STORY, v=v[:4])
    if rc == "dormant":
        return row
    cands = []
    for rt in (rr or ff):
        wr = res.walks.get(rt.label)
        for st in (wr.states.get(s, ()) if wr else ()):
            cls, det = classify_walk(md, st, op, v, s)
            cands.append((cls, dict(det, root=rt.label, slot=rt.slot)))
    if cands:
        row["reached"] = True
        row["all"] = cands
        row["lock"] = any(det.get("lock") for _, det in cands)
        row["classes_seen"] = sorted({c for c, _ in cands})
    else:
        k, why = explain_unreached(res, s, "site")
        row["reached"] = False
        row["unreached"] = (k, why)
        row["all"] = []
        row["lock"] = False
        row["classes_seen"] = []
    return row


def all_maps_with_sites():
    out = []
    for mid in sorted(G["names"]):
        mp = Map(mid)
        C = mp.C
        pc = sum(2 * len(t) for t in mp.T.values())
        has = False
        while pc < len(C):
            if C[pc] in WALKOPS:
                has = True
                break
            pc += SZ.get(C[pc]) or 1
        if has:
            out.append(mid)
    return out


def run_pass(maps):
    import time
    rows = []
    t0 = time.time()
    for mid in maps:
        res = analyze_map(mid)
        for s in res.md.sites:
            rows.append(site_row(res, s))
        print("  map %d done (%d sites, %d iterations) %.0fs" % (mid, len(res.md.sites), res.iterations, time.time() - t0), flush=True)
    return rows


def signature(rows):
    return sorted((r["map"], r["pc"], tuple(r.get("classes_seen", ())), r.get("reached"), r.get("unreached", (None,))[0]) for r in rows)


def main(only=None, passes=4):
    global RES, PREV, FLAGDEAD
    maps = [m for m in all_maps_with_sites() if not only or m in only]
    prev_sig = None
    rows = []
    for p in range(passes):
        print("pass", p + 1, MODE, flush=True)
        RES = {}
        FLAGDEAD = {}
        rows = run_pass(maps)
        sig = signature(rows)
        if prev_sig is not None:
            a = {(x[0], x[1]): x[2:] for x in prev_sig}
            b = {(x[0], x[1]): x[2:] for x in sig}
            diff = [(k, a.get(k), b[k]) for k in b if a.get(k) != b[k]]
            print("  differences with the previous pass:", len(diff), diff[:12], flush=True)
        PREV = dict(RES)
        if sig == prev_sig:
            print("stable after pass", p + 1)
            break
        prev_sig = sig
    return rows


if __name__ == "__main__":
    passes = int(os.environ.get("PASSES", "4"))
    only = set(int(a) for a in sys.argv[1:]) or None
    rows = main(only, passes)
    tag = os.environ.get("TAG", MODE)
    out = HERE + ("/rows_%s_partial.pkl" % tag if only else "/rows_%s.pkl" % tag)
    pickle.dump(rows, open(out, "wb"))
    # keep the RES walks needed by cloud.py (DLL) and the provenance
    keep = {}
    for mid, res in RES.items():
        keep[mid] = {lab: {pc: list(sts) for pc, sts in wr.states.items()} for lab, wr in res.walks.items()}
        keep[mid]["__owners__"] = {rt.label: rt.owner for rt in res.md.roots + res.md.fallback}
    pickle.dump(keep, open(HERE + ("/walks_%s%s.pkl" % (tag, "_partial" if only else "")), "wb"))
    pickle.dump(PROV, open(HERE + ("/prov_%s%s.pkl" % (tag, "_partial" if only else "")), "wb"))
    c = collections.Counter((r["reach"], tuple(r.get("classes_seen", ())) if r.get("reached") else ("U", r.get("unreached", ("?",))[0]))
                            for r in rows)
    for k, n in sorted(c.items(), key=lambda t: -t[1]):
        print(n, k)
