"""E20.h4a T0 (d): the hero animations 50, 51 and 52 of the export (alundra-project) read against the oracle (data-extracted/data/map_alundra.json,
the hero record Sector5Id 0, the bytes the binary's UpdateAnimation 0x80038AB4 reads: Delay & 0x7F ticks per frame, a terminator of Delay 0 = chain to
TransformIndexLow, Delay 1 = loop).

usage: python -B anims_50_52.py REPO_ROOT       -> prints the table; exit code 0 iff the export equals the oracle in every direction."""
import json
import os
import sys

root = sys.argv[1]
export = os.path.join(root, "alundra-project", "Entities", "Alundra")
heroid = json.load(open(os.path.join(export, "Alundra.entity"), encoding="utf-8"))["id"]
records = json.load(open(os.path.join(root, "alundra-project", "Data", "sprite-records.json"), encoding="utf-8"))[heroid]
oracle = json.load(open(os.path.join(root, "data-extracted", "data", "map_alundra.json"), encoding="utf-8-sig"))
hero = [r for r in oracle["SpriteInfo"]["SpriteRecords"] if r and r["Header"]["Sector5Id"] == 0][0]
names = ["down", "up", "left", "right"]          # oracle order of AnimationOffsets (down, up, left, right)
bad = 0


def oracle_of(anim, direction):
    frames = hero["AnimSets"][anim]["PreloadedAnims"][direction]["Frames"]
    ticks = sum(f["Delay"] & 0x7F for f in frames if not f["IsTransitionFrame"])
    last = frames[-1]
    assert last["IsTransitionFrame"]
    if last["Delay"] == 0:
        return ticks, "chain", last["TransformIndexLow"]
    assert last["Delay"] == 1
    return ticks, "loop", None


def export_of(anim, name):
    doc = json.load(open(os.path.join(export, "bankalundra_0_anim%d_%s.anim2d" % (anim, name)), encoding="utf-8"))
    last = max(k["time_seconds"] for t in doc["tracks"] for key, v in t.items() if key.endswith("keyframes") for k in v)
    ticks = round(last / 0.02)
    assert abs(ticks * 0.02 - last) < 1e-4
    end = [e for e in records["IdsvAnimDirs"] if e["Anim"] == anim and e["Direction"] == names.index(name)]
    assert len(end) == 1
    # the exported record numbers its directions 0..3 in the order down, up, left, right as the oracle does
    kind = {"Chain": "chain", "Loop": "loop"}[end[0]["End"]]
    return ticks, kind, end[0].get("ChainTo"), doc["animation_type"]


print("anim dir    oracle(ticks,end,target)   export(ticks,end,target,type)   speed/accel/sfx oracle | export")
for anim in (50, 51, 52):
    sets_o = hero["AnimSets"][anim]
    sets_e = [s for s in records["AnimSets"] if s["Anim"] == anim][0]
    for d, name in enumerate(names):
        o = oracle_of(anim, d)
        e = export_of(anim, name)
        same = o == e[:3]
        extra = (sets_o["Speed"], sets_o["Acceleration"], sets_o["Sfx"]) == (sets_e["Speed"], sets_e["Acceleration"], sets_e["Sfx"])
        bad += 0 if same and extra else 1
        print("%4d %-5s %-26s %-32s %s | %s %s" % (anim, name, o, e, (sets_o["Speed"], sets_o["Acceleration"], sets_o["Sfx"]), (sets_e["Speed"], sets_e["Acceleration"], sets_e["Sfx"]), "OK" if same and extra else "DIFFERENT"))
print("differences: %d" % bad)
sys.exit(1 if bad else 0)
