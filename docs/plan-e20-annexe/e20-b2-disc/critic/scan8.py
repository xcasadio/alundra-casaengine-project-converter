"""Independent scan: every map event program, linear decode with the DLL's size table (verbatim from the decompilation) + the two binary corrections,
looking for item-grant opcodes on the item 8 and for spawn opcodes 0x8A/0x8B. Output: matches by map."""
import json, os, re, sys, glob

ROOT = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps"
SRC = open(r"D:/development/repo/alundra-casaengine-project-converter/Alundra/Events/EventOpcodeSizeTable.cs", encoding="utf-8").read()
sizes = {}
for m in re.finditer(r"\[0x([0-9A-Fa-f]{2})\]\s*=\s*new\(\s*(\d+)", SRC):
    sizes[int(m.group(1), 16)] = int(m.group(2))
if not sizes:
    for m in re.finditer(r"\{\s*0x([0-9A-Fa-f]{2})\s*,\s*new\(\s*(\d+)", SRC):
        sizes[int(m.group(1), 16)] = int(m.group(2))
print('sizes parsed', len(sizes), file=sys.stderr)

files = glob.glob(ROOT + "/**/*.events.json", recursive=True)
res = {'82': [], '83': [], '84': [], '8A': [], '8B': []}
nmaps = 0
bad = 0
for f in files:
    d = json.load(open(f, encoding="utf-8"))
    mi = d['MapIndex']
    codes = d['Codes']
    starts = set()
    for k in 'ABCDEF':
        for off in d.get('EventCodes%sTable' % k, []):
            starts.add(off)
    nmaps += 1
    seen = set()
    for st in sorted(starts):
        pc = st
        while 0 <= pc < len(codes) - 3 and pc not in seen:
            seen.add(pc)
            op = codes[pc]
            sz = sizes.get(op)
            if sz is None or sz < 1:
                bad += 1
                break
            if op in (0x82, 0x83, 0x84):
                res['%02X' % op].append((mi, pc, codes[pc + 1]))
            if op in (0x8A, 0x8B):
                res['%02X' % op].append((mi, pc, tuple(codes[pc + 1:pc + sz])))
            pc += sz
            if op == 0x00 and False:
                break
print('maps', nmaps, 'undecodable stops', bad)
for k in ('82', '83', '84'):
    hits = [(m, pc, a) for m, pc, a in res[k] if a == 8]
    print('op 0x%s with operand 8:' % k, hits)
    print('  total op 0x%s sites' % k, len(res[k]))
json.dump({k: res[k] for k in res}, open('scan8_out.json', 'w'))
