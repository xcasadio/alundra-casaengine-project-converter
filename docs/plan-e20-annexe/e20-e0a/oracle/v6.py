"""v6.py - E20.e0a3a oracle V6: what a fresh entity runs in its birth tick, by spawn site and slot (twelve lines BT-M1..BT-L3).

Driver around p3_tick.py (byte copy of the discovery probe, not edited): the cases and the real passes are p3_tick's; this module only records, per tick and per pass, what is read from RAM
after the real code ran, and derives where each thing first happens.  Imported by run_e0a.py after espawn (which puts tools/ on sys.path).  Nothing here knows an expected value of the DLL;
the only classification ('kind') is the one written in the annex README before the first run, and run_e0a.py checks the measured lines against it."""
import p3_tick as P

PASSES = ('slots', 'events', 'counters', 'lists', 'anim', 'physics')


def _record(c, m, find=None):
    """one tick, pass by pass; returns {pass: fields} for the passes where the new entity exists"""
    out = {}
    for a, nm in zip((0x80038634, 0x800386D0, 0x80038998, 0x800384F4, 0x80038E18, 0x80038364), PASSES):
        m.call(a, limit=5_000_000)
        if find is not None and nm == 'events' and not c.box.get('new'):
            c.box['new'] = find()
        p = c.box.get('new')
        if p:
            out[nm] = dict(status=m.peek(p + 0x10), trig=m.peeks(p + 0x228), c1c=m.peeks(p + 0x1C), delay=m.peeks(p + 0xA8), posx=m.peeks(p + 0x114) >> 16,
                           in_update=p in P.lst(m, P.UPD, P.UPDN), in_collision=p in P.lst(m, P.COL, P.COLN), in_draw=p in P.lst(m, P.DRW, P.DRWN),
                           a_runs=c.counts.get(p, 0), count=m.peeks(0x80134360))
    return out


def _first(ticks, pred):
    for ti, t in enumerate(ticks, 1):
        for nm in PASSES:
            if nm in t and pred(t[nm]):
                return [ti, nm]
    return None


def _row(bt, kind, c, ticks, extra=None):
    p = c.box.get('new')
    row = dict(id=bt, kind=kind, title=c.title, spawned=bool(p), slot=P.slot_of(p) if p else None, trig_at_spawn=c.box.get('new_trig_at_spawn'), ticks=ticks)
    if p:
        t1, t2 = ticks[0], ticks[1]
        a1 = t1['physics']['a_runs']
        a2 = t2['physics']['a_runs'] - a1
        row['a_runs_tick1'] = a1
        row['a_runs_tick2'] = a2
        row['first_status2'] = _first(ticks, lambda d: d['status'] == 2)
        row['first_in_update_list'] = _first(ticks, lambda d: d['in_update'])
        row['first_in_draw_list'] = _first(ticks, lambda d: d['in_draw'])
        row['first_c1c_nonzero'] = _first(ticks, lambda d: d['c1c'] != 0)
        row['first_a_run'] = _first(ticks, lambda d: d['a_runs'] > 0)
        row['first_x_moved'] = _first(ticks, lambda d: d['posx'] != t1.get('events', t1['physics'])['posx'])
        row['status_end_tick1'] = t1['physics']['status']
        row['x_end_tick1'] = t1['physics']['posx']
        row['x_end_tick2'] = t2['physics']['posx']
    if extra:
        row.update(extra)
    return row


def _run_simple(bt, kind, c):
    ticks = [_record(c, c.m), _record(c, c.m)]
    return _row(bt, kind, c, ticks)


def _run_loop1(bt, kind, c):
    m = c.m
    ticks = []
    for _ in (1, 2):
        ticks.append(_record(c, m, c.find_new))
    return _row(bt, kind, c, ticks, dict(dying_slot=P.slot_of(c.dying), dying_status_after_tick1=None))


def run():
    rows = []
    rows.append(_run_simple('BT-M1', 'birth_full', P.case_moveplayer('M1', filler=[])))
    rows.append(_run_simple('BT-M2', 'birth_full', P.case_moveplayer('M2', filler=[2, 3])))
    rows.append(_run_simple('BT-M3', 'birth_full', P.case_moveplayer('M3', filler=[1])))
    rows.append(_run_simple('BT-H1', 'a_twice', P.case_loop2('H1', 1, filler=[])))
    rows.append(_run_simple('BT-H2', 'postponed', P.case_loop2('H2', 1, filler=[4], free_stale={2: -1})))
    rows.append(_run_simple('BT-H3', 'postponed', P.case_loop2('H3', 3, filler=[2])))
    rows.append(_run_simple('BT-H4', 'a_twice', P.case_loop2('H4', 1, filler=[], dead_before=[])))
    rows.append(_run_simple('BT-H5', 'postponed', P.case_loop2('H5', 1, filler=[], free_stale={2: -1})))
    rows.append(_run_simple('BT-H6', 'a_twice', P.case_loop2('H6', 1, filler=[], dead_before=[2, 3])))
    rows.append(_run_loop1('BT-L1', 'loot_full', P.case_loop1('L1', 1, [], None)))
    rows.append(_run_loop1('BT-L2', 'postponed', P.case_loop1('L2', 3, [2], None)))
    rows.append(_run_loop1('BT-L3', 'loot_full', P.case_loop1('L3', 1, [4], None)))
    return rows


def classify(r):
    """the measured behaviour, in the words of the annex README (kind): birth_full / loot_full = A once in tick 1, status 2 and in the lists in tick 1, +0x1C = 1;
    a_twice = A once in tick 1 (status 1, out of the lists) and once more in tick 2; postponed = A not run in tick 1, once in tick 2"""
    if not r['spawned']:
        return 'no_spawn'
    t1 = r['ticks'][0]['physics']
    if r['a_runs_tick1'] == 1 and r['a_runs_tick2'] == 0 and t1['status'] == 2 and t1['in_update']:
        return 'birth_full'
    if r['a_runs_tick1'] == 1 and r['a_runs_tick2'] == 1 and t1['status'] == 1 and not t1['in_update']:
        return 'a_twice'
    if r['a_runs_tick1'] == 0 and r['a_runs_tick2'] == 1 and t1['status'] == 1 and not t1['in_update']:
        return 'postponed'
    return 'other'
