"""(1) Linear census of every SEQ (no jump taken): CC 99 values, count-latch candidates (CC 98 / CC 6 after 99=20),
controllers used, running-status at the loop-position event, ticks to the loop position.
(2) Compare seqsim (binary and analyser variants) with loop-disc/loops.tsv column by column."""
import csv, os
from collections import Counter
import seqsim
from seqsim import Sim, seq_data, run

HERE = os.path.dirname(os.path.abspath(__file__))
TSV = os.path.join(os.path.dirname(HERE), "loop-disc", "loops.tsv")


class Lin(Sim):
    def do_jump(self):  # linear walk: record, never jump
        self.jumps.append(self.frame)


cen = {}
for n in range(1, 47):
    off, data = seq_data(n)
    s = Lin(data)
    s.lcount = 1
    ccs = Counter()
    s.ctls = ccs
    orig_cc = s.cc

    def cc(start, c, s=s, ccs=ccs, orig=orig_cc):
        v = s.d[s.p]
        ccs[(c, v) if c == 99 else c] += 1
        orig(start, c)
    s.cc = cc
    # tick accounting: walk events linearly
    tick = 0
    ls_tick = None
    steps = 0
    while s.alive and steps < 10 ** 6:
        if s.ls_marker is not None and ls_tick is None and s.p == s.loop:
            ls_tick = tick
            b = s.d[s.p]
            ls_running = not (b & 0x80)
            ls_byte = b
        s.event()
        tick += s.D // 10
        steps += 1
    first_delta_tick = Sim(data).D // 10
    cen[n] = dict(cc99=sorted((k[1], v) for k, v in ccs.items() if isinstance(k, tuple)),
                  ctl=sorted((k, v) for k, v in ccs.items() if not isinstance(k, tuple)),
                  latch=[e[3] for e in s.ev if e[2] == "CNT"],
                  ls_tick=None if ls_tick is None else ls_tick + first_delta_tick,
                  ls_running=None if ls_tick is None else ls_running,
                  ls_byte=None if ls_tick is None else hex(ls_byte))

for n in range(1, 47):
    print(n, cen[n])

print("\n--- comparison with loops.tsv ---")
rows = {int(r["idx"]): r for r in csv.DictReader(open(TSV), delimiter="\t")}
bad = 0
for n in range(1, 47):
    _, b = run(n, "bin")
    _, a = run(n, "ana")
    r = rows[n]
    mine = {}
    if b.jumps:
        mine = {"LS_frame": b.loop_first, "J1_frame": b.jumps[0], "J2_frame": b.jumps[1],
                "LS_tick": cen[n]["ls_tick"]}
        mine_a = {"LS_frame": a.loop_first, "J1_frame": a.jumps[0], "J2_frame": a.jumps[1]}
        starts = [b.loop_first] + b.jumps
        mine["P1_frames"], mine["P2_frames"], mine["P3_frames"] = [starts[k + 1] - starts[k] for k in range(3)]
        sa = [a.loop_first] + a.jumps
        mine_a["P1_frames"], mine_a["P2_frames"], mine_a["P3_frames"] = [sa[k + 1] - sa[k] for k in range(3)]
        mine_a["J1_sample"] = a.jumps[0] * 735
        mine_a["J2_sample"] = a.jumps[1] * 735
    else:
        mine = {"end_frame": b.end_frame}
        mine_a = {"end_frame": a.end_frame}
    for k, v in mine.items():
        tv = r.get(k)
        if str(v) != str(tv):
            bad += 1
            print("  idx %d %s: tsv=%s mine(bin)=%s mine(ana)=%s" % (n, k, tv, v, mine_a.get(k)))
    for k, v in mine_a.items():
        tv = r.get(k)
        if tv is not None and str(v) != str(tv):
            print("  idx %d %s: tsv=%s mine(ana)=%s" % (n, k, tv, v))
print("mismatches vs bin:", bad)
