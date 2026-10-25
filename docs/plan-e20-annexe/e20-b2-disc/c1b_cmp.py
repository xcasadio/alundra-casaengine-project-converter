import os, sys
HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'c1b')
sys.path.insert(0, HERE)
import kworld as K
import k1
K.INVENTORIES.append(('legend', (1, 17, 25, 8), 1, None, 'x'))
K.INV['legend'] = dict(name='legend', owned=(1, 17, 25, 8), weapon=1, item=None, desc='x')
a, _ = k1.case(K.INV['kit'])
b, _ = k1.case(K.INV['legend'])
c, _ = k1.case(K.INV['holy'])
print('kit    sheet', a['sheet'][:60], 'records', a['record_words'])
print('legend sheet', b['sheet'][:60], 'records', b['record_words'])
print('holy   sheet', c['sheet'][:60], 'records', c['record_words'])
print('sheet kit == legend ?', a['sheet'] == b['sheet'], ' kit == holy ?', a['sheet'] == c['sheet'])
