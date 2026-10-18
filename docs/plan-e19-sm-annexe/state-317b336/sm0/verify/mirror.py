"""Independent check: Dispatch cases (EventProgramRunner.cs:563-2105) vs IntroTraceHarnessTests.ImplementedOpcodes; size table."""
import re
import sys

repo = sys.argv[1]
runner = open(f"{repo}/Alundra/Events/EventProgramRunner.cs", encoding="utf-8").read().split("\n")
cases = set()
for i in range(562, 2105):
    m = re.match(r"^            case 0x([0-9A-Fa-f]{2}):", runner[i])
    if m:
        cases.add(int(m.group(1), 16))
print("dispatch cases", len(cases))

harness = open(f"{repo}/Alundra.Tests/IntroTraceHarnessTests.cs", encoding="utf-8").read().split("\n")
start = next(i for i, l in enumerate(harness) if "HashSet<int> ImplementedOpcodes" in l)
end = next(i for i in range(start, len(harness)) if harness[i].strip() == "};")
mirror = set()
for line in harness[start:end]:
    code = line.split("//")[0]
    mirror.update(int(x, 16) for x in re.findall(r"0x([0-9A-Fa-f]{2})", code))
print("mirror lines", start + 1, end + 1, "size", len(mirror))
print("in Dispatch not in mirror:", " ".join(f"{x:02X}" for x in sorted(cases - mirror)))
print("in mirror not in Dispatch:", " ".join(f"{x:02X}" for x in sorted(mirror - cases)))

table = open(f"{repo}/Alundra/Events/EventOpcodeSizeTable.cs", encoding="utf-8").read().split("\n")
sizes = {}
for idx, line in enumerate(table):
    m = re.match(r"^\s*\{\s*0x([0-9A-Fa-f]{2}),\s*new\(\s*(-?\d+)\s*,\s*\"([^\"]*)\"", line)
    if m:
        sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3), idx + 1)
print("table entries", len(sizes))
skipped = sorted(op for op, (s, _, _) in sizes.items() if s > 0 and op not in cases and op not in (0x00, 0xFF))
print("size>0 without case:", len(skipped), " ".join(f"{x:02X}" for x in skipped))
zero = sorted(op for op, (s, _, _) in sizes.items() if s <= 0 and op not in (0x00, 0xFF))
print("size<=0:", " ".join(f"{x:02X}@{sizes[x][2]}" for x in zero))
absent = [op for op in range(256) if op not in sizes]
print("absent", len(absent), f"{absent[0]:02X}..{absent[-1]:02X}")
for op in (0x60, 0x61, 0x6A, 0x6B, 0x6C, 0x6D, 0x88, 0x97, 0xA9, 0xAA, 0xB4, 0xB5, 0xB6, 0xB7, 0xBB, 0xC2, 0x96, 0xB2, 0x3C, 0x3D, 0x9E, 0x72, 0xB3, 0x56, 0xB9, 0x5F):
    s, lab, ln = sizes[op]
    print(f"{op:02X} size {s} line {ln} label '{lab}'")
