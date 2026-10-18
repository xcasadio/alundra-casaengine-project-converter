"""Independent check: offset profile old(9502f75) -> HEAD for a file, via difflib on lines with 'Alundra' removed.

Usage: python -I offsets.py <repo> <oldpath> <newpath>
Prints runs of constant offset (old_start-old_end -> delta) and unmatched old ranges.
"""
import difflib
import re
import subprocess
import sys

REV = "9502f75"


def norm(s):
    s = s.rstrip("\r").strip()
    s = re.sub(r"Alundra", "", s)
    return s


def main():
    repo, op, np_ = sys.argv[1:4]
    o = subprocess.run(["git", "-C", repo, "show", f"{REV}:{op}"], capture_output=True).stdout.decode("utf-8", "replace").split("\n")
    n = open(f"{repo}/{np_}", "rb").read().decode("utf-8", "replace").split("\n")
    sm = difflib.SequenceMatcher(None, [norm(x) for x in o], [norm(x) for x in n], autojunk=False)
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            print(f"equal old {i1+1}-{i2} -> new {j1+1}-{j2} delta {j1-i1:+d}")
        else:
            print(f"{tag} old {i1+1}-{i2} -> new {j1+1}-{j2}")


main()
