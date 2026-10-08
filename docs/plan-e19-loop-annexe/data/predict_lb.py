"""Predict the re-extraction and in-place export changes of slice L-b (option B) - read-only on the repository.
Extraction: today's full extraction (full-today/, db69b82 Release) equals data-extracted/ byte for byte (diff -rq, 4536
files); ExtractDataFromBgm runs last and writes only sound/bgm/*.wav and sound/bgm.json; so the predicted tree is
data-extracted/ with those replaced by the oracle (oracle/sound/). This script diffs them file by file (SHA-256).
Export: model of AudioWriter.ConvertBgm (Writers/AudioWriter.cs:66-100, 151-175, 198-200) with BgmEntry gaining
Looping (bool?), LoopStartFrame (int?), LoopStartSample (int?) appended AFTER AssetId (ADR-0003 precedent, SfxTone),
serialised SnakeCaseLower + WriteIndented (null written as null). Step 1 validates the model on today's files.
Writes predicted_extract_changes.txt, predicted_export_changes.txt, export_values.tsv, predicted/bgm-manifest.json."""
import hashlib, json, os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = "D:/development/repo/alundra-casaengine-project-converter"
PROJ = REPO + "/alundra-project"
DE = REPO + "/data-extracted"
ORACLE = os.path.join(HERE, "oracle")
os.makedirs(os.path.join(HERE, "predicted"), exist_ok=True)

FIELDS = [("SoundIndex", "sound_index"), ("File", "file"), ("Frames", "frames"), ("DurationSeconds", "duration_seconds"),
          ("LoopDetected", "loop_detected"), ("PeakLeft", "peak_left"), ("PeakRight", "peak_right"), ("RmsLeft", "rms_left"),
          ("RmsRight", "rms_right"), ("FirstAudibleFrame", "first_audible_frame")]
NEW_FIELDS = [("Looping", "looping"), ("LoopStartFrame", "loop_start_frame"), ("LoopStartSample", "loop_start_sample")]


def num(v):
    if v is None:
        return "null"
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, float):
        r = repr(v)
        return r[:-2] if r.endswith(".0") else r
    if isinstance(v, int):
        return str(v)
    return json.dumps(v, ensure_ascii=False)


def manifest(entries, ids, new_fields):
    nl = "\r\n"
    out = ["["]
    for i, e in enumerate(entries):
        out.append("  {")
        lines = ['    "%s": %s' % (sk, num(e[k])) for k, sk in FIELDS]
        lines.append('    "asset_id": "%s"' % ids[e["File"]])
        if new_fields:
            lines += ['    "%s": %s' % (sk, num(e.get(k))) for k, sk in NEW_FIELDS]
        out.append(("," + nl).join(lines))
        out.append("  }" + ("," if i < len(entries) - 1 else ""))
    out.append("]")
    return nl.join(out).encode("utf-8")


sha1 = lambda b: hashlib.sha1(b).hexdigest()
sha256 = lambda b: hashlib.sha256(b).hexdigest()
rb = lambda p: open(p, "rb").read()

# ---------------- extraction
ext_changes = []
ext_rows = ["Path\tTodaySHA256\tPredictedSHA256\tTodayBytes\tPredictedBytes"]
today_bgm = json.load(open(DE + "/sound/bgm.json", encoding="utf-8"))
new_bgm = json.load(open(ORACLE + "/sound/bgm.json", encoding="utf-8"))
today_files = sorted(os.listdir(DE + "/sound/bgm"))
new_files = sorted(os.listdir(ORACLE + "/sound/bgm"))
assert today_files == new_files, (today_files, new_files)
for f in new_files:
    a, b = rb(DE + "/sound/bgm/" + f), rb(ORACLE + "/sound/bgm/" + f)
    if a != b:
        ext_changes.append("M sound/bgm/" + f)
    ext_rows.append("sound/bgm/%s\t%s\t%s\t%d\t%d" % (f, sha256(a), sha256(b), len(a), len(b)))
a, b = rb(DE + "/sound/bgm.json"), rb(ORACLE + "/sound/bgm.json")
if a != b:
    ext_changes.append("M sound/bgm.json")
ext_rows.append("sound/bgm.json\t%s\t%s\t%d\t%d" % (sha256(a), sha256(b), len(a), len(b)))
ext_changes.sort(key=lambda s: s[2:])
open(os.path.join(HERE, "predicted_extract_changes.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(ext_changes) + "\n")
open(os.path.join(HERE, "extract_values.tsv"), "w", encoding="utf-8", newline="\n").write("\n".join(ext_rows) + "\n")
print("extraction changes:", len(ext_changes))

# sanity: the first ten fields of every entry, against today's (what must and must not move)
moved = {}
for t, n in zip(today_bgm, new_bgm):
    assert t["SoundIndex"] == n["SoundIndex"] and t["File"] == n["File"]
    for k, _ in FIELDS:
        if t[k] != n[k]:
            moved.setdefault(k, []).append(t["SoundIndex"])
print("bgm.json fields that move:", {k: len(v) for k, v in moved.items()})
print("  LoopDetected moves on:", moved.get("LoopDetected"), " FirstAudibleFrame moves on:", moved.get("FirstAudibleFrame"))

# ---------------- export
cur_manifest_bytes = rb(PROJ + "/Musics/bgm-manifest.json")
cur_manifest = json.loads(cur_manifest_bytes.decode("utf-8-sig"))
ids = {m["file"]: m["asset_id"] for m in cur_manifest}
model_today = manifest(today_bgm, ids, False)
print("step 1: model(today bgm.json) == current Musics/bgm-manifest.json:", model_today == cur_manifest_bytes)
new_manifest = manifest(new_bgm, ids, True)
open(os.path.join(HERE, "predicted", "bgm-manifest.json"), "wb").write(new_manifest)
# an older bgm.json read by the new converter: the three fields read as null
compat_manifest = manifest(today_bgm, ids, True)
open(os.path.join(HERE, "predicted", "bgm-manifest.from-old-bgm-json.json"), "wb").write(compat_manifest)

exp_changes = []
rows = ["Path\tTodaySHA1\tPredictedSHA1\tPredictedSHA256\tTodayBytes\tPredictedBytes\tNote"]
delta = 0
for e in new_bgm:
    f = e["File"]
    newb = rb(ORACLE + "/sound/bgm/" + f)
    oldb = rb(PROJ + "/Musics/" + f)
    assert oldb == rb(DE + "/sound/bgm/" + f), f  # today's export is in sync with data-extracted
    if newb != oldb:
        exp_changes.append("M Musics/" + f)
    delta += len(newb) - len(oldb)
    rows.append("Musics/%s\t%s\t%s\t%s\t%d\t%d\t%s" % (f, sha1(oldb), sha1(newb), sha256(newb), len(oldb), len(newb), "changed" if newb != oldb else "unchanged"))
exp_changes.append("M Musics/bgm-manifest.json")
delta += len(new_manifest) - len(cur_manifest_bytes)
rows.append("Musics/bgm-manifest.json\t%s\t%s\t%s\t%d\t%d\t45 entries, 3 fields appended" % (sha1(cur_manifest_bytes), sha1(new_manifest), sha256(new_manifest), len(cur_manifest_bytes), len(new_manifest)))
ai = rb(PROJ + "/AssetInfos.json")
rows.append("AssetInfos.json\t%s\t%s\t%s\t%d\t%d\tunchanged (same 45 BGM files, names and ids)" % (sha1(ai), sha1(ai), sha256(ai), len(ai), len(ai)))
old44 = PROJ + "/Musics/bgm_044.wav"
if os.path.exists(old44):
    b44 = rb(old44)
    rows.append("Musics/bgm_044.wav\t%s\t%s\t-\t%d\t%d\tunreferenced since X2, unchanged" % (sha1(b44), sha1(b44), len(b44), len(b44)))
exp_changes.append("M report.json")
exp_changes.sort(key=lambda s: s[2:])
rep = json.loads(rb(PROJ + "/report.json").decode("utf-8-sig"))
osb = rep["Metrics"]["OutputSizeBytes"]
rows.append("report.json\t-\t-\t-\t-\t-\tcounters unchanged (Audio.Bgm 45, Audio.WavCopied 1041, Verify.*); Metrics.OutputSizeBytes %d -> %d (+%d, +-few bytes of the previous report.json), OutputSizeMegabytes %.3f -> %.3f, OutputFileCount unchanged %d"
            % (osb, osb + delta, delta, osb / 2**20, (osb + delta) / 2**20, rep["Metrics"]["OutputFileCount"]))
open(os.path.join(HERE, "predicted_export_changes.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(exp_changes) + "\n")
open(os.path.join(HERE, "export_values.tsv"), "w", encoding="utf-8", newline="\n").write("\n".join(rows) + "\n")
print("export changes:", len(exp_changes), "size delta bytes:", delta)
print("new manifest sha1", sha1(new_manifest), "sha256", sha256(new_manifest), "bytes", len(new_manifest))
print("current report: OutputSizeBytes", osb, "OutputFileCount", rep["Metrics"]["OutputFileCount"], "Audio.Bgm", rep["Counters"]["Audio.Bgm"], "WavCopied", rep["Counters"]["Audio.WavCopied"])
