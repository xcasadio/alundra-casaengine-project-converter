"""wind1_tables.py - h4e1 discovery: compact tables read from the keys (births, queries, hits) for the report.  usage: python -B wind1_tables.py KEYDIR NAME [NAME...]"""
import json, sys, os
K = sys.argv[1]
for nm in sys.argv[2:]:
    d = json.load(open(os.path.join(K, nm + '.json')))
    print('== %s' % nm)
    print('   tick | counter | branch | chosen slot | born slot | X raw (px) | Y raw (px) | Z arg | draws | rng0 -> rng1 -> rng2')
    for r in d['rows']:
        for q in r['queries']:
            sp = [s for s in r['spawns'] if s['caller'] == 0 and s['args'][2] == 22]
            s = sp[0] if sp else None
            def sg(v): return v - (1 << 32) if v >= 1 << 31 else v
            print('   %4d | %4d | %-6s | %s | %s | %s | %s | %s | %d | %#010x -> %#010x -> %#010x   cands=%s' % (
                r['t'], q['counter'], q['branch'], q['chosen'], s['ret'] if s else None,
                '%d (%.4f)' % (sg(s['args'][3]), sg(s['args'][3]) / 65536) if s else None, '%d (%.4f)' % (sg(s['args'][4]), sg(s['args'][4]) / 65536) if s else None,
                sg(s['args'][5]) if s else None, r['draws'], r['rng0'], r['rng1'], r['rng2'], q['cands']))
    hits = [(r['t'], r['hit_targets'], [(k, e['touch']) for k, e in r['ents'].items() if e['c1d0'] == 25 and e['type'] == 25]) for r in d['rows'] if r['hit_targets']]
    print('   hits (tick, target indexes, (slot, attacker slot)):', hits)
