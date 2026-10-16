#!/usr/bin/env python
"""Extract, for each case 0xNN of AlundraEventProgramRunner.Dispatch, its comment block and some keyword flags (label script2). Read-only."""
import re, json, os
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
OUT = os.path.dirname(os.path.abspath(__file__))
src = open(REPO + '/Alundra/Scripts/AlundraEventProgramRunner.cs', encoding='utf-8').read().split('\n')
cases = {int(k, 16): v for k, v in json.load(open(OUT + '/cases.json')).items()}
order = sorted(cases.items(), key=lambda kv: kv[1])
end_dispatch = 2154
res = {}
for n, (op, line) in enumerate(order):
    nxt = order[n + 1][1] if n + 1 < len(order) else end_dispatch
    block = src[line - 1:nxt - 1]
    text = '\n'.join(block)
    comment = ' '.join(re.sub(r'^\s*//\s?', '', l.split('//', 1)[1]) if '//' in l else '' for l in block)
    comment = re.sub(r'\s+', ' ', comment).strip()
    addrs = sorted(set(re.findall(r'0x80[0-9A-Fa-f]{6}', text)))
    # handler call target (helper method) if any
    kw = {
        'per_the_binary': bool(re.search(r'PER THE BINARY|per the binary|\[binaire\]|binary', text)),
        'degraded': bool(re.search(r'[Dd]egraded|LogDegraded', text)),
        'not_ported': bool(re.search(r'not ported|NOT ported|unported|not implemented', text)),
        'frozen': bool(re.search(r'[Ff]rozen|FROZEN', text)),
        'deviation': bool(re.search(r'[Dd]eviation|V1 deviation|accepted|écart', text)),
        'decomp_ref': bool(re.search(r'EntityEventHandlers\.cs', text)),
        'script_name': (re.search(r'Script_\w+', text) or [None])[0] if re.search(r'Script_\w+', text) else None,
    }
    res['%02X' % op] = {'line': line, 'end_line': nxt - 1, 'addresses': addrs, 'kw': kw, 'comment_head': comment[:240]}
json.dump(res, open(OUT + '/handler-doc.json', 'w', encoding='utf-8'), indent=1)
nb = [k for k, v in res.items() if not v['addresses'] and not v['kw']['per_the_binary']]
print('cases', len(res), 'without any binary address/word in their block:', len(nb))
print(' '.join(nb))
