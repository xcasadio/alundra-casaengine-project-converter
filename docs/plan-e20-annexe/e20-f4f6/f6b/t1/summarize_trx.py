import sys, xml.etree.ElementTree as ET, collections, re
ns='{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
t=ET.parse(sys.argv[1])
names=collections.defaultdict(lambda:[0,0])
for r in t.getroot().iter(ns+'UnitTestResult'):
    n=r.get('testName'); base=re.sub(r'\(.*','',n); o=r.get('outcome')
    names[base][0 if o=='Passed' else 1]+=1
for k,(p,f) in sorted(names.items()): print('%-150s pass=%d fail=%d'%(k.replace('Alundra.Tests.',''),p,f))
