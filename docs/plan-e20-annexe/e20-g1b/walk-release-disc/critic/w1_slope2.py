import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import w1_slope as S
found = []
for slope in list(range(0, 256, 1)):
    w = S.probe(0, slope)
    if w['p188'] or w['adjx'] - w['fx'] or w['adjy']:
        found.append((slope, w['p188'], w['adjx'] - w['fx'], w['adjy']))
print(found[:30])
