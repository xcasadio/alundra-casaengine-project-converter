"""Independent check: skipped sites from my census run vs docs/plan-e19-sm-annexe/skipped-sites.tsv; per-opcode, per-slice counts."""
import collections
import re
import sys

repo, census_tsv = sys.argv[1:3]
runner = open(f"{repo}/Alundra/Events/EventProgramRunner.cs", encoding="utf-8").read().split("\n")
cases = {int(m.group(1), 16) for l in runner[562:2105] for m in [re.match(r"^            case 0x([0-9A-Fa-f]{2}):", l)] if m}
sizes = {}
for line in open(f"{repo}/Alundra/Events/EventOpcodeSizeTable.cs", encoding="utf-8"):
    m = re.match(r"^\s*\{\s*0x([0-9A-Fa-f]{2}),\s*new\(\s*(-?\d+)", line)
    if m:
        sizes[int(m.group(1), 16)] = int(m.group(2))
skipped_ops = {op for op, s in sizes.items() if s > 0 and op not in cases and op not in (0, 0xFF)}
rows = [l.rstrip("\n").split("\t") for l in open(census_tsv, encoding="utf-8")][1:]
mine = collections.Counter()
zero_or_unknown = 0
for mp, slot, prog, pc, op in rows:
    o = int(op, 16)
    if o in skipped_ops:
        mine[(int(mp), slot, int(prog), int(pc), o)] += 1
    if o != 0xFF and (o not in sizes or sizes[o] <= 0):
        zero_or_unknown += 1
ann = collections.Counter()
for l in list(open(f"{repo}/docs/plan-e19-sm-annexe/skipped-sites.tsv", encoding="utf-8"))[1:]:
    c = l.rstrip("\n").split("\t")
    ann[(int(c[0]), c[2], int(c[3]), int(c[4]), int(c[5], 16))] += 1
print("mine", sum(mine.values()), "distinct", len(mine), "annex", sum(ann.values()), "distinct", len(ann))
print("equal multisets:", mine == ann)
print("maps", len({k[0] for k in mine}))
print("zero/unknown size reached (excluding FF):", zero_or_unknown)
per_op = collections.Counter(k[4] for k in mine.elements())
print("per opcode:", " ".join(f"{op:02X}:{n}" for op, n in sorted(per_op.items())))
slices = {"sm1": {0x60, 0x61, 0x3C, 0x3D}, "sm2": {0x6A, 0x6B, 0x6C, 0x6D}, "sm3": {0xA9, 0xAA, 0xB2, 0xB4, 0xB5, 0xB6, 0xB7, 0x9E},
          "sm4": {0x72, 0x88, 0x96, 0x97, 0xB3, 0x15, 0x66, 0x7A}, "sm5": {0x56}, "sm7": {0xB9}, "sm8": {0xBB, 0xC2}}
owned = set().union(*slices.values())
print("skipped opcodes without slice:", " ".join(f"{x:02X}" for x in sorted(skipped_ops - owned)))
for name, ops in slices.items():
    ks = [k for k in mine.elements() if k[4] in ops]
    print(name, len(ks), "maps", len({k[0] for k in ks}))
chain = {389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185}
combat = {14, 15, 44, 115, 116, 117, 362}
print("on chain:", [k for k in mine if k[0] in chain])
print("on combat:", [k for k in mine if k[0] in combat])
dups = [k for k, n in mine.items() if n > 1]
print("duplicate keys:", dups)
