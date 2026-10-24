"""w2_catalog_vs_binary.py - walk-release-disc W2: the animation sets of the hero in the RAM of the oracle machine (the REAL sprite record of ALUN_CD.EXE: table base = *(hero+0x64), 14 bytes per
animation, Speed u16 at +8, the byte +0xD = Acceleration, +0xA/+0xB = IsZForceApplied) against alundra-project/Data/sprite-records.json (hero record 192c2eeb-...), which the DLL reads in production
(WorldProxy.ApplyHeroSpriteHeader -> proxy.AnimSetsByAnim = header.AnimSets). Prints the mismatches (expected: none).
Run from the copy of critic-scenarios:  python -B -I w2_catalog_vs_binary.py
"""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl

H = gl.HERO


def main():
    m = gl.world(hero_px=(100, 100), direction=24)
    gl.run(m, 1, {})                                    # one tick so that the first UpdateAnimation has loaded the sheet pointers
    rec = m.peek(H + 0x64)
    base = m.peek(rec)
    d = json.load(open(r'D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json', encoding='utf-8-sig'))
    sets = {x['Anim']: x for x in d['192c2eeb-bdda-5bb4-92d6-39a4ae298307']['AnimSets']}
    bad = []
    for a in range(len(sets)):
        p = base + 14 * a
        speed = m.peek(p + 8) & 0xFFFF
        unk = (m.peek(p + 8) >> 16) & 0xFF
        flags = (m.peek(p + 8) >> 24) & 0xFF
        sfx = m.peek(p + 0xC) & 0xFF
        acc = (m.peek(p + 0xC) >> 8) & 0xFF
        j = sets[a]
        if (speed, acc, sfx, flags, unk) != (j['Speed'], j['Acceleration'], j['Sfx'], j['Flags'], j['Unknown']):
            bad.append((a, (speed, acc, sfx, flags, unk), (j['Speed'], j['Acceleration'], j['Sfx'], j['Flags'], j['Unknown'])))
    print('hero sheet base in RAM: %08X ; animations compared: %d ; mismatches: %d' % (base, len(sets), len(bad)))
    for b in bad[:20]:
        print('  anim %d  binary(speed,acc,sfx,flags,unk)=%s  json=%s' % b)
    for a in (0, 1, 3, 7, 12, 29, 42, 44, 45):
        p = base + 14 * a
        print('  anim %2d  binary Speed %4d  Acceleration byte %3d (& 0xF = %d)' % (a, m.peek(p + 8) & 0xFFFF, (m.peek(p + 0xC) >> 8) & 0xFF, ((m.peek(p + 0xC) >> 8) & 0xFF) & 0xF))


if __name__ == '__main__':
    main()
