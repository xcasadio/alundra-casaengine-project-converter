"""Own check: every `File.cs:NNN[-MMM]` citation of the report must at least exist in range (file found + line <= length). Read-only."""
import re, os, sys, glob
ROOT = r"D:/development/repo/alundra-casaengine-project-converter/"
text = open(r"C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4b/dll/state.md", encoding="utf-8").read()
files = {}
for p in glob.glob(ROOT + "Alundra/**/*.cs", recursive=True) + glob.glob(ROOT + "Alundra.Tests/**/*.cs", recursive=True):
    files.setdefault(os.path.basename(p), []).append(p)
pat = re.compile(r"([A-Za-z0-9_\.]+\.cs):(\d+)(?:-(\d+))?")
bad = []; n = 0; amb = 0
for m in pat.finditer(text):
    name, a, b = m.group(1), int(m.group(2)), m.group(3)
    cand = files.get(name)
    if not cand:
        bad.append((m.group(0), "file not found (decompilation or abbreviated)"))
        continue
    if len(cand) > 1: amb += 1
    ok = False
    for p in cand:
        L = sum(1 for _ in open(p, encoding="utf-8-sig", errors="replace"))
        if a <= L and (b is None or int(b) <= L): ok = True
    n += 1
    if not ok: bad.append((m.group(0), "line out of range"))
print("citations with a file name:", n, "ambiguous basenames:", amb)
for b in bad: print("  ", b)
