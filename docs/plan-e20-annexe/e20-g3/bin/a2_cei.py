"""a2_cei.py - disc-e20g3 A2: CheckEntityInteraction 0x8002E910 called with the hero in contact with rec5 (state after tick 28 of the walk): what does it return, and what are the crate's +0x60 / +0x84?"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
m, S = L.world392(hero_px=(720, 296), direction=8, rec_ids=(5,))
pads, n = L.pad_seq([(30, L.LEFT)])
rows = L.gl.run(m, n, pads, crates=list(S.values()))
h = L.K.ent(0); c = L.K.ent(S[5])
print('hero +0x130 =', hex(m.peek(h + 0x130)), '(crate rec5 is', hex(c), ')')
print('crate +0x60 =', hex(m.peek(c + 0x60)), ' +0x84 =', hex(m.peek(c + 0x84)), ' flags +0x6C =', hex(m.peek(c + 0x6C)), ' (flags & 0x8000 = %d)' % ((m.peek(c + 0x6C) >> 15) & 1))
print('CheckEntityInteraction(0x8002E910) returns', m.call(0x8002E910))
