"""Which c1 hit-pass traces touch / hurt the oracle hero (read-only on docs/). Run with python -I."""
import json, glob, os
root = r"D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-c1/oracle/out"
for f in sorted(glob.glob(os.path.join(root, "H*.json"))):
    d = json.load(open(f))
    rows = d["rows"]
    touched = [i + 1 for i, r in enumerate(rows) if r["hero"].get("touching_slot") not in (-1, None)]
    hurt = [i + 1 for i, r in enumerate(rows) if r["hero"].get("anim_target") == 0x31]
    print(os.path.basename(f), "ticks", len(rows), "hero touched", touched[:5], "first 0x31", hurt[:1],
          "hp min", min(r["hero"]["hp"] for r in rows), "hero_px", d["meta"].get("hero_start_px"))
