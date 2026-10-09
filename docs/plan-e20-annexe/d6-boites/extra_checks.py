"""D6 extra checks, on the same bank set as d6_boxes.py:
 A. frame classes vs the binary's frame rule (UpdateAnimation 0x80038BF4-0x80038C00: byte0 bit 0x80 clear = not a
    displayed frame): byte0 bit 7, IsTransitionFrame, number of images, record, per frame;
 B. zero-delay displayed frames (Delay & 0x7F == 0) and their records;
 C. TICK-LEVEL proof: for every animation and every logical tick k in [0, T] (T = sum of the delays), the box of the
    frame the BINARY shows at tick k (last displayed frame whose cumulative start <= k) against the box the EXPORT gives at
    tick k with the rule a DLL can apply from AnimatedSpriteComponent.LogicalTick (last keyframe whose round(time*50) <= k);
 D. the engine's LogicalDurationTicks (round(DurationSeconds*50), at least 1) vs the integer sum of the delays.
Writes out/extra_checks.txt. Run: python -B extra_checks.py
"""
import sys, os, json, collections
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import d6_boxes as d

banks, occ, _, _ = d.read_all_banks()
idx, _ = d.index_anim2d()
# raw byte0 is needed: compact_frame keeps Delay (= byte0) as the first element.

A = collections.Counter()
B = collections.Counter()
C = collections.Counter()
Dd = collections.Counter()
c_examples = []
zero_delay_list = []
d_examples = []
for key, b in banks.items():
    for a, dirs in enumerate(b["anims"]):
        for di, frames in enumerate(dirs):
            if not frames:
                continue
            name = "bank%s_anim%d_%s" % (key, a, d.DIRS[di])
            # A / B
            disp = []  # (start_tick, six or None, nq)
            t = 0
            for fi, (delay, nq, six, co, lo, hi, trans) in enumerate(frames):
                bit7 = bool(delay & 0x80)
                A[("transition=%s" % trans, "bit7=%s" % bit7, "images=%s" % (nq > 0), "record=%s" % (six is not None))] += 1
                if trans:
                    continue
                if nq > 0:
                    dl = delay & 0x7F
                    if dl == 0:
                        B["zero_delay_displayed_frames"] += 1
                        zero_delay_list.append((name, fi, "last_displayed" if False else "", six))
                        if six is not None:
                            B["zero_delay_with_record"] += 1
                    disp.append((t, six, fi, dl))
                    t += dl
            T = t
            if not disp:
                continue
            path = idx.get(name)
            if path is None:
                continue
            ad = d.jload(path)
            kfs = []
            for k in ad.get("collision_keyframes") or []:
                fx = k.get("fixtures") or []
                six = d.fixture_box(fx[0])[0] if fx else None
                kfs.append((int(round(k["time_seconds"] * 50)), six))
            # D: duration in ticks
            if "duration_seconds" in ad:
                pass
            # engine duration = max time over the tracks; recompute as float32 sum like the converter
            cum = d.f32(0.0)
            for (delay, nq, six, co, lo, hi, trans) in frames:
                if nq > 0:
                    cum = d.f32(cum + d.f32(d.f32(float(delay & 0x7F)) * d.PSX))
            eng = max(1, int(round(cum * 50 + 1e-9))) if cum > 0 else 0
            Dd["animations"] += 1
            if eng != T and not (cum <= 0 and T == 0):
                Dd["duration_ticks_differ"] += 1
                if len(d_examples) < 5:
                    d_examples.append((name, eng, T))
            # C: tick level
            starts = [x[0] for x in disp]
            bi = 0
            ki = -1
            has_any_record = any(x[1] is not None for x in disp)
            for k in range(0, T + 1):
                while bi + 1 < len(disp) and starts[bi + 1] <= k:
                    bi += 1
                while ki + 1 < len(kfs) and kfs[ki + 1][0] <= k:
                    ki += 1
                bin_box = disp[bi][1]
                exp_box = kfs[ki][1] if ki >= 0 else None
                if has_any_record or exp_box is not None:
                    C["ticks_compared"] += 1
                    if bin_box == exp_box:
                        C["equal"] += 1
                    else:
                        C["different"] += 1
                        if len(c_examples) < 10:
                            c_examples.append((name, k, bin_box, exp_box))
            if has_any_record:
                C["animations_with_record"] += 1

with open(os.path.join(HERE, "out", "extra_checks.txt"), "w", encoding="utf-8") as fh:
    fh.write("A. frame classes (transition, byte0 bit7, has images, has record) -> count, canonical banks\n")
    for k, v in sorted(A.items()):
        fh.write("  %s : %d\n" % (k, v))
    fh.write("B. %s\n" % dict(B))
    for z in zero_delay_list:
        fh.write("   zero-delay displayed frame: anim=%s frame=%d record=%s\n" % (z[0], z[1], z[3]))
    fh.write("C. tick-level %s\n" % dict(C))
    for e in c_examples:
        fh.write("   C-diff example %s\n" % (e,))
    fh.write("D. %s\n" % dict(Dd))
    for e in d_examples:
        fh.write("   D-diff example %s\n" % (e,))
print(open(os.path.join(HERE, "out", "extra_checks.txt"), encoding="utf-8").read())
