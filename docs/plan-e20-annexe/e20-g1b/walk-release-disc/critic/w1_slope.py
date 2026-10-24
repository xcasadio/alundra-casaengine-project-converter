"""w1_slope.py - walk-release-disc W1: does a slide term (TileAttributes & 0xF, ApplyEntityForces 0x800366FC) change the release ? Scan of the ground property (gp) and slope bytes of a strip of cells:
which value makes the hero's +0x188 (TileAttributes) non zero and AdjustedForce != Force while he stands still (the slide). Then, for the first such value, the release of the walk on the strip.
Run from the copy of critic-scenarios:  python -B -I w1_slope.py
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl

H = gl.HERO


def probe(gp, slope):
    def cells(col, row):
        return dict(gp=gp, slope=slope) if 4 <= col <= 20 else None
    m = gl.world(cells=cells, hero_px=(150, 100), direction=24)
    rows = gl.run(m, 3, {}, post=lambda mm, t, r: r.update(w=dict(p188=mm.peek(H + 0x188), p180=mm.peek(H + 0x180), slope=mm.peeks(H + 0x18C), adjx=mm.peeks(H + 0xDC), adjy=mm.peeks(H + 0xE0), fx=mm.peeks(H + 0xC4), px=mm.peeks(H + 0x114), py=mm.peeks(H + 0x118))))
    return rows[-1]['w']


def main():
    print('gp   slope | +0x188  +0x180  +0x18C | AdjX-ForceX  AdjY   (standing still, 3 ticks)')
    hits = []
    for slope in (0, 1, 2, 3, 5, 7, 8):
        for gp in (0, 1, 2, 3, 4, 5, 8, 0x10, 0x20, 0x40):
            w = probe(gp, slope)
            if w['p188'] or w['slope'] or w['adjx'] - w['fx'] or w['adjy']:
                print('%02X   %d     | %08X %08X %5d   | %10d %7d' % (gp, slope, w['p188'], w['p180'], w['slope'], w['adjx'] - w['fx'], w['adjy']))
                hits.append((gp, slope))
    print('values with an effect:', hits[:20])


if __name__ == '__main__':
    main()
