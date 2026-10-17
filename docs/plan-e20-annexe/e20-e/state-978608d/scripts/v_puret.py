import sys, collections
sys.argv, files = [sys.argv[0]], sys.argv[1:]
exec(open("v_pure.py", encoding="utf-8").read().split("for old in sys.argv")[0])
for f in files:
    o = norm(show("9502f75", f), True); n = norm(show("HEAD", f), False)
    a, b = o - n, n - o
    print(f, sum(o.values()), sum(n.values()), "only_old", sum(a.values()), "only_new", sum(b.values()))
    for k, v in list(a.items())[:8]: print("  -", v, k[:180])
    for k, v in list(b.items())[:8]: print("  +", v, k[:180])
