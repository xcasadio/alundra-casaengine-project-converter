"""Compare the REAL effect code (fxm.FxMachine, MIPS interpreter) with the python model (model/fxpool.py)."""
import sys, os, time, json, random
H = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, H)
sys.path.insert(0, os.path.join(H, "..", "model"))
import fxm
import fxpool as P

FX = P.load_all()
GB = P.build_bank(FX["alundra"])
_bank_cache = {}


def bank_of(m):
    if m not in _bank_cache:
        _bank_cache[m] = P.build_bank(FX[str(m)]) if str(m) in FX else P.Bank([])
    return _bank_cache[m]


def new_pair(m, hero_tile=(0, 0)):
    f = fxm.FxMachine(m, hero_tile=hero_tile)
    recs = P.records_of(FX[str(m)]) if str(m) in FX else []
    p = P.Pool(bank_of(m), GB, recs, hero_tile=hero_tile)
    return f, p


def emu_state(f):
    """list of dicts for active slots in slot order + pushed list (as slot indexes)."""
    out = {}
    m = f.m
    for i in f.active():
        s = f.slot(i)
        spr = s["spr"]
        setid = None
        if s["imgs"]:
            setid = s["imgs"] - spr - 2
        frame = None
        if s["f0"] and s["fptr"]:
            frame = (s["fptr"] - s["f0"]) // 3
        out[i] = dict(status=s["status"], recidx=s["recidx"], mode=s["mode"], x=s["x"], y=s["y"], z=s["z"], forces=s["forces"],
                      off=s["off"], doff=s["doff"], depth=s["depth"] & 0xFFFFFFFF, tb=s["tb"], cb=s["cb"], ts=s["ts"], cs=s["cs"], ta=s["ta"], ca=s["ca"],
                      frame=frame, cnt=s["cnt"], dead=s["dead"], set=setid, bias=s["bias"], n=s["n"],
                      pagebase=s["pagebase"], clutbase=s["clutbase"], rx=s["rx"], ry=s["ry"], rz=s["rz"], rdepth=s["rdepth"] & 0xFFFFFFFF)
    return out


def model_state(p, anim_loaded_only=True):
    out = {}
    for s in p.slots:
        if s.status == 0:
            continue
        out[s.idx] = dict(status=s.status, recidx=s.recidx, mode=s.mode, x=s.x, y=s.y, z=s.z, forces=s.forces,
                          off=s.off, doff=s.doff, depth=s.depth & 0xFFFFFFFF, tb=s.tb, cb=s.cb, ts=s.ts, cs=s.cs, ta=s.ta, ca=s.ca,
                          frame=(s.frame if s.anim is not None else None), cnt=s.cnt, dead=s.dead, set=s.set, bias=s.bias, n=s.n,
                          pagebase=s.pagebase, clutbase=s.clutbase, rx=s.rx, ry=s.ry, rz=s.rz, rdepth=s.rdepth & 0xFFFFFFFF)
    return out


def diff(a, b):
    d = []
    for k in sorted(set(a) | set(b)):
        if k not in a or k not in b:
            d.append((k, "missing", k in a, k in b))
            continue
        for fld in a[k]:
            va, vb = a[k][fld], b[k][fld]
            if fld == "frame" and (va is None or vb is None):
                continue      # frame pointer is only meaningful once an animation is loaded
            if fld == "frame" and va != vb:
                d.append((k, fld, va, vb)); continue
            if va != vb:
                d.append((k, fld, va, vb))
    return d


def check(f, p, ctx, listcheck=True):
    d = diff(emu_state(f), model_state(p))
    if d:
        return ["%s: %s" % (ctx, x) for x in d[:6]]
    return []


def run_ticks(f, p, n, ctx, blocked_fn=None, errs=None):
    errs = errs if errs is not None else []
    for t in range(n):
        b = blocked_fn(t) if blocked_fn else 0
        lst = f.tick(b)
        pl = p.tick(b)
        # compare pushed list: the pushed pointers are slot+0x0C
        el = [(x - fxm.POOL - 0x0C) // 0x80 for x in lst]
        ml = [s.idx for s in pl]
        if el != ml:
            errs.append("%s tick %d: pushed list %s != %s" % (ctx, t, el[:8], ml[:8]))
            return errs
        e = check(f, p, "%s tick %d" % (ctx, t))
        if e:
            errs.extend(e)
            return errs
    return errs


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "load"
    if mode == "load":
        maps = [int(m) for m in FX if m != "alundra"] if (len(sys.argv) < 3 or sys.argv[2] == "all") else [int(x) for x in sys.argv[2].split(",")]
        nt = int(sys.argv[3]) if len(sys.argv) > 3 else 40
        bad = 0
        t0 = time.time()
        for m in sorted(maps):
            for tile in ((0, 0), (26, 30)):
                f, p = new_pair(m, tile)
                f.map_load_spawn(); p.map_load()
                errs = check(f, p, "map %d tile %s load" % (m, tile))
                if not errs:
                    errs = run_ticks(f, p, nt, "map %d tile %s" % (m, tile))
                if errs:
                    bad += 1
                    print("MISMATCH", errs[:3])
        print("maps", len(maps), "bad", bad, "time %.1f" % (time.time() - t0))
