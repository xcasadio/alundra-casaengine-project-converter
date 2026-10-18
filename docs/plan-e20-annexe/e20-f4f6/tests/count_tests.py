# Read-only: counts [Fact] and [Theory] attributes in Alundra.Tests (source files only, bin/obj excluded).
import re, glob, os
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/Alundra.Tests'
facts = theories = files = 0
for p in glob.glob(ROOT + '/**/*.cs', recursive=True):
    rel = os.path.relpath(p, ROOT).replace('\\', '/')
    if rel.startswith(('bin/', 'obj/')):
        continue
    files += 1
    s = open(p, encoding='utf-8', errors='replace').read()
    facts += len(re.findall(r'^\s*\[(?:Fact|AlundraGpuFact)\b', s, re.M))
    theories += len(re.findall(r'^\s*\[Theory\b', s, re.M))
print('files', files, 'facts', facts, 'theories', theories)
