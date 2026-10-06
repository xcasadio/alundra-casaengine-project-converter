import json, os, re, glob, sys
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
src = open(os.path.join(REPO, "Alundra", "Scripts", "EventOpcodeSizeTable.cs"), encoding="utf-8").read()
SIZES = {int(m.group(1),16): int(m.group(2)) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),', src)}
print("sizes known:", len(SIZES), "0x82 ->", SIZES.get(0x82))
index = json.load(open(os.path.join(REPO,"alundra-project","Maps","world-index.json"),encoding="utf-8"))
print("maps in index:", len(index))
nfiles=0
raw=[]; aligned=[]
mapsdirs = set()
for ms, rel in index.items():
    folder = os.path.dirname(os.path.join(REPO,"alundra-project",rel))
    evs = glob.glob(os.path.join(folder,"events","*.events.json"))
    if not evs: continue
    nfiles += 1
    ev = json.load(open(evs[0],encoding="utf-8")); codes = ev["Codes"]
    for i in range(len(codes)-1):
        if codes[i]==0x82 and 0x50 <= codes[i+1] <= 0x56:
            raw.append((int(ms), i, codes[i+1]))
    # aligned sweep over all start offsets (union of A..F tables)
    starts = sorted({v for t in "ABCDEF" for v in ev["EventCodes%sTable"%t] if v})
    starts=[x for x in starts if 0<x<len(codes)]
    for n, st in enumerate(starts):
        end = starts[n+1] if n+1 < len(starts) else len(codes)
        pc = st
        while pc < end and pc < len(codes)-1:
            op = codes[pc]; sz = SIZES.get(op, 0)
            if sz <= 0: break
            if op == 0x82 and 0x50 <= codes[pc+1] <= 0x56:
                aligned.append((int(ms), pc, codes[pc+1]))
            pc += sz
print("event files:", nfiles)
print("raw 0x82 0x50..0x56 hits:", sorted(raw))
print("aligned:", sorted(aligned))
