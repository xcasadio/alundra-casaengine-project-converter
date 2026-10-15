import sys, os
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import k0lib as K
m = K.make_world()
slots, info = K.spawn_record_entities(m, 'map_392', 36, [(10, 11, 0)])
s = slots[0]
print(s, K.snap(m, s))
print({k: hex(m.peek(K.ent(s) + o)) for k, o in (('A70', 0x70), ('B74', 0x74), ('C78', 0x78), ('D7C', 0x7C), ('E80', 0x80), ('F84', 0x84), ('rec', 0x64), ('44', 0x44), ('48', 0x48), ('1E4', 0x1E4), ('1E8', 0x1E8), ('1EC', 0x1EC), ('1F0', 0x1F0), ('1F4', 0x1F4), ('1F8', 0x1F8), ('108', 0x108), ('fc', 0xFC), ('100', 0x100), ('104', 0x104))})
# handler table lookup of C23
tbl = m.peek(0x80098F4C + 4 * 2)
print('C table', hex(tbl), 'C23', hex(m.peek(tbl + 4 * 23)), 'E table', hex(m.peek(0x80098F4C + 4 * 4)), 'E2', hex(m.peek(m.peek(0x80098F4C + 16) + 8)))
rows = K.run(m, 5, [s])
print(K.table(rows, s, ['status', 'anim_t', 'px', 'py', 'pz', 'ground_144', 'collz_140', 'fnz_EC', 'prog228']))
