"""Combine the four Debug samples and four Release samples (2 runs x 2 passes) per music-keeping transition."""
import csv
import statistics

RUNS = {"Debug": ["out-debug-sleep20", "out-debug"], "Release": ["out-release-sleep20", "out-release"]}


def per_transition(out):
    frames = {int(f["frame"]): f for f in csv.DictReader(open(f"{out}/frames.csv"))}
    trans = list(csv.DictReader(open(f"{out}/transitions.csv")))
    last = max(frames)
    res = []
    for i, t in enumerate(trans):
        a = int(t["trigger_frame"])
        b = int(trans[i + 1]["trigger_frame"]) if i + 1 < len(trans) else last + 1
        ints = [float(frames[n]["interval_ms"]) for n in range(a + 1, b + 1) if n in frames]
        fills = [float(frames[n]["fill_gap_ms"]) for n in range(a, b) if n in frames]
        res.append(dict(key=(int(t["from"]), int(t["to"])), setup=t["setup"] == "1", tracks=(t["track_from"], t["track_to"]),
                        load=float(t["load_ms"]), upd=float(frames[a]["update_ms"]), tick=max(ints), fill=max(fills),
                        gap2=max(fills[k] + fills[k + 1] for k in range(len(fills) - 1)),
                        n100=sum(1 for x in ints if x > 100), s100=sum(x for x in ints if x > 100), music=(t["music_before"], t["music_after"])))
    return res


data = {cfg: [r for out in outs for r in per_transition(out)] for cfg, outs in RUNS.items()}
order = []
for r in data["Debug"]:
    if not r["setup"] and r["key"] not in order:
        order.append(r["key"])

print("| transition | track | Debug: load ms (min-max) | Debug: max Tick ms (median / max) | Debug: max 2-frame fill gap ms | Release: load ms (min-max) | Release: max Tick ms (median / max) | Release: max 2-frame fill gap ms | Ticks > 100 ms per sample |")
print("|---|---|---|---|---|---|---|---|---|")
for key in order:
    cells = []
    n100 = set()
    track = None
    for cfg in ("Debug", "Release"):
        rs = [r for r in data[cfg] if r["key"] == key and not r["setup"]]
        track = rs[0]["tracks"][0]
        loads = [r["load"] for r in rs]
        ticks = [r["tick"] for r in rs]
        gaps = [r["gap2"] for r in rs]
        n100.update(r["n100"] for r in rs)
        cells.append(f"{min(loads):.0f}-{max(loads):.0f} | {statistics.median(ticks):.0f} / {max(ticks):.0f} | {max(gaps):.0f}")
    print(f"| {key[0]} -> {key[1]} | {track} | {cells[0]} | {cells[1]} | {'/'.join(str(x) for x in sorted(n100))} |")

for cfg in ("Debug", "Release"):
    keep = [r for r in data[cfg] if not r["setup"]]
    print(f"\n{cfg}: {len(keep)} samples; worst load {max(r['load'] for r in keep):.1f} ms; worst Tick {max(r['tick'] for r in keep):.1f} ms; "
          f"worst fill gap {max(r['fill'] for r in keep):.1f} ms; worst 2-frame fill gap {max(r['gap2'] for r in keep):.1f} ms; "
          f"median of per-sample max Tick {statistics.median(r['tick'] for r in keep):.1f} ms; samples with max Tick > 278.6 ms: "
          f"{sum(1 for r in keep if r['tick'] > 278.6)}; > 185.8 ms: {sum(1 for r in keep if r['tick'] > 185.8)}; > 95.9 ms: {sum(1 for r in keep if r['tick'] > 95.9)}")
    voices_kept = sum(1 for r in keep if r["music"][0] == r["music"][1])
    print(f"  music state (index + voice handle) identical before/after: {voices_kept}/{len(keep)}")
    setup = [r for r in data[cfg] if r["setup"]]
    print("  setup (track change) max Tick: " + ", ".join(f"{r['key'][0]}->{r['key'][1]} {r['tick']:.0f}" for r in setup))
