"""Linear decode of one program of one map. Usage: python v_decode.py MAP SLOT INDEX [maxinstr]"""
import json, re, sys, glob, os
REPO = "D:/development/repo/alundra-casaengine-project-converter"

def sizes():
    s = {}
    for line in open(REPO + "/Alundra/Events/EventOpcodeSizeTable.cs", encoding="utf-8"):
        m = re.search(r"\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*\"([^\"]*)\"", line)
        if m:
            s[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
    return s

def find(mapno):
    hits = [p for p in glob.glob(REPO + "/alundra-project/Maps/*/*/events/*.events.json") if re.search(r"-%d\.events\.json$" % mapno, p)]
    assert len(hits) == 1, hits
    return hits[0]

def main():
    mapno, slot, idx = int(sys.argv[1]), sys.argv[2], int(sys.argv[3])
    maxi = int(sys.argv[4]) if len(sys.argv) > 4 else 200
    d = json.load(open(find(mapno), encoding="utf-8"))
    codes = d["Codes"]
    start = d["EventCodes%sTable" % slot][idx]
    S = sizes()
    pc = start
    for _ in range(maxi):
        op = codes[pc]
        sz = S.get(op, (0, "?"))[0]
        name = S.get(op, (0, "UNKNOWN"))[1]
        print(f"@{pc:5d} {op:02X} {' '.join('%02X' % b for b in codes[pc+1:pc+max(sz,1)])}  [{name}]")
        if op == 0xFF or sz == 0:
            break
        pc += sz

if __name__ == "__main__":
    main()
