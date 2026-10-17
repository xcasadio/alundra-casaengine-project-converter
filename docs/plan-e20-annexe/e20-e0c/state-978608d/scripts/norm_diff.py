import sys, difflib, json
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20-e-state/e0c')
from map_lines import git_show, load_lines, REPO, OLD

def main(pairs):
    for old_path, new_path in pairs:
        old = git_show(OLD, old_path)
        if old is None:
            print('OLD MISSING', old_path); continue
        try:
            new = open(REPO + '/' + new_path, encoding='utf-8', errors='replace').read()
        except FileNotFoundError:
            print('NEW MISSING', new_path); continue
        o = load_lines(old, True)
        n = load_lines(new, False)
        a = [x[1] for x in o]; b = [x[1] for x in n]
        sm = difflib.SequenceMatcher(None, a, b, autojunk=False)
        import re
        def code(x):
            x = re.sub(r'\s*//.*$', '', x) if x.lstrip().startswith('//') or ('"' not in x) else x
            return x.strip()
        diffs = []
        for t, i1, i2, j1, j2 in sm.get_opcodes():
            if t == 'equal': continue
            ca = [code(x) for x in a[i1:i2] if code(x)]
            cb = [code(x) for x in b[j1:j2] if code(x)]
            if ca != cb:
                diffs.append((t, i1, i2, j1, j2))
        print(f'## {old_path} -> {new_path}: {len(diffs)} non-equal blocks (old {len(o)} lines, new {len(n)} lines)')
        for t, i1, i2, j1, j2 in diffs[:25]:
            print(f'  {t} old {o[i1][0] if i1 < len(o) else "-"}..{o[i2-1][0] if i2 > i1 else "-"} new {n[j1][0] if j1 < len(n) else "-"}..{n[j2-1][0] if j2 > j1 else "-"}')
            for x in a[i1:min(i2, i1 + 3)]:
                print('     - ' + x[:150])
            for x in b[j1:min(j2, j1 + 3)]:
                print('     + ' + x[:150])

if __name__ == '__main__':
    main(json.loads(sys.argv[1]))
