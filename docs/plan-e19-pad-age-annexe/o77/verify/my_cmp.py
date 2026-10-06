"""Full-state, per-sample comparison: binary state after RS(s+1) (+ sounds of Update(s) and RS(s+1)) vs port state after tick s
(+ its sounds). Fields: flag6, sel6, flag4, sel4, post, MenuOpen, sounds. Usage: my_cmp.py port-rows[2].tsv"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import my_bin as B

ST, DN, UP, RT, LF, L1, TRI, R2, L2 = B.START, B.DOWN, B.UP, B.RIGHT, B.LEFT, B.L1, B.TRI, B.R2, B.L2
S = {
    'A_down18': ({0: ST, 18: DN}, 30),
    'B_down19': ({0: ST, 19: DN}, 30),
    'C_close19_start37': ({0: ST, 19: ST, 37: ST}, 60),
    'D_close19_start38': ({0: ST, 19: ST, 38: ST}, 60),
    'E_sub_right55': ({0: ST, 19: L1, 55: RT}, 64),
    'F_sub_right56': ({0: ST, 19: L1, 56: RT}, 64),
    'G_back_down94': ({0: ST, 19: L1, 56: L1, 94: DN}, 100),
    'H_down_held': (dict([(0, ST)] + [(k, DN) for k in range(10, 46)]), 50),
    'I_sub_up56_left57_tri58_l2_reopen': ({0: ST, 19: L1, 56: UP, 57: LF, 58: TRI, 76: L2, 77: L2 | DN}, 100),
    'J_tri_close_start_held': (dict([(0, R2), (19, TRI)] + [(k, ST | LF) for k in range(30, 71)]), 80),
}
P = 5

def port_rows(path):
    d = {}
    for line in open(path, encoding='utf-8'):
        c = line.rstrip('\n').split('\t')
        snd = [int(x) for x in c[8].split(',')] if c[8] else []
        d.setdefault(c[0], {})[int(c[1])] = (tuple(int(x) for x in c[2:8]), snd)
    return d

if __name__ == '__main__':
    port = port_rows(sys.argv[1])
    bad = 0
    for name, (presses, frames) in S.items():
        b = B.run(presses, P, frames)
        p = port[name]
        diffs = [(s, b[s], p[s]) for s in sorted(p) if s in b and (b[s][0] != p[s][0] or sorted(b[s][1]) != sorted(p[s][1]))]
        changes = [s for s in sorted(b) if s - 1 in b and b[s] != b[s - 1] or b[s][1]]
        if not diffs:
            print('EQUAL     %-36s %d samples compared, binary state/sound changes at samples %s' % (name, len(p), changes))
        else:
            bad += 1
            print('DIFFERENT %-36s first diffs (sample, binary(flag6,sel6,flag4,sel4,post,menu)+snd, port):' % name)
            for x in diffs[:6]: print('    ', x)
    print('%d schedules, %d different' % (len(S), bad))
