import sys
import corpus
args = sys.argv[1:]
for a in args:
    m, s, p, pc = a.split(':')
    corpus.show(int(m), s, int(p), int(pc))
    print()
