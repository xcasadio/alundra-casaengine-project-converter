import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import neighbour_probe as N
for dy in (4, 8, 12, 14, 16, 20, 26):
    N.run_case("pot-like south dy=%d" % dy, 1, 0, dy, verbose=(dy in (8, 14)))
for dy in (-8, -14):
    N.run_case("pot-like north dy=%d" % dy, 1, 0, dy, verbose=(dy == -8))
