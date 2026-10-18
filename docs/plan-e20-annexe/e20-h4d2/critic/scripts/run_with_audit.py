import sys, runpy, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import emu
assert emu.__file__.endswith('emu.py') and 'HAZARDS' in dir(emu), 'not my interpreter'
sys.argv = ['fire1_make_keys.py'] + sys.argv[1:]
try:
    runpy.run_path(os.path.join(HERE, 'fire1_make_keys.py'), run_name='__main__')
finally:
    out = sys.argv[1]
    with open(os.path.join(out, 'load_delay_hazards.txt'), 'w') as f:
        f.write('hazards (pc, reg): count -> %d distinct\n' % len(emu.HAZARDS))
        for (pc, reg), c in sorted(emu.HAZARDS.items()):
            f.write('%08x r%d %d\n' % (pc, reg, c))
