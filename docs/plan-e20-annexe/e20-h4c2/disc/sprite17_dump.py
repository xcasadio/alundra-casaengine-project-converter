import json, sys

REC = "D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json"
d = json.load(open(REC, encoding="utf-8"))
key = sys.argv[1] if len(sys.argv) > 1 else "alundra_17"
for k, v in d.items():
    if v.get("BankKey") == key:
        vv = dict(v)
        dirs = vv.pop("IdsvAnimDirs", [])
        print(json.dumps(vv))
        print("IdsvAnimDirs:")
        for e in dirs:
            print("  ", json.dumps(e))
