import sys, os
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import k0lib as K
m = K.make_world()
slots, info = K.spawn_record_entities(m, 'map_392', 36, [(10, 11, 0)])
s = slots[0]
e = K.ent(s)
print('+0x20', m.peek(e+0x20), '+0x224', m.peek(e+0x224), '+0x1C', m.peek(e+0x1c), 'status', m.peek(e+0x10), 'hero struct 80127108', hex(m.peek(0x80127108)))
for t in range(3):
    K.run(m, 1, [s])
    print('after tick', t+1, 'prog228', m.peeks(e+0x228), '+0x20', m.peek(e+0x20), 'journal', m.journal[-3:])
