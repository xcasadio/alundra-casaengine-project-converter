import re, sys
# Extract line-number citations (`:NNN` / `:NNN-MMM`) with the nearest preceding file mention, from a text file.
path = sys.argv[1]
base = int(sys.argv[2]) if len(sys.argv) > 2 else 1
lines = open(path, encoding='utf-8').read().split('\n')
filepat = re.compile(r'(Alundra\.Tests/[\w/.]+|Alundra/[\w/.]+|\b[A-Z][A-Za-z0-9_.]*\.cs)')
citepat = re.compile(r'`?(?<![0-9A-Za-z])(:[0-9]+(?:\s*[-–,]\s*[0-9]+)*)`?')
for i, ln in enumerate(lines):
    # tokens in order
    events = []
    for m in filepat.finditer(ln):
        events.append((m.start(), 'F', m.group(1)))
    for m in re.finditer(r'(?<![0-9A-Za-z0-9x])(:[0-9]{2,5}(?:[-–][0-9]{2,5})?(?:,\s*:?[0-9]{2,5})*)', ln):
        events.append((m.start(), 'C', m.group(1)))
    events.sort()
    cur = None
    for pos, kind, val in events:
        if kind == 'F':
            cur = val
        else:
            ctx = ln[max(0, pos-70):pos+len(val)+30].replace('\n', ' ')
            print(f"{base+i}\t{cur}\t{val}\t{ctx}")
