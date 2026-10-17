import json, re, subprocess, sys, difflib
# Compare a file at 146e1d81 (before R2..R4) with its HEAD counterpart, after applying the table renames to the old text
# and dropping using/namespace lines and blank lines. Read-only (git show + the working tree).
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
OLD = '146e1d81'
t = json.load(open(REPO + '/tools/dll-structure/table.json', encoding='utf-8'))
ren = {}
for ty in t['types']:
    if ty['old_name'] != ty['final']['name']:
        ren[ty['old_name']] = ty['final']['name']
pat = re.compile(r'\b(' + '|'.join(sorted(map(re.escape, ren), key=len, reverse=True)) + r')\b')

def norm(text, rename):
    out = []
    for ln in text.split('\n'):
        s = ln.strip()
        if s.startswith('using ') or s.startswith('namespace ') or s == '' or s.startswith('//'):
            continue
        ln = re.sub(r'\s//.*$', '', ln)   # trailing comment (code lines only)
        if rename:
            ln = pat.sub(lambda m: ren[m.group(1)], ln)
        ln = re.sub(r'(Alundra\.(Scripts|[A-Z][A-Za-z]+(\.[A-Z][A-Za-z]+)?)|Maps|Loop|Session|Entities|Spawning|Events|Ai|Enemies|Projectiles|Objects|Hero|Combat|Items|Rng|Physics|Cells|Cameras|Rendering|Backdrops|Effects|Audio|Transitions|Dialogues|UI|SaveGames|Probes)\.(?=[A-Z])', '', ln)
        out.append(ln.rstrip())
    return out

def show(rev, path):
    return subprocess.run(['git', '-C', REPO, 'show', f'{rev}:{path}'], capture_output=True).stdout.decode('utf-8-sig')

pairs = {}
for ty in t['types']:
    if ty['main']:
        pairs[ty['old_file']] = ty['final']['file']
for old in sys.argv[1:]:
    new = pairs.get(old)
    a = norm(show(OLD, old), True)
    b = norm(open(REPO + '/' + new, encoding='utf-8-sig').read(), False)
    d = [l for l in difflib.unified_diff(a, b, lineterm='', n=0) if not l.startswith(('---', '+++', '@@'))]
    print(f'== {old} -> {new}: {len(a)} vs {len(b)} normalised lines, {len(d)} differing lines')
    for l in d[:30]:
        print('   ', l[:170])
