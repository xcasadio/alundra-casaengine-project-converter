"""cmp_trx.py HEAD.trx PROTO.trx - the outcomes of two full runs of Alundra.Tests (baseline HEAD, then HEAD + the throw-away prototype of 0x72/0xA9/0xAA), per test:
counts, and the tests whose outcome differs. Read-only."""
import sys, collections
import xml.etree.ElementTree as ET
def load(p):
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    r = {}
    for u in ET.parse(p).getroot().iter('{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult'):
        r[u.get('testName')] = u.get('outcome')
    return r
a, b = load(sys.argv[1]), load(sys.argv[2])
for n, r in (('HEAD', a), ('PROTO', b)):
    print(n, len(r), dict(collections.Counter(r.values())))
diff = [(k, a.get(k), b.get(k)) for k in sorted(set(a) | set(b)) if a.get(k) != b.get(k)]
print('tests whose outcome differs:', len(diff))
for k, x, y in diff[:60]:
    print('  %s  HEAD=%s PROTO=%s' % (k, x, y))
