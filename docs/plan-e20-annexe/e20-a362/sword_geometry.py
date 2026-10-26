"""sword_geometry.py - E20.A362 T0 (d2): the position and the facing of the hero from which the basic sword (anim 0x10) overlaps the body of the lid (witness (n2)).

Data: the hero's sprite record read by docs/plan-e20-annexe/e20-c1/oracle/tools/bank.py (data-extracted/data/map_alundra.json, SpriteRecords[0]), animation set 0x10,
the frame boxes of the 'right' animation (the facing 24, east). Rule: Alundra/Combat/HitPass.cs (AxisOverlaps, widened by one raw unit), the box = Pos + offset with
sizes (n << 16) - 1, the body = Pos + Mod with sizes (n << 16) - 1 (the same convention as the box).
Check of the model: it must give the contact at the first tick for dx -9..38 against the Beannoid body of the c1a oracle (offset -10, width 20), as
docs/plan-e20-annexe/e20-c1/oracle/out/ writes it. Then the lid: body x 189..237, y 100..108, z 32..76 (the record's (216, 104, 32) moved by C[3] to (212, 100, 32),
header offset (-23, 0, 0), size 48 x 8 x 44).
usage: sword_geometry.py"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.normpath(os.path.join(HERE, "..", "e20-c1", "oracle", "tools"))
sys.path.insert(0, TOOLS)
import bank  # noqa: E402

ONE = 1 << 16


def axis_overlaps(box_pos, box_size, body_pos, body_size):
    d = box_pos - body_pos
    return d < body_size + 1 if d >= 0 else -d < box_size + 1


def axis_centre(box_pos, box_size, body_pos, body_size):
    low = max(box_pos, body_pos)
    high = min(box_pos + box_size, body_pos + body_size)
    s = low + high
    return -((-s) // 2) if s < 0 else s // 2  # truncated toward zero, as HitPass.AxisCentre


def size(n):
    return (n << 16) - 1


def frames_right():
    hero = bank.load_hero_record()
    an = hero["AnimSets"][0x10]["PreloadedAnims"][3]
    out = []
    t = 1
    for f in an["Frames"]:
        if f["IsTransitionFrame"]:
            continue
        cd = f["CollisionData"]
        ticks = f["Delay"] & 0x7F
        out.append((t, t + ticks - 1, cd["OffsetX"], cd["OffsetY"], cd["OffsetZ"], cd["Width"], cd["Depth"], cd["Height"]))
        t += ticks
    return out


def overlap(hx, hy, hz, frame, body):
    """hero position in px, frame = (.., ox, oy, oz, w, d, h), body = (x, y, z, sx, sy, sz) in px."""
    _, _, ox, oy, oz, w, d, h = frame
    bx, by, bz, sx, sy, sz = body
    return (axis_overlaps((hx + ox) * ONE, size(w), bx * ONE, size(sx))
            and axis_overlaps((hy + oy) * ONE, size(d), by * ONE, size(sy))
            and axis_overlaps((hz + oz) * ONE, size(h), bz * ONE, size(sz)))


def main():
    fr = frames_right()
    print("hero anim 0x10, 'right' (east, TargetDirection 24): frames (first tick, last tick, offset x y z, size w d h)")
    for f in fr:
        print("  ticks %d-%d  offset (%d,%d,%d)  size %dx%dx%d" % f)
    print()

    # model check against the c1a oracle sweep: Beannoid body offset (-10,-7,0), size 20x14x8, hero at (0,0,0), target at (dx, 0, 0)
    first = fr[0]
    hits = [dx for dx in range(-30, 80)
            if overlap(0, 0, 0, first, (dx - 10, 0 - 7, 0, 20, 14, 8))]
    print("model check, first tick (frame 0) against the Beannoid body: contact for dx %d..%d (the oracle: -9..38)" % (min(hits), max(hits)))
    assert (min(hits), max(hits)) == (-9, 38)
    print()

    body = (189, 100, 32, 48, 8, 44)
    print("lid body (x, y, z, sx, sy, sz) =", body)
    xs = [hx for hx in range(100, 300) if axis_overlaps(hx * ONE, size(first[5]), body[0] * ONE, size(body[3]))]
    ys = [hy for hy in range(60, 160) if axis_overlaps((hy + first[3]) * ONE, size(first[6]), body[1] * ONE, size(body[4]))]
    zs = [hz for hz in range(0, 120) if axis_overlaps((hz + first[4]) * ONE, size(first[7]), body[2] * ONE, size(body[5]))]
    print("frame 0 (tick 1 of the stroke), hero facing east: hero x in %d..%d, y in %d..%d, z in %d..%d overlap the lid body" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))
    hero = (176, 100, 32)
    for f in fr:
        print("  hero at %s, ticks %d-%d (offset %d,%d,%d): %s" % (hero, f[0], f[1], f[2], f[3], f[4], "OVERLAPS" if overlap(*hero, f, body) else "no overlap"))
    assert overlap(*hero, fr[0], body)
    print()
    print("margins of the chosen position (176, 100, 32): x %d inside from the low edge (first x = %d) and %d from the high edge (last x = %d); y %d and %d (first y = %d, last y = %d); z 32 in %d..%d"
          % (176 - min(xs), min(xs), max(xs) - 176, max(xs), 100 - min(ys), max(ys) - 100, min(ys), max(ys), min(zs), max(zs)))
    _, _, ox, oy, oz, w, d, h = fr[0]
    centre = (axis_centre((hero[0] + ox) * ONE, size(w), body[0] * ONE, size(body[3])),
              axis_centre((hero[1] + oy) * ONE, size(d), body[1] * ONE, size(body[4])),
              axis_centre((hero[2] + oz) * ONE, size(h), body[2] * ONE, size(body[5])))
    print("centre of the overlap at the first tick (where the three sparks appear), raw 16.16: %s = (%.5f, %.5f, %.5f) px" % (centre, centre[0] / ONE, centre[1] / ONE, centre[2] / ONE))
    # a no-overlap control on the same frame 0: the hero 50 px further west / 20 px further north
    for name, pos in (("50 px west", (126, 100, 32)), ("20 px north (y 80)", (176, 80, 32)), ("3 px above the body in z (z 80)", (176, 100, 80))):
        print("  control %-32s %s: frame 0 %s" % (name, pos, "overlaps" if overlap(*pos, fr[0], body) else "does not overlap"))


if __name__ == "__main__":
    main()
