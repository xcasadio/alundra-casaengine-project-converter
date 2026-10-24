"""w4_steady.py - walk-release-disc W4: is the one-tick delay of the contact from the top of a crate (O-G3-3) tied to the start ramp (acceleration) of the walk ?
Own scene on the REAL code of ALUN_CD.EXE (France), the REAL map-record spawner for the crates of the map 392 (sprite 36), a flat field:
  A row  : three crates at (204,104), (228,104), (252,104), z 0 (top 16)           -> the hero walks on them at z = 16
  B stack: a crate at (276,104) z 0 and one on it at z 16 (the shape of rec6 + rec10) -> the hero at z = 16 meets the UPPER one (its body is at z 16..32)
  control: the same with the hero on the ground (z 0) walking east into the lower crate of the stack (the ground rule of g3: contact on the tick that shortens the step)
Variants of the start: far (the hero starts 50 px from the stack, steady force 2.4375 for ~15 ticks before the arrival) and near (the hero starts 4 px from the face: the arrival comes
at the 2nd tick, as in R7).
Run:  python -B -I w4_steady.py [far|near|ground_far|ground_near]
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import k0lib as K

H = gl.HERO
R = gl.RIGHT


def build(on_top):
    m = gl.world(hero_px=(204, 104), direction=24)
    pos = [(16, 12, 0), (18, 12, 0), (20, 12, 0), (22, 12, 0), (22, 12, 2)]
    slots, info = K.spawn_record_entities(m, 'map_392', 36, pos)
    return m, slots


def snap(m, t, r):
    r['w4'] = dict(fx=m.peeks(H + 0xC4), step=m.peeks(H + 0xD4), support=m.peek(H + 0x12C), ground=m.peeks(H + 0x144), collz=m.peeks(H + 0x140))


def run(name, start_x, z_px, n=24, press_from=6):
    m, slots = build(z_px > 0)
    nm = {K.ent(s): n_ for s, n_ in zip(slots, ['A1', 'A2', 'A3', 'Blow', 'Bup'])}
    m.poke(H + 0x114, int(start_x * 65536))
    m.poke(H + 0x11C, int(z_px * 65536) + (2 if z_px else 0))
    seq = [(press_from - 1, 0), (n - press_from + 1, R)]
    pads, total = gl.pad_script(seq)
    rows = gl.run(m, total, pads, crates=slots, post=snap)
    print('== %s: hero from x=%s z=%s, RIGHT from t%d' % (name, start_x, z_px, press_from))
    print('tick pad  | ForceX   StepX    | PosX      PosZ  | adj contact support ground')
    for r in rows:
        Hh, w = r['hero'], r['w4']
        print('t%02d %04X | %8.4f %8.4f | %9.4f %6.1f | %3d %-7s %-9s %d' % (
            r['tick'], r['hold'], w['fx'] / 65536, w['step'] / 65536, Hh['px'] / 65536, Hh['pz'] / 65536, Hh['adj'], nm.get(Hh['xcoll'], hex(Hh['xcoll']) if Hh['xcoll'] else '-'),
            nm.get(w['support'], hex(w['support']) if w['support'] else '-'), w['ground']))
    return rows


def main():
    names = sys.argv[1:] or ['far', 'near']
    for n in names:
        if n == 'far':
            run('far (steady force at the arrival)', 204, 16, n=30)
        elif n == 'near':
            run('near (the arrival at the 2nd tick, as R7)', 250, 16, n=14)
        elif n == 'ground_far':
            run('ground far: the lower crate of the stack, hero on the ground', 150, 0, n=40)
        elif n == 'ground_near':
            run('ground near', 250, 0, n=14)


if __name__ == '__main__':
    main()
