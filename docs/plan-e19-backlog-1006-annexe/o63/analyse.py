"""O-E19-63: per-transition frame statistics from the harness output (frames.csv, transitions.csv).

Usage: python analyse.py <out-dir> [label]
Window of a transition = frames [trigger, next trigger) (the last one runs to the end of the run).
- interval_ms(N) = Update-start(N) - Update-start(N-1): wall time of one Game.Tick (Update + Draw + Present/vsync).
- fill_gap_ms(N) = Update-end(N) - Update-end(N-1): the audio component updates last in base.Update, so this is the
  gap between two consecutive music-queue top-ups (the starvation-relevant number).
- gap2 = two consecutive fill gaps (a backend whose pending count is stale for one frame refills one frame later).
"""
import csv
import statistics
import sys

out = sys.argv[1]
label = sys.argv[2] if len(sys.argv) > 2 else out

frames = list(csv.DictReader(open(f"{out}/frames.csv")))
for f in frames:
    for k in ("interval_ms", "update_ms", "draw_ms", "fill_gap_ms"):
        f[k] = float(f[k])
    f["frame"] = int(f["frame"])
trans = list(csv.DictReader(open(f"{out}/transitions.csv")))
by_frame = {f["frame"]: f for f in frames}
last_frame = frames[-1]["frame"]

# idle baseline: frames before the first trigger, after the startup load has settled (frames 120..first trigger)
first_trigger = int(trans[0]["trigger_frame"])
idle = [f["interval_ms"] for f in frames if 120 <= f["frame"] < first_trigger]
print(f"# {label}")
print(f"idle before first trigger: median interval {statistics.median(idle):.2f} ms, max {max(idle):.2f} ms, n={len(idle)}")
print()
print("| pass | from -> to | tracks | load (UpdateWorld) ms | trigger-frame Update ms | max Tick ms | max fill gap ms | max 2-frame fill gap ms | Ticks > 100 ms | sum of Ticks > 100 ms | Ticks > 50 ms | settled median ms |")
print("|---|---|---|---|---|---|---|---|---|---|---|---|")
rows = []
for i, t in enumerate(trans):
    a = int(t["trigger_frame"])
    b = int(trans[i + 1]["trigger_frame"]) if i + 1 < len(trans) else last_frame + 1
    w = [by_frame[n] for n in range(a, b) if n in by_frame]
    # the stall of frame a shows in interval(a+1) (Update(a) is inside it) and in fill_gap(a)
    w_int = [by_frame[n]["interval_ms"] for n in range(a + 1, b + 1) if n in by_frame]
    w_fill = [by_frame[n]["fill_gap_ms"] for n in range(a, b) if n in by_frame]
    gap2 = max(w_fill[k] + w_fill[k + 1] for k in range(len(w_fill) - 1))
    over100 = [x for x in w_int if x > 100]
    over50 = [x for x in w_int if x > 50]
    settled = statistics.median(w_int[-60:])
    tr = f"{t['track_from']}->{t['track_to']}" + (" (setup)" if t["setup"] == "1" else "")
    row = dict(p=t["pass"], f=t["from"], to=t["to"], tr=tr, load=float(t["load_ms"]), upd=by_frame[a]["update_ms"],
               maxint=max(w_int), maxfill=max(w_fill), gap2=gap2, n100=len(over100), s100=sum(over100), n50=len(over50),
               settled=settled, setup=t["setup"] == "1")
    rows.append(row)
    print(f"| {row['p']} | {row['f']} -> {row['to']} | {tr} | {row['load']:.1f} | {row['upd']:.1f} | {row['maxint']:.1f} | "
          f"{row['maxfill']:.1f} | {row['gap2']:.1f} | {row['n100']} | {row['s100']:.1f} | {row['n50']} | {row['settled']:.2f} |")

keep = [r for r in rows if not r["setup"]]
print()
print(f"music-keeping transitions: {len(keep)}; worst max Tick {max(r['maxint'] for r in keep):.1f} ms; "
      f"worst max fill gap {max(r['maxfill'] for r in keep):.1f} ms; worst 2-frame gap {max(r['gap2'] for r in keep):.1f} ms; "
      f"worst load {max(r['load'] for r in keep):.1f} ms")
# top frames of each keep-transition window (to see whether the stall is one frame or several)
print()
print("largest three Ticks per music-keeping transition (ms):")
for i, t in enumerate(trans):
    if t["setup"] == "1":
        continue
    a = int(t["trigger_frame"])
    b = int(trans[i + 1]["trigger_frame"]) if i + 1 < len(trans) else last_frame + 1
    w = sorted(((by_frame[n]["interval_ms"], n - a) for n in range(a + 1, b + 1) if n in by_frame), reverse=True)[:3]
    print(f"  pass {t['pass']} {t['from']}->{t['to']}: " + ", ".join(f"{x:.1f} (+{d})" for x, d in w))
