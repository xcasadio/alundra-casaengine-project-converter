"""events_dump.py - E20.f1a T5: the UNCOLLAPSED event list of every tick of the five charge probes, in the order the binary produced them.

out/<name>.txt prints the events through collapse() (ext.py), which merges neighbouring events whose text up to the first '(' and the first 'id=' agree: 'sound(43)' and 'sound(36)' print as 'sound(43) x2', and the
burst's 16 creations as one 'x16'.  The DLL tests compare the sounds one by one, so this script writes them in full: out_events/<name>.events.json, one object per tick,
  {"tick": N, "events": ["c16", "s428", "c16", ..., "s43", "s36"]}
where 'c<id>' is a call of the effect creator (0x8003BDD8, 0x8003BE74: the id is the effect number) and 's<id>' a call of PlaySoundEffect (id 0 is the call with 'no sound', which the DLL does not ask).
Run from this folder:  python -B events_dump.py     (it runs the same scenarios as probe_ext.py; nothing else is written).
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'tools'))
import ext
import probe_ext as P

OUT = os.path.join(HERE, 'out_events')
os.makedirs(OUT, exist_ok=True)

SOUND = re.compile(r'^sound\((\d+)\)$')
CREATE = re.compile(r'^effect_3B(?:DD8|E74)\(.*?id=(\d+),')


def token(event):
    m = SOUND.match(event)
    if m:
        return 's' + m.group(1)
    m = CREATE.match(event)
    if m:
        return 'c' + m.group(1)
    return 'x:' + event.split('(')[0]     # spawn(...) and any other journalled call


if __name__ == '__main__':
    for name in P.SCEN:
        res = ext.run_ext(P.spec_of(name))
        rows = [{'tick': r['tick'], 'events': [token(e) for e in r['events']]} for r in res['rows']]
        with open(os.path.join(OUT, name + '.events.json'), 'w', newline='\n') as f:
            json.dump(rows, f, indent=0)
            f.write('\n')
        print(name, len(rows), 'ticks')
