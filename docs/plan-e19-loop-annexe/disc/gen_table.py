"""Read-only. Builds the per-track markdown table of notes.md from loops.tsv (table.md)."""
import csv, os
HERE = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(HERE, "loops.tsv"), encoding="utf-8"), delimiter="\t"))
out = ["| idx | kind | LS frame (tick) | intro s | J1 = loop start (sample) | J2 = file end | loop frames (P2) | loop s | today Frames | iter 1 differs | used by (map entry / 0xA7) |",
       "|---|---|---|---|---|---|---|---|---|---|---|"]
for r in rows:
    maps = r["used_by_map_entry"]
    nmaps = len(maps.split(",")) if maps else 0
    a7 = r["used_by_0xA7"]
    na7 = a7.count(")")
    use = []
    if nmaps:
        use.append("%d maps%s" % (nmaps, (" (" + maps + ")") if nmaps <= 3 else ""))
    if na7:
        use.append("%d A7 sites" % na7)
    use = "; ".join(use) or "none"
    if r["idx"] == "45":
        use += " (map 476 value 45 = stop, not this track)"
    if r["kind"].startswith("loop"):
        out.append("| %s | loop | %s (%s) | %s | %s (%s) | %s | %s | %s | %s | %s | %s |" % (
            r["idx"], r["LS_frame"], r["LS_tick"], r["intro_s"], r["J1_frame"], r["J1_sample"], r["J2_frame"],
            r["P2_frames"], r["loop_s"], r["bgm_json_Frames_today"],
            "**yes**" if r["iter1_differs_from_steady_state"] == "yes" else "no", use))
    else:
        out.append("| %s | one-shot (end frame %s) | - | - | - | %s (+ release) | - | - | %s | - | %s |" % (
            r["idx"], r["end_frame"], int(r["end_frame"]) + 1, r["bgm_json_Frames_today"], use))
open(os.path.join(HERE, "table.md"), "w", encoding="utf-8").write("\n".join(out) + "\n")
print("\n".join(out))
