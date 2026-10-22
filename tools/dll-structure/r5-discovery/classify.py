"""R5 discovery step 2: the automatic proposal of a folder per test file (rule below) + the review view.
Reads evidence.json; writes auto.json. build.py is the authority: it applies the author's answer to Q-R-3 (decisions.py: structural entries, then the
name, then the subject) over this proposal, whose rule 3 and 4 are only the fallback of the "refs" basis.

Rule (INFERENCE, stated in the report):
  1. a test class mirrors the DLL type it is NAMED after: the longest DLL type name (final name) that is a CamelCase prefix of the file stem
     (Alundra prefix and Tests/Support/Oracle... suffix removed) gives its folder;
  2. else the first CamelCase word(s) of the stem name a DLL folder (Hero -> Hero, Camera -> Cameras...);
  3. else the folder holding most DISTINCTIVE references (IL calls/news/fields/types + lexical identifiers, DLL types referenced by <= 20 % of the
     test files; the ubiquitous fixture types such as EntityScriptProxy, GameState, WorldProxy are discounted);
  4. else the folder with most idf-weighted references; else none.
"""
import json
import math
import os
import sys
import collections

sys.stdout.reconfigure(encoding='utf-8')
HERE = __file__.rsplit('\\', 1)[0].rsplit('/', 1)[0] if ('\\' in __file__ or '/' in __file__) else '.'
d = json.load(open(os.environ.get('R5_WORK', '.') + '/evidence.json', encoding='utf-8'))
ev, df, N = d['ev'], d['df'], d['N']

import os
sys.path.insert(0, os.getcwd())
from common import load_table, dll_world  # noqa: E402

table = load_table()
cur, name_idx, unmatched, _ = dll_world(table)

UBIQ_MAX = 0.20 * N


def dist_scores(e):
    s = collections.Counter()
    for full in set(e['il']) | set(e['lex']):
        if full not in cur or df.get(full, 0) > UBIQ_MAX:
            continue
        a = e['il'].get(full, [0, 0, 0, 0])
        s[cur[full]['final_folder']] += sum(a) + e['lex'].get(full, 0)
    return s


def top2(counter):
    items = [(k, v) for k, v in counter.most_common() if v > 0]
    return items[:2]


auto = {}
for rel, e in ev.items():
    dist = dist_scores(e)
    idf = collections.Counter(e['folder_idf'])
    nt, tk = e['name_type'], e['name_tok']
    src, folder, alts = None, None, []
    if nt:
        folder, src = nt[1], 'name-type:%s' % nt[0]
    elif tk and len(tk[1]) == 1:
        folder, src = tk[1][0], 'name-word:%s' % tk[0]
    elif dist and sum(dist.values()) >= 3:
        folder, src = dist.most_common(1)[0][0], 'refs-distinctive'
    elif idf and sum(idf.values()) > 0:
        folder, src = idf.most_common(1)[0][0], 'refs-idf'
    else:
        folder, src = None, 'none'
    auto[rel] = dict(folder=folder, src=src, dist=dict(dist), idf_top=top2(idf), dist_top=top2(dist),
                     name_type=nt, name_tok=tk)
json.dump(auto, open(os.environ.get('R5_WORK', '.') + '/auto.json', 'w', encoding='utf-8'), indent=1)

c = collections.Counter((v['folder'], v['src'].split(':')[0]) for v in auto.values())
fc = collections.Counter(v['folder'] for v in auto.values())
print('auto folders:')
for k, v in sorted(fc.items(), key=lambda x: str(x[0])):
    print('  %-22s %3d' % (k, v))
disagree = []
for rel, v in sorted(auto.items()):
    if v['dist_top'] and v['folder'] != v['dist_top'][0][0]:
        disagree.append(rel)
print(len(disagree), 'files where the auto folder differs from the distinctive-refs top folder')
