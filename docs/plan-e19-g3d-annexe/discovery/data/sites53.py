"""sites53.py: every 0x82 site with sub-case in 0x50..0x56 (and a robust sweep: also a plain byte scan to detect missed sites),
with decoded context (opcode names from EventOpcodeSizeTable.cs) in the program that holds it."""
import json, os, re, sys, glob, collections
sys.stdout.reconfigure(encoding="utf-8")
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
src = open(os.path.join(REPO, "Alundra", "Scripts", "EventOpcodeSizeTable.cs"), encoding="utf-8").read()
SIZES = {}
NAMES = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"', src):
    SIZES[int(m.group(1), 16)] = int(m.group(2))
    NAMES[int(m.group(1), 16)] = m.group(3)
index = json.load(open(os.path.join(REPO, "alundra-project", "Maps", "world-index.json"), encoding="utf-8"))
SUBS = set(range(0x50, 0x57))
want = {int(a, 16) for a in sys.argv[1:]} if len(sys.argv) > 1 else {0x53}
found = []
byte_scan = collections.Counter()
for m_s, rel in sorted(index.items(), key=lambda kv: int(kv[0])):
    m = int(m_s)
    folder = os.path.dirname(os.path.join(REPO, "alundra-project", rel))
    evs = glob.glob(os.path.join(folder, "events", "*.events.json"))
    if not evs:
        continue
    ev = json.load(open(evs[0], encoding="utf-8")); codes = ev["Codes"]
    # plain byte scan: every position where codes[i]==0x82 and codes[i+1] in want
    for i in range(len(codes) - 1):
        if codes[i] == 0x82 and codes[i + 1] in want:
            byte_scan[(m, i)] += 1
    starts = []
    for t in "ABCDEF":
        for k, v in enumerate(ev["EventCodes%sTable" % t]):
            if v: starts.append((v, t, k))
    starts.sort()
    for n, (st, t, k) in enumerate(starts):
        if not (0 < st < len(codes)):
            continue
        end = min(starts[n + 1][0] if n + 1 < len(starts) else len(codes), len(codes)); pc = st
        listing = []
        while pc < end:
            op = codes[pc]; sz = SIZES.get(op, 0)
            if sz <= 0:
                listing.append((pc, op, None)); break
            listing.append((pc, op, codes[pc:pc + sz]))
            pc += sz
        for j, (pc, op, raw) in enumerate(listing):
            if op == 0x82 and raw and raw[1] in want:
                found.append((m, t, k, pc, j, listing))

print("sites by sweep:", len(found), " by plain byte scan:", len(byte_scan))
swept = {(m, pc) for m, t, k, pc, j, l in found}
print("plain-scan positions not in sweep:", sorted(set(byte_scan) - swept))
for m, t, k, pc, j, listing in found:
    print("=== map %d program %s[%d] pc %d (instruction #%d of %d)" % (m, t, k, pc, j, len(listing)))
    for jj in range(max(0, j - 10), min(len(listing), j + 11)):
        p, op, raw = listing[jj]
        mark = "  <== " if jj == j else "     "
        print("%s%5d  %02X %-34s %s" % (mark, p, op, NAMES.get(op, "?"), (" ".join("%02X" % b for b in raw[1:]) if raw else "UNKNOWN SIZE")))
