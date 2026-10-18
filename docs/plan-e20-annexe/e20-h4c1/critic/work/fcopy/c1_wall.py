import sys, os, json
sys.argv = ['x', '../keys']
exec(open('c1_make_keys.py').read().split("scen = []")[0])
res, path = run('earth1_wall_col9', cells=fw.wall_col(9))
print(path)
