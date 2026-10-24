import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw
import run_flames as RF
from run_arrows import save
spec, state = RF.make(emulate_a=False, n=60)
res = fw.run(spec)
cols = ['status', 'anim_t', 'cnt1c', 'px', 'py', 'pz', 'fz', 'coll_140', 'ground_144', 'terr_138', 'slope_18C', 'reset_AC', 'flags6C', 'atk_attr', 'atk_pow', 'bw', 'bd', 'bh']
save('flames309_without_A_program', res, state['slots'], rel=1, title='CONTROL: same three records but the A bytecode (SetAnim 2; clear 0xF0) NOT run: the ball explodes on the ground at once', cols=cols)
print(state['slots'])
