import sys, os
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import k0lib as K

m = K.make_world()
hz = K.hero_z(m)
print('hero Z raw', hz)
s = K.spawn_global(m, 11, 100, 100, hz + (0x20 << 16), 24)
print('slot', s, K.snap(m, s))
rows = K.run(m, 16, [s])
cols = ['status', 'anim_t', 'anim_c', 'izf_F8', 'fz_B8', 'fnz_EC', 'px', 'pz', 'terr_138', 'floor_134', 'ground_144', 'collz_140', 'adj_13C', 'p18C']
print(K.table(rows, s, cols))
