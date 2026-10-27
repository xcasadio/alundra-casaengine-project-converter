"""Launcher for the annex oracle scripts under `python -I` (isolated mode drops the script directory from sys.path).
usage: python -I run.py <workdir> <script.py> [args...]
Runs the script from <workdir> as __main__ with <workdir> first on sys.path. Never writes inside the repository."""
import sys
import os
import runpy

work = os.path.abspath(sys.argv[1])
script = sys.argv[2]
sys.path.insert(0, work)
sys.path.append(r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
os.chdir(work)
sys.argv = [script] + sys.argv[3:]
runpy.run_path(os.path.join(work, script), run_name='__main__')
