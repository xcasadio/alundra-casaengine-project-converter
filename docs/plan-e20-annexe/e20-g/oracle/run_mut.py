"""run_mut.py - T0 of E20.g (added): run one scenario script with one-word mutants of ALUN_CD.EXE patched in the oracle machine's RAM (after gl.world builds the machine).
Usage: python -B -I run_mut.py <outdir> <script.py> <addr:word>[,<addr:word>...]   (hex).  The scenario's outputs go to <outdir> instead of out/."""
import sys, os, runpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import k0lib as K

outdir, script, patches = sys.argv[1], sys.argv[2], [tuple(int(x, 16) for x in p.split(':')) for p in sys.argv[3].split(',')]
os.makedirs(outdir, exist_ok=True)
gl.OUT = outdir
_mk = K.make_world


def make_world(*a, **k):
    m = _mk(*a, **k)
    for addr, w in patches:
        m.poke(addr, w)
    return m


K.make_world = make_world
sys.argv = [script]
runpy.run_path(os.path.join(HERE, script), run_name='__main__')
