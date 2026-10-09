"""D6 (docs/plan-e20-heros.md section 1.2): per-frame collision box proof over the WHOLE corpus.

For every sprite bank that SpriteBankReader.ReadAllBanks returns (same order, same "first occurrence of a
bank key is canonical" rule) and every frame that has a CollisionData record, recover the six values
(OffsetX/Y/Z, Width/Depth/Height) from the exported collision_keyframes of
alundra-project/Entities/**/bank{key}_anim{a}_{dir}.anim2d and compare them to the decoded record.

How the export is read back (SpriteWriter.cs, ConvertAnimation / ConvertCollisionKeyframes /
CreateColliderFixture):
  - frame start time = float32 running sum of (Delay & 0x7F) * float32(1/50) over the NON-terminator frames
    (terminator = a frame with no Images.Images quads, SpriteFrame.IsTerminator);
  - a keyframe is emitted at a frame start when the volume changes (empty fixture set = volume lost);
  - Box.Size = (Width, Depth, Height); LocalPosition = Offset + Size / 2;
  - the engine samples the LAST keyframe whose time <= t (Animation2dCompositionSampler
    .EvaluateCollisionKeyframeIndex).
So for a frame starting at t the exported box is the last keyframe with time <= t.

Read-only on every input. Writes only under out/ next to this script. Run: python -B d6_boxes.py
"""
import csv, glob, json, os, struct, sys, collections

sys.dont_write_bytecode = True

ROOT = r"D:\development\repo\alundra-casaengine-project-converter"
DATA = os.path.join(ROOT, "data-extracted", "data")
ENT = os.path.join(ROOT, "alundra-project", "Entities")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
os.makedirs(OUT, exist_ok=True)

DIRS = ["down", "up", "left", "right"]


def f32(x):
    return struct.unpack("<f", struct.pack("<f", x))[0]


PSX = f32(1.0 / 50.0)


def jload(path):
    with open(path, encoding="utf-8-sig") as fh:
        return json.load(fh)


# ----------------------------------------------------------------------------------------------
# 1. Replicate SpriteBankReader.ReadAllBanks
# ----------------------------------------------------------------------------------------------
def native_map_indices():
    idx = []
    for p in glob.glob(os.path.join(DATA, "map_*.json")):
        stem = os.path.basename(p)[:-5]
        tail = stem[4:]
        if tail.isdigit():  # int.TryParse on "alundra" fails, on "12" works
            idx.append(int(tail))
    return sorted(idx)


def compact_frame(fr):
    """Only what D6 needs, kept per frame (keeps memory small)."""
    imgs = fr.get("Images")
    nq = 0
    if isinstance(imgs, dict) and isinstance(imgs.get("Images"), list):
        nq = len(imgs["Images"])
    cd = fr.get("CollisionData")
    six = None
    if isinstance(cd, dict):
        six = (cd["OffsetX"], cd["OffsetY"], cd["OffsetZ"], cd["Width"], cd["Depth"], cd["Height"])
    return (fr["Delay"], nq, six, fr["CollisionOffset"], fr["TransformIndexLow"], fr["TransformIndexHigh"],
            bool(fr["IsTransitionFrame"]))


def read_record_anims(rec):
    """Mirror of ReadAnimSet/ReadAnimation: returns list (animSetIndex) of 4-slot lists of compact-frame lists or None."""
    sets = []
    for aset in rec["AnimSets"]:
        dirs = [None] * 4
        if isinstance(aset, dict) and isinstance(aset.get("PreloadedAnims"), list):
            for di, anim in enumerate(aset["PreloadedAnims"]):
                if di >= 4:
                    break
                if isinstance(anim, dict) and isinstance(anim.get("Frames"), list):
                    frames = [compact_frame(f) for f in anim["Frames"] if isinstance(f, dict)]
                    dirs[di] = frames if frames else None
        sets.append(dirs)
    return sets


def read_all_banks():
    banks = collections.OrderedDict()   # key -> dict(canonical)
    occurrences = collections.defaultdict(list)  # key -> list of (mapname, anims) for non canonical
    sources = [("map_alundra.json", True, -1)] + [("map_%d.json" % i, False, i) for i in native_map_indices()]
    total_records = 0
    for fn, is_alundra, mi in sources:
        path = os.path.join(DATA, fn)
        if not os.path.exists(path):
            print("MISSING", path)
            continue
        d = jload(path)
        si = d.get("SpriteInfo")
        if not isinstance(si, dict) or not isinstance(si.get("SpriteRecords"), list):
            continue
        for rec in si["SpriteRecords"]:
            if not isinstance(rec, dict):
                continue
            total_records += 1
            sid = rec["Header"]["Sector5Id"]
            key = ("alundra_%d" % sid) if is_alundra else ("%d" % sid)
            if key in banks:
                if isinstance(rec.get("AnimSets"), list):
                    occurrences[key].append((fn, read_record_anims(rec)))
                continue
            if not isinstance(rec.get("AnimSets"), list):
                continue
            banks[key] = {"key": key, "alundra": is_alundra, "sid": sid, "map": fn, "anims": read_record_anims(rec)}
    return banks, occurrences, total_records, len(sources)


# ----------------------------------------------------------------------------------------------
# 2. Export index
# ----------------------------------------------------------------------------------------------
def index_anim2d():
    idx = {}
    dup = []
    for dp, _, files in os.walk(ENT):
        for f in files:
            if f.endswith(".anim2d"):
                n = f[:-7]
                if n in idx:
                    dup.append(n)
                idx[n] = os.path.join(dp, f)
    return idx, dup


def fixture_box(fx):
    """Return (six, anomalies) from one exported ColliderFixture."""
    anomalies = []
    sh = fx["shape"]
    if sh.get("shape_type") != "Box":
        anomalies.append("shape_type=%s" % sh.get("shape_type"))
    w, h, l = sh["w"], sh["h"], sh["l"]  # Size = (Width, Depth, Height)
    lp = fx["local_position"]
    lr = fx.get("local_rotation") or {}
    if (lr.get("x"), lr.get("y"), lr.get("z"), lr.get("w")) != (0.0, 0.0, 0.0, 1.0):
        anomalies.append("rotation")
    if fx.get("collision_profile", "") != "" or fx.get("tag", "") != "":
        anomalies.append("profile/tag")
    ox = lp["x"] - w / 2.0
    oy = lp["y"] - h / 2.0
    oz = lp["z"] - l / 2.0
    vals = [ox, oy, oz, w, h, l]
    for v in vals:
        if v != int(v):
            anomalies.append("non-integral")
            break
    return tuple(int(round(v)) for v in vals), anomalies


FIELDS = ["OffsetX", "OffsetY", "OffsetZ", "Width", "Depth", "Height"]


def main():
    banks, occurrences, total_records, nsrc = read_all_banks()
    anim_index, dup_names = index_anim2d()

    per_bank = []
    mism_rows = []
    cls_count = collections.Counter()
    cls_examples = collections.defaultdict(list)
    totals = collections.Counter()
    export_anomalies = collections.Counter()
    seen_anim_files = set()

    # range / index-visibility checks over the canonical banks AND every other occurrence
    range_stats = collections.Counter()
    range_min = [999] * 6
    range_max = [-999] * 6
    idx_stats = collections.Counter()
    table_sharing = collections.defaultdict(dict)  # (bankkey, occ) -> {co: six}

    def check_ranges_and_index(bkey, occ_tag, anims):
        shared = {}
        for a, dirs in enumerate(anims):
            for di, frames in enumerate(dirs):
                if not frames:
                    continue
                for (delay, nq, six, co, lo, hi, trans) in frames:
                    idx_stats["frames"] += 1
                    raw = (hi << 8) | lo
                    if trans:
                        idx_stats["transition"] += 1
                        if six is not None or co != -1:
                            idx_stats["transition_with_record_or_index"] += 1
                        continue
                    idx_stats["real"] += 1
                    # CollisionOffset is the short (hi<<8|lo)
                    sraw = raw - 0x10000 if raw >= 0x8000 else raw
                    if sraw != co:
                        idx_stats["co_ne_rawbytes"] += 1
                    if co == -1:
                        idx_stats["index_FFFF"] += 1
                        if six is not None:
                            idx_stats["FFFF_but_record"] += 1
                    else:
                        idx_stats["index_valid"] += 1
                        if co < -1:
                            idx_stats["index_negative_other"] += 1
                        if raw >= 0x8000:
                            idx_stats["index_ge_0x8000"] += 1
                        if six is None:
                            idx_stats["index_but_no_record"] += 1
                        else:
                            if co in shared and shared[co] != six:
                                idx_stats["same_index_different_record"] += 1
                            shared.setdefault(co, six)
                            if co % 6 != 0:
                                idx_stats["index_not_multiple_of_6"] += 1
                    if six is not None:
                        range_stats["records"] += 1
                        for i, v in enumerate(six):
                            range_min[i] = min(range_min[i], v)
                            range_max[i] = max(range_max[i], v)
                        if not all(-128 <= six[i] <= 127 for i in range(3)):
                            range_stats["offset_outside_int8"] += 1
                        if not all(0 <= six[i] <= 255 for i in range(3, 6)):
                            range_stats["size_outside_uint8"] += 1
                        if any(six[i] < 0 for i in range(3, 6)):
                            range_stats["size_negative"] += 1
        # distinct index -> distinct record? (a record may be shared by several indexes)
        idx_stats["distinct_indexes"] += len(shared)
        idx_stats["distinct_records"] += len(set(shared.values()))

    for key, b in banks.items():
        check_ranges_and_index(key, "canonical", b["anims"])
    for key, occs in occurrences.items():
        for fn, anims in occs:
            check_ranges_and_index(key, fn, anims)

    # S3: non-canonical occurrences equal to canonical on (delay, nquads, six, co)?
    occ_stats = collections.Counter()
    occ_diffs = []
    for key, occs in occurrences.items():
        canon = banks[key]["anims"]
        for fn, anims in occs:
            occ_stats["occurrences"] += 1

            def sig(an):
                return [[None if fr is None else [(f[0], f[1], f[2], f[3]) for f in fr] for fr in dirs] for dirs in an]
            if sig(anims) == sig(canon):
                occ_stats["identical_on_delay_quads_record_index"] += 1
            else:
                occ_stats["different"] += 1
                if len(occ_diffs) < 50:
                    occ_diffs.append((key, banks[key]["map"], fn))

    # ------------------------------------------------------------------------------------------
    # main comparison
    # ------------------------------------------------------------------------------------------
    for key, b in banks.items():
        pb = collections.Counter()
        pb["animations"] = 0
        for a, dirs in enumerate(b["anims"]):
            for di, frames in enumerate(dirs):
                if not frames:
                    continue
                pb["animations"] += 1
                name = "bank%s_anim%d_%s" % (key, a, DIRS[di])
                # converter's time axis
                times = []
                cum = f32(0.0)
                ticks = []
                tcum = 0
                for (delay, nq, six, co, lo, hi, trans) in frames:
                    times.append(cum)
                    ticks.append(tcum)
                    if nq > 0:  # not IsTerminator
                        dly = delay & 0x7F
                        cum = f32(cum + f32(f32(float(dly)) * PSX))
                        tcum += dly
                duration = cum
                max_quads = max(f[1] for f in frames)
                path = anim_index.get(name)
                kfs = None
                if path:
                    seen_anim_files.add(name)
                    ad = jload(path)
                    kfs = ad.get("collision_keyframes") or []
                    parsed = []
                    for k in kfs:
                        t = f32(k["time_seconds"])
                        fx = k.get("fixtures") or []
                        if len(fx) == 0:
                            parsed.append((t, None))
                        else:
                            if len(fx) != 1:
                                export_anomalies["keyframe_with_%d_fixtures" % len(fx)] += 1
                            six, an = fixture_box(fx[0])
                            for x in an:
                                export_anomalies[x] += 1
                            parsed.append((t, six))
                    kfs = parsed
                    # monotone times?
                    if any(kfs[i][0] > kfs[i + 1][0] for i in range(len(kfs) - 1)):
                        export_anomalies["keyframes_not_sorted"] += 1
                    # every keyframe time must be a frame start (no orphan keyframe), and sit on an integer tick
                    starts = {times[i] for i, f in enumerate(frames) if f[1] > 0}
                    for (t, _) in kfs:
                        if t not in starts:
                            export_anomalies["keyframe_not_on_frame_start"] += 1
                        if abs(t * 50 - round(t * 50)) > 1e-3:
                            export_anomalies["keyframe_not_on_integer_tick"] += 1
                elif max_quads > 0:
                    totals["anim_with_quads_but_no_file"] += 1

                # same-instant frames (zero delay)
                start_count = collections.Counter(times[i] for i, f in enumerate(frames) if f[1] > 0)

                for fi, (delay, nq, six, co, lo, hi, trans) in enumerate(frames):
                    if trans:
                        continue
                    has_rec = six is not None
                    if nq == 0 and not has_rec:
                        continue
                    if not has_rec:
                        # secondary check: "no box" agreement (0xFFFF index)
                        if nq == 0 or kfs is None:
                            continue
                        pb["nobox_checked"] += 1
                        totals["nobox_checked"] += 1
                        t = times[fi]
                        act = None
                        for (kt, ks) in kfs:
                            if kt <= t:
                                act = ks
                            else:
                                break
                        if act is not None and not (duration > 0 and t >= duration):
                            pb["nobox_mismatch"] += 1
                            totals["nobox_mismatch"] += 1
                            row = [key, name, fi, "EXPORT_HAS_BOX_BUT_RECORD_IS_FFFF", "", ",".join(map(str, act)), ""]
                            mism_rows.append(row)
                            cls_count["EXPORT_HAS_BOX_BUT_RECORD_IS_FFFF"] += 1
                            if len(cls_examples["EXPORT_HAS_BOX_BUT_RECORD_IS_FFFF"]) < 5:
                                cls_examples["EXPORT_HAS_BOX_BUT_RECORD_IS_FFFF"].append(row)
                        continue

                    # a frame with a record
                    pb["frames_with_record"] += 1
                    totals["frames_with_record"] += 1
                    cls = None
                    detail = ""
                    exp = None
                    if kfs is None:
                        cls = "NO_ANIM2D_FILE" if max_quads == 0 else "NO_ANIM2D_FILE_WITH_QUADS"
                    elif nq == 0:
                        cls = "RECORD_ON_FRAME_WITHOUT_IMAGES"  # converter treats it as a terminator: never emitted
                    else:
                        t = times[fi]
                        if duration > 0 and t >= duration:
                            cls = "FRAME_AT_OR_AFTER_DURATION"
                        else:
                            act = None
                            for (kt, ks) in kfs:
                                if kt <= t:
                                    act = ks
                                else:
                                    break
                            exp = act
                            if start_count[t] > 1:
                                # another displayed frame starts at the same instant
                                ties = [j for j, f in enumerate(frames) if f[1] > 0 and times[j] == t]
                                if ties[-1] != fi:
                                    cls = "TIE_ZERO_DELAY_FRAME_SHADOWED_BY_LATER_FRAME"
                                    detail = "frames %s at t=%r" % (ties, t)
                            if cls is None:
                                if act is None:
                                    cls = "EXPORT_HAS_NO_BOX"
                                elif act != six:
                                    diff = [FIELDS[i] for i in range(6) if act[i] != six[i]]
                                    cls = "VALUE_DIFF"
                                    detail = ",".join("%s %d->%d" % (FIELDS[i], six[i], act[i]) for i in range(6) if act[i] != six[i])
                    if cls is None:
                        pb["exact"] += 1
                        totals["exact"] += 1
                    else:
                        pb["mismatch"] += 1
                        totals["mismatch"] += 1
                        pb["mismatch_" + cls] += 1
                        row = [key, name, fi, cls, ",".join(map(str, six)),
                               "" if exp is None else ",".join(map(str, exp)), detail]
                        mism_rows.append(row)
                        cls_count[cls] += 1
                        if len(cls_examples[cls]) < 5:
                            cls_examples[cls].append(row)
        per_bank.append((key, b["map"], pb))
        totals["banks"] += 1
        totals["animations"] += pb["animations"]
        if pb["frames_with_record"]:
            totals["banks_with_record"] += 1

    # export files that no decoded animation accounts for
    orphan_files = sorted(set(anim_index) - seen_anim_files)

    # ------------------------------------------------------------------------------------------
    # outputs
    # ------------------------------------------------------------------------------------------
    with open(os.path.join(OUT, "per_bank.csv"), "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["bank", "first_map", "animations", "frames_with_record", "exact", "mismatch",
                    "nobox_frames_checked", "nobox_mismatch"])
        for key, mp, pb in per_bank:
            w.writerow([key, mp, pb["animations"], pb["frames_with_record"], pb["exact"], pb["mismatch"],
                        pb["nobox_checked"], pb["nobox_mismatch"]])
    with open(os.path.join(OUT, "mismatches.csv"), "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["bank", "animation", "frame_index", "class", "decoded_six(ox,oy,oz,w,d,h)", "export_six", "detail"])
        for r in mism_rows:
            w.writerow(r)
    banks_with_mismatch = sorted({r[0] for r in mism_rows})
    summary = {
        "sources_read": nsrc,
        "sprite_records_read_total": total_records,
        "banks_canonical": totals["banks"],
        "banks_with_a_record": totals["banks_with_record"],
        "animations_in_banks": totals["animations"],
        "frames_with_record": totals["frames_with_record"],
        "exact": totals["exact"],
        "mismatch": totals["mismatch"],
        "mismatch_classes": dict(cls_count),
        "banks_with_a_mismatch": banks_with_mismatch,
        "nobox_frames_checked": totals["nobox_checked"],
        "nobox_mismatch": totals["nobox_mismatch"],
        "animations_with_quads_but_no_anim2d_file": totals["anim_with_quads_but_no_file"],
        "anim2d_files_in_export": len(anim_index),
        "anim2d_name_collisions": len(dup_names),
        "anim2d_files_not_matched_by_a_decoded_animation": len(orphan_files),
        "export_anomalies": dict(export_anomalies),
        "range_checks": {
            "records_all_occurrences": range_stats["records"],
            "offset_outside_int8": range_stats["offset_outside_int8"],
            "size_outside_uint8": range_stats["size_outside_uint8"],
            "size_negative": range_stats["size_negative"],
            "min_per_field": dict(zip(FIELDS, range_min)),
            "max_per_field": dict(zip(FIELDS, range_max)),
        },
        "index_checks": dict(idx_stats),
        "duplicate_bank_occurrences": dict(occ_stats),
        "duplicate_bank_differences_first50": occ_diffs,
    }
    with open(os.path.join(OUT, "summary.json"), "w", encoding="utf-8") as fh:
        json.dump(summary, fh, indent=1)
    with open(os.path.join(OUT, "class_examples.json"), "w", encoding="utf-8") as fh:
        json.dump(cls_examples, fh, indent=1)
    with open(os.path.join(OUT, "orphan_anim2d.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(orphan_files))
    print(json.dumps({k: v for k, v in summary.items() if k not in ("banks_with_a_mismatch", "duplicate_bank_differences_first50")}, indent=1))
    print("banks with mismatch:", len(banks_with_mismatch))


if __name__ == "__main__":
    main()
