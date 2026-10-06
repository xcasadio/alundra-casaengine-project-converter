"""Dump the raw frames of banks 127 and 151 as the converter reads them (first map wins)."""
import json, sys
root = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\data"
DIRS = ["down", "up", "left", "right"]
targets = {127: [75, 137], 151: [48, 49, 50, 51, 52, 53, 435, 436, 437, 438]}
for bank, maps in targets.items():
    for m in maps:
        d = json.load(open(f"{root}\\map_{m}.json", encoding="utf-8-sig"))
        recs = d["SpriteInfo"]["SpriteRecords"]
        for ri, r in enumerate(recs):
            if not r or r["Header"]["Sector5Id"] != bank:
                continue
            print(f"== bank {bank} map {m} record {ri} animsets {len(r['AnimSets'])} header-keys {list(r['Header'].keys())[:6]}")
            for ai, aset in enumerate(r["AnimSets"]):
                hdr = {k: v for k, v in aset.items() if k != "PreloadedAnims"}
                line = []
                for di, anim in enumerate(aset.get("PreloadedAnims", [])):
                    fr = anim.get("Frames", []) if anim else []
                    desc = []
                    for f in fr:
                        imgs = (f.get("Images") or {}).get("Images") or []
                        desc.append(f"D=0x{f['Delay']:02X} TIL={f.get('TransformIndexLow')} q={len(imgs)}")
                    line.append(f"  anim{ai} {DIRS[di]}: " + " | ".join(desc))
                print(f" animset {ai} hdr={ {k: hdr[k] for k in list(hdr)[:8]} }")
                print("\n".join(line))
        if "--all" not in sys.argv:
            break
