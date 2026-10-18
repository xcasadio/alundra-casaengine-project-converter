"""keyfacts.py (critic h4d2): independent extraction of the calendar facts from a key JSON (my own reading of the columns)"""
import json, sys
def load(p): return json.load(open(p))
def facts(path):
    d = load(path); rows = d['rows']
    births = {}   # (slot,id) -> first tick seen
    out = {}
    seen = {}
    for r in rows:
        for s, e in r['ents'].items():
            k = (s, e['id'])
            if k not in seen:
                seen[k] = dict(first=r['t'], type=e['type'], slot=int(s), last=r['t'])
            seen[k]['last'] = r['t']
            seen[k]['status_first'] = seen[k].get('status_first', e['status'])
    return d, rows, seen
if __name__ == '__main__':
    d, rows, seen = facts(sys.argv[1])
    print('meta', d['meta'])
    for k, v in sorted(seen.items(), key=lambda kv: (kv[1]['first'], kv[1]['slot'])):
        if v['type'] in (20, 23):
            print(k, v)
    # item
    prev = None
    for r in rows:
        it = (r['item']['inuse'], r['item']['phase'], r['hero']['anim_t'])
        if it != prev:
            print('t', r['t'], 'inuse/phase/hero_anim_t', it, 'mp', r['item']['mp'], 'counter', r['item']['counter'], 'ptr_slot', r['ptr_slot'], 'ident', r['ident'])
            prev = it
