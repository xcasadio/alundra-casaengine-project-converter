"""geo.py - E19.d2 census0b: walk geometry under three rules (read-only, scratch).

DLL  : the port today. Cell field of the controller (AlundraCellsCollisionField + CharacterControllerComponent field stage,
       CharacterControllerComponent.cs:1108-1286): per-axis resolution (h1 then h2), a corner blocks when its cell hits the
       walkability mask or when its ground is higher than foot + StepHeight 3; out-of-grid points read the edge cell
       (AlundraCellsCollisionField.cs:279-301), so an entity can leave the map; entity bodies are ghosts (D-E12D-1).
FIX  : DLL + the recommended fix of the collision surface (option E): entity boxes ORed into the field stage's blocked test
       (same per-axis resolution, no slide, no border clamp).
ORIG : the binary rule (collision.md F3-F5, collision-check.md, border.md F1-F4, e19a2/binary.md F1/F9/F14):
       joint ray (both axes halved together, no per-axis advance for an oblique step); FindEntityCollisionCandidate before the
       tile test (half-open AABB on X, Y and Z, mover and obstacle Collidable, AnimFlags 0x80 clear); tile corners: mask, or
       height >= ModdedPosZ unless the gravity snap (Gravity flag, ForceZ 0, rise < 3 px + 2 units); the corner slide for a
       cardinal step blocked on ONE front corner by the tiles when NoObstacleSlide (0x2000) is clear (an entity never slides);
       the map-border clamp of ApplyEntityForces: footprint kept in [0, 1248] x [0, 960].

Positions are pixels (floats); one iteration advances the major axis of the direction by 1 px, like census_exec._cast.
"""
import math

W_PX, H_PX = 1248, 960
UNIT = 1.0 / 65536.0


class Mover:
    """the walking actor: footprint and gates."""

    def __init__(self, key, hdr, mask, gravity, noslide, collidable, animflag80):
        self.key = key
        self.ox, self.oy, self.oz = hdr["OffsetX"], hdr["OffsetY"], hdr.get("OffsetZ", 0)
        self.sw, self.sh, self.sz = max(hdr["SizeX"], 1), max(hdr["SizeY"], 1), max(hdr.get("SizeZ", 1), 1)
        self.mask = mask
        self.gravity = gravity
        self.noslide = noslide
        self.collidable = collidable and not animflag80


class Obstacle:
    __slots__ = ("key", "x1", "x2", "y1", "y2", "z1", "z2", "src", "x", "y", "z")

    def __init__(self, key, x, y, z, hdr, src):
        self.key = key
        self.x, self.y, self.z = x, y, z
        self.x1 = x + hdr["OffsetX"]
        self.x2 = self.x1 + max(hdr["SizeX"], 1)
        self.y1 = y + hdr["OffsetY"]
        self.y2 = self.y1 + max(hdr["SizeY"], 1)
        self.z1 = z + hdr.get("OffsetZ", 0)
        self.z2 = self.z1 + max(hdr.get("SizeZ", 1), 1)
        self.src = src


def corners(mv, x, y):
    x1, y1 = x + mv.ox, y + mv.oy
    x2, y2 = x1 + mv.sw - UNIT, y1 + mv.sh - UNIT
    # order: (x1,y1) top-left, (x2,y1) top-right, (x1,y2) bottom-left, (x2,y2) bottom-right
    return [(x1, y1), (x2, y1), (x1, y2), (x2, y2)]


def ground_max(cells, mv, x, y):
    return max(cells.ground(cx, cy) for cx, cy in corners(mv, x, y))


def obstacle_hit(mv, obstacles, x, y, z):
    """first obstacle whose half-open box overlaps the mover's box at (x, y, z) - binary 0x80036F34 order: list order."""
    if not mv.collidable:
        return None
    mx1, my1 = x + mv.ox, y + mv.oy
    mx2, my2 = mx1 + mv.sw, my1 + mv.sh
    mz1 = z + mv.oz
    mz2 = mz1 + mv.sz
    for o in obstacles:
        if mx1 < o.x2 and o.x1 < mx2 and my1 < o.y2 and o.y1 < my2 and mz1 < o.z2 and o.z1 < mz2:
            return o
    return None


def corner_flags(cells, mv, x, y, z, rule):
    """-> (list of 4 blocked flags, new z). rule DLL/FIX: mask or ground > z + 3 (foot + StepHeight); the foot follows the
    ground when Gravity is on (drop or <= 3 px rise). rule ORIG: gravity snap when 0 < rise < 3 px + 2 units (integer heights:
    rise <= 3), then a corner blocks on mask or height > z (>= ModdedPosZ)."""
    cs = corners(mv, x, y)
    gs = [cells.ground(cx, cy) for cx, cy in cs]
    gmax = max(gs)
    if rule == "ORIG":
        nz = z
        if mv.gravity and gmax - z <= 3:
            nz = gmax if gmax >= z else z      # snap up; a drop is not blocked (fall handled below)
        fl = [cells.blocked(cx, cy, mv.mask) or (g > nz) for (cx, cy), g in zip(cs, gs)]
        if not any(fl) and mv.gravity and gmax < nz:
            nz = gmax                          # falls to the lower ground
        return fl, nz
    fl = [cells.blocked(cx, cy, mv.mask) or (g > z + 3) for (cx, cy), g in zip(cs, gs)]
    nz = z
    if not any(fl) and mv.gravity:
        nz = gmax
    return fl, nz


def border_clamp(mv, x, y, nx, ny):
    """ApplyEntityForces: PosX kept in [-offX, 1248 - offX - sizeX], PosY in [-offY, 960 - offY - sizeY]."""
    lo_x, hi_x = -mv.ox, W_PX - mv.ox - mv.sw
    lo_y, hi_y = -mv.oy, H_PX - mv.oy - mv.sh
    cx = min(max(nx, lo_x), hi_x)
    cy = min(max(ny, lo_y), hi_y)
    return cx, cy, (cx != nx or cy != ny)


def walk(cells, mv, obstacles, rule, x0, y0, d, offx, offy, radius=None, max_iters=None, slide_limit=48):
    """Walk from (x0, y0) along direction d (offsets offx/offy). Ends:
       'done'    radius reached (max(|dx|,|dy|) >= radius) or max_iters major px travelled;
       'blocked' no progress possible (reason: 'cell', 'step', 'obstacle', 'border');
       'left'    DLL/FIX only: no block within the traversal limit (the entity leaves the map forever).
    Returns dict(kind, x, y, n, reason, cell, obst, slid)."""
    m = max(abs(offx), abs(offy))
    if m == 0:
        return dict(kind="zero", x=x0, y=y0, n=0, reason="no direction", cell=None, obst=None, slid=0)
    sx, sy = offx / m, offy / m
    cardinal = (offx == 0 or offy == 0)
    x, y = float(x0), float(y0)
    z = ground_max(cells, mv, x, y)
    obs = obstacles if (rule in ("ORIG", "FIX") and mv.collidable) else ()
    if obs:
        # keep the obstacles the swept band can touch (cheap filter)
        far = (radius if radius is not None else (max_iters if max_iters is not None else 2600)) + 64
        bx1 = min(x, x + sx * far) + mv.ox - 2
        bx2 = max(x, x + sx * far) + mv.ox + mv.sw + 2
        by1 = min(y, y + sy * far) + mv.oy - 2
        by2 = max(y, y + sy * far) + mv.oy + mv.sh + 2
        if rule == "ORIG" and not mv.noslide:
            bx1 -= slide_limit
            bx2 += slide_limit
            by1 -= slide_limit
            by2 += slide_limit
        obs = [o for o in obs if o.x1 < bx2 and bx1 < o.x2 and o.y1 < by2 and by1 < o.y2]
    limit = 2600
    n = 0
    slid = 0
    last = dict(reason=None, cell=None, obst=None)

    def done():
        if radius is not None and (abs(x - x0) >= radius or abs(y - y0) >= radius):
            return True
        if max_iters is not None and n >= max_iters:
            return True
        return False

    if done():
        return dict(kind="done", x=x, y=y, n=n, reason=None, cell=None, obst=None, slid=0)
    # an obstacle overlapping the mover at its start blocks every probe that still overlaps it (binary); in the census this
    # mostly means a wrong start or a mover standing on the obstacle: reported apart ('start-overlap')
    o0 = obstacle_hit(mv, obs, x, y, z) if obs else None
    if o0 is not None:
        return dict(kind="blocked", x=x, y=y, n=0, reason="start-overlap", cell=None, obst=o0, slid=0)
    for _ in range(limit):
        if rule == "ORIG":
            nx, ny = x + sx, y + sy
            nx, ny, clamped = border_clamp(mv, x, y, nx, ny)
            if clamped and (nx, ny) == (x, y):
                return dict(kind="blocked", x=x, y=y, n=n, reason="border", cell=None, obst=None, slid=slid)
            o = obstacle_hit(mv, obs, nx, ny, z) if obs else None
            if o is not None:
                return dict(kind="blocked", x=x, y=y, n=n, reason="obstacle", cell=None, obst=o, slid=slid)
            fl, nz = corner_flags(cells, mv, nx, ny, z, "ORIG")
            if not any(fl):
                x, y, z = nx, ny, nz
                n += 1
                if clamped:
                    # the clamp raised ForceAdjusted; the free axis may still advance next ticks
                    pass
                if done():
                    return dict(kind="done", x=x, y=y, n=n, reason=None, cell=None, obst=None, slid=slid)
                continue
            # tile block: the corner slide (cardinal, NoObstacleSlide clear, exactly one FRONT corner, no back corner)
            if not mv.noslide and cardinal and slid < slide_limit:
                if offy < 0:
                    front, back, side = (0, 1), (2, 3), (-1, +1)   # north: TL, TR; TR blocked -> slide west
                elif offy > 0:
                    front, back, side = (2, 3), (0, 1), (-1, +1)
                elif offx < 0:
                    front, back, side = (0, 2), (1, 3), (-1, +1)   # west: TL, BL; BL blocked -> slide north
                else:
                    front, back, side = (1, 3), (0, 2), (-1, +1)
                fb = [fl[i] for i in front]
                bb = [fl[i] for i in back]
                if fb.count(True) == 1 and not any(bb):
                    # move away from the blocked corner, 1 px at a time, along the perpendicular axis
                    away = side[0] if fb[1] else side[1]
                    if offy != 0:
                        px, py = x + away, y
                    else:
                        px, py = x, y + away
                    px, py, cl = border_clamp(mv, x, y, px, py)
                    ok = (px, py) != (x, y)
                    if ok and obs and obstacle_hit(mv, obs, px, py, z) is not None:
                        ok = False
                    if ok:
                        fl2, nz2 = corner_flags(cells, mv, px, py, z, "ORIG")
                        ok = not any(fl2)
                    if ok:
                        x, y, z = px, py, nz2
                        slid += 1
                        if done():
                            return dict(kind="done", x=x, y=y, n=n, reason=None, cell=None, obst=None, slid=slid)
                        continue
            i = next(k for k in range(4) if fl[k])
            cx, cy = corners(mv, nx, ny)[i]
            reason = "cell" if cells.blocked(cx, cy, mv.mask) else "step"
            return dict(kind="blocked", x=x, y=y, n=n, reason=reason, cell=cells.cell_of(cx, cy), obst=None, slid=slid)
        # DLL / FIX: per axis, h1 = X then h2 = Y
        moved = False
        reason = None
        cell = None
        obst = None
        for ax in (0, 1):
            step = sx if ax == 0 else sy
            if step == 0:
                continue
            nx, ny = (x + step, y) if ax == 0 else (x, y + step)
            o = obstacle_hit(mv, obs, nx, ny, z) if obs else None
            if o is not None:
                reason, obst = "obstacle", o
                continue
            fl, nz = corner_flags(cells, mv, nx, ny, z, "DLL")
            if any(fl):
                i = next(k for k in range(4) if fl[k])
                cx, cy = corners(mv, nx, ny)[i]
                reason = "cell" if cells.blocked(cx, cy, mv.mask) else "step"
                cell = cells.cell_of(cx, cy)
                continue
            x, y, z = nx, ny, nz
            moved = True
        if not moved:
            return dict(kind="blocked", x=x, y=y, n=n, reason=reason, cell=cell, obst=obst, slid=0)
        n += 1
        if done():
            return dict(kind="done", x=x, y=y, n=n, reason=None, cell=None, obst=None, slid=0)
        if x < -48 or y < -48 or x > W_PX + 48 or y > H_PX + 48:
            return dict(kind="left", x=x, y=y, n=n, reason="left the map", cell=None, obst=None, slid=0)
    return dict(kind="left" if rule != "ORIG" else "blocked", x=x, y=y, n=n, reason="limit", cell=None, obst=None, slid=slid)
