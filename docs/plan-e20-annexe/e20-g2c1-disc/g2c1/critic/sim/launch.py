import sys, runpy, os
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
script = sys.argv[1]
sys.argv = [script] + sys.argv[2:]
sys.path.insert(0, os.path.dirname(os.path.abspath(script)))
runpy.run_path(script, run_name='__main__')
