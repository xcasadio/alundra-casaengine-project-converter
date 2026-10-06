"""Writes states.json (read by the C# probe) and values_all.json (read by compare.py) from the binary's value tables.

values.json comes from the annex of E19.f3 (model/choice_model.py, equal to the real binary code on 2000 random pad sequences); V10 (the answer the test hook gives for NON:
Right at N+19 then Cross at N+20) is run here through the same model, from a COPY of the model in ./annex (nothing is written in the repository).
"""
import json
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'annex', 'model'))
import scenarios as sc  # noqa: E402  (the annex's own scenario runner)
import choice_model as cm  # noqa: E402

d = json.load(open(os.path.join(HERE, 'annex', 'values.json'), encoding='utf-8'))
allv = {}
scn = {}


def add(sid, name, pad, default_sel, anim0, table):
    allv[sid] = dict(name=name, pad=pad, default_sel=default_sel, anim0=anim0, table=table)
    scn[sid] = dict(default_sel=default_sel, anim0=anim0, pad={str(k): v for k, v in pad.items()}, last_t=len(table) - 1)


for s in d['bare']:
    sid = s['name'].split()[0]
    pad = {int(k[2:]): int(v, 16) for k, v in s['pad'].items()}
    add(sid, s['name'], pad, s['default_sel'], s['anim0'], s['table'])

v10 = sc.scenario_bare('V10 the test hook answers NON: Right at N+19 then Cross at N+20', {19: cm.PAD_RIGHT, 20: cm.PAD_CROSS})
add('V10', v10['name'], {19: cm.PAD_RIGHT, 20: cm.PAD_CROSS}, 0, 0, v10['table'])
print('V10 summary', v10['summary'], 'result', v10['result'])

# the cursor counter is never reset by the opener: a second choice starts where the first left it (35 drawn passes after a complete OUI, 9 after a cancel at N+10)
for sid, anim0 in (('V1_anim35', 35), ('V1_anim9', 9)):
    v = sc.scenario_bare('%s the V1 pad with the cursor counter at %d' % (sid, anim0), {19: cm.PAD_CROSS}, anim0=anim0)
    add(sid, v['name'], {19: cm.PAD_CROSS}, 0, anim0, v['table'])

json.dump(allv, open(os.path.join(HERE, 'values_all.json'), 'w', encoding='utf-8'))

states = []


def st(id_, scenario, t, ks, underlay='none', order='text_first', offset=None):
    e = dict(id=id_, scenario=scenario, t=t, ks=ks, underlay=underlay, order=order)
    if offset:
        e['offset'] = offset
    states.append(e)


# 1. every frame of every scenario, choice alone (the closed pass included)
for sid, s in scn.items():
    ks = [1, 2] if sid in ('V1', 'V2', 'V5', 'V6', 'V7') else [1]
    for t in range(1, s['last_t'] + 1):
        st('%s_t%02d' % (sid, t), sid, t, ks)

# 2. x3 for the slide, the rest, the cursor images
for sid, ts in (('V1', (2, 5, 16, 19, 34, 35)), ('V2', (22,)), ('V6', (2, 16))):
    for t in ts:
        st('%s_t%02d_k3' % (sid, t), sid, t, [3])

# 3. a window that is not 4:3: the view is offset in a larger target; frames partly or wholly off the right edge of the image (x 311 .. 320 and beyond)
for sid, ts in (('V1', (2, 3, 4, 19, 34, 35, 36)), ('V6', (2, 3, 16)), ('V2', (22, 38, 39))):
    for t in ts:
        if t <= scn[sid]['last_t']:
            st('%s_t%02d_o' % (sid, t), sid, t, [2, 3], offset=[97, 41])

# 4. the choice over the text box (push order: the text box first, the choice above), the 8-row overlap
for ul in ('rest', 'mrow'):
    for sid, ts in (('V1', (2, 5, 16, 19, 25, 34, 35)), ('V2', (22,)), ('V10', (22, 30))):
        for t in ts:
            st('U%s_%s_t%02d' % (ul, sid, t), sid, t, [1, 2], underlay=ul)
    # negative controls: the same states with the windows added in the other order (the text box above the choice)
    st('INV%s_V1_t19' % ul, 'V1', 19, [1], underlay=ul, order='choice_first')
    st('INV%s_V2_t22' % ul, 'V2', 22, [1], underlay=ul, order='choice_first')

dynamic = [dict(id='DYN_V1', scenario='V1'), dict(id='DYN_V2', scenario='V2')]

integrated = [
    dict(id='book_oui', answer=0, open_at=61, draw_at=[1, 2, 3, 5, 19, 25, 36, 37, 38], last_tick=180, question='Enregistrer tes progrès?', table='V1'),
    dict(id='book_non', answer=1, open_at=61, draw_at=[2, 19, 22, 30, 37, 38, 39], last_tick=180, question='Enregistrer tes progrès?', table='V10'),
]

json.dump(dict(scenarios=scn, states=states, dynamic=dynamic, integrated=integrated), open(os.path.join(HERE, 'states.json'), 'w', encoding='utf-8'))
print('scenarios', len(scn), 'states', len(states), 'renders', sum(len(s['ks']) for s in states))
