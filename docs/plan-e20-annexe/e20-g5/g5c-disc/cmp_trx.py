"""cmp_trx.py base.trx wired.trx - the tests whose outcome differs between two runs of the same suite (TRX), by test name. Prints counts, then the tests that pass in the base run and fail in the wired one (the 'moving tests'), then the reverse."""
import sys, xml.etree.ElementTree as ET, collections
def load(p):
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    r = {}
    for u in ET.parse(p).getroot().iter('{%s}UnitTestResult' % ns['t']):
        r[u.get('testName')] = (u.get('outcome'), (u.findtext('.//t:Message', default='', namespaces=ns) or '')[:300])
    return r
a, b = load(sys.argv[1]), load(sys.argv[2])
print('base: %d tests, %s' % (len(a), dict(collections.Counter(v[0] for v in a.values()))))
print('wired: %d tests, %s' % (len(b), dict(collections.Counter(v[0] for v in b.values()))))
only_a, only_b = sorted(set(a) - set(b)), sorted(set(b) - set(a))
print('only in base:', len(only_a), only_a[:5]); print('only in wired:', len(only_b), only_b[:10])
pf = sorted(k for k in a if k in b and a[k][0] == 'Passed' and b[k][0] != 'Passed')
fp = sorted(k for k in a if k in b and a[k][0] != 'Passed' and b[k][0] == 'Passed')
print('--- passed in base, NOT passed in wired (%d):' % len(pf))
for k in pf: print('  ', k, '|', b[k][1].replace('\n', ' ')[:200])
print('--- not passed in base, passed in wired (%d):' % len(fp))
for k in fp: print('  ', k)
