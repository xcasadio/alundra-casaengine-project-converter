#!/usr/bin/env python3
"""Counter-check helper (read-only on the repo): enumerate every file:line citation of the report
under check (state.md of the previous agent), resolve the file in the repo and print the cited
line(s) with the claim context, so each citation can be eyeballed.  Usage: python -B dump_cites.py <state.md> <out.txt>"""
import os
import re
import sys

REPO = "D:/development/repo/alundra-casaengine-project-converter"
ROOTS = ["Alundra", "Alundra.Tests", "alundra-project", "docs", "tools"]

index = {}
for root in ROOTS:
    for dp, dn, fn in os.walk(os.path.join(REPO, root)):
        dn[:] = [d for d in dn if d not in ("bin", "obj", ".git", "node_modules", "out")]
        for f in fn:
            index.setdefault(f, []).append(os.path.join(dp, f).replace("\\", "/"))

lines_cache = {}


def lines_of(path):
    if path not in lines_cache:
        with open(path, encoding="utf-8", errors="replace") as fh:
            lines_cache[path] = fh.read().split("\n")
    return lines_cache[path]


def resolve(name):
    base = os.path.basename(name)
    cands = index.get(base, [])
    if "/" in name:
        tail = name.replace("\\", "/")
        narrowed = [c for c in cands if c.endswith(tail)]
        if narrowed:
            cands = narrowed
    return cands


FILE_RE = re.compile(r"([A-Za-z0-9_./\-]+\.(?:cs|json|md|py|tsv|buttonsMapping|anim2d))(?::(\d+)(?:-(\d+))?)")
BARE_RE = re.compile(r"(?<![A-Za-z0-9_.\-/])`?:(\d+)(?:-(\d+))?")

src = open(sys.argv[1], encoding="utf-8").read().split("\n")
out = []
n = 0
for ln, text in enumerate(src, 1):
    last_file = None
    pos = 0
    items = []
    for m in FILE_RE.finditer(text):
        items.append((m.start(), m.group(1), int(m.group(2)), int(m.group(3)) if m.group(3) else None))
    # bare ones: resolve to the last file named before them on the same line
    for m in BARE_RE.finditer(text):
        before = [i for i in items if i[0] < m.start()]
        # also files mentioned without a line number
        names = list(re.finditer(r"([A-Za-z0-9_./\-]+\.cs)", text[: m.start()]))
        fname = names[-1].group(1) if names else None
        if fname:
            items.append((m.start(), fname, int(m.group(1)), int(m.group(2)) if m.group(2) else None))
    items.sort()
    for start, fname, a, b in items:
        n += 1
        cands = resolve(fname)
        ctx = text[max(0, start - 40): start + 70].replace("\n", " ")
        if not cands:
            out.append(f"[{ln}] UNRESOLVED {fname}:{a} | ctx: {ctx}")
            continue
        path = cands[0]
        ls = lines_of(path)
        hi = b if b else a
        if a > len(ls):
            out.append(f"[{ln}] {fname}:{a} OUT OF RANGE (len {len(ls)}) | ctx: {ctx}")
            continue
        body = " // ".join(x.strip()[:110] for x in ls[a - 1: min(hi, a + 3)])
        extra = "" if len(cands) == 1 else f" (ambiguous x{len(cands)})"
        out.append(f"[{ln}] {os.path.relpath(path, REPO).replace(chr(92), '/')}:{a}{('-' + str(b)) if b else ''}{extra}\n      ctx: {ctx}\n      src: {body}")
with open(sys.argv[2], "w", encoding="utf-8") as fh:
    fh.write(f"{n} citations\n" + "\n".join(out) + "\n")
print(n, "citations ->", sys.argv[2])
