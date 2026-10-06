import sys
sys.path.insert(0, r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\night-pad-disc\verify")
import v_bin as vb
P = 5
for q in (14, 16, 17, 18, 19, 20, 21, 24):
    phys = lambda f, q=q: vb.RIGHT if f == P + q else (vb.CROSS if f in (P, P + q + 1) else 0)
    m, rows = vb.run(phys, P + 100)
    o, t = vb.summarize(rows, P)
    res = [r['result10'] for r in rows if r['f'] == (t or 0)]
    print("NON Right@P+%d Cross@P+%d: opened P+%s taken P+%s result=%s" % (q, q + 1, None if o is None else o - P, None if t is None else t - P, [hex(x) for x in res]))
