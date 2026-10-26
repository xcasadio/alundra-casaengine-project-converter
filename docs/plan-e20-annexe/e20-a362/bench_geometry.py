"""bench_geometry.py - E20.A362 T1: the position of the witness (n2) once the ground of the hero is counted.

T0 (d2) (sword_geometry.py) chose the hero at (176, 100, 32). T1 found that the hero cannot stand there: his body (21 x 15, local (0.5, 0.5)) spans y 93..108 at y = 100,
over the row 5 of the cells (y 80..95, a wall of height 5 = 80 px), so the ground under his footprint is 80, not 32 (PlaceHero then Hero.PosZ = 80), and the box of his sword (z 80..) is above the
body of the lid (z 32..76). The overlap window of T0 (d2) (hero x 161..236, y 86..107, z 18..75) holds for y = 104..107 too, where the footprint (y - 7 .. y + 8) stays in the row 6 (y 96..111)
and in the row 7 (y 112..127), both walkable at height 2. This script re-runs the model of sword_geometry.py (same functions, same data) at the chosen position (176, 105, 32) and writes the centre of the
overlap at the first tick of the stroke (the three sparks) and the controls that must not overlap. It reads no run of the DLL.
usage: bench_geometry.py"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sword_geometry as g  # noqa: E402

ONE = g.ONE


def main():
    fr = g.frames_right()
    body = (189, 100, 32, 48, 8, 44)
    hero = (176, 105, 32)
    print("chosen position of the witness (n2): hero at %s facing east (TargetDirection 24)" % (hero,))
    for f in fr:
        print("  ticks %d-%d (offset %d,%d,%d): %s" % (f[0], f[1], f[2], f[3], f[4], "OVERLAPS" if g.overlap(*hero, f, body) else "no overlap"))
    assert g.overlap(*hero, fr[0], body)
    _, _, ox, oy, oz, w, d, h = fr[0]
    centre = (g.axis_centre((hero[0] + ox) * ONE, g.size(w), body[0] * ONE, g.size(body[3])),
              g.axis_centre((hero[1] + oy) * ONE, g.size(d), body[1] * ONE, g.size(body[4])),
              g.axis_centre((hero[2] + oz) * ONE, g.size(h), body[2] * ONE, g.size(body[5])))
    print("centre of the overlap at the first tick, raw 16.16: %s = (%.5f, %.5f, %.5f) px" % (centre, centre[0] / ONE, centre[1] / ONE, centre[2] / ONE))
    # controls: no frame of the stroke may overlap. The rows of PreloadedAnims are the four facings; the row 3 is the east one used above. The first T1 run showed the DLL taking the row 1 for the facing 16 (north:
    # a hit at the tick 12 of the stroke, the first tick of the row 1 that overlaps below), so the facing 8 (west) is the row 2, where no frame overlaps.
    hero_record = g.bank.load_hero_record()
    rows = hero_record["AnimSets"][0x10]["PreloadedAnims"]

    def frames_of(row):
        t = 1
        out = []
        for f in rows[row]["Frames"]:
            if f["IsTransitionFrame"]:
                continue
            cd = f["CollisionData"]
            ticks = f["Delay"] & 0x7F
            out.append((t, t + ticks - 1, cd["OffsetX"], cd["OffsetY"], cd["OffsetZ"], cd["Width"], cd["Depth"], cd["Height"]))
            t += ticks
        return out

    for name, pos, row, direction in (("same place, facing west", (176, 105, 32), 2, 8), ("30 px south, facing east", (176, 135, 32), 3, 24)):
        over = [("%d-%d" % (f[0], f[1])) for f in frames_of(row) if g.overlap(*pos, f, body)]
        print("  control %-26s %s direction %d: frames that overlap: %s" % (name, pos, direction, over if over else "none"))
        assert not over


if __name__ == "__main__":
    main()
