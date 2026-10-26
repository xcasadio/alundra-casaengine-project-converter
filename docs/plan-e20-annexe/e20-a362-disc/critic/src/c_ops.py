import re
txt = open('../a362-h3a/out/map362_programs.txt').read()
ops=set()
for m in re.finditer(r'^\s+(\d+)\s+([0-9a-f]{2})\s', txt, re.M):
    ops.add(int(m.group(2),16))
for m in re.finditer(r'^\s*(?:\d+)\s+([0-9a-f]{2})\s', txt, re.M): pass
print(len(ops), sorted(hex(o) for o in ops))
src = open('head/EventProgramRunner.cs').read()
# only the Dispatch method region
i = src.index('private int Dispatch(')
cases = set(int(x,16) for x in re.findall(r'case 0x([0-9A-Fa-f]{1,2}):', src[i:]))
missing = [hex(o) for o in sorted(ops) if o not in cases and o not in (0,0xff)]
print('missing in Dispatch:', missing)
