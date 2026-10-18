#!/usr/bin/env python3
"""Independent line map: old file at 9502f75 -> new file at HEAD.
Usage: python v_map.py OLDPATH NEWPATH spec [spec...]
spec = N or N-M (old line numbers). Prints old line, mapped new line, and both texts.
Renames from tools/dll-structure/table.json (final names) are applied to old text before matching."""
import subprocess, sys, json, re, difflib

REPO = "D:/development/repo/alundra-casaengine-project-converter"
BASE = "9502f75"

def show(rev, path):
    return subprocess.run(["git", "-C", REPO, "show", f"{rev}:{path}"], capture_output=True, check=True).stdout.decode("utf-8-sig", errors="replace").splitlines()

def renames():
    t = json.load(open(REPO + "/tools/dll-structure/table.json", encoding="utf-8"))
    r = {}
    for e in t["types"]:
        if e["old_name"] != e["final"]["name"]:
            r[e["old_name"]] = e["final"]["name"]
    return r

def norm(s, rn, rx):
    s = rx.sub(lambda m: rn[m.group(0)], s)
    return re.sub(r"\s+", " ", s).strip()

def build(oldp, newp):
    old = show(BASE, oldp)
    new = show("HEAD", newp)
    rn = renames()
    rx = re.compile(r"\b(" + "|".join(sorted(map(re.escape, rn), key=len, reverse=True)) + r")\b")
    a = [norm(x, rn, rx) for x in old]
    b = [norm(x, rn, rx) for x in new]
    sm = difflib.SequenceMatcher(None, a, b, autojunk=False)
    m = {}
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            for k in range(i2 - i1):
                m[i1 + k + 1] = j1 + k + 1
    return old, new, m

def main():
    oldp, newp = sys.argv[1], sys.argv[2]
    old, new, m = build(oldp, newp)
    for spec in sys.argv[3:]:
        if "-" in spec:
            lo, hi = map(int, spec.split("-"))
        else:
            lo = hi = int(spec)
        for n in sorted({lo, hi}):
            nn = m.get(n)
            ot = old[n - 1].strip() if n <= len(old) else "<EOF>"
            nt = new[nn - 1].strip() if nn else "<UNMAPPED>"
            print(f"{spec:>12} old {n:5d} -> new {str(nn):>5} | {ot[:90]}")
            if nn is None or re.sub(r"\s+", " ", ot) != re.sub(r"\s+", " ", nt):
                print(f"{'':>12}                       NEW: {nt[:90]}")

if __name__ == "__main__":
    main()
