"""Survey the controller settings of every prefab (step height, mask) - checker side, read-only."""
import collections
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
ROOT = "D:/development/repo/alundra-casaengine-project-converter"
A = json.load(open(ROOT + "/alundra-project/AssetInfos.json", encoding="utf-8-sig"))["asset_infos"]
steps = collections.Counter()
keys = collections.Counter()
n = 0
for e in A:
    fn = e["file_name"].replace("\\", "/")
    p = ROOT + "/alundra-project/" + fn
    if not fn.lower().endswith(".entity") or not os.path.exists(p):
        continue
    d = json.load(open(p, encoding="utf-8-sig"))
    for comp in d.get("components", []):
        if comp.get("type") == "CharacterControllerComponent":
            s = comp["settings"]
            steps[s.get("step_height")] += 1
            for k in s:
                keys[k] += 1
            n += 1
print(n, steps.most_common())
print(keys.most_common())
