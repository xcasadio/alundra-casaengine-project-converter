"""Independent check: print old line (rev 9502f75) next to claimed HEAD line.

Usage: python -I pair.py <repo> <oldpath> <newpath> old:new [old:new ...]
A spec may be a range old1-old2:new1-new2 (prints both ends).
"""
import subprocess
import sys

REV = "9502f75"


def old_lines(repo, path):
    out = subprocess.run(["git", "-C", repo, "show", f"{REV}:{path}"], capture_output=True)
    if out.returncode != 0:
        sys.exit(f"cannot read {REV}:{path}")
    return out.stdout.decode("utf-8", "replace").split("\n")


def new_lines(repo, path):
    with open(f"{repo}/{path}", "rb") as h:
        return h.read().decode("utf-8", "replace").split("\n")


def get(lines, n):
    if 1 <= n <= len(lines):
        return lines[n - 1].strip().rstrip("\r")[:120]
    return "<out of file>"


def main():
    repo, op, np_ = sys.argv[1:4]
    o = old_lines(repo, op)
    n = new_lines(repo, np_)
    print(f"# {op} ({len(o)}) -> {np_} ({len(n)})")
    for spec in sys.argv[4:]:
        a, b = spec.split(":")
        olds = [int(x) for x in a.split("-")]
        news = [int(x) for x in b.split("-")]
        for ol, nl in zip(olds, news):
            to, tn = get(o, ol), get(n, nl)
            same = "=" if to.replace("Alundra", "") == tn.replace("Alundra", "") else "DIFF"
            print(f"{ol:>5} | {to}")
            print(f"{nl:>5} | {tn}   [{same}]")
        print("---")


main()
