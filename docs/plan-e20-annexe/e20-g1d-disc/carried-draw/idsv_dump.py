"""Read-only: dump IdsvAnimDirs of chosen banks for the given anims.
usage: idsv_dump.py <BankKey> <anim,anim,...|all>"""
import json, sys

P = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json"
d = json.load(open(P, encoding="utf-8"))
key = sys.argv[1]
anims = None if sys.argv[2] == "all" else {int(x) for x in sys.argv[2].split(",")}
for gid, rec in d.items():
    if rec["BankKey"] != key:
        continue
    print(f"BankKey {key} gid {gid}")
    for e in rec["IdsvAnimDirs"]:
        if anims is None or e["Anim"] in anims:
            print(f"  anim {e['Anim']:3d} dir {e['Direction']} frames {e['Frames']} end {e['End']}" + (f" chain->{e.get('ChainTo')}" if 'ChainTo' in e else ""))
    print("  AnimSets:", [(a["Anim"], a["Flags"], a["IsZForceApplied"]) for a in rec["AnimSets"] if anims is None or a["Anim"] in anims])
