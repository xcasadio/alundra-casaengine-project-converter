#!/usr/bin/env python3
# Maps old (9502f75) line numbers of a DLL file to the HEAD line numbers of the same code.
# Usage: linemap.py <old_path> <new_path> <line_or_range> [...]   (reads git objects only)
import subprocess, sys, json, re, difflib

REPO = "D:/development/repo/alundra-casaengine-project-converter"
OLD_REV = "9502f75"


def git_show(rev, path):
    r = subprocess.run(["git", "-C", REPO, "show", f"{rev}:{path}"], capture_output=True)
    if r.returncode != 0:
        return None
    return r.stdout.decode("utf-8-sig").replace("\r\n", "\n").split("\n")


table = json.load(open(REPO + "/tools/dll-structure/table.json", encoding="utf-8"))
ren = {}
for t in table["types"]:
    o, n = t["old_name"], t["final"]["name"]
    if o != n:
        ren[o] = n
pat = re.compile(r"\b(" + "|".join(sorted(map(re.escape, ren), key=len, reverse=True)) + r")\b")


def norm(s):
    s = pat.sub(lambda m: ren[m.group(1)], s)
    return s.strip()


def build(old_path, new_path):
    old = git_show(OLD_REV, old_path)
    new = git_show("HEAD", new_path)
    on = [norm(x) for x in old]
    nn = [x.strip() for x in new]
    sm = difflib.SequenceMatcher(None, on, nn, autojunk=False)
    m = {}
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            for k in range(i2 - i1):
                m[i1 + k + 1] = j1 + k + 1
    return old, new, m, sm


def near(m, ln, direction):
    k = ln
    while 1 <= k <= 10**6:
        if k in m:
            return m[k], k
        k += direction
        if k < 1 or k > 100000:
            break
    return None, None


if __name__ == "__main__":
    old_path, new_path = sys.argv[1], sys.argv[2]
    old, new, m, sm = build(old_path, new_path)
    print(f"old {len(old)} lines, new {len(new)} lines, ratio {sm.ratio():.4f}, matched {len(m)}")
    for a in sys.argv[3:]:
        a = a.strip(":~")
        parts = re.split(r"-", a)
        lo = int(parts[0]); hi = int(parts[1]) if len(parts) > 1 else lo
        out = []
        for ln in (lo, hi):
            if ln in m:
                out.append(f"{m[ln]}")
            else:
                f, fk = near(m, ln, +1)
                b, bk = near(m, ln, -1)
                out.append(f"~(unmatched; before {b}@{bk}, after {f}@{fk})")
        print(f"{a}: old[{lo}]={old[lo-1].strip()[:70]!r} -> {out[0]} ; old[{hi}]={old[hi-1].strip()[:70]!r} -> {out[1]}")
