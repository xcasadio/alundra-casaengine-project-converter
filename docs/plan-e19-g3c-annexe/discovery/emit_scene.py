"""Model (g3c_model) against the REAL emitter for every element pushed at load + tick 0 of a map (load-time effects): vertices, semi/ABR, W/H, OT slot."""
import sys, os
H = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, H)
import fxm, g3c_model as M
def tpage_tables(m):
    def get_tpage(tp, abr, x, y): return ((tp & 3) << 7) | ((abr & 3) << 5) | ((y & 0x100) >> 4) | ((x & 0x3FF) >> 6) | ((y & 0x200) << 2)
    for abr in range(4):
        k = 0
        for y in (0, 0x100):
            for x in range(0x140, 0x400, 0x40):
                m.wh(0x800DC4F0 + abr * 0x2C + 2 * k, get_tpage(0, abr, x, y)); k += 1
def check(map_id, cam, hero=(26, 30), ticks=0):
    f = fxm.FxMachine(map_id, hero_tile=hero); m = f.m; tpage_tables(m)
    f.map_load_spawn()
    for t in range(ticks + 1): pushed = f.tick(0)
    doc, sheet = M.load_companion(map_id)
    PRIMS = 0x80110000; OT = 0x80140000; PTRCELL = 0x80102000
    m.ww(0x80126F10, cam[0] & 0xFFFFFFFF); m.ww(0x80126F14, cam[1] & 0xFFFFFFFF)
    bad = 0; nel = 0; nquads = 0
    for el in pushed:
        idx = (el - fxm.POOL - 0x0C) // 0x80
        sl = f.slot(idx)
        m.ww(PTRCELL, PRIMS)
        for i in range(0x400): m.ww(OT + 4 * i, 0)
        n = m.call(0x8002DB48, el, PTRCELL, OT, 0, limit=50_000_000)
        pos = (m.rws(el + 4), m.rws(m.rws(0) if False else el + 8), m.rws(el + 0xC))
        frame_idx = (sl["fptr"] - sl["f0"]) // 3 - 1
        quads = M.slot_quads(doc, sl["ts"], sl["ca"], frame_idx, pos, idx, cam, "map")
        touched = [i for i in range(0x400) if m.rw(OT + 4 * i) != 0]
        if quads:
            ax, ay = quads[0]["anchor"][0] - cam[0], quads[0]["anchor"][1] - cam[1]
            if not (-128 <= ax <= 448 and -128 <= ay <= 368):
                quads = []                      # the anchor cull: the whole element is dropped
        elev = M.slot_quads(doc, sl["ts"], sl["ca"], frame_idx, pos, idx, cam, "map")[0]["sortkey"][0]
        ok = (n == len(quads)) and (touched == ([elev] if n else []))
        for i in range(n):
            p = PRIMS + 0x28 * i
            verts = [(m.rhs(p + 0x08), m.rhs(p + 0x0A)), (m.rhs(p + 0x10), m.rhs(p + 0x12)), (m.rhs(p + 0x18), m.rhs(p + 0x1A)), (m.rhs(p + 0x20), m.rhs(p + 0x22))]
            code = m.rb(p + 0x07); tp = m.rh(p + 0x16)
            semi = (code >> 1) & 1; abr = (tp >> 5) & 3
            q = quads[i]
            ok &= verts == [tuple(c) for c in q["corners"]] and (q["mode"] is not None) == bool(semi) and (q["mode"] is None or q["mode"] == abr)
        nel += 1; nquads += n; bad += (not ok)
        print("  slot", idx, "record", sl["recidx"], "table", sl["ts"], "anim", sl["ca"], "frame", frame_idx, "pos", pos, "emitted", n, "model quads", len(quads), "OT touched", touched, "model elev", elev, "OK" if ok else "MISMATCH")
    print("map", map_id, "cam", cam, "elements", nel, "quads", nquads, "mismatching elements", bad)
check(163, (220, 40)); check(391, (836, 400)); check(391, (836, 640))
