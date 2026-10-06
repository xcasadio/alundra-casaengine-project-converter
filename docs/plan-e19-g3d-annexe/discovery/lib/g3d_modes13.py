"""g3d_modes13.py: values of the native creators of modes 1 (attached, 0x8003BE74) and 3 (detached, 0x8003BFE8) and of the Move rules of the modes 1, 2 and 3 (0x8003C284),
produced by the REAL code in the interpreter (lib/fxm.py). Output: g3d-modes13.tsv

Row kinds:
  C  <scenario> <kind> <slot> <mode> <x> <y> <z> <fx> <fy> <fz> <off x y z> <depth offset> <entity key>      right after the creator (before any update)
  M  <scenario> <update> <mode> <x> <y> <z> <depth_hex> <active>                                               after each update (the entity of the scenario is rewritten before it)
  E  <scenario> <update> <status> <x> <y> <z> <depthkey>                                                       the entity as it was set before that update
"""
import sys, os
H = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, H)
import fxm

M32 = 0xFFFFFFFF
E1 = 0x80180000          # fake entity 1 (fxm.FAKE_ENT)


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def run(rows, name, kind, entity_steps, *, forces=(0, 0, 0), doff=0, off=(0, 0, 0), start=(0, 0, 0), updates=6):
    """entity_steps[u] = (status, x, y, z, depthkey) set before update u (u = 0 .. updates-1); the entity of the creation is entity_steps[0]."""
    f = fxm.FxMachine(476)
    st, ex, ey, ez, dk = entity_steps[0]
    f.set_entity(1, E1, ex, ey, ez, status=st, depthkey=dk)
    if kind == "attached":
        ptr = f.create_attached(0, 14, 0, E1, doff, off[0], off[1], off[2])
    else:
        ptr = f.create_detached(0, 14, 0, E1, doff, start[0], start[1], start[2])
    assert ptr, "creation failed"
    idx = (ptr - fxm.POOL) // 0x80
    m = f.m
    a = f.slot_addr(idx)
    m.ww(a + 0x54, forces[0] & M32); m.ww(a + 0x58, forces[1] & M32); m.ww(a + 0x5C, forces[2] & M32)   # native code writes the forces after the creator
    s = f.slot(idx)
    rows.append("\t".join(["C", name, kind, str(idx), str(s["mode"]), str(s["x"]), str(s["y"]), str(s["z"]), *[str(v) for v in s["forces"]], *[str(v) for v in s["off"]],
                           str(s["doff"]), "1"]))
    for u in range(updates):
        st, ex, ey, ez, dk = entity_steps[min(u, len(entity_steps) - 1)]
        f.set_entity(1, E1, ex, ey, ez, status=st, depthkey=dk)
        rows.append("\t".join(["E", name, str(u), str(st), str(ex), str(ey), str(ez), str(dk)]))
        f.tick()
        s = f.slot(idx)
        active = idx in f.active()
        rows.append("\t".join(["M", name, str(u), str(s["mode"]), str(s["x"]), str(s["y"]), str(s["z"]), "0x%08X" % (s["depth"] & M32), "1" if active else "0"]))


def main(check):
    rows = []
    P = 1 << 16
    ent = lambda st, x, y, z, k: (st, x * P, y * P, z * P, k)
    # attached: the position is recomputed from the entity every update (+ the offsets), the forces are added on top, never accumulated
    walk = [ent(2, 100 + u, 50, 3, (20 + u) << 20) for u in range(6)]
    run(rows, "A1-follow", "attached", walk, doff=-2 * P, off=(4 * P, -3 * P, 10 * P))
    run(rows, "A2-forces-on-top", "attached", walk, doff=3 * P, off=(0, 0, 0), forces=(P // 2, -P // 4, 0))
    run(rows, "A3-entity-gone", "attached", [ent(2, 100, 50, 3, 7 << 20), ent(2, 101, 50, 3, 7 << 20), ent(0, 102, 50, 3, 7 << 20)], doff=P, off=(P, P, P), forces=(P, 0, 0))
    run(rows, "A4-entity-flagged", "attached", [ent(2, 100, 50, 3, 7 << 20), ent(4, 101, 50, 3, 7 << 20), ent(4, 102, 50, 3, 7 << 20)], doff=P, off=(P, P, P), forces=(P, 0, 0))
    run(rows, "A5-status-1-and-3", "attached", [ent(1, 100, 50, 3, 1 << 20), ent(3, 101, 50, 3, 2 << 20), ent(2, 102, 50, 3, 3 << 20)], off=(2 * P, 0, 0))
    # detached: the position is its own (created at x, y, z), the forces move it, the depth key follows the entity while it lives
    run(rows, "D1-fly", "detached", walk, doff=-2 * P, start=(200 * P, 80 * P, 5 * P), forces=(0, -P // 2, P))
    run(rows, "D2-entity-flagged", "detached", [ent(2, 100, 50, 3, 9 << 20), ent(2, 101, 50, 3, 9 << 20), ent(4, 102, 50, 3, 9 << 20)], start=(200 * P, 80 * P, 5 * P), forces=(P, 0, 0))
    run(rows, "D3-entity-gone", "detached", [ent(2, 100, 50, 3, 9 << 20), ent(0, 101, 50, 3, 9 << 20)], start=(200 * P, 80 * P, 5 * P), forces=(P, 0, 0))
    text = "\n".join(rows) + "\n"
    path = os.path.join(H, "g3d-modes13.tsv")
    if check:
        print("MATCH" if open(path, encoding="utf-8").read() == text else "DIFFERENT")
    else:
        open(path, "w", encoding="utf-8", newline="\n").write(text)
        print("wrote", path, len(rows), "rows")


if __name__ == "__main__":
    main("--check" in sys.argv)
