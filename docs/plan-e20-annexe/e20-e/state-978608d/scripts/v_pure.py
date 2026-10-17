import json, re, subprocess, collections, sys
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
t = json.load(open(REPO + r"\tools\dll-structure\table.json", encoding="utf-8"))
ren = {}
dest = collections.defaultdict(set)
for ty in t["types"]:
    fin = ty.get("final") or ty.get("r3")
    if ty["old_name"] != fin["name"]:
        ren[ty["old_name"]] = fin["name"]
    dest[ty["old_file"]].add(fin["file"])
names = sorted(ren, key=len, reverse=True)
pat = re.compile(r"\b(" + "|".join(map(re.escape, names)) + r")\b")

def show(rev, path):
    return subprocess.run(["git", "-C", REPO, "show", f"{rev}:{path}"], capture_output=True).stdout.decode("utf-8-sig", "replace")

def norm(text, rename):
    out = []
    for line in text.splitlines():
        s = line.strip()
        if not s or s.startswith("using ") or s.startswith("namespace ") or s.startswith("#nullable"):
            continue
        if rename:
            s = pat.sub(lambda m: ren[m.group(1)], s)
        # drop namespace qualifications Alundra.X.Y. in front of identifiers (cref etc.)
        s = re.sub(r"\bAlundra\.(Scripts\.)?", "", s)
        s = re.sub(r"\b(Ai|Hero|Entities|Cells|Physics|Maps|Rendering|Effects|Items|Rng|Combat|Events|Audio|Projectiles|Enemies|Objects|UI|Hud|SaveGames|Transitions|Probes|Backdrops|Dialogue|Inventory)\.(?=[A-Z])", "", s)
        out.append(s)
    return collections.Counter(out)

for old in sys.argv[1:]:
    o = norm(show("9502f75", old), True)
    n = collections.Counter()
    for f in sorted(dest[old]):
        n += norm(show("HEAD", f), False)
    only_old = o - n
    only_new = n - o
    print(f"{old} -> {sorted(dest[old])}: old {sum(o.values())} new {sum(n.values())} only_old {sum(only_old.values())} only_new {sum(only_new.values())}")
    for k, v in list(only_old.items())[:200]:
        print("   -", v, k[:200])
    for k, v in list(only_new.items())[:200]:
        print("   +", v, k[:200])
