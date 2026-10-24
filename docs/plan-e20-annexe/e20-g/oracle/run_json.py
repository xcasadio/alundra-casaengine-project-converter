"""run_json.py - T0 of E20.g (added, not part of the copied tools): run one scenario script unchanged and write the rows of every gl.run call as out/<script>.json
(keys = the names of HFIELDS and CFIELDS of gl.py, plus tick, hold, journal, lists, carry_ctr, use164).  The scenario is not modified: gl.run is wrapped.
Usage: python -B -I run_json.py <script.py>"""
import sys, os, json, runpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl

runs = []
_orig = gl.run


def _run(*a, **k):
    r = _orig(*a, **k)
    runs.append(r)
    return r


gl.run = _run
script = sys.argv[1]
sys.argv = [script]
runpy.run_path(os.path.join(HERE, script), run_name='__main__')
stem = os.path.splitext(script)[0]
with open(os.path.join(gl.OUT, stem + '.json'), 'w', newline='\n') as f:
    json.dump(dict(script=script, nruns=len(runs), runs=runs), f, sort_keys=True, separators=(',', ':'))
    f.write('\n')
