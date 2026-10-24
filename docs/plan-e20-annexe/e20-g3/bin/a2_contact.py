"""a2_contact.py - disc-e20g3 A2: the life of the contact field hero.+0x130 and of the Square edge, on rec5 of map 392 (real code). Output out/a2_contact.txt"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
OUT=[]
def P(s=''):
    OUT.append(s); print(s, flush=True)

def go(seq, start=(720, 296), y=296.0, rec_ids=(5,), direction=8):
    m, S = L.world392(hero_px=start, direction=direction, rec_ids=rec_ids)
    h = L.K.ent(0); m.poke(h + 0x118, int(round(y * 65536)))
    pads, n = L.pad_seq(seq)
    rows = L.gl.run(m, n, pads, crates=list(S.values()))
    return m, S, rows

def line(rows, a, b, S):
    c5 = L.K.ent(S[5])
    return ' '.join('t%d:%s%s' % (r['tick'], 'C' if r['hero']['xcoll'] == c5 else ('?' if r['hero']['xcoll'] else '-'), '*' if r['hero']['carried2C'] else '') for r in rows if a <= r['tick'] <= b)

P('Legend: per tick, C = hero.+0x130 is rec5, - = 0 ; * = hero carries (+0x2C != 0).  hero walks west from x=720 at y=296; rec5 east face at x=647, contact at hero x=657.')
m, S, rows = go([(33, L.LEFT), (10, 0)])
P('1) push then release at tick 34: ' + line(rows, 24, 40, S))
m, S, rows = go([(26, L.LEFT), (1, L.LEFT | L.SQUARE), (40, 0)])
P('2) Square edge at tick 27, the tick whose PHYSICS first blocks the hero (MovePlayer of tick 27 runs before that physics and reads the contact of tick 26 = none): ' + line(rows, 24, 32, S) + ' ; hero state t27..t32: ' + str([r['hero']['anim_t'] for r in rows[26:32]]) + '  (16 = sword swing: no lift)')
m, S, rows = go([(27, L.LEFT), (1, L.LEFT | L.SQUARE), (40, 0)])
P('3) Square edge at tick 28 (reads the contact written by the physics of tick 27): ' + line(rows, 26, 34, S) + ' ; hero state t28..t33: ' + str([r['hero']['anim_t'] for r in rows[27:33]]) + '  (5 = lift gesture)')
m, S, rows = go([(33, L.LEFT), (3, 0), (1, L.SQUARE), (20, 0)])
P('4) push, release for 3 ticks, then Square alone at tick 37: ' + line(rows, 33, 40, S) + ' ; hero state after: ' + str([r['hero']['anim_t'] for r in rows[35:42]]) + '  (state 16 = sword swing: no lift)')
m, S, rows = go([(33, L.LEFT), (3, 0), (1, L.LEFT | L.SQUARE), (20, 0)])
P('5) push, release for 3 ticks, then LEFT+Square together at tick 37 (the contact of tick 36 is 0): ' + line(rows, 33, 40, S) + ' ; hero state after: ' + str([r['hero']['anim_t'] for r in rows[35:42]]))
m, S, rows = go([(33, L.LEFT)], start=(720, 296), y=296.0)
c5 = L.K.ent(S[5])
P('6) the contact keeps being re-written every tick while the hero pushes (ticks 27..33 all C): ' + line(rows, 27, 33, S))
# push diagonally: hero comes from the south-east lane y=312.. pressing LEFT+UP into rec5's east face
m, S, rows = go([(55, L.LEFT | L.UP), (10, 0)], start=(720, 296), y=312.0)
P('7) LEFT+UP pressed from y=312 (the north wall of the doorway ends the Y motion at 295, the crate blocks X): ' + ' '.join('t%d:x=%.1f,y=%.1f,%s' % (r['tick'], r['hero']['px'] / 65536, r['hero']['py'] / 65536, 'C' if r['hero']['xcoll'] == L.K.ent(S[5]) else '-') for r in rows if 30 <= r['tick'] <= 50 and r['tick'] % 2 == 0))
open(os.path.join(L.OUT, 'a2_contact.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
