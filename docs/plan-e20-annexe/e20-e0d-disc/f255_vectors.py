"""Exports the F255 decision table, computed by the REAL code (f255_oracle machine), as a TSV the DLL's tests can embed: one row per vector.
Columns: guard (none|mask|menu|carried|dialog), contents, chest88 (before), spawn (ok|fail), stock (count of the item in the bag, only meaningful for 0x24-0x27 and 0x29),
outcome, chest88_after, item_rise_force, item_delay, item_kind, item_state, item_cantake, item_fanfare, calls (spawn|block|bgm|open:<etc>:<mode>), item_flags_after (from 0x8180)."""
import sys
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import f255_oracle as F

OUT = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest/f255_vectors.tsv'
items = [0, 1, 2, 3, 4, 6, 0x0E, 0x10, 0x13, 0x1A, 0x20, 0x24, 0x25, 0x26, 0x27, 0x29, 0x2B, 0x3C, 0x3D, 0x45, 0x47, 0x48, 0x4F, 0x50, 0x53]
rows = []
def one(guard, contents, chest88, spawn_ok, stock):
    kw = dict(contents=contents, chest88=chest88, spawn=(F.ITEM if spawn_ok else 0), itemflags=0x8180)
    if guard == 'mask':
        kw['g_mask'] = 8
    elif guard == 'menu':
        kw['g_menu'] = 1
    elif guard == 'carried':
        kw['carried'] = F.ITEM
    elif guard == 'dialog':
        kw['dlg'] = 4
    if stock is not None:
        kw['count'] = {contents: stock}
    F.setup(**kw)
    snap, j = F.run()
    calls = []
    for t in j:
        if t[0] == 'spawn':
            calls.append('spawn(type=%d,z+=%#x)' % (t[3], t[6] - 0x00440000))
        elif t[0] == 'block':
            calls.append('block')
        elif t[0] == 'bgm':
            calls.append('bgm(%d)' % t[1])
        elif t[0] == 'open':
            calls.append('open(etc=%#x,mode=%d)' % (t[1][1], t[2]))
    it = snap['item']
    if guard != 'none':
        outcome = 'guard'
    elif contents == 0 or chest88 != 0:
        outcome = 'empty_msg'
    elif not spawn_ok:
        outcome = 'spawn_failed_msg'
    else:
        outcome = 'opens'
    rows.append((guard, '%#x' % contents, chest88, 'ok' if spawn_ok else 'fail', '' if stock is None else stock, outcome, snap['chest'][0x88],
                 '%#x' % it[0xB8], it[0x278], it[0x274], it[0x27C], it[0x284], it[0x288], '|'.join(calls), '%#x' % it[0x6C]))

for g in ('mask', 'menu', 'carried', 'dialog'):
    one(g, 0x24, 0, True, 0)
for c in items:
    for b88 in (0, 1, 2):
        for sp in (True, False):
            stocks = [None]
            if c in (0x24, 0x25, 0x26, 0x27, 0x29):
                stocks = [0, 1, 8, 9]
            for s in stocks:
                one('none', c, b88, sp, s)
with open(OUT, 'w', encoding='utf-8') as f:
    f.write('\t'.join(['guard', 'contents', 'chest88_before', 'spawn', 'stock', 'outcome', 'chest88_after', 'item_riseforce', 'item_delay', 'item_kind', 'item_state', 'item_cantake', 'item_fanfare', 'calls', 'item_flags_after']) + '\n')
    for r in rows:
        f.write('\t'.join(str(x) for x in r) + '\n')
print('rows', len(rows), '->', OUT)
