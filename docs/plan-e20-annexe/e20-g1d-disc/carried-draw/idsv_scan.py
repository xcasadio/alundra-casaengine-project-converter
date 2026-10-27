"""Read-only: IDSV and flags of the hero (alundra_0), the bomb (global sprite 2), and a few carried objects.
Source: alundra-project/Data/sprite-records.json (exported from the binary's sprite tables)."""
import json, sys

P = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json"
d = json.load(open(P, encoding="utf-8"))

def show(key):
    for gid, rec in d.items():
        if rec["BankKey"] == key:
            flags = rec["MoreFlags"] | (rec["CanPickup"] << 8) | (rec["FlagsPortraitShadowType"] << 16)
            print(f"== BankKey {key} gid {gid} MoreFlags={rec['MoreFlags']} (0x{rec['MoreFlags']:02X}) CanPickup={rec['CanPickup']} (0x{rec['CanPickup']:02X}) FPS={rec['FlagsPortraitShadowType']} (0x{rec['FlagsPortraitShadowType']:02X})  -> Flags=0x{flags:06X} collidable(0x80)={bool(flags & 0x80)}")
            print("   body", rec["OffsetX"], rec["OffsetY"], rec["OffsetZ"], rec["SizeX"], rec["SizeY"], rec["SizeZ"])
            return rec
    print("== BankKey", key, "NOT FOUND")
    return None

keys = sys.argv[1:] or ["alundra_0", "alundra_1", "alundra_2", "alundra_3"]
for k in keys:
    show(k)
print("global keys:", sorted([r["BankKey"] for r in d.values() if r["BankKey"].startswith("alundra_")], key=lambda s: int(s.split("_")[1])))
