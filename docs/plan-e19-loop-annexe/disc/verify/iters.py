"""Iteration 1 vs 2 vs 3 comparison with the independent driver (seqsim.py, binary variant).
For each looping track:
  - event-position sequences of iterations 1, 2, 3 identical?
  - note-ons whose (vel, CC7, CC10, prog, bend) differ between iteration 1 and 2 (count per channel / attribute)
  - frame-offset jitter (iteration-relative) 1 vs 2 and 2 vs 3
  - musical state (CCs, programs, bends, tempo) at loop entry vs J1, and J1 vs J2
  - notes sounding (on without off) at entry, at J1, at J2 (+ whether J1's extra notes are released later, and when)
"""
import json, sys
from collections import Counter
from seqsim import run

out = []
for n in [int(a) for a in sys.argv[1:]] or range(1, 47):
    _, s = run(n, "bin", njumps=3)
    if not s.jumps:
        out.append({"idx": n, "kind": "one-shot", "end": s.end_frame})
        continue
    i0 = s.entry_evi
    i1, i2, i3 = s.jump_evi
    starts = [s.loop_first] + s.jumps
    its = [s.ev[i0:i1], s.ev[i1:i2], s.ev[i2:i3]]
    pos = [[e[1] for e in it] for it in its]
    r = {"idx": n, "LS": s.loop_first, "J": s.jumps, "P": [starts[k + 1] - starts[k] for k in range(3)]}
    r["seq_identical_12"] = pos[0] == pos[1]
    r["seq_identical_23"] = pos[1] == pos[2]

    def ndiff(a, b):
        c = Counter()
        ex = []
        for x, y in zip(a, b):
            if x[2] == "on" and y[2] == "on" and x[3] != y[3]:
                (ch, k, v, vol, pan, prog, bend), (_, _, v2, vol2, pan2, prog2, bend2) = x[3], y[3]
                for nm, p, q in (("vel", v, v2), ("vol", vol, vol2), ("pan", pan, pan2), ("prog", prog, prog2),
                                 ("bend", bend, bend2)):
                    if p != q:
                        c["ch%d %s %s->%s" % (ch, nm, p, q)] += 1
        return dict(c)

    def jit(a, sa, b, sb):
        c = Counter()
        for x, y in zip(a, b):
            dx = x[0] - sa
            dy = y[0] - sb
            if dx != dy:
                c[dy - dx] += 1
        return dict(c)

    r["notes_differ_12"] = ndiff(its[0], its[1])
    r["notes_differ_23"] = ndiff(its[1], its[2])
    r["jitter_12"] = jit(its[0], starts[0], its[1], starts[1])
    r["jitter_23"] = jit(its[1], starts[1], its[2], starts[2])
    e_cs, e_on, e_T = s.entry_snap
    j1_cs, j1_on, j1_T = s.jump_snap[0]
    j2_cs, j2_on, j2_T = s.jump_snap[1]

    def csdiff(a, b):
        ks = set(a) | set(b)
        return sorted(["%s:%s->%s" % (k, a.get(k), b.get(k)) for k in ks if a.get(k) != b.get(k)])

    r["state_entry_vs_J1"] = csdiff(e_cs, j1_cs)
    r["state_J1_vs_J2"] = csdiff(j1_cs, j2_cs)
    r["T_entry_J1_J2"] = [e_T, j1_T, j2_T]
    r["on_entry"] = sorted(e_on)
    r["on_J1"] = sorted(j1_on)
    r["on_J2"] = sorted(j2_on)
    # notes on at J1 but not at entry: when released in iteration 2 (frames after J1), or never
    extra = []
    for key in set(j1_on) - set(e_on):
        rel = None
        for e in its[1]:
            if e[2] == "off" and e[3] == key:
                rel = e[0] - s.jumps[0]
                break
            if e[2] == "on" and (e[3][0], e[3][1]) == key:
                rel = ("retrigger", e[0] - s.jumps[0])
                break
        extra.append((key, rel))
    r["extra_held_at_J1"] = sorted(extra, key=str)
    missing = sorted(set(e_on) - set(j1_on))
    r["held_at_entry_not_J1"] = missing
    out.append(r)

with open("iters.jsonl", "w") as f:
    for r in out:
        f.write(json.dumps(r, default=str) + "\n")
for r in out:
    if r.get("kind") == "one-shot":
        print(r["idx"], "one-shot end", r["end"])
        continue
    differ = bool(r["notes_differ_12"]) or bool(r["extra_held_at_J1"]) or bool(r["held_at_entry_not_J1"])
    print(r["idx"], "P", r["P"], "seq12", r["seq_identical_12"], "seq23", r["seq_identical_23"],
          "DIFF" if differ else "same",
          "notes12", r["notes_differ_12"], "notes23", r["notes_differ_23"],
          "extraJ1", r["extra_held_at_J1"], "lostJ1", r["held_at_entry_not_J1"],
          "J1vsJ2", r["state_J1_vs_J2"], "onJ1==onJ2", sorted(r["on_J1"]) == sorted(r["on_J2"]),
          "jit12", r["jitter_12"], "jit23", r["jitter_23"])
