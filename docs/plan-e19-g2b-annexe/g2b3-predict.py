"""E19.g G2b-3 (G2b3-0): prediction of the corner-track export, committed before any converter code.

Independent of the converter code: it reads data-extracted/ (the JSON of the extractor) and the current export only to
resolve the folder of each file name and the guard file's duration. Rules (plan section "E19.g G2b-3", G2b3-R1):
  - banks de-duplicated like the converter: map_alundra.json first, then map_N.json in ascending N, first occurrence wins;
  - an animation (4 directions per anim set: down, up, left, right) with at least one displayed quad gets a file
    `bank<key>_anim<set>_<dir>.anim2d`; frame times are the cumulative float32 sum of (Delay & 0x7F) * (1f / 50f), a frame
    without quad adds nothing; Hold/Chain animations (terminator code != 1) repeat the last displayed frame on the terminator;
  - part p of a frame is hidden when the frame has fewer than p + 1 quads;
  - a quad is plain or mirrored (not deformed) when X1 == X3, X2 == X4, Y1 == Y2, Y3 == Y4, |X2 - X1| == W and |Y3 - Y1| == H;
    anything else is deformed;
  - a part with at least one deformed quad gets a `Corners` track appended after all existing tracks (part order);
    keys at the times of the part's displayed frames, only when the state changes (initial state: disabled), no key for a hidden
    frame; an active key holds the four raw PS1 corners (y flipped) minus the part Position and source_offset = (-mx, -my) with
    mx = X1 > X2, my = Y1 > Y3; a disabled key follows a deformed frame by a plain one.
  - Position = ((minX + maxX) / 2 - ox, -(minY + maxY) / 2 + oy), ox = W / 2f - W / 2, oy = H / 2f - H / 2 (integer division).

Output (next to this file): g2b3-prediction.json. Per file, `tracks` is a list of {"part": "partN", "keys": [...]}, a key is
  [t, 0]                                          (disabled)   or
  [t, 1, tlx, tly, trx, try, blx, bly, brx, bry, sox, soy]     (enabled), t = float32 time written as its shortest decimal.
Also: the counters, the list of changed files (must be exactly the 1923 paths of predicted_anim2d_changes.tsv, else exit 1), the
guard file (Entities/Coffre d’Anzes/bankalundra_227_anim1_down.anim2d: keys per Corners track, first key, DurationSeconds of
today's export) and a self-check that every key time is the time of a Sprite key of the same part.

Run from anywhere:  python g2b3-predict.py [--check]   (--check does not write the JSON)
"""
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DATA = os.path.join(ROOT, "data-extracted", "data")
EXPORT_ENTITIES = os.path.join(ROOT, "alundra-project", "Entities")
TSV = os.path.join(HERE, "predicted_anim2d_changes.tsv")
OUT = os.path.join(HERE, "g2b3-prediction.json")
DIRECTIONS = ["down", "up", "left", "right"]
PSX = np.float32(1) / np.float32(50)
GUARD = "Entities/Coffre d’Anzes/bankalundra_227_anim1_down.anim2d"


def list_map_indices():
    indices = []
    for name in os.listdir(DATA):
        if name.startswith("map_") and name.endswith(".json"):
            stem = name[:-5][4:]
            if stem.isdigit():
                indices.append(int(stem))
    return sorted(indices)


def read_quad(q):
    return dict(w=q["Swidth"], h=q["Sheight"], x1=q["X1"], y1=q["Y1"], x2=q["X2"], y2=q["Y2"],
                x3=q["X3"], y3=q["Y3"], x4=q["X4"], y4=q["Y4"])


def read_animation(anim):
    if not isinstance(anim, dict) or not isinstance(anim.get("Frames"), list):
        return None
    frames = []
    for f in anim["Frames"]:
        if not isinstance(f, dict):
            continue
        quads = []
        images = f.get("Images")
        if isinstance(images, dict) and isinstance(images.get("Images"), list):
            quads = [read_quad(q) for q in images["Images"]]
        frames.append(dict(delay=f["Delay"], tl=f.get("TransformIndexLow", 0), quads=quads))
    return frames or None


def read_banks():
    banks = {}  # key -> list of anim sets, each a list of 4 animations (frames list or None)
    sources = [(os.path.join(DATA, "map_alundra.json"), True)]
    sources += [(os.path.join(DATA, "map_%d.json" % i), False) for i in list_map_indices()]
    for path, is_alundra in sources:
        with open(path, encoding="utf-8") as fh:
            doc = json.load(fh)
        info = doc.get("SpriteInfo")
        if not isinstance(info, dict) or not isinstance(info.get("SpriteRecords"), list):
            continue
        for rec in info["SpriteRecords"]:
            if not isinstance(rec, dict):
                continue
            sid = rec["Header"]["Sector5Id"]
            key = ("alundra_%d" if is_alundra else "%d") % sid
            if key in banks:
                continue
            if not isinstance(rec.get("AnimSets"), list):
                continue
            sets = []
            for aset in rec["AnimSets"]:
                dirs = [None] * 4
                if isinstance(aset, dict) and isinstance(aset.get("PreloadedAnims"), list):
                    for i, a in enumerate(aset["PreloadedAnims"][:4]):
                        dirs[i] = read_animation(a)
                sets.append(dirs)
            banks[key] = sets
    return banks


def is_deformed(q):
    plain = (q["x1"] == q["x3"] and q["x2"] == q["x4"] and q["y1"] == q["y2"] and q["y3"] == q["y4"]
             and abs(q["x2"] - q["x1"]) == q["w"] and abs(q["y3"] - q["y1"]) == q["h"])
    return not plain


def corner_state(q):
    min_x = min(q["x1"], q["x2"], q["x3"], q["x4"])
    max_x = max(q["x1"], q["x2"], q["x3"], q["x4"])
    min_y = min(q["y1"], q["y2"], q["y3"], q["y4"])
    max_y = max(q["y1"], q["y2"], q["y3"], q["y4"])
    ox = q["w"] / 2.0 - q["w"] // 2
    oy = q["h"] / 2.0 - q["h"] // 2
    cx = (min_x + max_x) / 2.0 - ox
    cy = -(min_y + max_y) / 2.0 + oy
    corners = []
    for x, y in ((q["x1"], q["y1"]), (q["x2"], q["y2"]), (q["x3"], q["y3"]), (q["x4"], q["y4"])):
        corners += [x - cx + 0.0, (-y) - cy + 0.0]
    mx = 1 if q["x1"] > q["x2"] else 0
    my = 1 if q["y1"] > q["y3"] else 0
    return (tuple(corners), (-mx, -my))


def fmt_time(t):
    return float(str(np.float32(t)))


def predict_animation(frames):
    """Returns (file produced, quads deformed, tracks) for one animation."""
    times = []
    cum = np.float32(0)
    for f in frames:
        times.append(cum)
        if f["quads"]:
            cum = np.float32(cum + np.float32(f["delay"] & 0x7F) * PSX)
    max_quads = max(len(f["quads"]) for f in frames)
    if max_quads == 0:
        return False, 0, []
    last = frames[-1]
    if not last["quads"]:
        code = last["delay"]
        if code == 1:
            loop = True
        elif (code & 0x80) == 0:
            loop = False  # hold or chain
        else:
            loop = False
    else:
        loop = True
    repeat_last = (not loop) and len(frames) >= 2 and not frames[-1]["quads"]
    deformed_count = 0
    tracks = []
    for p in range(max_quads):
        current = None  # disabled
        keys = []
        any_deformed = False
        for fi, f in enumerate(frames):
            terminal_repeat = repeat_last and fi == len(frames) - 1
            src = frames[len(frames) - 2] if terminal_repeat else f
            if p >= len(src["quads"]):
                continue
            q = src["quads"][p]
            d = is_deformed(q)
            if d and not terminal_repeat:
                deformed_count += 1
            any_deformed |= d
            state = corner_state(q) if d else None
            if state != current:
                t = fmt_time(times[fi])
                if state is None:
                    keys.append([t, 0])
                else:
                    c, so = state
                    keys.append([t, 1] + [float(v) for v in c] + [so[0], so[1]])
                current = state
        if any_deformed:
            tracks.append(dict(part="part%d" % p, keys=keys, sprite_times=None))
    return True, deformed_count, tracks


def sprite_times_of(frames, p, repeat_last):
    """Times of the Sprite keys of part p (displayed frames, terminal repeat included), as float32 shortest decimals."""
    times = []
    cum = np.float32(0)
    for f in frames:
        times.append(cum)
        if f["quads"]:
            cum = np.float32(cum + np.float32(f["delay"] & 0x7F) * PSX)
    out = set()
    for fi, f in enumerate(frames):
        terminal_repeat = repeat_last and fi == len(frames) - 1
        src = frames[len(frames) - 2] if terminal_repeat else f
        if p < len(src["quads"]):
            out.add(fmt_time(times[fi]))
    return out


def main():
    check_only = "--check" in sys.argv
    banks = read_banks()
    # folder of every existing export file (basename -> relative path)
    folders = {}
    for dirpath, _dirs, files in os.walk(EXPORT_ENTITIES):
        for name in files:
            if name.endswith(".anim2d"):
                rel = os.path.relpath(os.path.join(dirpath, name), os.path.join(ROOT, "alundra-project")).replace(os.sep, "/")
                folders.setdefault(name, []).append(rel)

    files_out = {}
    quads_deformed = 0
    tracks_total = 0
    keys_total = 0
    enabled_total = 0
    disabled_total = 0
    times_ok = True
    animations = 0
    for key, sets in banks.items():
        for si, dirs in enumerate(sets):
            for di, frames in enumerate(dirs):
                if frames is None:
                    continue
                produced, ndef, tracks = predict_animation(frames)
                if not produced:
                    continue
                animations += 1
                quads_deformed += ndef
                if not tracks:
                    continue
                name = "bank%s_anim%d_%s.anim2d" % (key, si, DIRECTIONS[di])
                if name not in folders or len(folders[name]) != 1:
                    print("ERROR: file name not found exactly once in the export: %s %s" % (name, folders.get(name)))
                    return 1
                last = frames[-1]
                repeat_last = (not last["quads"]) and last["delay"] != 1 and len(frames) >= 2
                for tr in tracks:
                    p = int(tr["part"][4:])
                    st = sprite_times_of(frames, p, repeat_last)
                    for k in tr["keys"]:
                        if fmt_time(k[0]) not in st:
                            times_ok = False
                    del tr["sprite_times"]
                    tracks_total += 1
                    keys_total += len(tr["keys"])
                    enabled_total += sum(1 for k in tr["keys"] if k[1] == 1)
                    disabled_total += sum(1 for k in tr["keys"] if k[1] == 0)
                files_out[folders[name][0]] = tracks

    with open(TSV, encoding="utf-8") as fh:
        lines = fh.read().split("\n")
    expected = sorted(l.split("\t")[0] for l in lines[1:] if l.strip())
    got = sorted(files_out)
    same_list = expected == got

    # guard file: today's export
    guard_path = os.path.join(ROOT, "alundra-project", GUARD)
    with open(guard_path, encoding="utf-8") as fh:
        gdoc = json.load(fh)
    duration = np.float32(0)
    for t in gdoc["tracks"]:
        for kname, kvals in t.items():
            if kname.endswith("_keyframes"):
                for k in kvals:
                    duration = max(duration, np.float32(k["time_seconds"]))
    for e in gdoc.get("events", []) or []:
        duration = max(duration, np.float32(e["time_seconds"]))
    for k in gdoc.get("collision_keyframes", []) or []:
        duration = max(duration, np.float32(k["time_seconds"]))
    gtracks = files_out.get(GUARD)
    guard = dict(
        path=GUARD,
        corner_track_count=len(gtracks) if gtracks else 0,
        keys_per_corners_track=[len(t["keys"]) for t in gtracks] if gtracks else [],
        parts=[t["part"] for t in gtracks] if gtracks else [],
        first_key_of_each_track=[t["keys"][0] for t in gtracks] if gtracks else [],
        duration_seconds_today=fmt_time(duration),
    )

    counters = {"Sprites.QuadsDeformed": quads_deformed, "Sprites.CornerTracks": tracks_total,
                "Sprites.CornerKeyframes": keys_total}
    summary = dict(counters=counters, enabled_keys=enabled_total, disabled_keys=disabled_total,
                   changed_files=len(got), list_equals_tsv=same_list, key_times_are_sprite_key_times=times_ok,
                   animations_with_file=animations, banks=len(banks))
    print(json.dumps(summary, indent=1))
    print(json.dumps(guard))
    if not same_list:
        print("ERROR: changed file list differs from predicted_anim2d_changes.tsv")
        print("only in prediction:", sorted(set(got) - set(expected))[:10])
        print("only in tsv:", sorted(set(expected) - set(got))[:10])
        return 1
    if not times_ok:
        print("ERROR: a key time is not a Sprite key time")
        return 1
    if not check_only:
        doc = dict(
            format="per file: tracks=[{part, keys}]; key [t, 0] disabled or [t, 1, tlx, tly, trx, try, blx, bly, brx, bry, sox, soy]",
            counters=counters, enabled_keys=enabled_total, disabled_keys=disabled_total,
            changed_files=got, guard=guard,
            files={k: files_out[k] for k in got})
        with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
            json.dump(doc, fh, separators=(",", ":"), ensure_ascii=False)
            fh.write("\n")
        print("wrote", OUT, os.path.getsize(OUT), "bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
