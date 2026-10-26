import sys, os, runpy
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
script = os.path.abspath(sys.argv[1])
sys.path.insert(0, os.path.dirname(script))
sys.argv = sys.argv[1:]
runpy.run_path(script, run_name="__main__")
