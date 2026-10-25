import os, runpy, sys
sys.path.append(r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
script = os.path.abspath(sys.argv[1])
sys.argv = sys.argv[1:]
sys.path.insert(0, os.path.dirname(script))
os.chdir(os.path.dirname(script))
runpy.run_path(script, run_name="__main__")
