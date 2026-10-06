import json, os, re, sys, glob, collections
sys.stdout.reconfigure(encoding="utf-8")
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
src = open(os.path.join(REPO, "Alundra", "Scripts", "EventOpcodeSizeTable.cs"), encoding="utf-8").read()
SIZES = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r"\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),", src)}
index = json.load(open(os.path.join(REPO, "alundra-project", "Maps", "world-index.json"), encoding="utf-8"))
CHAIN = {389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185}
subs = collections.defaultdict(list)
for m_s, rel in index.items():
    m = int(m_s)
    folder = os.path.dirname(os.path.join(REPO, "alundra-project", rel))
    evs = glob.glob(os.path.join(folder, "events", "*.events.json"))
    if not evs: continue
    ev = json.load(open(evs[0], encoding="utf-8")); codes = ev["Codes"]
    starts = set()
    for t in "ABCDEF":
        for v in ev["EventCodes%sTable" % t]:
            if v: starts.add(v)
    starts = sorted(s for s in starts if 0 < s < len(codes))
    for i, st in enumerate(starts):
        end = min(starts[i + 1] if i + 1 < len(starts) else len(codes), len(codes)); pc = st
        while pc < end:
            op = codes[pc]; sz = SIZES.get(op, 0)
            if sz <= 0: break
            if op == 0x82: subs[codes[pc + 1]].append((m, pc))
            pc += sz
for s in sorted(subs):
    ms = sorted({m for m, _ in subs[s]})
    print("0x82 sub 0x%02X: %d sites, %d maps, on chain: %s" % (s, len(subs[s]), len(ms), sorted(set(ms) & CHAIN)))
print("maps for 0x50-0x56:", {hex(s): sorted({m for m, _ in subs[s]}) for s in range(0x50, 0x57) if s in subs})
