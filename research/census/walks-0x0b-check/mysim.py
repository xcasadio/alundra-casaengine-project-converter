"""mysim.py - checker's own walk geometry (independent re-implementation, 16.16 integer arithmetic).

DLL rule  [DLL][engine]: AlundraCellsCollisionField (24x16 cells, clamped, ground per slope kind) + CharacterControllerComponent
          field stage (:1108-1286): per axis h1 = X then h2 = Y; an axis is blocked when a corner of the footprint at the
          candidate is not walkable under the mask or has ground > foot + StepHeight (3 for all 383 controller prefabs);
          far corner exclusive (BitDecrement). Foot = ground at start; follows the ground after an accepted step when the
          Gravity flag (0x100) is set (same approximation as the census).
ORIG rule [binary] (e19a2/binary.md F1-F9, F14; PhysicsEngine.cs:600-810 slide cases, which F13 says match the binary):
          joint step; corners x1 = (X+ModX)>>16, x2 = (X+ModX+Width)>>16, Width = Size*65536-1; a corner blocks on the mask
          or MapHeight >= ModdedPosZ (foot + 1 unit); gravity snap (Flags 0x100) when the rise is <= 3 px; border clamp
          [0,1248]x[0,960]; when NO probe advances: the slide table of DECIDE_FINAL_OBSTACLE for ALL 32 directions
          (cardinal: one front corner, no back corner -> 0.75 px (N/S) or 0.5 px (W/E) sideways; oblique: zero one force
          component and retry), never for an entity contact.
One iteration = one step of 1 px on the major axis (the census granularity); the walk ends when (|dX|>>16) >= r or
(|dY|>>16) >= r, as 0x0B/0x1E do.
"""
import glob
import json
import os
import re
import sys

ROOT = "D:/development/repo/alundra-casaengine-project-converter"
U = 65536
_tables = open(ROOT + "/Alundra/Scripts/AnimationTables.cs", encoding="utf-8-sig").read()


def _arr(name):
    m = re.search(r"public static readonly \w+\[\] %s =\s*\{(.*?)\};" % name, _tables, re.S)
    out = []
    for tok in re.finditer(r"unchecked\(\(short\)(0x[0-9a-fA-F]+)\)|(0x[0-9a-fA-F]+)", m.group(1)):
        v = int(tok.group(1) or tok.group(2), 16)
        out.append(v - 65536 if (tok.group(1) and v >= 32768) else v)
    return out


OFFX = _arr("OffsetXList")
OFFY = _arr("OffsetYList")
HEIGHTS = _arr("HeightsTable_800236d4")
assert len(OFFX) == 32 and len(OFFY) == 32 and len(HEIGHTS) == 24

SPR = json.load(open(ROOT + "/alundra-project/Data/sprite-records.json", encoding="utf-8-sig"))
ASSETS = {e["id"]: e for e in json.load(open(ROOT + "/alundra-project/AssetInfos.json", encoding="utf-8-sig"))["asset_infos"]}
HERO = "192c2eeb-bdda-5bb4-92d6-39a4ae298307"
_ctrl = {}


def has_controller(pid):
    if pid not in _ctrl:
        v = None
        info = ASSETS.get(pid)
        if info:
            p = ROOT + "/alundra-project/" + info["file_name"].replace("\\", "/")
            if os.path.exists(p):
                d = json.load(open(p, encoding="utf-8-sig"))
                v = any(c.get("type") == "CharacterControllerComponent" for c in d.get("components", []))
        _ctrl[pid] = v
    return _ctrl[pid]


def flags_of(h):
    return h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)


def mask_of(flags):
    m = 0x40
    if flags & 0x08:
        m |= 0x01
    if flags & 0x01:
        m |= 0x1000
    return m


class Field:
    def __init__(self, cells, W, H):
        self.W, self.H = W, H
        self.wk, self.gp, self.sl, self.ht = cells["walkability"], cells["ground_property"], cells["slope"], cells["height"]

    def _i(self, px, py):
        cx = min(max(px // 24, 0), self.W - 1)
        cy = min(max(py // 16, 0), self.H - 1)
        return cy * self.W + cx

    def hit(self, px, py, mask):
        i = self._i(px, py)
        return ((self.wk[i] | (self.gp[i] << 8)) & mask) != 0

    def ground(self, px, py):
        i = self._i(px, py)
        h, k = self.ht[i], self.sl[i] & 3
        if k == 1:
            return (h - 1) * 16 + 16 - (py % 16)
        if k == 2:
            return (h - 1) * 16 + HEIGHTS[(23 - (px % 24)) % 24]
        if k == 3:
            return (h - 1) * 16 + HEIGHTS[px % 24]
        return h * 16

    def cellxy(self, px, py):
        i = self._i(px, py)
        return (i % self.W, i // self.W)


class Body:
    def __init__(self, h, flags=None, mask=None):
        self.mx, self.my = h["OffsetX"] * U, h["OffsetY"] * U
        self.wx = max(h["SizeX"], 1) * U - 1
        self.wy = max(h["SizeY"], 1) * U - 1
        self.sx, self.sy = max(h["SizeX"], 1), max(h["SizeY"], 1)
        self.ox, self.oy = h["OffsetX"], h["OffsetY"]
        self.flags = flags_of(h) if flags is None else flags
        self.mask = mask_of(self.flags) if mask is None else mask
        self.grav = bool(self.flags & 0x100)
        self.noslide = bool(self.flags & 0x2000)

    def corners(self, X, Y):
        x1 = (X + self.mx) >> 16
        x2 = (X + self.mx + self.wx) >> 16
        y1 = (Y + self.my) >> 16
        y2 = (Y + self.my + self.wy) >> 16
        return [(x1, y1), (x2, y1), (x1, y2), (x2, y2)]   # TL, TR, BL, BR


def _done(X, Y, X0, Y0, r):
    return (abs(X - X0) >> 16) >= r or (abs(Y - Y0) >> 16) >= r


def walk_dll(F, b, X0, Y0, d, r, limit=3000):
    """-> (kind, X, Y, info). kind: pass / blocked / left."""
    ox, oy = OFFX[d], OFFY[d]
    m = max(abs(ox), abs(oy))
    if m == 0:
        return ("zero", X0, Y0, None)
    stx, sty = (ox * U) // m if ox >= 0 else -((-ox * U) // m), (oy * U) // m if oy >= 0 else -((-oy * U) // m)
    X, Y = X0, Y0
    z = max(F.ground(cx, cy) for cx, cy in b.corners(X, Y))
    if _done(X, Y, X0, Y0, r):
        return ("pass", X, Y, None)
    why = None
    for _ in range(limit):
        moved = False
        for ax in (0, 1):
            st = stx if ax == 0 else sty
            if st == 0:
                continue
            cx_, cy_ = (X + st, Y) if ax == 0 else (X, Y + st)
            cs = b.corners(cx_, cy_)
            bad = None
            for (px, py) in cs:
                if F.hit(px, py, b.mask):
                    bad = ("cell", F.cellxy(px, py))
                    break
                if F.ground(px, py) > z + 3:
                    bad = ("step", F.cellxy(px, py))
                    break
            if bad:
                why = bad
                continue
            X, Y = cx_, cy_
            moved = True
            if b.grav:
                z = max(F.ground(px, py) for px, py in cs)
        if not moved:
            return ("blocked", X, Y, why)
        if _done(X, Y, X0, Y0, r):
            return ("pass", X, Y, None)
        if X < -48 * U or Y < -48 * U or X > (1248 + 48) * U or Y > (960 + 48) * U:
            return ("left", X, Y, None)
    return ("left", X, Y, "limit")


def walk_orig(F, b, X0, Y0, d, r, limit=6000, obstacles=(), slide=True, oblique_slide=True):
    ox, oy = OFFX[d], OFFY[d]
    m = max(abs(ox), abs(oy))
    if m == 0:
        return ("zero", X0, Y0, None)
    stx = (ox * U) // m if ox >= 0 else -((-ox * U) // m)
    sty = (oy * U) // m if oy >= 0 else -((-oy * U) // m)
    loX, hiX = -b.ox * U, (1248 - b.ox - b.sx) * U
    loY, hiY = -b.oy * U, (960 - b.oy - b.sy) * U
    X, Y = X0, Y0
    z = max(F.ground(cx, cy) for cx, cy in b.corners(X, Y))
    if _done(X, Y, X0, Y0, r):
        return ("pass", X, Y, None)

    def flags_at(Xc, Yc, zc):
        cs = b.corners(Xc, Yc)
        gs = [F.ground(px, py) for px, py in cs]
        gm = max(gs)
        zz = zc
        if b.grav and abs(gm - zc) <= 3:
            zz = gm
        fl = [F.hit(px, py, b.mask) or g > zz for (px, py), g in zip(cs, gs)]
        if not any(fl) and b.grav and gm < zz:
            zz = gm
        return fl, zz, cs

    def ent_hit(Xc, Yc, zc):
        for o in obstacles:
            x1, y1 = Xc + b.mx, Yc + b.my
            if x1 < o[2] and o[0] < x1 + b.wx + 1 and y1 < o[3] and o[1] < y1 + b.wy + 1 and zc * U < o[5] and o[4] < zc * U + 32 * U:
                return o
        return None

    def try_move(dx, dy):
        nonlocal X, Y, z
        nx = min(max(X + dx, loX), hiX)
        ny = min(max(Y + dy, loY), hiY)
        if (nx, ny) == (X, Y):
            return "border", None
        if obstacles and ent_hit(nx, ny, z):
            return "entity", None
        fl, zz, cs = flags_at(nx, ny, z)
        if any(fl):
            return "tile", fl
        X, Y, z = nx, ny, zz
        return "ok", None

    slides = 0
    for _ in range(limit):
        res, fl = try_move(stx, sty)
        if res == "ok":
            if _done(X, Y, X0, Y0, r):
                return ("pass", X, Y, None)
            continue
        if res in ("border", "entity") or not slide or b.noslide:
            return ("blocked", X, Y, res)
        # advance to the contact first (the binary's halving reaches it, F4), by bisection on the fraction of the step
        lo, hi = 0, U
        while hi - lo > 1:
            mid = (lo + hi) // 2
            fl_m, _, _ = flags_at(X + stx * mid // U, Y + sty * mid // U, z)
            if any(fl_m):
                hi = mid
            else:
                lo = mid
        if lo:
            fl_m, zz_m, _ = flags_at(X + stx * lo // U, Y + sty * lo // U, z)
            X, Y, z = X + stx * lo // U, Y + sty * lo // U, zz_m
        # probe flags 1 unit forward (the last, smallest probe)
        px_ = X + (1 if stx > 0 else -1 if stx < 0 else 0)
        py_ = Y + (1 if sty > 0 else -1 if sty < 0 else 0)
        fl, _, _ = flags_at(px_, py_, z)
        c0, c1, c2, c3 = fl
        sdx = sdy = None
        if d == 16:
            if (c0 and c1) or c2 or c3:
                return ("blocked", X, Y, "tile")
            sdx, sdy = (int(0.75 * U) if (c0 and not c1) else -int(0.75 * U) if (c1 and not c0) else 0), 0
        elif d == 0:
            if (c2 and c3) or c0 or c1:
                return ("blocked", X, Y, "tile")
            sdx, sdy = (int(0.75 * U) if (c2 and not c3) else -int(0.75 * U) if (c3 and not c2) else 0), 0
        elif d == 8:
            if (c0 and c2) or c1 or c3:
                return ("blocked", X, Y, "tile")
            sdx, sdy = 0, (U // 2 if (c0 and not c2) else -(U // 2) if (c2 and not c0) else 0)
        elif d == 24:
            if (c1 and c3) or c0 or c2:
                return ("blocked", X, Y, "tile")
            sdx, sdy = 0, (U // 2 if (c1 and not c3) else -(U // 2) if (c3 and not c1) else 0)
        else:
            if not oblique_slide:
                return ("blocked", X, Y, "tile")
            q = d // 8
            zx = zy = False
            if q == 0:          # 1-7
                if c0:
                    if c3:
                        return ("blocked", X, Y, "tile")
                    zx = True
                elif c3:
                    zy = True
            elif q == 1:        # 9-15
                if not c1:
                    if c2:
                        zx = True
                else:
                    if c2:
                        return ("blocked", X, Y, "tile")
                    zy = True
            elif q == 2:        # 17-23
                if c0 and c3:
                    return ("blocked", X, Y, "tile")
                if not c3:
                    if c0:
                        zy = True
                else:
                    zx = True
            else:               # 25-31
                if c1 and c2:
                    return ("blocked", X, Y, "tile")
                if c2:
                    zy = True
                if c1:
                    zx = True
            sdx = 0 if zx else stx
            sdy = 0 if zy else sty
            if not zx and not zy:
                return ("blocked", X, Y, "tile")
        if sdx == 0 and sdy == 0:
            return ("blocked", X, Y, "tile")
        res2, _ = try_move(sdx, sdy)
        if res2 != "ok":
            return ("blocked", X, Y, "tile")
        slides += 1
        if _done(X, Y, X0, Y0, r):
            return ("pass", X, Y, "slid")
    return ("blocked", X, Y, "limit")
