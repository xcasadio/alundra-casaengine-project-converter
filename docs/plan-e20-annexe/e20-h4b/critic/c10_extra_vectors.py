"""c10_extra_vectors.py - the critic's COMPLEMENTARY vectors, recorded from the REAL code with the critic's interpreter (cmips), in the same JSON format as the
other agent's pack (README of bin/vec).  They fill the gaps measured in c9b/check.md: arrival exactly on Y-min, both axes clamped, flag kept at 1, push not shifted,
query SENTINEL slot (slot == count, live and eligible, listed in 'reads' although the exclusive loop never reads it: 'sentinel_reads'), count kept after a death,
n == 2 in the wrong order, n == 3 with equal keys, immunity byte boundaries, camera first call / kick consumed once, cancel of fire 2 before any save.
Output: critic/vec_extra/vec_extra_*.json ; replay with c9_replay.py's logic via c10_replay_extra.py."""
import json, os
from cmips import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "vec_extra")
os.makedirs(OUT, exist_ok=True)
HERO, STR = 0x80127D30, 0x294


def w32(ram, a, v):
    v &= M
    for i in range(4):
        ram[a + i] = (v >> (8 * i)) & 0xFF


def record(fn, args, ram, stop_at=(), stubs=None, extra_reads=()):
    m = Mem(dict(ram), strict=False)
    c = Cpu(m, stubs=stubs or {})
    ret = c.run(fn, args, stop_at=stop_at)
    reads = [[a, n, v] for a, n, v in m.rlog]
    # sentinel / extra reads that the real code does not read (kept so that a strict sparse memory has the data)
    have = {(a, n) for a, n, v in m.rlog}
    for a, n in extra_reads:
        if (a, n) not in have:
            reads.append([a, n, sum(ram.get(a + i, 0) << (8 * i) for i in range(n))])
    wl = {}
    order = []
    for a, n, v in m.wlog:
        if 0x801FFE00 <= a < 0x801FFF00:
            continue
        if (a, n) not in wl:
            order.append((a, n))
        wl[(a, n)] = v
    return {"fn": "0x%08x" % fn, "args": list(args), "reads": reads, "writes": [[a, n, wl[(a, n)]] for a, n in order], "ret": ret & M, "ret_v1": c.r[3] & M}


def push_ent(base=HERO + STR * 3, idx=0, sh=0, fx=0, fy=0, cx=0, cy=0, px=0, py=0, flag=0, bounds=None, pos=(0x100000, 0x100000)):
    ram = {}
    bx0, bx1, by0, by1 = bounds or (0, 0x4E00000, 0, 0x3C00000)
    for off, v in ((0x188, idx), (0xC4, fx), (0xC8, fy), (0xCC, cx), (0xD0, cy), (0x13C, flag), (0x108, bx0), (0xFC, bx1), (0x10C, by0), (0x100, by1), (0x114, pos[0]), (0x118, pos[1])):
        w32(ram, base + off, v)
    mp = 0x80200000
    w32(ram, 0x800E4334, mp)
    ram[mp + 8] = sh & 0xFF
    return base, ram


def main():
    vecs = {"vec_extra_push.json": [], "vec_extra_query.json": [], "vec_extra_camera.json": [], "vec_extra_slotpass.json": [], "vec_extra_cancel_fire2.json": []}

    # ---- push ----
    cases = []
    cases.append(("Y arrival exactly on the minimum (no clamp, no flag)", dict(fy=-0x10000, pos=(0x200000, 0x30000), bounds=(0, 0x4E00000, 0x20000, 0x3C00000))))
    cases.append(("Y arrival exactly on the maximum (no clamp, no flag)", dict(fy=0x10000, pos=(0x200000, 0x3BF0000), bounds=(0, 0x4E00000, 0, 0x3C00000))))
    cases.append(("X arrival exactly on the minimum", dict(fx=-0x10000, pos=(0x30000, 0x200000), bounds=(0x20000, 0x4E00000, 0, 0x3C00000))))
    cases.append(("both axes clamped (X max, Y min)", dict(fx=0x300000, fy=-0x300000, pos=(0x4D00000, 0x100000))))
    cases.append(("flag already 1, no clamp: stays 1", dict(flag=1, fx=0x1000, pos=(0x200000, 0x200000))))
    cases.append(("sh=1, push (+0x80000, -0x40000) is NOT shifted", dict(sh=1, fx=0x20000, fy=0x20000, cx=0x80000, cy=-0x40000)))
    cases.append(("idx=5, sh=1 (slide table + shift + push)", dict(idx=5, sh=1, fx=0x20000, fy=0x20000, cx=0x10000, cy=0x10000)))
    cases.append(("idx=7 (-983040), sh=0", dict(idx=7, fx=0, fy=0, cx=0, cy=0)))
    cases.append(("sh byte 0x21 (&31 = 1)", dict(sh=0x21, fx=0x40000, fy=0x40000)))
    cases.append(("sh byte 0xFF (lb = -1, &31 = 31)", dict(sh=0xFF, fx=0x40000, fy=-0x40000)))
    cases.append(("32-bit wrap: ForceX + table overflows before the shift", dict(idx=11, sh=1, fx=0x7FF00000, fy=0, cx=0, cy=0)))
    cases.append(("push zeroed even when nothing else happens", dict(cx=0x1234, cy=-0x4321, pos=(0x200000, 0x200000))))
    for label, kw in cases:
        base, ram = push_ent(**kw)
        v = record(0x800366FC, (base,), ram)
        v["case"] = label
        vecs["vec_extra_push.json"].append(v)

    # ---- query ----
    def qworld(count, ents, caller_slot=0, mask=1, atk=0):
        ram = {}
        tab = 0x80220000
        for j in range(64):
            ram[tab + j] = 0
        w32(ram, 0x80134360, count)
        for i in range(0, 64):
            w32(ram, HERO + STR * i + 0x10, 0)
        for slot, d in ents.items():
            e = HERO + STR * slot
            w32(ram, e + 0x10, d.get("st", 2)); w32(ram, e + 0x6C, d.get("fl", 1)); w32(ram, e + 0x1C4, tab + 0x100 * slot)
            w32(ram, e + 0x114, d.get("x", 0) << 16); w32(ram, e + 0x118, d.get("y", 0) << 16)
            for off in ("20", "1D0", "1CC", "B4"):
                if off in d: w32(ram, e + int(off, 16), d[off])
            if "imm" in d: ram[tab + 0x100 * slot + atk + 2] = d["imm"]
        w32(ram, 0x801FFF10, 0x800CC158)
        return HERO + STR * caller_slot, ram, mask, atk

    def rq(label, count, ents, caller_slot=0, sentinel=None, **kw):
        caller, ram, mask, atk = qworld(count, ents, caller_slot, **kw)
        extra = []
        if sentinel is not None:
            e = HERO + STR * sentinel
            extra = [(e + 0x10, 4), (e + 0x6C, 4), (e + 0x1C4, 4), (e + 0x114, 4), (e + 0x118, 4), (e + 0x20, 4), (e + 0x1D0, 4), (e + 0x1CC, 4), (e + 0xB4, 4)]
        v = record(0x8003AF70, (caller, mask, atk, 0x800CC058), ram, extra_reads=extra)
        v["case"] = label
        v["sentinel_slot"] = sentinel
        vecs["vec_extra_query.json"].append(v)

    rq("SENTINEL: slot == count is live and eligible and is NOT returned (exclusive bound)", 4, {1: dict(x=30), 2: dict(x=20), 3: dict(x=10), 4: dict(x=1)}, sentinel=4)
    rq("stored count 6 after the death of slot 6 (status 4): slot 5 IS returned", 6, {1: dict(x=50), 2: dict(x=40), 3: dict(x=30), 4: dict(x=20), 5: dict(x=10), 6: dict(st=4, x=5)})
    rq("n == 2, far one at the LOWER slot: no sort, slot order kept", 3, {1: dict(x=100), 2: dict(x=10)})
    rq("n == 2, equal keys: order of slots", 3, {1: dict(x=10), 2: dict(x=-10)})
    rq("n == 3, all keys equal: Hoare order (not the stable one)", 4, {1: dict(x=10), 2: dict(x=-10), 3: dict(y=10)})
    rq("n == 4, ties 2 and 3", 5, {1: dict(x=40), 2: dict(x=10), 3: dict(y=10), 4: dict(x=-20)})
    rq("immunity byte 0x80 is skipped, 0xC0 / 0x40 / 0x00 are kept", 5, {1: dict(imm=0x80, x=5), 2: dict(imm=0xC0, x=6), 3: dict(imm=0x40, x=7), 4: dict(imm=0x00, x=8)})
    rq("status 3 eligible, status 1 and 4 not", 5, {1: dict(st=1, x=5), 2: dict(st=3, x=6), 3: dict(st=4, x=7), 4: dict(st=2, x=8)})
    rq("mask 2 vs flags 1: nothing", 4, {1: dict(x=5), 2: dict(x=6), 3: dict(x=7)}, mask=2)
    rq("caller is slot 2 (excluded), hero never a candidate", 4, {1: dict(x=5), 2: dict(x=6), 3: dict(x=7)}, caller_slot=2)

    # ---- camera ----
    def cam(label, first, ky, kx=0):
        ram = {}
        for i in range(9): w32(ram, 0x800E4338 + 4 * i, 0)
        w32(ram, 0x800E4328, 400); w32(ram, 0x800E432C, 300); w32(ram, 0x800E42B8, first)
        w32(ram, 0x800DD870, kx); w32(ram, 0x800DD874, ky)
        v = record(0x8002CDA0, (0, 400 + 0xA0, 300 + 0x88, 0), ram, stop_at=[0x8002CF38])
        v["case"] = label; v["stop_at"] = "0x8002cf38"
        vecs["vec_extra_camera.json"].append(v)
    cam("kick +2 at equilibrium: scrollY 302, kick cleared", 0, 2)
    cam("first call after a map load: kick NOT applied, cleared", 1, 2)
    cam("kick X (never written by the game) would be added too", 0, 0, 3)

    # ---- slot pass ----
    def sp(label, sts, tmpl_marker=0xABCD):
        ram = {}
        for i in range(64): w32(ram, HERO + STR * i + 0x10, 0)
        for s, st in sts.items(): w32(ram, HERO + STR * s + 0x10, st)
        for k in range(0, 0x294, 4): w32(ram, 0x80134368 + k, tmpl_marker if k == 0x48 else 0)
        v = record(0x80038634, (), ram)
        v["case"] = label
        vecs["vec_extra_slotpass.json"].append(v)
    sp("status 4 above the highest live slot does not count and is recycled", {1: 2, 2: 2, 6: 4, 9: 4})
    sp("status 1 (newborn) and 3 count; status 5 (unknown) counts", {1: 1, 3: 3, 7: 5})
    sp("only the hero: count 0", {0: 2})

    # ---- fire 2 cancel before any save ----
    ram = {}
    for i in range(9): w32(ram, 0x800E4338 + 4 * i, 0x11111111 * (i + 1))
    for i in range(9): w32(ram, 0x80127138 + 4 * i, 0)
    w32(ram, 0x80134360, 2)
    for i in (1, 2):
        e = HERO + STR * i
        w32(ram, e + 0x68, 0x15); w32(ram, e + 0x10, 2); w32(ram, e + 0x20, 1)
    v = record(0x80034E08, (), ram)
    v["case"] = "cancel before any save: the zero buffer is copied into the shake block; type 0x15 status 2 -> 3, +0x20 = 0"
    vecs["vec_extra_cancel_fire2.json"].append(v)

    for name, lst in vecs.items():
        json.dump({"function": name, "note": "critic's complementary vectors; same format as bin/vec; 'sentinel_slot' vectors list reads the real code does not perform", "vectors": lst}, open(os.path.join(OUT, name), "w"), indent=0)
        print(name, len(lst))


if __name__ == "__main__":
    main()
