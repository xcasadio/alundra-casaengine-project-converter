import subprocess, sys, os
REPO = r"D:/development/repo/alundra-casaengine-project-converter"
_cache = {}
def lines(rev, path):
    key = (rev, path)
    if key not in _cache:
        try:
            out = subprocess.run(["git", "-C", REPO, "show", f"{rev}:{path}"], capture_output=True, check=True).stdout
            _cache[key] = out.decode("utf-8", "replace").splitlines()
        except subprocess.CalledProcessError:
            _cache[key] = None
    return _cache[key]

def show(rev, path, a, b=None):
    b = b or a
    L = lines(rev, path)
    if L is None:
        print(f"  [{rev}] {path}: NOT FOUND"); return
    for i in range(a, b + 1):
        if 1 <= i <= len(L):
            print(f"  [{rev}] {os.path.basename(path)}:{i}: {L[i-1].strip()[:170]}")

# input file: lines "label|oldrev|oldpath|a-b|newpath|c-d"
for raw in open(sys.argv[1], encoding="utf-8"):
    raw = raw.rstrip("\n")
    if not raw or raw.startswith("#"):
        continue
    p = raw.split("|")
    label = p[0]
    print(f"== {label}")
    if p[1]:
        r = p[3].split("-"); show(p[1], p[2], int(r[0]), int(r[-1]))
    if len(p) > 4 and p[4]:
        r = p[5].split("-"); show("HEAD", p[4], int(r[0]), int(r[-1]))
