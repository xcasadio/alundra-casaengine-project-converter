"""Read-only helper: translate a (file, line) citation of the state 9502f75 into the HEAD line.

Usage: python -I linemap.py <repo> <oldpath> <newpath> <line> [<line-or-range> ...]
Matches lines after applying the type renames of tools/dll-structure/table.json (old_name -> final name).
Prints, per citation: old line text, HEAD line(s), status SAME (identical up to the rename) or CHANGED.
"""
import difflib
import json
import re
import subprocess
import sys

OLD_REV = "9502f75"


def git_show(repo, rev, path):
    out = subprocess.run(["git", "-C", repo, "show", f"{rev}:{path}"], capture_output=True)
    if out.returncode != 0:
        raise SystemExit(f"cannot read {rev}:{path}")
    return out.stdout.decode("utf-8", "replace").split("\n")


def read_head(repo, path):
    with open(f"{repo}/{path}", "rb") as handle:
        return handle.read().decode("utf-8", "replace").split("\n")


def build_renames(repo):
    table = json.load(open(f"{repo}/tools/dll-structure/table.json", encoding="utf-8"))
    pairs = {}
    for ty in table["types"]:
        if ty["old_name"] != ty["final"]["name"]:
            pairs[ty["old_name"]] = ty["final"]["name"]
    names = sorted(pairs, key=len, reverse=True)
    pattern = re.compile(r"\b(" + "|".join(re.escape(n) for n in names) + r")\b")
    return lambda text: pattern.sub(lambda m: pairs[m.group(1)], text)


def parse_lines(args):
    lines = []
    for arg in args:
        if "-" in arg:
            lo, hi = arg.split("-")
            lines.append((int(lo), int(hi)))
        else:
            lines.append((int(arg), int(arg)))
    return lines


def main():
    repo, old_path, new_path = sys.argv[1:4]
    citations = parse_lines(sys.argv[4:])
    rename = build_renames(repo)
    old = git_show(repo, OLD_REV, old_path)
    new = read_head(repo, new_path)
    norm_old = [rename(line).rstrip("\r") for line in old]
    norm_new = [line.rstrip("\r") for line in new]
    matcher = difflib.SequenceMatcher(None, norm_old, norm_new, autojunk=False)
    mapping = {}
    for tag, i1, i2, j1, j2 in matcher.get_opcodes():
        if tag == "equal":
            for k in range(i2 - i1):
                mapping[i1 + k] = (j1 + k, "SAME")
        elif tag == "replace" and (i2 - i1) == (j2 - j1):
            for k in range(i2 - i1):
                mapping[i1 + k] = (j1 + k, "CHANGED")
    print(f"# {old_path}@{OLD_REV} ({len(old)} l.) -> {new_path}@HEAD ({len(new)} l.)")
    for lo, hi in citations:
        parts = []
        for line in range(lo, hi + 1):
            hit = mapping.get(line - 1)
            parts.append(None if hit is None else (hit[0] + 1, hit[1]))
        first = parts[0]
        last = parts[-1]
        statuses = {p[1] for p in parts if p}
        missing = sum(1 for p in parts if p is None)
        text_old = old[lo - 1].strip()[:110] if lo - 1 < len(old) else "<out of file>"
        if first and last:
            span = f"{first[0]}" if lo == hi else f"{first[0]}-{last[0]}"
        elif first:
            span = f"{first[0]}-?"
        elif last:
            span = f"?-{last[0]}"
        else:
            span = "NOT MAPPED"
        label = f"{lo}" if lo == hi else f"{lo}-{hi}"
        flag = "/".join(sorted(statuses)) if statuses else "-"
        if missing:
            flag += f" (+{missing} unmapped)"
        print(f"{label}\t->\t{span}\t{flag}\t| {text_old}")


main()
