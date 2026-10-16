import json
ROOT = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/"
d = json.load(open(ROOT+"map_14.json", encoding="utf-8-sig"))
recs = d["SpriteInfo"]["SpriteRecords"]
for ti,name in [(136,'Poisson'),(199,'Guepe')]:
    r = recs[ti]
    print(name, 'header', {k:r["Header"][k] for k in ("ProgramLoad","ProgramTick","ProgramTouch","ProgramDeactivate","ProgramInteract","BreakEffect","Contents","SizeX","SizeY","SizeZ")})
    for a,aset in enumerate(r["AnimSets"]):
        fr = aset["PreloadedAnims"][0]["Frames"]
        img=[f for f in fr if not f["IsTransitionFrame"]]
        tr=[f for f in fr if f["IsTransitionFrame"]]
        ticks=sum(f["Delay"]&0x7f for f in img)
        print(' anim',a,'speed',aset["Speed"],'z',aset["Flags"],aset["Unknown"],'accel',hex(aset["Acceleration"]),'sfx',aset["Sfx"],'ticks',ticks,'nimg',len(img),'trans',[(t["Delay"],t["TransformIndexLow"]) for t in tr][-1:])
print(list(recs[136]["AnimSets"][0].keys()))
print(list(recs[136]["AnimSets"][0]["PreloadedAnims"][0].keys())[:20])
