import subprocess, sys
# usage: show.py REV PATH START END  (REV = HEAD or a commit; WT = working tree)
REPO = r"D:/development/repo/alundra-casaengine-project-converter"
rev, path, a, b = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
if rev == "WT":
    text = open(REPO + "/" + path, encoding="utf-8-sig").read()
else:
    text = subprocess.run(["git", "-C", REPO, "show", f"{rev}:{path}"], capture_output=True).stdout.decode("utf-8-sig", "replace")
lines = text.splitlines()
for i in range(a, min(b, len(lines)) + 1):
    print(f"{i}: {lines[i-1]}")
