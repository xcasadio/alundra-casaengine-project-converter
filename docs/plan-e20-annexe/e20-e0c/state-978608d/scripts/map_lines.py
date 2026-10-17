import json, re, subprocess, sys, difflib

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
OLD = '2010ec3'

def git_show(rev, path):
    r = subprocess.run(['git', '-C', REPO, 'show', f'{rev}:{path}'], capture_output=True)
    if r.returncode != 0:
        return None
    return r.stdout.decode('utf-8', 'replace')

table = json.load(open(REPO + '/tools/dll-structure/table.json', encoding='utf-8'))
ren = {}
for e in table['types']:
    if e['old_name'] != e['final']['name']:
        ren[e['old_name']] = e['final']['name']
# longest first
pat = re.compile(r'\b(' + '|'.join(sorted(map(re.escape, ren), key=len, reverse=True)) + r')\b')

def norm(line):
    s = pat.sub(lambda m: ren[m.group(1)], line)
    s = s.replace('Alundra.Scripts.', '')
    return s.strip()

def keep(line):
    t = line.strip()
    if t.startswith('using ') and t.endswith(';') and not t.startswith('using ('):
        return False
    if t.startswith('namespace '):
        return False
    return True

def load_lines(text, do_norm):
    out = []  # (orig_lineno, normalized)
    for i, l in enumerate(text.split('\n'), 1):
        l = l.rstrip('\r')
        if not keep(l):
            continue
        out.append((i, norm(l) if do_norm else l.strip()))
    return out

def map_file(old_path, new_path, lines_wanted):
    old = git_show(OLD, old_path)
    if old is None:
        print('OLD MISSING', old_path); return
    try:
        new = open(REPO + '/' + new_path, encoding='utf-8', errors='replace').read()
    except FileNotFoundError:
        print('NEW MISSING', new_path); return
    o = load_lines(old, True)
    n = load_lines(new, False)
    sm = difflib.SequenceMatcher(None, [x[1] for x in o], [x[1] for x in n], autojunk=False)
    mapping = {}
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == 'equal':
            for k in range(i2 - i1):
                mapping[o[i1 + k][0]] = n[j1 + k][0]
    newlines = new.split('\n')
    oldlines = old.split('\n')
    for ln in lines_wanted:
        h = mapping.get(ln)
        ol = oldlines[ln - 1].rstrip('\r').strip() if ln - 1 < len(oldlines) else '<beyond EOF>'
        if h is None:
            print(f'  {old_path}:{ln} -> NOT MAPPED (changed). old text: {ol[:140]}')
        else:
            print(f'  {old_path}:{ln} -> {new_path}:{h}   | OLD: {ol[:110]} || NEW: {newlines[h-1].strip()[:110]}')

if __name__ == '__main__':
    spec = json.loads(sys.argv[1])
    for old_path, new_path, lines in spec:
        print(f'## {old_path} -> {new_path}')
        map_file(old_path, new_path, lines)
