"""census_v2.py - E19.d D5 corrected census of the 0x24 (Wait force adjusted) sites (D-E19-24).

Corrections against the first model (census_exec.py):
  1. every actor starts where it really appears: a record that the map-load pass does not spawn (IsEnabled = 0 or SpriteDirection bit 0x40
     clear, binary gate 0x8003A268) is never started at its record position; its candidate positions are the 0x8A / 0x64 (literal or
     through the logic entity) written by ANY program of the map; a record only activated by 0x2D appears at its record position unless a
     position write exists;
  2. the hero starts from its real arrivals: the story-chain entry (0x53 operand or portal, see ENTRY_SPEC), otherwise every 0x53 / portal
     that lands inside the zone of the map event, plus the tile samples of a 0x3B box once the program passed it, plus the contact positions
     of an interaction; never every free cell of the zone;
  3. a 0x0B that the cell model blocks (the wall comes before the radius) is NOT clipped: the rest of that program is
     "non atteint (0x0B bloque)" (0x0B has no ForceAdjusted exit);
  4. wait / branch on a flag that no reached program sets: T flags within the map (fixpoint over all programs of the map), the G flags that an
     entry depends on across maps (corpus-wide setters, evaluated on demand): "non atteint (drapeau jamais pose)".
Population, reachability and wall rule are unchanged.
"""
import collections
import math
import os
import pickle
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
EX = os.path.dirname(os.path.abspath(__file__))  # archived: was a folder of the session scratchpad
sys.path.insert(0, EX)
import census_exec as X  # noqa: E402
from census_exec import (Map, SZ, s16, HERO, UNKNOWN, CAP, CARDINAL, OFFX, OFFY, SPRITES, HERO_ID, succ, reach, rec_pos, rec_dir, header,
                         anim_speed, mask_for, step_height, getA, setA, targets, apply_dir, resolve_dir, travel_actor, cast, Cells,
                         PRIO, SLOTS)  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")
G = pickle.load(open(os.path.dirname(os.path.abspath(__file__)) + "/gstatic.pkl", "rb"))
STORY = set(range(162, 183)) | {44, 10}

# -------------------------------------------------------------------------------------------------------------- entry specs
# The real story-chain entry of a hero program whose entry is fixed by a flag gate that a static walk cannot see (a tile that a map event
# makes unwalkable while a flag is off, a program gated by flags that the chain sets). Each entry cites its source; the value is read from the
# data (operands of the 0x53, portal rectangle), never typed. "needs" = G flags that must be set by a reached program of the corpus.
ENTRY_SPEC = {
    # 10 B[20] (mapevent19, gate G1654 on, G1655 off): the arrival of 176 @600 0x53 -> (10,58)
    (10, "B[20]"): [dict(kind="0x53", src=176, pc=600)],
    # 176 B[7] (gate G1654 on, G1655 off): the arrival of 179 @568 0x53 -> (5,33)
    (176, "B[7]"): [dict(kind="0x53", src=179, pc=568)],
    # 179 B[3] (gate G1653 on, G1654 off): G1653 is set by 176 B[6] @530 immediately followed by the 0x53 @533 (17,7) (and by 44 @985)
    (179, "B[3]"): [dict(kind="0x53", src=176, pc=533), dict(kind="0x53", src=44, pc=985)],
    # 178 B[1]: the only entry inside the zone is the portal 7 of 176 (2,22) -> (38,25); 176 B[8] @617 makes its source tile unwalkable while
    # G1655 is off
    (178, "B[1]"): [dict(kind="portal", src=176, idx=7, needs=[1655])],
    # 135 B[14] (G1655 setter @953): entered by the 0x53 @2463 of map 10 (the church door portal is made unwalkable by 10 B[1] while G1655 is off)
    (135, "B[14]"): [dict(kind="0x53", src=10, pc=2463)],
    # 10 B[8] @1047: T300 is set only by rec34 Septimus C[18] @3210 after T3 (his F[5] interaction): the hero touches him
    (10, "B[8]"): [dict(kind="contact", rec=34, flag=300)],
}

CARD = CARDINAL


def tile_pos(tx, ty):
    return (tx * 24 + 12, ty * 16 + 8)


def is_T(f):
    return bool(f & 0x8000)


def flagname(f):
    return ("T%d" % (f & 0x7FFF)) if f & 0x8000 else ("G%d" % f)


# -------------------------------------------------------------------------------------------------------------- map data
class BPin(tuple):
    """a hero position that a 0x3B box gave: the player stands there, so a flag raised afterwards carries it to the programs that wait for it."""


class Root:
    def __init__(self, kind, slot, owner, start, zone, label, dll_only=False):
        self.kind, self.slot, self.owner, self.start, self.zone, self.label, self.dll_only = kind, slot, owner, start, zone, label, dll_only
        self.entry = label.split()[-1]        # "B[8]" / "C[47]"


class MCtx(X.Ctx):
    def __init__(self, mp):
        super().__init__(mp)
        self.load_spawned = set()
        for i, r in self.rec.items():
            if int(r.get("IsEnabled", 1)) != 0 and (int(r.get("SpriteDirection", 0)) & 0x40):
                self.load_spawned.add(i)

    def init_actor(self, actor):
        if actor == HERO:
            return (None, None, None, False)
        if isinstance(actor, tuple) and actor[0] == "R" and actor[1] in self.rec:
            r = self.rec[actor[1]]
            return (0, rec_dir(r), rec_pos(r) if actor[1] in self.load_spawned else None, False)
        return (None, None, None, False)


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
            if op == 0x24:
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
        # static facts over the reachable instructions
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
    """Dynamic knowledge of a map, grown to a fixpoint: flags set by reached programs (with the hero lock at the setter), position writes."""

    def __init__(self):
        self.setflags = {}                     # flag -> lock (0/1, OR over setters)
        self.setpins = collections.defaultdict(set)   # T flag -> hero pins known at its setters
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


# -------------------------------------------------------------------------------------------------------------- hero starts
def hero_free(md, pos):
    hdr = header(HERO_ID)
    m = mask_for(HERO_ID)
    cells = md.ctx.cells
    ox, oy, sw, sh = hdr["OffsetX"], hdr["OffsetY"], max(hdr["SizeX"], 1), max(hdr["SizeY"], 1)
    x1, y1 = pos[0] + ox, pos[1] + oy
    pts = [(x1, y1), (x1 + sw - 1, y1), (x1, y1 + sh - 1), (x1 + sw - 1, y1 + sh - 1)]
    return all(0 <= px < cells.W * 24 and 0 <= py < cells.H * 16 and not cells.blocked(px, py, m) for px, py in pts)


def box_samples(md, x1, y1, x2, y2, limit=60):
    """free hero positions inside the tile box (inclusive), on a stride grid, at most ~limit."""
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
    # hero box (px + ox .. px + ox + hw - 1) touches the record box on one side (gap 0)
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
    """every portal and 0x53 of the corpus landing in map mid: (pin, dir, anim, description)."""
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


# -------------------------------------------------------------------------------------------------------------- analysis
RES = {}
PREV = {}
STACK = []


class WalkResult:
    def __init__(self):
        self.states = collections.defaultdict(set)
        self.cuts = []            # (successor pc, kind, detail)
        self.widened = set()
        self.entry_cut = None     # (kind, detail) when the entry itself is unreached
        self.entries = []         # descriptions of the starts


class MapResult:
    pass


def hero_initial(md, rt, env):
    """-> (list of (pin, dir, anim, why), entry_cut)."""
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
                        # mutual chain (176 <-> 179): the tile of the operands, direction and animation left to the program
                        for (m_, pc_, dest_, tx_, ty_, tz_) in G["ch53"]:
                            if m_ == s["src"] and pc_ == s["pc"]:
                                starts.append((tile_pos(tx_, ty_), None, None, "0x53 %d@%d (static tile, cycle)" % (s["src"], s["pc"])))
                        continue
                    sts = [(st, rr) for rr in src.walks.values() for st in rr.states.get(s["pc"], ())]
                    if not sts:
                        cuts.append(explain_unreached(src, s["pc"], "entry 0x53 %d@%d" % (s["src"], s["pc"])))
                        continue
                    sm = src.md
                    op = sm.C[s["pc"]]
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
                    # the hero touches the record when the flag that its interaction raises is awaited: the pin is set there
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
    """-> (list of states, entry_cut, descriptions)."""
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


def step_state2(md, env, new, op, v, st, pc, sz):
    """-> (list of successor states, cut or None). Mirrors census_exec.step_state, with the D-E19-24 corrections."""
    ctx = md.ctx
    logic, actors, lock = st
    if op == 0x0B and ctx.cells is not None:
        anim, d, pin, mv = getA(actors, logic, ctx)
        pid = ctx.pid(logic)
        rad = float(v[2] | (v[3] << 8))
        # the animation of the call is the one that walks
        if pin is not None and d is not None and pid and header(pid) is not None:
            sp = anim_speed(pid, v[1])
            if sp:
                res = cast(ctx.cells, pin, d, header(pid), mask_for(pid), step_height(pid))
                if res[0] == "wall" and res[4] < rad - 1:
                    return [], ("0x0B", "0x0B @%d blocked after %d px of %d (cell %s)" % (pc, res[4], rad, res[2]))
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
        # the hero is inside the box once the program passes it: every free sample of the box
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
        # the entity appears at its record position (SpawnEntity with notCheckSpawnZone), unless it already has a known position
        t = ("R", v[1])
        anim, d, pin, mv = getA(actors, t, ctx)
        if pin is None:
            r = ctx.rec[v[1]]
            return [(logic, setA(actors, t, (anim if anim is not None else 0, d if d is not None else rec_dir(r), rec_pos(r), False)), lock)], None
        return [(logic, actors, lock)], None
    # fall back on the first model for everything else
    outs = X.step_state(ctx, op, v, st, pc)
    # 0x0B: the radius walk is clipped by the first model; the blocked case was cut above, the unblocked one is exact
    return refine_lost(ctx, actors, outs), None


def refine_lost(ctx, a_in, outs):
    """An actor that walked during a wait of unknown length and then turned lost its pin in the first model. It is somewhere between where it
    was and the wall it walks to: both ends become states (an actor that may leave the map stays indeterminate)."""
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
            if hdr and d_p is not None and ctx.cells is not None:
                rr = cast(ctx.cells, pin_p, d_p, hdr, mask_for(pid), step_height(pid))
                if rr[0] == "wall":
                    m = max(abs(OFFX[d_p]), abs(OFFY[d_p]))
                    alts.append((round(pin_p[0] + OFFX[d_p] / m * rr[4], 3), round(pin_p[1] + OFFY[d_p] / m * rr[4], 3)))
                else:
                    alts = [None]
            else:
                alts = [None]
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
        # flag dependencies: a T flag of this map or a G flag of the corpus that no reached program sets can never be on
        f = v[1] | (v[2] << 8)
        if op == 0x36 and flag_dead(md, env, f):
            wr.cuts.append((pc + sz, "flag", flagname(f)))
            continue
        if op == 0x36 and f in env.setflags and env.setflags[f]:
            st = (logic, actors, 1)
        if op in (0x30, 0x31, 0x7B, 0x7C) and flag_dead(md, env, f):
            # the "flag on" outcome is infeasible: jump target for 0x30/0x7B, fall through for 0x31/0x7C
            if op in (0x30, 0x7B):
                wr.cuts.append((nxt[0], "flag", flagname(f)))
                nxt = nxt[1:]
            else:
                wr.cuts.append((nxt[1], "flag", flagname(f)))
                nxt = nxt[:1]
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
            inherit = env.setflags.get(f, 0)          # the program that raised the flag held the hero
            if f in md.contact_at:
                carried = md.contact_at[f]
                inherit = 1                           # the interaction that raises it holds the hero (0x10)
            elif is_T(f) and env.setpins.get(f):
                carried = sorted(env.setpins[f])      # the hero stands where the setter program saw him
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


def flag_dead(md, env, f):
    """True when flag f can never be on: it has setters (T: in this map; G: in the corpus) and no reached program sets it."""
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
                return False   # not cached: a cycle made the answer optimistic
            FLAGDEAD[key] = not ok
    return FLAGDEAD[key]


CYCLE = False


def analyze_map(mid):
    if mid in RES:
        return RES[mid]
    if mid in STACK:
        global CYCLE
        if mid in PREV:
            return PREV[mid]          # a cycle between maps: the answer of the previous pass
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
        # DLL-only roots (index without bit 0x80) are walked only to give a state to the sites no root reaches
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
    """the cut that explains why no state reaches pc: ('0x0B'|'flag'|'entry', text)."""
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
    for k in ("0x0B", "flag", "entry"):
        for c in causes:
            if c[0] == k:
                return c
    return causes[0]


def flag_reached(f, asking_map):
    """is some live setter of flag f (corpus-wide; a setter in dormant code does not count) reached by a modelled walk? -> (bool, why)."""
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
            continue        # dormant: not a setter
        live += 1
        if any(res.walks.get(rt.label) and res.walks[rt.label].states.get(pc) for rt in rts):
            return True, "set at %d @%d" % (m, pc)
        notes.append("%d @%d: %s" % (m, pc, explain_unreached(res, pc, "setter")[1]))
    if not live:
        return True, "no live setter in the corpus (only dormant code sets it)"
    return False, "no reached setter: " + "; ".join(notes)


# -------------------------------------------------------------------------------------------------------------- classification
def classify_state(md, st, rt):
    ctx = md.ctx
    logic, actors, lock = st
    if logic in (UNKNOWN, None) or not (logic == HERO or (isinstance(logic, tuple) and logic[0] == "R" and logic[1] in ctx.rec)):
        return "indeterminate", dict(actor=ctx.name(logic), why="logic entity not deducible")
    pid = ctx.pid(logic)
    hdr = header(pid) if pid else None
    anim, d, pin, moved = getA(actors, logic, ctx)
    if pin is not None:
        pin = tuple(pin)
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
    if pin is None:
        return "indeterminate", dict(det, why="position not deducible")
    mask = mask_for(pid)
    step = step_height(pid)
    res = cast(ctx.cells, pin, d, hdr, mask, step)
    det.update(mask=mask, step=step, res=res, frm="pinned %s" % (pin,))
    return ("wall" if res[0] == "wall" else "nowall"), det


CLSNAME = {"wall": "wall found", "zero": "zero speed", "nowall": "no wall", "indeterminate": "indeterminate",
           "0x0B": "not reached (0x0B blocked)", "flag": "not reached (flag never set)", "entry": "not reached (entry)"}


def site_row(res, s):
    md = res.md
    mid = md.mid
    rr = [rt for rt in md.roots if s in md.reach[rt.label]]
    ff = [rt for rt in md.fallback if s in md.reach[rt.label]]
    rc = "reachable" if rr else ("DLL only" if ff else "dormant")
    row = dict(map=mid, name=md.mp.d["name"], zone=md.mp.d["zone"], pc=s, reach=rc, roots=[r.label for r in (rr or ff)])
    if rc == "dormant":
        return row
    cands = []
    entries = []
    for rt in (rr or ff):
        wr = res.walks.get(rt.label)
        entries.extend(wr.entries if wr else [])
        for st in (wr.states.get(s, ()) if wr else ()):
            cls, det = classify_state(md, st, rt)
            cands.append((cls, dict(det, root=rt.label, slot=rt.slot)))
    row["entries"] = sorted(set(entries))
    if cands:
        best = None
        for cls, det in cands:
            if best is None or PRIO[cls] > PRIO[best[0]]:
                best = (cls, det)
        row["class"], row["detail"] = best
        row["all"] = cands
        row["lock"] = any(det.get("lock") for _, det in cands)
        row["reached"] = True
        walls = [det["res"][1] for c, det in cands if c == "wall" and det.get("res")]
        row["dist"] = (min(walls), max(walls)) if walls else None
        row["classes_seen"] = sorted({c for c, _ in cands})
    else:
        k, why = explain_unreached(res, s, "site")
        row["class"], row["detail"] = k, dict(why=why, root=(rr or ff)[0].label, slot=(rr or ff)[0].slot)
        row["all"] = []
        row["lock"] = False
        row["reached"] = False
        row["dist"] = None
    return row


def run_pass(only=None):
    import time
    rows = []
    old = pickle.load(open(EX + "/census.pkl", "rb"))[0]
    maps = sorted({r["map"] for r in old})
    t0 = time.time()
    for mid in maps:
        if only and mid not in only:
            continue
        res = analyze_map(mid)
        for s in res.md.sites:
            rows.append(site_row(res, s))
        print("  map %d done (%d sites, %d iterations) %.0fs" % (mid, len(res.md.sites), res.iterations, time.time() - t0), flush=True)
    return rows


def signature(rows):
    return sorted((r["map"], r["pc"], r.get("class"), r.get("dist")) for r in rows)


def main(only=None, passes=3):
    global RES, PREV, FLAGDEAD
    prev_sig = None
    rows = []
    for p in range(passes):
        print("pass", p + 1, flush=True)
        RES = {}
        FLAGDEAD = {}
        rows = run_pass(only)
        sig = signature(rows)
        pickle.dump(sig, open(os.path.dirname(os.path.abspath(__file__)) + "/sig_pass%d.pkl" % (p + 1), "wb"))
        if prev_sig is not None:
            a = {(m, pc): (c, d) for m, pc, c, d in prev_sig}
            b = {(m, pc): (c, d) for m, pc, c, d in sig}
            diff = [(k, a[k], b[k]) for k in b if a.get(k) != b[k]]
            print("  differences with the previous pass:", len(diff), diff[:12], flush=True)
        PREV = dict(RES)
        if sig == prev_sig:
            print("stable after pass", p + 1)
            break
        prev_sig = sig
    return rows


if __name__ == "__main__":
    passes = int(os.environ.get("PASSES", "3"))
    only = set(int(a) for a in sys.argv[1:]) or None
    rows = main(only, passes)
    out = os.path.dirname(os.path.abspath(__file__)) + ("/census_v2_partial.pkl" if only else "/census_v2.pkl")
    pickle.dump(rows, open(out, "wb"))
    c = collections.Counter((r["reach"], r.get("class")) for r in rows)
    print(dict(c))
