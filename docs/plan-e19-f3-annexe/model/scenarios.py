"""Value tables for E19.f3 tests: scenarios run through choice_model (the bare-0x44 ones are ALSO executed on the real
binary code by ../validate.py and checked equal). Output: ../values.md and ../values.json.
N = the script tick of the opener (0x44 first entry); every number below is relative to N unless stated."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..'))
import choice_model as cm
import model

OUT_MD = os.path.join(HERE, '..', 'values.md')
OUT_JSON = os.path.join(HERE, '..', 'values.json')
N0 = 3          # absolute opener frame used for the bare runs


def bare(events, default_sel=0, anim0=0, frames=120):
    prog = cm.ChoiceProgramState()
    scr = cm.script_choice_only(prog, default_sel=default_sel)
    rows, ctx = cm.run_choice(scr, model.pad_none, cm.raw_script({f + N0: w for f, w in events.items()}),
                              frames=frames, open_frame=N0, anim0=anim0)
    return rows, ctx, prog


def table(rows, n):
    out = []
    for r in rows:
        c = r['choice']
        rel = r['frame'] - n
        if c is None:
            if rel == 0:
                out.append(dict(t=0, update='(opener runs in the script phase)', sfx=[4]))
            continue
        e = dict(t=rel, update=c['update'], drawn=c['drawn'], sfx=c['sfx'], closed=c['closed'])
        if c['drawn']:
            e.update(frame_x=c['frame_xy'][0], label0_x=c['labels'][0]['x'], label1_x=c['labels'][1]['x'],
                     cursor_x=c['cursor']['x'], cursor_img=c['cursor']['img'], sel=c['sel'])
        out.append(e)
    return out


def fmt_table(tb):
    lines = ['| t | update | drawn | frame x | label0 x | label1 x | cursor x | cursor img | sel | sounds |',
             '|---|---|---|---|---|---|---|---|---|---|']
    for e in tb:
        lines.append('| N+%d | %s | %s | %s | %s | %s | %s | %s | %s | %s |' % (
            e['t'], e['update'], 'yes' if e.get('drawn') else ('closed' if e.get('closed') else 'no'),
            e.get('frame_x', ''), e.get('label0_x', ''), e.get('label1_x', ''), e.get('cursor_x', ''),
            e.get('cursor_img', ''), e.get('sel', ''), ','.join(str(s) for s in e.get('sfx', []))))
    return '\n'.join(lines)


def events_of(ctx, n):
    return [(f - n, e) for f, e in ctx.events]


def scenario_bare(name, events, default_sel=0, anim0=0, frames=120):
    rows, ctx, prog = bare(events, default_sel, anim0, frames)
    tb = table(rows, N0)
    ev = events_of(ctx, N0)
    return dict(name=name, pad={('N+%d' % k): hex(v) for k, v in events.items()}, default_sel=default_sel, anim0=anim0,
                events=ev, result=prog.result, summary=cm.choice_summary(rows, ctx, N0), table=tb)


def main():
    CR, L, R = cm.PAD_CROSS, cm.PAD_LEFT, cm.PAD_RIGHT
    sc = []
    # V1: validate the default at once: Cross first seen at N+19 (the first interactive pass)
    sc.append(scenario_bare('V1 validate the default (OUI) at once', {19: CR}))
    # V2: move right (first interactive pass), then validate on N+22
    sc.append(scenario_bare('V2 Right at N+19 then Cross at N+22 (NON)', {19: R, 22: CR}))
    # V3: Cross pressed during the slide-in is lost; held through N+19 it is no edge; re-press at N+30
    ev3 = {k: CR for k in range(10, 28)}
    ev3.update({30: CR})
    sc.append(scenario_bare('V3 Cross held N+10..N+27 (no edge at N+19), pressed again N+30', ev3))
    # V4: Right held from N+19: auto-repeat bits at N+19, N+40, N+41, ... (sound 1 only once), then Left at N+60
    ev4 = {k: R for k in range(19, 60)}
    ev4.update({60: L, 62: CR})
    sc.append(scenario_bare('V4 Right held N+19..N+59, Left at N+60, Cross at N+62', ev4))
    # V5: Cross and Right in the same pass: result from the OLD selection, cursor moves on the way out
    sc.append(scenario_bare('V5 Cross+Right together at N+19', {19: CR | R}))
    # V6: variant opener 0x80050C00 (default selection NON), Cross at N+19
    sc.append(scenario_bare('V6 variant opener, default NON, Cross at N+19', {19: CR}, default_sel=1))
    # V7: cursor tick persisted from an earlier box (anim0 = 17)
    sc.append(scenario_bare('V7 anim0 = 17 (counter left by an earlier box), Cross at N+19', {19: CR}, anim0=17))

    # V8: sailor 12 (map 389), text box pad A (the DLL arc pad), Cross at the earliest, one run per pad
    res8 = []
    for label, tpad in (('A', model.pad_every_frame), ('B', model.PadOncePerPage())):
        prog = cm.ChoiceProgramState()
        probe, pctx = cm.run_choice(cm.script_389_sailor12_choice(cm.ChoiceProgramState()), tpad if label == 'A' else model.PadOncePerPage(), lambda f: 0, frames=700)
        nc = next(f for f, e in pctx.events if e == '0x44 first entry')
        tp = tpad if label == 'A' else model.PadOncePerPage()
        rows, ctx = cm.run_choice(cm.script_389_sailor12_choice(prog), tp, cm.press({nc + 19: CR}), frames=700)
        s = model.summary(rows, ctx, 1)
        res8.append(dict(textpad=label, choice_open=nc, cross_seen=nc + 19, events=[(f, e) for f, e in ctx.events],
                         text=s, choice=cm.choice_summary(rows, ctx, nc)))
    # V9: save book (AI_ProcessWarpTransitionState): text ETC 0x40 (mode 1, 0x50 4) at tick T0, opener 61 ticks later
    text = b'Enregistrer tes progr' + bytes([0xe8]) + b's?'

    def make_book():
        prog = cm.ChoiceProgramState()

        def book(ctx):
            yield from ctx.open(text, 1, label='open ETC0x40')
            ctx.op('50', 4)
            for _ in range(61):
                yield
            yield from cm.op44(ctx, prog, b'OUI', b'NON')
            ctx.op('51')
            yield from ctx.wait39()
        return book
    rows, ctx = cm.run_choice(make_book(), model.pad_none, lambda f: 0, frames=400)
    nb = next(f for f, e in ctx.events if e == '0x44 first entry')
    rows, ctx = cm.run_choice(make_book(), model.pad_none, cm.press({nb + 19: CR}), frames=400)
    s9 = model.summary(rows, ctx, 1)
    v9 = dict(name='V9 save book: choice opens over a box still typing', choice_open=nb, events=[(f, e) for f, e in ctx.events],
              text=s9, choice=cm.choice_summary(rows, ctx, nb),
              text_row_at_choice_open=[r['rows'] for r in rows if r['frame'] == nb][0],
              text_row_at_cross=[r['rows'] for r in rows if r['frame'] == nb + 19][0],
              text_row_at_result=[r['rows'] for r in rows if r['frame'] == nb + 37][0],
              glyph_count_at_choice_open=[r['glyphs'] for r in rows if r['frame'] == nb][0])

    out = dict(bare=sc, sailor12=res8, savebook=v9)
    json.dump(out, open(OUT_JSON, 'w'), indent=1, default=str)

    md = ['# E19.f3 value tables (model/scenarios.py; bare scenarios also executed on the real binary code, see validate.py)', '',
          'N = tick of the opener (0x44 first entry). t = frame (main-loop iteration) relative to N. "pad N+k" = the raw pad word the box sees in frame N+k.', '']
    for s in sc:
        md += ['## ' + s['name'], '', 'pad: ' + (', '.join('%s=%s' % kv for kv in s['pad'].items()) if len(s['pad']) < 12 else
                                                '%d frames' % len(s['pad'])) + ('; default selection %d' % s['default_sel']) +
               ('; anim0 %d' % s['anim0']), '', 'events: ' + '; '.join('N+%d %s' % e for e in s['events']),
               'summary: ' + json.dumps(s['summary']), 'Result of the program: %s' % s['result'], '', fmt_table(s['table']), '']
    md += ['## V8 sailor 12 (map 389) with the real choice box', '']
    for r in res8:
        md += ['text pad %s: choice opens at N+%d (T999 step), Cross first seen at N+%d' % (r['textpad'], r['choice_open'], r['cross_seen']),
               '  events: ' + '; '.join('%d %s' % e for e in r['events']),
               '  text box: ' + json.dumps(r['text']), '  choice: ' + json.dumps(r['choice']), '']
    md += ['## ' + v9['name'], '', 'events: ' + '; '.join('%d %s' % e for e in v9['events']), 'text: ' + json.dumps(v9['text']),
           'choice: ' + json.dumps(v9['choice']), 'text rows when the choice opens: %r (glyphs %d), at the Cross: %r, at the result: %r' % (
               v9['text_row_at_choice_open'], v9['glyph_count_at_choice_open'], v9['text_row_at_cross'], v9['text_row_at_result'])]
    open(OUT_MD, 'w', encoding='utf-8').write('\n'.join(md) + '\n')
    print('written', OUT_MD, OUT_JSON)


if __name__ == '__main__':
    main()
