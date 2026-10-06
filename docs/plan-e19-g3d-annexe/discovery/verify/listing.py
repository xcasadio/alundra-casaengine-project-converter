import json, os, re, glob, sys
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
src = open(os.path.join(REPO, "Alundra", "Scripts", "EventOpcodeSizeTable.cs"), encoding="utf-8").read()
SIZES = {}; NAMES = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"', src):
    SIZES[int(m.group(1),16)] = int(m.group(2)); NAMES[int(m.group(1),16)] = m.group(3)
index = json.load(open(os.path.join(REPO,"alundra-project","Maps","world-index.json"),encoding="utf-8"))
mid = sys.argv[1]; slot = sys.argv[2]; k = int(sys.argv[3])
folder=os.path.dirname(os.path.join(REPO,"alundra-project",index[mid]))
ev=json.load(open(glob.glob(os.path.join(folder,"events","*.events.json"))[0],encoding="utf-8")); codes=ev["Codes"]
st = ev["EventCodes%sTable"%slot][k]
print("start", st)
pc=st
for _ in range(80):
    op=codes[pc]; sz=SIZES.get(op,0)
    if sz<=0: print(pc, "UNKNOWN", op); break
    print("%5d  %02X %-30s %s" % (pc, op, NAMES.get(op,'?'), " ".join("%02X"%b for b in codes[pc+1:pc+sz])))
    pc += sz
    if op==0xFF: break
