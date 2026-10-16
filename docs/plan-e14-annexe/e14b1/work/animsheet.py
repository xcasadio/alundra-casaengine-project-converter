"""Animation sheets of the five E14.b1 types from data-extracted/data/map_N.json (read only). Output: markdown tables on stdout.
Conventions (as docs/plan-e14-annexe/e14a/muruta.md section 2): per anim, direction Down (index 0): ticks = sum(Delay & 0x7F) of the image frames;
end = the transition frame (Delay==1: Loop; Delay&0x80==0 and TransformIndexLow&0x80: Hold; Delay&0x80==0 else Chain -> TransformIndexLow); Z impulse = s16 (Flags<<8|Unknown);
AnimFlags = byte 0xD ('Acceleration' of the export); boxed = number of image frames that carry a collision box."""
import json
import sys
import hashlib

ROOT = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/"
TYPES = {  # type: (name, table_index, [maps to cross-check])
    392: ("Poisson", 136, [14, 4, 8, 12, 79]),
    455: ("Guepe", 199, [14, 12, 30, 32]),
    341: ("Orc masse", 85, [15, 3, 7, 11, 48]),
    338: ("Orc hache", 82, [4, 5, 18, 19]),
    344: ("Orc armure de fer", 88, [3]),
}


def load(n):
    return json.load(open(ROOT + "map_%d.json" % n, encoding="utf-8-sig"))


def strip(o):
    if isinstance(o, dict):
        return {k: strip(v) for k, v in o.items() if k not in ("MemoryAddress", "BinOffset", "SpriteInfoMemoryAddress", "Images", "AnimationOffsetsPointer", "AnimationsPointer", "FrameCollisionPointer", "FramesPointer")}
    if isinstance(o, list):
        return [strip(x) for x in o]
    return o


def sheet(rec):
    out = []
    for a, aset in enumerate(rec["AnimSets"]):
        d0 = aset["PreloadedAnims"][0]
        frames = d0["Frames"]
        img = [f for f in frames if not f["IsTransitionFrame"]]
        tr = [f for f in frames if f["IsTransitionFrame"]]
        ticks = sum(f["Delay"] & 0x7F for f in img)
        boxed = sum(1 for f in img if f["CollisionData"] is not None)
        if tr:
            t = tr[-1]
            if t["Delay"] == 1:
                end = "Loop"
            elif (t["Delay"] & 0x80) == 0:
                end = "Hold" if (t["TransformIndexLow"] & 0x80) else "Chain -> %d" % t["TransformIndexLow"]
            else:
                end = "flag 0x%02X/%d" % (t["Delay"], t["TransformIndexLow"])
        else:
            end = "(no transition frame)"
        z = ((aset["Flags"] << 8) | aset["Unknown"])
        if z & 0x8000:
            z -= 0x10000
        out.append((a, aset["Speed"], z, aset["Acceleration"], aset["Sfx"], len(img), ticks, boxed, end,
                    [f["Delay"] & 0x7F for f in img]))
    return out


def main():
    for t, (name, ti, maps) in TYPES.items():
        base = None
        print("\n### Type %d %s (table index %d)\n" % (t, name, ti))
        for n in maps:
            d = load(n)
            si = d["SpriteInfo"]
            rec = si["SpriteRecords"][ti]
            h = hashlib.sha1(json.dumps(strip(rec), sort_keys=True).encode()).hexdigest()[:10]
            if base is None:
                base = h
                hd = rec["Header"]
                print("Header (map %d): MoreFlags 0x%02X, A/C/D/E/F = %d/%d/%d/%d/%d, offset (%d,%d,%d) size (%d,%d,%d), BreakEffect %d, Contents %d, shadow/flags %d, CanPickup %d"
                      % (n, hd["MoreFlags"], hd["ProgramLoad"], hd["ProgramTick"], hd["ProgramTouch"], hd["ProgramDeactivate"], hd["ProgramInteract"],
                         hd["OffsetX"], hd["OffsetY"], hd["OffsetZ"], hd["SizeX"], hd["SizeY"], hd["SizeZ"], hd["BreakEffect"], hd["Contents"], hd["FlagsPortraitShadowType"], hd["CanPickup"]))
                print("Ubuff: %s" % hd["Ubuff"])
                print("\n| Anim | Speed | Z impulse | AnimFlags(0xD) | Sfx | image frames | ticks | boxed frames | End | delays |")
                print("|---|---|---|---|---|---|---|---|---|---|")
                for row in sheet(rec):
                    print("| %d | %d | %d | 0x%02X | %d | %d | %d | %d | %s | %s |" % (row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[9]))
            print("\ncross-check map %d: record hash %s %s" % (n, h, "(identical)" if h == base else "(DIFFERENT)"))


if __name__ == "__main__":
    main()
